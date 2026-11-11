namespace Add3dGallery;

/// <summary>The gallery's state and its game loop: what the app moves every frame.</summary>
public partial class GalleryPage
{
    /// <summary>A small random number generator: the same confetti and crowd every run (as the Rust gallery).</summary>
    struct Dice
    {
        uint _state;

        public Dice(uint seed) => _state = seed;

        public float Next()
        {
            _state = unchecked(_state * 1_664_525u + 1_013_904_223u);
            return (_state >> 8) / (float)(1 << 24);
        }
    }

    struct Piece
    {
        public float X, Y, Fall, Sway, Spin, Facing;
    }

    struct Walker
    {
        public float X, Y, Speed;
    }

    const int PieceCount = 28;
    const int MaxMobs = 100;

    // Object names made once: a frame formats no strings.
    static readonly string[] PieceNames = Enumerable.Range(0, PieceCount).Select(i => $"c{i}").ToArray();
    static readonly string[] MobNames = Enumerable.Range(0, MaxMobs).Select(i => $"m{i}").ToArray();

    float _time, _pan;
    bool _unlocked = true;
    /// <summary>The medal's spin after unlocking, 0 to 1.</summary>
    float _spin = 1;
    bool _liked = true;
    /// <summary>The heart's pop after a like, 1 to 0.</summary>
    float _pop;
    bool _open;
    readonly Piece[] _pieces = new Piece[PieceCount];
    bool _rainReady;
    readonly Walker[] _walkers = new Walker[MaxMobs];
    int _walkersOnField = -1;
    int _mobs;
    Dice _dice = new(7);

    static float EaseOut(float t) => 1 - MathF.Pow(1 - Math.Clamp(t, 0, 1), 3);

    public override void GameLoop(float delta)
    {
        base.GameLoop(delta);
        // The first tick after a start or a pause carries the whole gap.
        delta = Math.Clamp(delta, 0, 0.1f);
        _time += delta;
        var t = _time;

        ListFrame();
        // The scene pans: every layer slides by its share, the ground (and the mobs on it) the most.
        _pan += Scenery.PanSpeed * delta;
        if (_field.WasInLastFrame())
            _scenery.Pan = _pan;
        CardFrame(delta);
        GlobeFrame(delta);
        FormFrame(delta);

        if (_spin < 1)
        {
            _spin = MathF.Min(_spin + delta / 0.9f, 1);
            _medal.Yaw = MedalYaw + 360 * EaseOut(_spin);
        }

        _pop = MathF.Max(_pop - delta * 3, 0);
        var beat = 1 + 0.05f * MathF.Max(MathF.Sin(t * 5), 0) + 0.35f * _pop;
        _heart.SetNodeScale("heart", beat, beat, 1);

        if (!_rainReady)
        {
            _rainReady = true;
            for (var i = 0; i < PieceCount; i++)
            {
                var p = new Piece
                {
                    X = _dice.Next() * RainWidth, Y = _dice.Next() * RainHeight, Fall = 40 + _dice.Next() * 50,
                    Sway = _dice.Next() * MathF.Tau, Spin = 120 + _dice.Next() * 300, Facing = _dice.Next() * 360,
                };
                _rain.Add(PieceNames[i], i % 4 == 0 ? "star" : "paper", p.X, p.Y, i % 4 == 0 ? 18 : 11);
                _rain.SetMaterialColor(PieceNames[i], "piece", ConfettiColors[i % ConfettiColors.Length]);
                _pieces[i] = p;
            }
        }
        for (var i = 0; i < PieceCount; i++)
        {
            ref var p = ref _pieces[i];
            p.Y += p.Fall * delta;
            p.Facing += p.Spin * delta;
            if (p.Y > RainHeight + 16)
                p.Y = -12;
            _rain.SetPosition(PieceNames[i], p.X + MathF.Sin(t * 2 + p.Sway) * 10, p.Y);
            _rain.SetFacing(PieceNames[i], p.Facing);
        }

        if (_walkersOnField != _mobs)
        {
            _walkersOnField = _mobs;
            _field.Clear();
            for (var i = 0; i < _mobs; i++)
            {
                var w = new Walker
                {
                    // On the ground band, below the hills.
                    X = _dice.Next() * FieldWidth, Y = Scenery.GroundTop + 10 + _dice.Next() * (Scenery.GroundBottom - Scenery.GroundTop - 10),
                    Speed = (_dice.Next() < 0.5f ? -1 : 1) * (30 + _dice.Next() * 50),
                };
                _field.Add(MobNames[i], "slime", w.X, w.Y, 46);
                _field.Play(MobNames[i], "CharacterArmature|Walk");
                _field.SetSpeed(MobNames[i], 0.8f + _dice.Next() * 0.5f);
                _field.SetFacing(MobNames[i], w.Speed > 0 ? 90 : -90);
                _walkers[i] = w;
            }
        }
        for (var i = 0; i < _mobs; i++)
        {
            ref var w = ref _walkers[i];
            // Walking on the ground, which moves with the pan; off one side, back in at the other.
            w.X += (w.Speed - Scenery.PanSpeed) * delta;
            if (w.X < -40)
                w.X += FieldWidth + 80;
            else if (w.X > FieldWidth + 40)
                w.X -= FieldWidth + 80;
            _field.SetPosition(MobNames[i], w.X, w.Y);
        }
    }
}
