#nullable disable
using System.Numerics;
using AppoMobi.Gestures;
using DrawnUi;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>
/// A glTF 2.0 scene (.glb) drawn in real 3D inside a DrawnUI screen: depth buffer, a light, base colors and color
/// textures, skeleton animations. Drawn with the GPU context Skia uses into a texture the control then shows, so layout,
/// gestures and everything drawn around it work as for any control. The app drives it: animations looped
/// (<see cref="Play"/>), played once and back (<see cref="PlayOnce"/>), held (<see cref="Hold"/>), layered
/// (<see cref="PlayLayer"/>), cross-faded; nodes and materials by name; taps that tell what was hit
/// (<see cref="Tapped3D"/>). Drag to turn it. Needs an accelerated canvas (RenderingMode Accelerated), and no
/// image cache on it or above it.
/// </summary>
public class SkiaModel3D : SkiaControl
{
    /// <summary>A node's own transform set from code: it replaces what the file and the animations give that part.</summary>
    struct NodeOverride
    {
        public bool HasT, HasR, HasS, Hidden;
        public Vector3 T, S;
        public Quaternion R;
    }

    readonly Scene3D _scene;
    readonly Player3D _player;
    readonly RenderTarget _target = new();
    readonly NodeOverride[] _nodes;
    readonly MaterialOverride[] _materials;
    readonly NodeTransform[] _locals;
    readonly Matrix4x4[] _pose;
    readonly bool[] _ownHidden, _hidden;
    readonly RenderItem[] _items = new RenderItem[1];
    bool _hiddenDirty = true;
    ActionOnTickAnimator _loop;
    long _lastNanos;
    bool _drawn;
    string _animation;
    float _yaw = 20, _pitch = 10, _autoRotate, _zoom = 1, _outlineWidth;
    bool _orbit = true;
    Look _look;
    Color _outlineColor = Colors.Black;

    /// <summary>
    /// A scene from the bytes of a .glb (or a .gltf with its buffers embedded). It plays its first animation in a
    /// loop; drags turn it. Controls showing the same byte array share one copy, read once.
    /// </summary>
    public SkiaModel3D(byte[] glb) : this(Scene3D.Shared(glb, out var error), error)
    {
    }

    /// <summary>A model made in code (<see cref="Model3D"/>): smooth shapes, flat colors. Same as the other otherwise.</summary>
    public SkiaModel3D(Model3D model) : this(model?.ToScene(), model == null ? "no model" : null)
    {
    }

    SkiaModel3D(Scene3D scene, string error)
    {
        _scene = scene;
        Error = error;
        _player = new Player3D(scene);
        var nodes = scene?.Nodes.Length ?? 0;
        _nodes = new NodeOverride[nodes];
        _locals = new NodeTransform[nodes];
        _pose = new Matrix4x4[nodes];
        _ownHidden = new bool[nodes];
        _hidden = new bool[nodes];
        _materials = new MaterialOverride[scene?.Materials.Length ?? 0];
        if (error != null)
            Super.Log($"[SkiaModel3D] {error}");
    }

    #region PROPERTIES

    /// <summary>The file could not be read: why (null when it was).</summary>
    public string Error { get; }

    /// <summary>The names of the scene's animations, in file order.</summary>
    public IReadOnlyList<string> Animations => _scene?.Animations.Select(a => a.Name).ToArray() ?? Array.Empty<string>();

    /// <summary>The names of the scene's nodes (objects, bones, cameras), in file order.</summary>
    public IReadOnlyList<string> NodeNames => _scene?.Nodes.Select(n => n.Name).ToArray() ?? Array.Empty<string>();

    /// <summary>The names of the scene's materials (its parts by look), in file order.</summary>
    public IReadOnlyList<string> MaterialNames => _scene?.Materials.Select(m => m.Name).ToArray() ?? Array.Empty<string>();

