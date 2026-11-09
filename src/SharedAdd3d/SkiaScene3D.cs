#nullable disable
using System.Numerics;
using AppoMobi.Gestures;
using DrawnUi;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// A 3D layer over a 2D game field. Many objects, each a model with its own place, size, facing and animations, are
/// drawn in one pass into one texture; each model is read and uploaded once however many objects use it. Objects are
/// placed in the layer's own points, like sprites: (x, y) is where an object stands (the bottom center of its model),
/// size its height. An object lower on the field is drawn in front. The view is straight on, each object tilted
/// forward by <see cref="Tilt"/> degrees, so a game moves them as it moves 2D sprites. Needs an accelerated canvas.
/// </summary>
public class SkiaScene3D : SkiaControl
{
    /// <summary>A model the layer can place: its scene, the point it stands on and its height at rest.</summary>
    sealed class ModelEntry
    {
        public string Name;
        public Scene3D Scene;
        public Vector3 Anchor;
        public float Height;
    }

    sealed class Object3D
    {
        public string Name;
        public ModelEntry Model;
        public float X, Y, Size, Facing;
        public bool Visible = true;
        public Player3D Player;
        public MaterialOverride[] Materials;
        public Matrix4x4[] Pose;
        public bool[] Hidden;
    }

    readonly List<ModelEntry> _models = new();
    readonly List<Object3D> _objects = new();
    readonly Dictionary<string, Object3D> _byName = new();
    readonly List<(string Model, string Error)> _errors = new();
    readonly RenderTarget _target = new();
    RenderItem[] _items = new RenderItem[16];
    NodeTransform[] _locals = Array.Empty<NodeTransform>();
    ActionOnTickAnimator _loop;
    long _lastNanos;
    float _tilt = 15, _outlineWidth;
    bool _playing = true;
    Look _look;
    Color _outlineColor = Colors.Black;

    #region PROPERTIES

    /// <summary>Forward tilt of every object, degrees (15 by default): how much of its top the player sees.</summary>
    public float Tilt
    {
        get => _tilt;
        set
        {
            _tilt = value;
            Update();
        }
    }

    /// <summary>How objects are drawn: Lit (default), Toon or Flat.</summary>
    public Look Look
    {
        get => _look;
        set
        {
            _look = value;
            Update();
        }
    }

    /// <summary>An outline around every shape, in points, like an illustration's stroke (0 = none).</summary>
    public float OutlineWidth
    {
        get => _outlineWidth;
        set
        {
            _outlineWidth = MathF.Max(0, value);
            Update();
        }
    }

    /// <summary>The outline's color (black by default).</summary>
    public Color OutlineColor
    {
        get => _outlineColor;
        set
        {
            _outlineColor = value ?? Colors.Black;
            Update();
        }
    }

    /// <summary>Plays every animation of the layer (on by default); off stops them where they are.</summary>
    public bool IsPlaying
    {
        get => _playing;
        set
        {
            _playing = value;
            Changed();
        }
    }

    /// <summary>The objects' names, in the order they were added.</summary>
    public IReadOnlyList<string> ObjectNames => _objects.Select(o => o.Name).ToArray();

    /// <summary>Models whose files could not be read: the model name and why.</summary>
    public IReadOnlyList<(string Model, string Error)> Errors => _errors;

    /// <summary>The main animation of an object, by name (null when none or no such object).</summary>
    public string CurrentAnimation(string obj)
    {
        if (!_byName.TryGetValue(obj, out var o))
            return null;
        var a = o.Player.Main.Animation;
        return a >= 0 && a < o.Model.Scene.Animations.Length ? o.Model.Scene.Animations[a].Name : null;
    }

    /// <summary>Whether the layer asks for frames now (something moves).</summary>
    public bool IsAnimating => WantsFrames;

    #endregion

    #region EVENTS

    /// <summary>An object's animation played with <see cref="PlayOnce"/> or <see cref="Hold"/> ended: the object's name and the animation's.</summary>
    public event EventHandler<SceneAnimationEnded> AnimationEnded;

    /// <summary>A tap landed on an object (the nearest one under the finger). Taps on empty field go to what is behind the layer.</summary>
    public event EventHandler<SceneHit> Tapped3D;

