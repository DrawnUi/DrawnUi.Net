#nullable disable
using System.Numerics;

namespace DrawnUi.Add3d;

/// <summary>
/// Plays a scene's animations: one main animation (looped, once and back, or held on its last frame), a cross-fade
/// from the one before, and animations layered on top for other objects.
/// </summary>
internal sealed class Player3D
{
    /// <summary>The main animation; Animation -1 = none.</summary>
    public Track Main = new(-1, 0, true);

    /// <summary>The animation the main one fades from, and the seconds the fade has run.</summary>
    Track _fading = new(-1, 0, true);
    float _fadeElapsed;

    /// <summary>Cross-fade length, seconds.</summary>
    public float Fade = 0.25f;

    /// <summary>A once-only animation goes back to this looping one when it ends (-1 = none).</summary>
    int _backTo = -1;

    /// <summary>Animations playing at the same time as the main one.</summary>
    readonly List<Track> _layers = new();

    public bool Playing = true;
    public float Speed = 1;

    /// <summary>Scratch for the pose faded from.</summary>
    NodeTransform[] _old = Array.Empty<NodeTransform>();

    /// <summary>Loops the scene's first animation (none when it has none).</summary>
    public Player3D(Scene3D scene)
    {
        if (scene != null && scene.Animations.Length > 0)
            Main = new Track(0, 0, true);
    }

    public bool HasMain => Main.Animation >= 0;

    void Start(int animation, bool looped)
    {
        if (Main.Animation >= 0 && Fade > 0)
        {
            _fading = Main;
            _fadeElapsed = 0;
        }
        Main = new Track(animation, 0, looped);
        Playing = true;
    }

    /// <summary>Ends a fade at once (the first animation shows without one).</summary>
    public void FadeNow() => _fading.Animation = -1;

    /// <summary>Loops <paramref name="animation"/>, blending into it.</summary>
    public void Play(int animation)
    {
        _backTo = -1;
        Start(animation, true);
    }

    /// <summary>Plays <paramref name="animation"/> once, then blends back into the looping one that played before it.</summary>
    public void PlayOnce(int animation)
    {
        if (Main.Animation >= 0 && Main.Looped)
            _backTo = Main.Animation;
        Start(animation, false);
    }

    /// <summary>Plays <paramref name="animation"/> once and stays on its last frame.</summary>
    public void Hold(int animation)
    {
        _backTo = -1;
        Start(animation, false);
    }

    /// <summary>Loops another animation together with the main one.</summary>
    public void AddLayer(int animation)
    {
        var found = false;
        foreach (var l in _layers)
            found |= l.Animation == animation;
        if (!found)
            _layers.Add(new Track(animation, 0, true));
        Playing = true;
    }

    public void RemoveLayer(int animation) => _layers.RemoveAll(l => l.Animation == animation);

    /// <summary>Something moves: the frames go on.</summary>
    public bool Busy => _fading.Animation >= 0 || (Playing && (Main.Animation >= 0 || _layers.Count > 0));

    /// <summary>One frame of time. Returns the once-only animation that just ended, -1 when none did.</summary>
    public int Advance(Scene3D scene, float delta)
    {
        if (!Playing)
            return -1;
        var step = delta * Speed;
        if (_fading.Animation >= 0)
        {
            _fading.Time += step;
            _fadeElapsed += delta;
            if (_fadeElapsed >= Fade)
                _fading.Animation = -1;
        }
        for (var i = 0; i < _layers.Count; i++)
        {
            var l = _layers[i];
            l.Time += step;
            _layers[i] = l;
        }
        if (Main.Animation < 0)
            return -1;
        Main.Time += step;
        var duration = Main.Animation < scene.Animations.Length ? scene.Animations[Main.Animation].Duration : 0;
        if (Main.Looped || Main.Time < duration)
            return -1;
        Main.Time = duration;
        var ended = Main.Animation;
        if (_backTo >= 0)
        {
            var back = _backTo;
            _backTo = -1;
            Start(back, true);
        }
        else
        {
            // Held on its last frame (a death).
            Playing = _layers.Count > 0;
        }
        return ended;
    }

    /// <summary>The local transform of every node: the file's, then the animations' (blended while fading).</summary>
    public void Locals(Scene3D scene, Span<NodeTransform> local)
    {
        scene.Rest(local);
        if (Main.Animation >= 0)
        {
            if (_fading.Animation >= 0)
            {
                var n = scene.Nodes.Length;
                if (_old.Length < n)
                    _old = new NodeTransform[n];
                var old = _old.AsSpan(0, n);
                scene.Rest(old);
                scene.Apply(_fading, old);
                scene.Apply(Main, local);
                var w = Math.Clamp(_fadeElapsed / Fade, 0, 1);
                for (var i = 0; i < n; i++)
                {
                    ref var l = ref local[i];
                    var o = old[i];
                    l = new NodeTransform(Vector3.Lerp(o.T, l.T, w), Quaternion.Slerp(o.R, l.R, w), Vector3.Lerp(o.S, l.S, w));
                }
            }
            else
                scene.Apply(Main, local);
        }
        foreach (var layer in _layers)
            scene.Apply(layer, local);
    }
}
