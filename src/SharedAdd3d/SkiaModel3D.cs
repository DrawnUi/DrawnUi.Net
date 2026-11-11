#nullable disable
using System.Net.Http;
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

    Scene3D _scene;
    Player3D _player;
    readonly RenderTarget _target = new();
    NodeOverride[] _nodes;
    MaterialOverride[] _materials;
    NodeTransform[] _locals;
    Matrix4x4[] _pose;
    bool[] _ownHidden, _hidden;
    string _source;
    int _loadVersion;
    /// <summary>The leaning toward the pointer (<see cref="TiltToPointer"/>): degrees at the edge, the pointer's place from -1 to 1, on or not.</summary>
    float _tilt, _tiltX, _tiltY;
    bool _tiltOn;
    /// <summary>The view at the last <see cref="ViewChanged"/>.</summary>
    float _shownYaw = float.NaN, _shownPitch, _shownZoom;
    readonly RenderItem[] _items = new RenderItem[1];
    bool _hiddenDirty = true;
    ActionOnTickAnimator _loop;
    long _lastNanos;
    bool _drawn;
    string _animation;
    float _yaw = 20, _pitch = 10, _autoRotate, _zoom = 1, _outlineWidth;
    bool _orbit = true;
    /// <summary>The view the app set last (the one <see cref="ResetView"/> glides back to).</summary>
    float _homeYaw = 20, _homePitch = 10, _homeZoom = 1;
    /// <summary>The glide back to the home view, 0 to 1 (1 = none running), and where it started.</summary>
    float _reset = 1, _fromYaw, _fromPitch, _fromZoom;
    bool _pinchZoom, _resetOnDoubleTap, _lockPitch;
    float _minZoom = 0.5f, _maxZoom = 2.5f;
    /// <summary>This gesture's drag: 0 not decided yet, 1 turns the model, 2 left to the parent (vertical, pitch locked).</summary>
    int _panAxis;
    long _lastTapMs;
    /// <summary>Drawn since the last frame tick: a model that was not (off screen) stops asking for frames.</summary>
    bool _painted;
    /// <summary>Something changed since the last 3D render (every change calls Update); false = its image can be shown again.</summary>
    bool _stale = true;
    Look _look;
    bool _antialias;
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

    /// <summary>An empty model that shows the .glb its <see cref="Source"/> names once it is loaded.</summary>
    public SkiaModel3D() : this((Scene3D)null, null)
    {
    }

    SkiaModel3D(Scene3D scene, string error)
    {
        _player = new Player3D(null);
        Attach(scene);
        Error = error;
        if (error != null)
            Super.Log($"[SkiaModel3D] {error}");
    }

    /// <summary>Shows a scene: its own pose, node and material slots; the playing state and speed carry over.</summary>
    void Attach(Scene3D scene)
    {
        var old = _player;
        _scene = scene;
        _player = new Player3D(scene) { Playing = old.Playing, Speed = old.Speed, Fade = old.Fade };
        var nodes = scene?.Nodes.Length ?? 0;
        _nodes = new NodeOverride[nodes];
        _locals = new NodeTransform[nodes];
        _pose = new Matrix4x4[nodes];
        _ownHidden = new bool[nodes];
        _hidden = new bool[nodes];
        _hiddenDirty = true;
        _materials = new MaterialOverride[scene?.Materials.Length ?? 0];
        if (scene != null && _animation != null && scene.AnimationNamed(_animation) is var a and >= 0)
        {
            _player.Play(a);
            _player.FadeNow();
        }
    }

    #region PROPERTIES

    /// <summary>The file could not be read, or loading <see cref="Source"/> failed: why (null when it was read).</summary>
    public string Error { get; private set; }

    /// <summary>
    /// The .glb to show, loaded in the background: a file shipped with the app (a name or path, as for images) or an
    /// http(s) address. <see cref="LoadState"/> and <see cref="LoadStateChanged"/> follow it; the app draws its own
    /// placeholder and error from them (the reason is in <see cref="Error"/>). Node and material changes made before
    /// the model is ready do not carry over: make them on Ready. Same as DrawnUI for Rust's source.
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            _source = value;
            _ = Load(value, ++_loadVersion);
        }
    }

    /// <summary>Where loading <see cref="Source"/> stands; Ready for a model given as bytes or made in code.</summary>
    public LoadState3D LoadState { get; private set; } = LoadState3D.Ready;

    /// <summary>Loading <see cref="Source"/> started (Loading), ended (Ready), or failed (Failed, the reason in <see cref="Error"/>).</summary>
    public event EventHandler<LoadState3D> LoadStateChanged;

    void SetLoadState(LoadState3D state)
    {
        if (LoadState == state)
            return;
        LoadState = state;
        LoadStateChanged?.Invoke(this, state);
    }

    async Task Load(string source, int version)
    {
        SetLoadState(LoadState3D.Loading);
        byte[] bytes = null;
        string problem = null;
        try
        {
            using var stream = await SkiaImageManager.OpenStreamAsync(source);
            if (stream == null)
                problem = "no file";
            else
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy);
                bytes = copy.ToArray();
            }
        }
        catch (HttpRequestException e)
        {
            problem = e.StatusCode is { } code ? $"the server answered {(int)code} {code}" : "there was no answer from the server";
        }
        catch (Exception e)
        {
            problem = e.Message;
        }
        Scene3D scene = null;
        if (bytes != null)
            scene = Scene3D.Shared(bytes, out problem);
        // The scene goes in before the next frame draws, on the drawing thread.
        SafeAction(() =>
        {
            if (version != _loadVersion || IsDisposed)
                return;
            if (scene == null)
            {
                Error = problem ?? "the file could not be read";
                Super.Log($"[SkiaModel3D] {source}: {Error}");
                Attach(null);
                SetLoadState(LoadState3D.Failed);
            }
            else
            {
                Error = null;
                Attach(scene);
                SetLoadState(LoadState3D.Ready);
            }
            Changed();
        });
    }

    /// <summary>
    /// Leans the model toward the mouse over it or the finger on it, up to this many degrees at the control's edge
    /// (0 = off, the default); it glides home when the pointer leaves or the finger lifts. On, drags do not turn it.
    /// The app may move the home view meanwhile (a card flip animates Yaw) and the lean follows.
    /// </summary>
    public float TiltToPointer
    {
        get => _tilt;
        set
        {
            _tilt = MathF.Max(0, value);
            ReceivesHover = _tilt > 0;
            if (_tilt == 0)
                _tiltOn = false;
            Changed();
        }
    }

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
            _yaw = _homeYaw = value;
            Update();
        }
    }

    /// <summary>The view's tilt, degrees (10 by default).</summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = _homePitch = value;
            Update();
        }
    }

    /// <summary>Distance as a factor of the one that fits the scene (1 = fits, below 1: closer).</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = _homeZoom = MathF.Max(0.05f, value);
            Update();
        }
    }

    /// <summary>Drags turn the scene (on by default).</summary>
    public bool Orbit
    {
        get => _orbit;
        set => _orbit = value;
    }

    /// <summary>
    /// Two fingers pinching and the mouse wheel zoom the view, between <see cref="MinZoom"/> and <see cref="MaxZoom"/>
    /// (off by default; on, the wheel over the model zooms instead of scrolling the page).
    /// </summary>
    public bool PinchZoom
    {
        get => _pinchZoom;
        set => _pinchZoom = value;
    }

    /// <summary>The nearest a pinch or the wheel brings the view, as a <see cref="Zoom"/> factor (0.5 by default).</summary>
    public float MinZoom
    {
        get => _minZoom;
        set => _minZoom = MathF.Max(0.05f, value);
    }

    /// <summary>The farthest a pinch or the wheel takes the view, as a <see cref="Zoom"/> factor (2.5 by default).</summary>
    public float MaxZoom
    {
        get => _maxZoom;
        set => _maxZoom = MathF.Max(0.05f, value);
    }

    /// <summary>Two taps in a row glide the view back home (<see cref="ResetView"/>); off by default.</summary>
    public bool ResetOnDoubleTap
    {
        get => _resetOnDoubleTap;
        set => _resetOnDoubleTap = value;
    }

    /// <summary>
    /// Drags turn the model only around its vertical axis, and a drag that starts vertical is left to the parent: a
    /// model in a scrolling list turns sideways while the list still scrolls (off by default).
    /// </summary>
    public bool LockPitch
    {
        get => _lockPitch;
        set => _lockPitch = value;
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

    /// <summary>
    /// Smooth 3D edges (4x multisampling, or the most the GPU offers), off by default. On, the control's GPU frame
    /// takes about four times the memory (a 300 x 200 point control at 3x: about 13 MB more); off, nothing extra is
    /// allocated. Can be switched at any time.
    /// </summary>
    public bool Antialias
    {
        get => _antialias;
        set
        {
            if (_antialias == value)
                return;
            _antialias = value;
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
    /// The view moved (turn, tilt or zoom: a drag, the auto rotation, a pinch, a reset, the app), at most once per
    /// frame, raised as the model draws: the place to move labels kept on its points (<see cref="ScreenPosition"/>).
    /// </summary>
    public event EventHandler ViewChanged;

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

    /// <summary>
    /// Glides the view (turn, tilt, zoom) back to the one the app set last through <see cref="Yaw"/>, <see cref="Pitch"/>
    /// and <see cref="Zoom"/>, in 300 ms; drags and pinches never change that home view.
    /// </summary>
    public void ResetView()
    {
        _fromYaw = _yaw;
        _fromPitch = _pitch;
        _fromZoom = _zoom;
        _reset = 0;
        Changed();
    }

    /// <summary>
    /// Where a point of the scene (scene units, the space of <see cref="ModelHit.Point"/>) shows in this control now:
    /// points from its top left, and <see cref="PointOnScreen.Occluded"/> when the model's own surface hides it (a ray
    /// from the eye to the point meets the model first). Null with no scene, before the control has a size, or for a
    /// point behind the eye. Call it each frame to keep a label on a point of a turning model.
    /// </summary>
    public PointOnScreen? ScreenPosition(Vector3 point)
    {
        if (_scene == null)
            return null;
        var (w, h) = SizeInPoints();
        if (w <= 0 || h <= 0)
            return null;
        var camera = Camera(w, h);
        var clip = Vector4.Transform(new Vector4(point, 1), camera);
        if (clip.W <= 1e-6f)
            return null;
        float fx = (clip.X / clip.W + 1) * 0.5f, fy = (1 - clip.Y / clip.W) * 0.5f;
        ComputePose();
        var (from, dir) = Scene3D.Ray(camera, fx, fy);
        // A point on the surface must not hide behind itself: half a percent of the model's size.
        var near = (_scene.BoundsMax - _scene.BoundsMin).Length() * 0.005f;
        var occluded = _scene.RayBlocked(_pose, _hidden, _materials, from, dir, Vector3.Distance(from, point) - near);
        return new PointOnScreen(fx * w, fy * h, occluded);
    }

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

    /// <summary>
    /// Puts an image on a material (a skin, a label, a photo, a pattern) in place of the file's own; null = the file's
    /// again. The image is multiplied by the material's color: a white material (or SetMaterialColor(name,
    /// Colors.White)) shows it as it is. False when there is no such material.
    /// </summary>
    public bool SetMaterialImage(string name, Texture3D image)
    {
        var i = _scene?.MaterialNamed(name) ?? -1;
        if (i < 0)
            return false;
        _materials[i].Image = image;
        Update();
        return true;
    }

    /// <summary>
    /// How matte a material is, 0 (glossy: leather, lacquer) to 1 (matte: no highlight, the default); null = the
    /// file's own roughness. Lit look only. False when there is no such material.
    /// </summary>
    public bool SetMaterialRoughness(string name, float? roughness)
    {
        var i = _scene?.MaterialNamed(name) ?? -1;
        if (i < 0)
            return false;
        _materials[i].HasRoughness = roughness.HasValue;
        _materials[i].Roughness = Math.Clamp(roughness ?? 1, 0, 1);
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

    bool WantsFrames => _scene != null && (_autoRotate != 0 || _reset < 1 || _player.Busy || TiltMoving);

    /// <summary>The lean toward the pointer has not reached where the pointer says.</summary>
    bool TiltMoving
    {
        get
        {
            if (_tilt <= 0)
                return false;
            var (yaw, pitch) = TiltTarget();
            return MathF.Abs(yaw - _yaw) > 0.02f || MathF.Abs(pitch - _pitch) > 0.02f;
        }
    }

    (float Yaw, float Pitch) TiltTarget() => _tiltOn ? (_homeYaw - _tiltX * _tilt, _homePitch + _tiltY * _tilt) : (_homeYaw, _homePitch);

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
                _painted = true;
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
        if (!_painted)
        {
            // Not drawn since the last tick (scrolled off screen, under a hidden parent): no frames, time stands still.
            // Drawing it again starts the loop (Paint).
            _loop.Stop();
            return;
        }
        _painted = false;
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
        if (_tilt > 0)
        {
            // Pointer moves come in bursts: the lean chases where they point, reaching it in about 0.15 s.
            var (yaw, pitch) = TiltTarget();
            var k = MathF.Min(1, delta * 14);
            _yaw += (yaw - _yaw) * k;
            _pitch += (pitch - _pitch) * k;
        }
        if (_reset < 1)
        {
            _reset = MathF.Min(1, _reset + delta / 0.3f);
            var e = 1 - MathF.Pow(1 - _reset, 3);
            // The yaw goes the short way round.
            var turn = ((_homeYaw - _fromYaw) % 360 + 540) % 360 - 180;
            _yaw = _fromYaw + turn * e;
            _pitch = _fromPitch + (_homePitch - _fromPitch) * e;
            _zoom = _fromZoom + (_homeZoom - _fromZoom) * e;
        }
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
                case TouchActionResult.Pointer or TouchActionResult.Down or TouchActionResult.Panning when _tilt > 0:
                    {
                        var pointer = PointInside(apply);
                        var (w, h) = SizeInPoints();
                        if (w > 0 && h > 0)
                        {
                            _tiltX = Math.Clamp(pointer.X / w * 2 - 1, -1, 1);
                            _tiltY = Math.Clamp(pointer.Y / h * 2 - 1, -1, 1);
                            _tiltOn = true;
                            Changed();
                        }
                        if (args.Type == TouchActionResult.Pointer)
                            break;
                        base.ProcessGestures(args, apply);
                        return this;
                    }
                case TouchActionResult.Up when _tilt > 0:
                    _tiltOn = false;
                    Changed();
                    break;
                case TouchActionResult.Down when _orbit || _pinchZoom || _resetOnDoubleTap || Tapped3D != null:
                    _panAxis = 0;
                    base.ProcessGestures(args, apply);
                    return this;
                case TouchActionResult.Panning when _pinchZoom && args.Event.Manipulation != null:
                    _reset = 1;
                    _zoom = Math.Clamp(_zoom / MathF.Max(0.2f, 1 + (float)args.Event.Manipulation.Scale), _minZoom, _maxZoom);
                    Update();
                    return this;
                case TouchActionResult.Panning when _orbit:
                    if (_lockPitch)
                    {
                        if (_panAxis == 0)
                        {
                            var total = args.Event.Distance.Total;
                            if (MathF.Abs(total.X) + MathF.Abs(total.Y) < 2)
                                return this;
                            _panAxis = MathF.Abs(total.Y) > MathF.Abs(total.X) ? 2 : 1;
                        }
                        if (_panAxis == 2)
                            return null;
                    }
                    var scale = MathF.Max(RenderingScale, 0.1f);
                    _reset = 1;
                    // The surface follows the finger: a drag to the right turns the near side to the right (the camera
                    // goes the other way round), in points whatever the density. Same as DrawnUI for Rust.
                    _yaw -= args.Event.Distance.Delta.X / scale * 0.5f;
                    if (!_lockPitch)
                        _pitch = Math.Clamp(_pitch + args.Event.Distance.Delta.Y / scale * 0.3f, -60, 80);
                    Update();
                    return this;
                case TouchActionResult.Wheel when _pinchZoom && args.Event.Wheel != null:
                    var perNotch = Superview?.WheelDeltaPerNotch ?? 0;
                    var notches = perNotch > 0 ? args.Event.Wheel.Delta / perNotch : MathF.Sign(args.Event.Wheel.Delta);
                    _reset = 1;
                    _zoom = Math.Clamp(_zoom * MathF.Pow(0.88f, notches), _minZoom, _maxZoom);
                    Update();
                    return this;
                case TouchActionResult.Tapped when _resetOnDoubleTap && Environment.TickCount64 - _lastTapMs < 300:
                    _lastTapMs = 0;
                    ResetView();
                    return this;
                case TouchActionResult.Tapped when _resetOnDoubleTap || Tapped3D != null:
                    if (_resetOnDoubleTap)
                        _lastTapMs = Environment.TickCount64;
                    if (Tapped3D == null)
                        return this;
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
        _painted = true;
        if (WantsFrames && _loop is not { IsRunning: true })
            UpdateTicking();
        if (_yaw != _shownYaw || _pitch != _shownPitch || _zoom != _shownZoom)
        {
            _shownYaw = _yaw;
            _shownPitch = _pitch;
            _shownZoom = _zoom;
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        var gr = Superview?.GetGRContext();
        if (gr == null)
            return;
        // Nothing changed since the last render (a parent redraws: the page scrolls, a sibling changes): its image
        // again, no 3D pass. GL keeps one texture per control; other GPU backends render every time.
        if (!_stale && gr.Backend == GRBackend.OpenGL && _target.Image != null && _target.Context == gr && _target.Width == w && _target.Height == h)
        {
            ctx.Context.Canvas.DrawImage(_target.Image, SKRect.Create(dest.Left, dest.Top, w, h));
            return;
        }
        _stale = false;
        ComputePose();
        _items[0] = new RenderItem { Scene = _scene, Pose = _pose, Hidden = _hidden, Materials = _materials };
        var style = new RenderStyle { Look = _look, Outline = _outlineWidth * ctx.Scale, OutlineColor = Math3D.Srgb(_outlineColor), Samples = _antialias ? 4 : 1 };
        var image = Renderer3D.Draw(Superview, gr, _target, _items, 1, style, Camera(w, h), w, h);
        if (image != null)
            ctx.Context.Canvas.DrawImage(image, SKRect.Create(dest.Left, dest.Top, w, h));
    }

    /// <inheritdoc />
    public override bool OnHover(bool state)
    {
        if (!state && _tiltOn)
        {
            // The pointer left: the lean glides home.
            _tiltOn = false;
            Changed();
        }
        return base.OnHover(state);
    }

    /// <inheritdoc />
    public override void Update()
    {
        _stale = true;
        base.Update();
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