    /// <summary>The main animation playing, by name (null when none).</summary>
    public string CurrentAnimation =>
        _scene != null && _player.Main.Animation >= 0 && _player.Main.Animation < _scene.Animations.Length
            ? _scene.Animations[_player.Main.Animation].Name
            : null;

    /// <summary>
    /// The animation it loops from the start, by name (glTF animation name); the first one when not set. Set later,
    /// it plays it, blending into it.
    /// </summary>
    public string Animation
    {
        get => _animation;
        set
        {
            _animation = value;
            var i = _scene?.AnimationNamed(value) ?? -1;
            if (i < 0)
                return;
            var playing = _player.Playing;
            _player.Play(i);
            if (!_drawn)
            {
                // The start pose: no fade, and playing only when the app did not say otherwise.
                _player.FadeNow();
                _player.Playing = playing;
            }
            Changed();
        }
    }

    /// <summary>Plays the animations (on by default); off stops them where they are.</summary>
    public bool IsPlaying
    {
        get => _player.Playing;
        set
        {
            _player.Playing = value;
            Changed();
        }
    }

    /// <summary>Animation speed, 1 = as authored.</summary>
    public float Speed
    {
        get => _player.Speed;
        set => _player.Speed = value;
    }

    /// <summary>How long one animation blends into the next, milliseconds (250 by default, 0 = cut).</summary>
    public float CrossfadeMs
    {
        get => _player.Fade * 1000;
        set => _player.Fade = MathF.Max(0, value) / 1000;
    }

    /// <summary>Turns the scene by itself, degrees per second (0 = still).</summary>
    public float AutoRotate
    {
        get => _autoRotate;
        set
        {
            _autoRotate = value;
            Changed();
        }
    }

    /// <summary>The view's turn around the vertical axis, degrees (20 by default).</summary>
    public float Yaw
    {
        get => _yaw;
        set
        {
            _yaw = value;
            Update();
        }
    }