    #endregion

    #region MODELS

    /// <summary>
    /// A model objects can be made of, by name, from the bytes of a .glb. The same byte array used by other layers or
    /// <see cref="SkiaModel3D"/> controls is read once. A name given again replaces the model.
    /// </summary>
    public SkiaScene3D Model(string name, byte[] glb)
    {
        AddModel(name, Scene3D.Shared(glb, out var error), error);
        return this;
    }

    /// <summary>A model made in code (<see cref="Model3D"/>) objects can be made of, by name.</summary>
    public SkiaScene3D Model(string name, Model3D model)
    {
        AddModel(name, model?.ToScene(), model == null ? "no model" : null);
        return this;
    }

    void AddModel(string name, Scene3D scene, string error)
    {
        if (scene == null)
        {
            _errors.Add((name, error));
            Super.Log($"[SkiaScene3D] model {name}: {error}");
            return;
        }
        var (lo, hi) = (scene.BoundsMin, scene.BoundsMax);
        var entry = new ModelEntry
        {
            Name = name,
            Scene = scene,
            Anchor = new Vector3((lo.X + hi.X) * 0.5f, lo.Y, (lo.Z + hi.Z) * 0.5f),
            Height = MathF.Max(hi.Y - lo.Y, 1e-3f),
        };
        var at = _models.FindIndex(m => m.Name == name);
        if (at >= 0)
            _models[at] = entry;
        else
            _models.Add(entry);
        if (_locals.Length < scene.Nodes.Length)
            _locals = new NodeTransform[scene.Nodes.Length];
    }

    #endregion

    #region OBJECTS

    /// <summary>
    /// Places an object made of <paramref name="model"/> (a name given to <see cref="Model(string, byte[])"/>), standing
    /// at (x, y) of the layer in points, <paramref name="size"/> points tall; it loops its model's first animation. The
    /// same name replaces an object. False when there is no such model.
    /// </summary>
    public bool Add(string name, string model, float x, float y, float size)
    {
        var entry = _models.Find(m => m.Name == model);
        if (entry == null)
            return false;
        var scene = entry.Scene;
        var obj = new Object3D
        {
            Name = name,
            Model = entry,
            X = x,
            Y = y,
            Size = size,
            Player = new Player3D(scene),
            Materials = new MaterialOverride[scene.Materials.Length],
            Pose = new Matrix4x4[scene.Nodes.Length],
            Hidden = new bool[scene.Nodes.Length],
        };
        if (_byName.TryGetValue(name, out var old))
            _objects[_objects.IndexOf(old)] = obj;
        else
            _objects.Add(obj);
        _byName[name] = obj;
        Changed();
        return true;
    }

    /// <summary>Removes an object; false when there is none such.</summary>
    public bool Remove(string name)
    {
        if (!_byName.Remove(name, out var obj))
            return false;
        _objects.Remove(obj);
        Changed();
        return true;
    }

    /// <summary>Removes every object.</summary>
    public void Clear()
    {
        _objects.Clear();
        _byName.Clear();
        Changed();
    }

    /// <summary>Moves an object: where it stands, in the layer's points.</summary>
    public bool SetPosition(string name, float x, float y)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        o.X = x;
        o.Y = y;
        Update();
        return true;
    }

    /// <summary>Turns an object around its vertical axis, degrees (0 = facing the player, 90 = to the right).</summary>
    public bool SetFacing(string name, float degrees)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        o.Facing = degrees;
        Update();
        return true;
    }

    /// <summary>An object's height in points.</summary>
    public bool SetSize(string name, float size)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        o.Size = size;
        Update();
        return true;
    }

    /// <summary>Shows or hides an object (a hidden one asks for no frames).</summary>
    public bool SetVisible(string name, bool visible)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        o.Visible = visible;
        Changed();
        return true;
    }

    /// <summary>Loops an object's animation by name, blending into it.</summary>
    public bool Play(string name, string animation) => WithAnimation(name, animation, 0);

    /// <summary>Plays an object's animation once, then back to the loop it played before.</summary>
    public bool PlayOnce(string name, string animation) => WithAnimation(name, animation, 1);

    /// <summary>Plays an object's animation once and holds its last frame.</summary>
    public bool Hold(string name, string animation) => WithAnimation(name, animation, 2);

    /// <summary>An object's animation speed, 1 = as authored (vary it so a crowd does not move in step).</summary>
    public bool SetSpeed(string name, float speed)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        o.Player.Speed = speed;
        return true;
    }

    /// <summary>Recolors one material of one object; null = the file's color.</summary>
    public bool SetMaterialColor(string name, string material, Color color)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        var i = o.Model.Scene.MaterialNamed(material);
        if (i < 0)
            return false;
        o.Materials[i].HasColor = color != null;
        o.Materials[i].Color = color != null ? Math3D.Linear(color) : default;
        Update();
        return true;
    }

    /// <summary>
    /// Puts an image on one material of one object (see <see cref="SkiaModel3D.SetMaterialImage"/>); null = the file's
    /// again. False when there is no such object or material.
    /// </summary>
    public bool SetMaterialImage(string name, string material, Texture3D image)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        var i = o.Model.Scene.MaterialNamed(material);
        if (i < 0)
            return false;
        o.Materials[i].Image = image;
        Update();
        return true;
    }

    /// <summary>Stops or resumes every animation of the layer.</summary>
    public void SetPlaying(bool on) => IsPlaying = on;

    bool WithAnimation(string name, string animation, int how)
    {
        if (!_byName.TryGetValue(name, out var o))
            return false;
        var a = o.Model.Scene.AnimationNamed(animation);
        if (a < 0)
            return false;
        switch (how)
        {
            case 0: o.Player.Play(a); break;
            case 1: o.Player.PlayOnce(a); break;
            default: o.Player.Hold(a); break;
        }
        Changed();
        return true;
    }

    #endregion

    #region FRAMES

    void Changed()
    {
        UpdateTicking();
        Update();
    }

    bool WantsFrames
    {
        get
        {
            if (!_playing)
                return false;
            foreach (var o in _objects)
                if (o.Visible && o.Player.Busy)
                    return true;
            return false;
        }
    }

    void UpdateTicking()
    {
        if (Superview == null || IsDisposed)
            return;
        if (WantsFrames)
        {
            _loop ??= new ActionOnTickAnimator(this, Tick);
            if (!_loop.IsRunning)
            {
                _lastNanos = 0;
                _loop.Start();
                Superview?.RequestNextFrame();
            }
        }
        else if (_loop is { IsRunning: true })
            _loop.Stop();
    }

    void Tick(long nanos)
    {
        var delta = _lastNanos == 0 ? 0 : (nanos - _lastNanos) / 1_000_000_000f;
        _lastNanos = nanos;
        Advance(delta);
        Update();
        if (!WantsFrames)
            _loop?.Stop();
    }

    /// <summary>The objects' animations advance by <paramref name="delta"/> seconds, then the app hears what ended.</summary>
    internal void Advance(float delta)
    {
        if (!_playing)
            return;
        // An index loop: a handler may add or remove objects.
        for (var i = 0; i < _objects.Count; i++)
        {
            var o = _objects[i];
            var ended = o.Player.Advance(o.Model.Scene, delta);
            if (ended >= 0)
                AnimationEnded?.Invoke(this, new SceneAnimationEnded(o.Name, o.Model.Scene.Animations[ended].Name));
        }
    }

    #endregion

    #region DRAWING

    /// <summary>The object's place in the layer: points, y down on the field, nearer the lower it stands.</summary>
    Matrix4x4 Placement(Object3D o)
    {
        var scale = o.Size / o.Model.Height;
        return Matrix4x4.CreateTranslation(-o.Model.Anchor)
               * Matrix4x4.CreateScale(scale)
               * Matrix4x4.CreateRotationY(Math3D.Radians(o.Facing))
               * Matrix4x4.CreateRotationX(Math3D.Radians(_tilt))
               * Matrix4x4.CreateTranslation(o.X, -o.Y, o.Y);
    }

    /// <summary>Every node's world matrix of an object, its place included, into its own pose.</summary>
    void Pose(Object3D o)
    {
        var scene = o.Model.Scene;
        var locals = _locals.AsSpan(0, scene.Nodes.Length);
        o.Player.Locals(scene, locals);
        scene.World(locals, o.Pose, Placement(o));
    }

    /// <summary>A straight-on view of the layer in points, deep enough for any object.</summary>
    static Matrix4x4 Camera(float w, float h)
    {
        var depth = (w + h) * 4 + 10_000;
        return Math3D.OrthographicGl(0, w, -h, 0, -depth, depth);
    }

    (float W, float H) SizeInPoints()
    {
        var scale = RenderingScale > 0 ? RenderingScale : 1;
        if (DrawingRect.Width > 0)
            return (DrawingRect.Width / scale, DrawingRect.Height / scale);
        return ((float)MeasuredSize.Units.Width, (float)MeasuredSize.Units.Height);
    }

    /// <summary>The nearest object surface under a point of the layer (points from its corner), null when none.</summary>
    public SceneHit HitTest(float x, float y)
    {
        var (w, h) = SizeInPoints();
        if (w <= 0 || h <= 0)
            return null;
        var (from, dir) = Scene3D.Ray(Camera(w, h), x / w, y / h);
        SceneHit best = null;
        var bestDistance = float.MaxValue;
        foreach (var o in _objects)
        {
            if (!o.Visible)
                continue;
            Pose(o);
            var scene = o.Model.Scene;
            if (scene.RayHit(o.Pose, o.Hidden, o.Materials, from, dir, out var node, out var primitive, out var t) && t < bestDistance)
            {
                bestDistance = t;
                best = new SceneHit(o.Name, scene.Nodes[node].Name, scene.MaterialOf(node, primitive));
            }
        }
        return best;
    }

    /// <summary>Where a gesture is inside this control, in points from its corner (as GetOffsetInsideControlInPoints).</summary>
    SKPoint PointInside(GestureEventProcessingInfo apply)
    {
        var offset = TranslateInputCoords(apply.ChildOffset, false);
        return new SKPoint((float)((apply.MappedLocation.X + offset.X) / RenderingScale - X), (float)((apply.MappedLocation.Y + offset.Y) / RenderingScale - Y));
    }

    /// <inheritdoc />
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (Tapped3D != null && args.Type == TouchActionResult.Tapped)
        {
            var at = PointInside(apply);
            var hit = HitTest(at.X, at.Y);
            if (hit != null)
            {
                Tapped3D?.Invoke(this, hit);
                return this;
            }
        }
        return base.ProcessGestures(args, apply);
    }

    /// <inheritdoc />
    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        var dest = ctx.Destination;
        int w = (int)MathF.Round(dest.Width), h = (int)MathF.Round(dest.Height);
        if (w <= 0 || h <= 0 || _objects.Count == 0)
            return;
        if (WantsFrames && _loop is not { IsRunning: true })
            UpdateTicking();
        var gr = Superview?.GetGRContext();
        if (gr == null)
            return;
        var count = 0;
        foreach (var o in _objects)
        {
            if (!o.Visible)
                continue;
            Pose(o);
            if (count == _items.Length)
                Array.Resize(ref _items, count * 2);
            _items[count++] = new RenderItem { Scene = o.Model.Scene, Pose = o.Pose, Hidden = o.Hidden, Materials = o.Materials };
        }
        var scale = MathF.Max(ctx.Scale, 0.1f);
        var style = new RenderStyle { Look = _look, Outline = _outlineWidth * scale, OutlineColor = Math3D.Srgb(_outlineColor) };
        var image = Renderer3D.Draw(Superview, gr, _target, _items, count, style, Camera(w / scale, h / scale), w, h);
        // The items keep no objects alive past the frame.
        Array.Clear(_items, 0, count);
        if (image != null)
            ctx.Context.Canvas.DrawImage(image, SKRect.Create(dest.Left, dest.Top, w, h));
    }

    /// <inheritdoc />
    public override void OnDisposing()
    {
        _loop?.Dispose();
        _loop = null;
        Renderer3D.Release(_target);
        base.OnDisposing();
    }

    #endregion
}