    /// <summary>The view's tilt, degrees (10 by default).</summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = value;
            Update();
        }
    }

    /// <summary>Distance as a factor of the one that fits the scene (1 = fits, below 1: closer).</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = MathF.Max(0.05f, value);
            Update();
        }
    }

    /// <summary>Drags turn the scene (on by default).</summary>
    public bool Orbit
    {
        get => _orbit;
        set => _orbit = value;
    }

    /// <summary>How it is drawn: Lit (default), Toon or Flat.</summary>
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

    #endregion

    #region EVENTS

    /// <summary>An animation played with <see cref="PlayOnce"/> or <see cref="Hold"/> ended: its name.</summary>
    public event EventHandler<string> AnimationEnded;

    /// <summary>
    /// A tap landed on the scene's surface (not on empty space around it): the node, the material and the point it hit.
    /// </summary>
    public event EventHandler<ModelHit> Tapped3D;

    #endregion

    #region METHODS

    /// <summary>Loops the animation with this name, blending into it; false when the scene has none such.</summary>
    public bool Play(string name) => WithAnimation(name, 0);

    /// <summary>
    /// Plays the animation with this name once, then blends back into the looping one that played before it (an
    /// attack, then idle again); <see cref="AnimationEnded"/> hears its end. With nothing looping before, it holds its
    /// last frame (a death). False when there is none such.
    /// </summary>
    public bool PlayOnce(string name) => WithAnimation(name, 1);

    /// <summary>Plays the animation once and stays on its last frame; nothing goes back. False when there is none such.</summary>
    public bool Hold(string name) => WithAnimation(name, 2);

    /// <summary>
    /// Loops another animation together with the main one (another object of the scene: a coin spinning while the
    /// boss attacks). False when the scene has none such.
    /// </summary>
    public bool PlayLayer(string name) => WithAnimation(name, 3);

    /// <summary>Stops an animation started with <see cref="PlayLayer"/>.</summary>
    public void StopLayer(string name) => WithAnimation(name, 4);

    /// <summary>Shows or hides a node by name, with what hangs under it. False when there is none such.</summary>
    public bool SetNodeVisible(string name, bool visible)
    {
        var i = NodeIndex(name);
        if (i < 0)
            return false;
        _nodes[i].Hidden = !visible;
        _hiddenDirty = true;
        Update();
        return true;
    }

    /// <summary>Moves a node by name: its own position, in its parent's units. It replaces what the file and the animations give it.</summary>
    public bool SetNodeTranslation(string name, float x, float y, float z)
    {
        var i = NodeIndex(name);
        if (i < 0)
            return false;
        _nodes[i].HasT = true;
        _nodes[i].T = new Vector3(x, y, z);
        Update();
        return true;
    }

    /// <summary>Turns a node by name: rotations around x, y, z in degrees (applied in that order).</summary>
    public bool SetNodeRotation(string name, float x, float y, float z)
    {
        var i = NodeIndex(name);
        if (i < 0)
            return false;
        _nodes[i].HasR = true;
        _nodes[i].R = Math3D.Euler(x, y, z);
        Update();
        return true;
    }

    /// <summary>Scales a node by name.</summary>
    public bool SetNodeScale(string name, float x, float y, float z)
    {
        var i = NodeIndex(name);
        if (i < 0)
            return false;
        _nodes[i].HasS = true;
        _nodes[i].S = new Vector3(x, y, z);
        Update();
        return true;
    }

    /// <summary>Gives a node back what the file and the animations say: position, turn, scale, shown.</summary>
    public bool ResetNode(string name)
    {
        var i = NodeIndex(name);
        if (i < 0)
            return false;
        _nodes[i] = default;
        _hiddenDirty = true;
        Update();
        return true;
    }

    /// <summary>
    /// Recolors a material by name (a part of the model: skin, armor); null = the file's color. A texture is tinted by
    /// it. False when there is none such.
    /// </summary>
    public bool SetMaterialColor(string name, Color color)
    {
        var i = _scene?.MaterialNamed(name) ?? -1;
        if (i < 0)
            return false;
        _materials[i].HasColor = color != null;
        _materials[i].Color = color != null ? Math3D.Linear(color) : default;
        Update();
        return true;
    }

    /// <summary>Shows or hides every surface with this material. False when there is none such.</summary>
    public bool SetMaterialVisible(string name, bool visible)
    {
        var i = _scene?.MaterialNamed(name) ?? -1;
        if (i < 0)
            return false;
        _materials[i].Hidden = !visible;
        Update();
        return true;
    }

    #endregion

    bool WithAnimation(string name, int how)
    {
        var i = _scene?.AnimationNamed(name) ?? -1;
        if (i < 0)
            return false;
        switch (how)
        {
            case 0: _player.Play(i); break;
            case 1: _player.PlayOnce(i); break;
            case 2: _player.Hold(i); break;
            case 3: _player.AddLayer(i); break;
            default: _player.RemoveLayer(i); break;
        }
        Changed();
        return true;
    }

    int NodeIndex(string name) => _scene?.NodeNamed(name) ?? -1;

    /// <summary>Something that may start or stop the motion changed: frames on or off, and one redraw.</summary>
    void Changed()
    {
        UpdateTicking();
        Update();
    }

    bool WantsFrames => _scene != null && (_autoRotate != 0 || _player.Busy);

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

    /// <summary>One frame: time moves on, an animation played once that ended is reported.</summary>
    void Tick(long nanos)
    {
        var delta = _lastNanos == 0 ? 0 : (nanos - _lastNanos) / 1_000_000_000f;
        _lastNanos = nanos;
        Advance(delta);
        Update();
        if (!WantsFrames)
            _loop?.Stop();
    }

    /// <summary>Advances the animations and the auto rotation by <paramref name="delta"/> seconds.</summary>
    internal void Advance(float delta)
    {
        if (_scene == null)
            return;
        _yaw = (_yaw + _autoRotate * delta) % 360;
        var ended = _player.Advance(_scene, delta);
        if (ended >= 0)
            AnimationEnded?.Invoke(this, _scene.Animations[ended].Name);
    }

    /// <summary>Whether the control asks for frames now (it moves).</summary>
    public bool IsAnimating => WantsFrames;

    /// <summary>Every node's world matrix for this frame: the animations', then what code set.</summary>
    void ComputePose()
    {
        _player.Locals(_scene, _locals);
        for (var i = 0; i < _locals.Length; i++)
        {
            ref readonly var o = ref _nodes[i];
            if (o.HasT)
                _locals[i].T = o.T;
            if (o.HasR)
                _locals[i].R = o.R;
            if (o.HasS)
                _locals[i].S = o.S;
        }
        _scene.World(_locals, _pose, Matrix4x4.Identity);
        if (_hiddenDirty)
        {
            for (var i = 0; i < _nodes.Length; i++)
                _ownHidden[i] = _nodes[i].Hidden;
            _scene.Hidden(_ownHidden, _hidden);
            _hiddenDirty = false;
        }
    }

    Matrix4x4 Camera(float width, float height) => _scene.Camera(_yaw, _pitch, _zoom, width / MathF.Max(height, 1));

    /// <summary>What the ray through this point of the control (in points from its corner) meets first, null when nothing.</summary>
    public ModelHit HitTest(float x, float y)
    {
        if (_scene == null)
            return null;
        var (w, h) = SizeInPoints();
        if (w <= 0 || h <= 0)
            return null;
        ComputePose();
        var (from, dir) = Scene3D.Ray(Camera(w, h), x / w, y / h);
        if (!_scene.RayHit(_pose, _hidden, _materials, from, dir, out var node, out var primitive, out var t))
            return null;
        return new ModelHit(_scene.Nodes[node].Name, _scene.MaterialOf(node, primitive), from + dir * t);
    }

    (float W, float H) SizeInPoints()
    {
        var scale = RenderingScale > 0 ? RenderingScale : 1;
        if (DrawingRect.Width > 0)
            return (DrawingRect.Width / scale, DrawingRect.Height / scale);
        return ((float)MeasuredSize.Units.Width, (float)MeasuredSize.Units.Height);
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
        if (_scene != null)
        {
            switch (args.Type)
            {
                case TouchActionResult.Down when _orbit || Tapped3D != null:
                    base.ProcessGestures(args, apply);
                    return this;
                case TouchActionResult.Panning when _orbit:
                    var scale = MathF.Max(RenderingScale, 0.1f);
                    _yaw += args.Event.Distance.Delta.X / scale * 0.5f;
                    _pitch = Math.Clamp(_pitch + args.Event.Distance.Delta.Y / scale * 0.3f, -60, 80);
                    Update();
                    return this;
                case TouchActionResult.Tapped when Tapped3D != null:
                    var at = PointInside(apply);
                    var hit = HitTest(at.X, at.Y);
                    if (hit != null)
                    {
                        Tapped3D?.Invoke(this, hit);
                        return this;
                    }
                    break;
            }
        }
        return base.ProcessGestures(args, apply);
    }

    /// <inheritdoc />
    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        if (_scene == null)
            return;
        var dest = ctx.Destination;
        int w = (int)MathF.Round(dest.Width), h = (int)MathF.Round(dest.Height);
        if (w <= 0 || h <= 0)
            return;
        _drawn = true;
        if (WantsFrames && _loop is not { IsRunning: true })
            UpdateTicking();
        var gr = Superview?.GetGRContext();
        if (gr == null)
            return;
        ComputePose();
        _items[0] = new RenderItem { Scene = _scene, Pose = _pose, Hidden = _hidden, Materials = _materials };
        var style = new RenderStyle { Look = _look, Outline = _outlineWidth * ctx.Scale, OutlineColor = Math3D.Srgb(_outlineColor) };
        var image = Renderer3D.Draw(Superview, gr, _target, _items, 1, style, Camera(w, h), w, h);
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
}
