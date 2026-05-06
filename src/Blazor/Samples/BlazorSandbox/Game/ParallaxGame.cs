using DrawnUi.Controls;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game;

/// <summary>
/// Side-scrolling parallax gameplay sample with keyboard-driven movement, jumping, and attack states.
/// </summary>
public sealed class ParallaxGame : DrawnUi.Gaming.Game
{
    private static readonly string[] SceneAssetSources =
    [
        "media/cold-corridors/back.png",
        "media/cold-corridors/far.png",
        "media/cold-corridors/middle.png",
        "media/cold-corridors/near.png",
        "media/cold-corridors/tileset.png",
        "media/cold-corridors/foreground.png",
        "media/cold-corridors/torch-sheet.png",
        "media/gothicvania/heroine-idle.png",
        "media/gothicvania/heroine-run.png",
        "media/gothicvania/heroine-jump.png",
        "media/gothicvania/heroine-attack.png",
        "media/gothicvania/ghost/ghost-Appear.png",
        "media/gothicvania/ghost/ghost-Chase.png",
        "media/gothicvania/ghost/ghost-Idle.png",
        "media/gothicvania/ghost/ghost-Shriek.png",
        "media/gothicvania/ghost/ghost-Vanish.png",
    ];

    private static readonly IReadOnlyDictionary<string, (float Width, float Height)> SceneAssetDimensions =
        new Dictionary<string, (float Width, float Height)>(StringComparer.Ordinal)
        {
            ["media/cold-corridors/back.png"] = (32f, 224f),
            ["media/cold-corridors/far.png"] = (32f, 224f),
            ["media/cold-corridors/middle.png"] = (80f, 224f),
            ["media/cold-corridors/near.png"] = (224f, 224f),
            ["media/cold-corridors/foreground.png"] = (224f, 224f),
        };

    private static readonly IReadOnlyDictionary<string, float> AuthoredRepeatWidths =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["media/cold-corridors/back.png"] = 320f,
            ["media/cold-corridors/far.png"] = 400f,
            ["media/cold-corridors/middle.png"] = 800f,
            ["media/cold-corridors/near.png"] = 896f,
            ["media/cold-corridors/foreground.png"] = 1344f,
        };

    /// <summary>
    /// Width of the visible gameplay viewport in device pixels.
    /// </summary>
    private const float SceneWidth = 940f;

    /// <summary>
    /// Height of the visible gameplay viewport in device pixels.
    /// </summary>
    private const float SceneHeight = 430f;

    /// <summary>
    /// Authored height of the original Godot corridor scene before scaling.
    /// </summary>
    private const float SourceSceneHeight = 239f;

    /// <summary>
    /// Scale factor that maps authored Godot scene coordinates into the runtime viewport.
    /// </summary>
    private const float EnvironmentScale = SceneHeight / SourceSceneHeight;

    /// <summary>
    /// Render size of the player sprite in device pixels.
    /// </summary>
    private const float PlayerSize = 233.28f;

    /// <summary>
    /// Actual frame width authored in the current player sheets.
    /// Measured from the source atlases: idle 512/4, run 896/7, jump 512/4, attack 640/5.
    /// </summary>
    private const float PlayerFrameWidth = 128f;

    /// <summary>
    /// Actual frame height authored in the current player sheets.
    /// </summary>
    private const float PlayerFrameHeight = 64f;

    /// <summary>
    /// Width of the player's stable render box.
    /// Derived from the actual widest frame size in the measured sprite sheets.
    /// </summary>
    private const float PlayerRenderWidth = PlayerSize * (PlayerFrameWidth / PlayerFrameHeight);

    /// <summary>
    /// Height of the player's stable render box.
    /// </summary>
    private const float PlayerRenderHeight = PlayerSize;

    /// <summary>
    /// Render height of the wandering ghost enemy.
    /// </summary>
    private const float GhostRenderHeight = 200f;

    /// <summary>
    /// Actual frame width authored in the ghost sprite sheets.
    /// </summary>
    private const float GhostFrameWidth = 64f;

    /// <summary>
    /// Actual frame height authored in the ghost sprite sheets.
    /// </summary>
    private const float GhostFrameHeight = 80f;

    /// <summary>
    /// Stable render width of the ghost enemy derived from its authored frame size.
    /// </summary>
    private const float GhostRenderWidth = GhostRenderHeight * (GhostFrameWidth / GhostFrameHeight);

    /// <summary>
    /// Left inset that keeps the visual player centered after expanding the render box width.
    /// </summary>
    private const float PlayerRenderLeftInset = (PlayerRenderWidth - PlayerSize) * 0.5f;

    /// <summary>
    /// Baseline X position of the heroine before camera drift is applied.
    /// </summary>
    private const float PlayerBaseX = 271.36f;

    /// <summary>
    /// Grounded Y position of the heroine when she is not jumping.
    /// </summary>
    private const float PlayerGroundTop = 164.00f;

    /// <summary>
    /// Vertical scene position of the ghost enemy's render box.
    /// The ghost floats slightly above the floor line.
    /// </summary>
    private const float GhostBaseTop = 170f;

    /// <summary>
    /// World-space spawn point for the ghost, one viewport width east of the player start.
    /// </summary>
    private const float GhostSpawnWorldX = PlayerBaseX + SceneWidth;

    /// <summary>
    /// Half-width of the ghost patrol around its spawn point.
    /// </summary>
    private const float GhostPatrolHalfWidth = 96f;

    /// <summary>
    /// Patrol oscillation speed in radians per second.
    /// </summary>
    private const float GhostPatrolSpeed = 1.2f;

    /// <summary>
    /// Small vertical bob amplitude applied while the ghost is alive.
    /// </summary>
    private const float GhostBobAmplitude = 6f;

    /// <summary>
    /// Vertical bob speed in radians per second.
    /// </summary>
    private const float GhostBobSpeed = 2.4f;

    /// <summary>
    /// Maximum world-space distance at which the player's attack can kill the ghost.
    /// </summary>
    private const float AttackRange = 320f;

    /// <summary>
    /// Duration of the ghost appear animation in seconds.
    /// </summary>
    private const float GhostAppearDuration = 6f / 10f;

    /// <summary>
    /// Duration of the ghost vanish animation in seconds.
    /// </summary>
    private const float GhostVanishDuration = 7f / 10f;

    /// <summary>
    /// Delay before a defeated ghost respawns at its patrol origin.
    /// </summary>
    private const float GhostRespawnDelay = 10f;

    /// <summary>
    /// Horizontal world travel speed that drives all parallax scrolling.
    /// </summary>
    private const float MoveSpeed = 230f;

    /// <summary>
    /// Initial upward jump velocity applied when a jump starts.
    /// Higher values make the heroine jump harder and stay airborne longer.
    /// </summary>
    private const float JumpImpulse = 520f;

    /// <summary>
    /// Downward acceleration applied every frame while airborne.
    /// </summary>
    private const float Gravity = 1450f;

    /// <summary>
    /// How much the heroine shifts on screen relative to world travel.
    /// </summary>
    private const float ScreenDriftFactor = 0.16f;

    /// <summary>
    /// Maximum horizontal on-screen drift allowed for the heroine.
    /// </summary>
    private const float MaxScreenDrift = 92f;

    /// <summary>
    /// Duration of the attack state in seconds.
    /// </summary>
    private const float AttackDuration = 0.32f;

    /// <summary>
    /// Runtime height used by the scrolling image layers.
    /// </summary>
    private const float LayerScreenHeight = SceneHeight;

    /// <summary>
    /// Top Y position of the floor strip in runtime scene space.
    /// </summary>
    private const float TilesetTop = 197f * EnvironmentScale;

    /// <summary>
    /// Height of the floor strip in runtime scene space.
    /// </summary>
    private const float TilesetHeight = 42f * EnvironmentScale;

    /// <summary>
    /// Width of one authored near-layer repeat band, used for torch repetition.
    /// </summary>
    private const float TorchBandWidth = 896f * EnvironmentScale;

    /// <summary>
    /// Parallax background strip layers ordered from farthest to nearest.
    /// </summary>
    private readonly RepeatingStripControl _back;
    private readonly RepeatingStripControl _far;
    private readonly RepeatingStripControl _middle;
    private readonly RepeatingStripControl _near;
    private readonly TilesetStripControl _tileset;
    private readonly RepeatingStripControl _foreground;

    /// <summary>
    /// Separate torch overlay band that moves with the near layer.
    /// </summary>
    private readonly SkiaLayer _torchBand;

    /// <summary>
    /// Player sprite-set used for the controllable actor.
    /// </summary>
    private readonly PlayerSprite _heroine;

    /// <summary>
    /// Single wandering enemy placed east of the player start.
    /// </summary>
    private readonly GhostSprite _ghost;

    /// <summary>
    /// Tracks whether one-time asset initialization has already completed.
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// Input latch for moving the player left.
    /// </summary>
    private bool _moveLeft;

    /// <summary>
    /// Input latch for moving the player right.
    /// </summary>
    private bool _moveRight;

    /// <summary>
    /// Queued jump request consumed on the next gameplay tick.
    /// </summary>
    private bool _jumpRequested;

    /// <summary>
    /// Current facing direction used to select left or right animation variants.
    /// </summary>
    private bool _facingLeft;

    /// <summary>
    /// Horizontal world travel accumulator that drives all parallax offsets.
    /// </summary>
    private float _worldPosition;

    /// <summary>
    /// Current vertical jump velocity of the player.
    /// </summary>
    private float _heroineVelocityY;

    /// <summary>
    /// Current vertical offset from the grounded player position.
    /// </summary>
    private float _heroineJumpOffset;

    /// <summary>
    /// Remaining attack-state time in seconds.
    /// </summary>
    private float _attackTimeRemaining;

    /// <summary>
    /// Last resolved high-level player gameplay state.
    /// </summary>
    private PlayerState _heroineState;

    /// <summary>
    /// Current high-level ghost behavior state.
    /// </summary>
    private GhostState _ghostState;

    /// <summary>
    /// Remaining time for transient ghost states such as appear and vanish.
    /// </summary>
    private float _ghostStateTimeRemaining;

    /// <summary>
    /// Patrol phase accumulator used to move the ghost left and right.
    /// </summary>
    private float _ghostPatrolTime;

    /// <summary>
    /// Vertical bobbing phase accumulator for the ghost.
    /// </summary>
    private float _ghostBobTime;

    /// <summary>
    /// Creates the scene graph, player sprite, and parallax layers for the playable corridor sample.
    /// </summary>
    public ParallaxGame()
    {
        Tag = nameof(ParallaxGame);
        Type = LayoutType.Absolute;
        HeightRequest = SceneHeight;
        BackgroundColor = Color.FromArgb("#060A12");
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        IsClippedToBounds = true;

        _back = CreateParallaxLayer("media/cold-corridors/back.png", 1, 0.96f);
        _far = CreateParallaxLayer("media/cold-corridors/far.png", 2, 1.0f);
        _middle = CreateParallaxLayer("media/cold-corridors/middle.png", 3, 1.0f);
        _near = CreateParallaxLayer("media/cold-corridors/near.png", 4, 1.0f);
        _tileset = CreateTilesetLayer();
        _foreground = CreateParallaxLayer("media/cold-corridors/foreground.png", 8, 1.0f);

        _torchBand = new SkiaLayer
        {
            WidthRequest = SceneWidth + (TorchBandWidth * 2f),
            HeightRequest = SceneHeight,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            ZIndex = 5,
            TranslationY = SceneHeight - (SourceSceneHeight * EnvironmentScale),
            Children =
            {
                CreateTorch(143f - 896f, 112f),
                CreateTorch(369f - 896f, 111f),
                CreateTorch(591f - 896f, 112f),
                CreateTorch(817f - 896f, 110f),
                CreateTorch(143f, 112f),
                CreateTorch(369f, 111f),
                CreateTorch(591f, 112f),
                CreateTorch(817f, 110f),
                CreateTorch(143f + 896f, 112f),
                CreateTorch(369f + 896f, 111f),
                CreateTorch(591f + 896f, 112f),
                CreateTorch(817f + 896f, 110f),
            }
        };

        _heroine = new PlayerSprite
        {
            WidthRequest = PlayerRenderWidth,
            HeightRequest = PlayerRenderHeight,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Left = PlayerBaseX - PlayerRenderLeftInset,
            Top = PlayerGroundTop,
            ZIndex = 7,
        };
        _heroine.State = PlayerSprite.PlayerAnimState.IdleRight;

        _ghost = new GhostSprite
        {
            WidthRequest = GhostRenderWidth,
            HeightRequest = GhostRenderHeight,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Left = GhostSpawnWorldX - (GhostRenderWidth * 0.5f),
            Top = GhostBaseTop,
            ZIndex = 6,
        };
        _ghost.State = GhostSprite.GhostAnimState.AppearLeft;
        _ghostState = GhostState.Appearing;
        _ghostStateTimeRemaining = GhostAppearDuration;

        Children =
        [
            _back,
            _far,
            _middle,
            _near,
            _torchBand,
            _tileset,
            _ghost,
            _heroine,
            _foreground,
        ];

        ApplyPlayerState(PlayerState.Idle);
        UpdateWorldVisuals();
    }

    /// <summary>
    /// Starts asynchronous asset initialization after the DrawnUI view tree is attached.
    /// </summary>
    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        Task.Run(async () =>
        {
            while (Superview == null || !Superview.HasHandler)
            {
                await Task.Delay(30);
            }

            await InitializeAsync();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the game loop before the control is disposed.
    /// </summary>
    public override void OnDisposing()
    {
        StopLoop();
        base.OnDisposing();
    }

    /// <summary>
    /// Captures keyboard input and converts it into movement, jump, and attack requests.
    /// </summary>
    public override void OnKeyDown(InputKey key)
    {
        switch (key)
        {
            case InputKey.ArrowLeft:
            case InputKey.KeyA:
                _moveLeft = true;
                _facingLeft = true;
                break;
            case InputKey.ArrowRight:
            case InputKey.KeyD:
                _moveRight = true;
                _facingLeft = false;
                break;
            case InputKey.ArrowUp:
            case InputKey.KeyW:
                if (_heroineJumpOffset == 0)
                {
                    _jumpRequested = true;
                }
                break;
            case InputKey.Space:
                _attackTimeRemaining = AttackDuration;
                break;
        }
    }

    /// <summary>
    /// Clears held movement input when the corresponding key is released.
    /// </summary>
    public override void OnKeyUp(InputKey key)
    {
        switch (key)
        {
            case InputKey.ArrowLeft:
            case InputKey.KeyA:
                _moveLeft = false;
                break;
            case InputKey.ArrowRight:
            case InputKey.KeyD:
                _moveRight = false;
                break;
        }
    }

    /// <summary>
    /// Advances one gameplay frame: reads input, updates physics, advances world travel, and refreshes visuals.
    /// </summary>
    public override void GameLoop(float deltaSeconds)
    {
        base.GameLoop(deltaSeconds);

        var moveInput = 0;
        if (_moveLeft)
        {
            moveInput -= 1;
        }

        if (_moveRight)
        {
            moveInput += 1;
        }

        if (_jumpRequested)
        {
            _jumpRequested = false;
            _heroineVelocityY = -JumpImpulse;
        }

        if (_heroineJumpOffset != 0 || _heroineVelocityY != 0)
        {
            _heroineVelocityY += Gravity * deltaSeconds;
            _heroineJumpOffset += _heroineVelocityY * deltaSeconds;

            if (_heroineJumpOffset >= 0)
            {
                _heroineJumpOffset = 0;
                _heroineVelocityY = 0;
            }
        }

        if (_attackTimeRemaining > 0)
        {
            _attackTimeRemaining = Math.Max(0, _attackTimeRemaining - deltaSeconds);
        }

        _worldPosition += moveInput * MoveSpeed * deltaSeconds;

        ApplyPlayerState(ResolveState(moveInput));
        UpdateWorldVisuals();

        if (_attackTimeRemaining > 0)
        {
            TryAttackGhost();
        }

        UpdateGhost(deltaSeconds);
    }

    /// <summary>
    /// Preloads image assets and starts the runtime loop once the scene can render safely.
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_initialized || Superview == null || !Superview.HasHandler)
        {
            return;
        }

        await SkiaImageManager.Instance.PreloadImages(SceneAssetSources);
        _tileset.SetBitmap(SkiaImageManager.Instance.GetFromCache("/media/cold-corridors/tileset.png")
            ?? SkiaImageManager.Instance.GetFromCache("media/cold-corridors/tileset.png"));

        IgnoreChildrenInvalidations = true;
        LastFrameTimeNanos = SkiaControl.GetNanoseconds();
        _initialized = true;
        Update();
        StartLoop();
    }

    /// <summary>
    /// Applies the current world position to each parallax layer and repositions the player and torch band.
    /// </summary>
    private void UpdateWorldVisuals()
    {
        _back.OffsetX = -_worldPosition * 0.18f;
        _far.OffsetX = -_worldPosition * 0.34f;
        _middle.OffsetX = -_worldPosition * 0.56f;

        var nearTileOffset = MathF.Round(-_worldPosition * 0.82f);
        _near.OffsetX = nearTileOffset;
        _tileset.OffsetX = -_worldPosition;
        _foreground.OffsetX = -_worldPosition * 1.25f;

        var drift = Math.Clamp(_worldPosition * ScreenDriftFactor, -MaxScreenDrift, MaxScreenDrift);
        _heroine.Left = (PlayerBaseX - PlayerRenderLeftInset) + drift;
        _heroine.Top = PlayerGroundTop + _heroineJumpOffset;

        var ghostWorldX = GetGhostWorldX();
        _ghost.Left = (ghostWorldX - _worldPosition) - (GhostRenderWidth * 0.5f);
        _ghost.Top = GhostBaseTop + (_ghostState == GhostState.Gone ? 0f : MathF.Sin(_ghostBobTime) * GhostBobAmplitude);

        _torchBand.TranslationX = nearTileOffset;
    }

    /// <summary>
    /// Advances the enemy patrol state machine and resolves attack kills against the ghost.
    /// </summary>
    private void UpdateGhost(float deltaSeconds)
    {
        if (_ghostState == GhostState.Gone)
        {
            _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
            if (_ghostStateTimeRemaining == 0)
            {
                _ghostPatrolTime = 0;
                _ghostBobTime = 0;
                SetGhostState(GhostState.Appearing);
            }
            return;
        }

        _ghostPatrolTime += deltaSeconds;
        _ghostBobTime += deltaSeconds * GhostBobSpeed;

        if (_ghostState == GhostState.Appearing)
        {
            _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
            if (_ghostStateTimeRemaining == 0)
            {
                SetGhostState(GhostState.Patrolling);
            }
            return;
        }

        if (_ghostState == GhostState.Vanishing)
        {
            _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
            if (_ghostStateTimeRemaining == 0)
            {
                SetGhostState(GhostState.Gone);
            }
            return;
        }

        var movingLeft = MathF.Cos(_ghostPatrolTime * GhostPatrolSpeed) < 0;
        _ghost.State = movingLeft ? GhostSprite.GhostAnimState.ChaseLeft : GhostSprite.GhostAnimState.ChaseRight;
    }

    /// <summary>
    /// Computes the ghost's world-space X position from its patrol origin and oscillation phase.
    /// </summary>
    private float GetGhostWorldX()
    {
        return GhostSpawnWorldX + (MathF.Sin(_ghostPatrolTime * GhostPatrolSpeed) * GhostPatrolHalfWidth);
    }

    /// <summary>
    /// Attempts to hit the ghost using the current melee attack window.
    /// Returns true when the ghost enters the vanish state.
    /// </summary>
    private bool TryAttackGhost()
    {
        if (_heroineState != PlayerState.Attack)
        {
            return false;
        }

        if (_ghostState is GhostState.Gone or GhostState.Vanishing or GhostState.Appearing)
        {
            return false;
        }

        if (!_heroine.DrawingRect.IntersectsWith(_ghost.DrawingRect))
        {
            return false;
        }

        SetGhostState(GhostState.Vanishing);
        return true;
    }

    /// <summary>
    /// Applies a high-level ghost state to the sprite and visibility model.
    /// </summary>
    private void SetGhostState(GhostState state)
    {
        _ghostState = state;

        switch (state)
        {
            case GhostState.Appearing:
                _ghost.IsVisible = true;
                _ghostStateTimeRemaining = GhostAppearDuration;
                _ghost.State = GhostSprite.GhostAnimState.AppearLeft;
                break;
            case GhostState.Patrolling:
                _ghost.IsVisible = true;
                _ghostStateTimeRemaining = 0;
                _ghost.State = GhostSprite.GhostAnimState.ChaseLeft;
                break;
            case GhostState.Vanishing:
                _ghost.IsVisible = true;
                _ghostStateTimeRemaining = GhostVanishDuration;
                _ghost.State = GhostSprite.GhostAnimState.VanishLeft;
                break;
            default:
                _ghostStateTimeRemaining = GhostRespawnDelay;
                _ghost.IsVisible = false;
                break;
        }
    }

    /// <summary>
    /// Resolves the player's high-level animation state from attack, jump, and movement conditions.
    /// </summary>
    private PlayerState ResolveState(int moveInput)
    {
        if (_attackTimeRemaining > 0)
        {
            return PlayerState.Attack;
        }

        if (_heroineJumpOffset != 0 || _heroineVelocityY != 0)
        {
            return PlayerState.Jump;
        }

        return moveInput == 0 ? PlayerState.Idle : PlayerState.Run;
    }

    /// <summary>
    /// Maps a gameplay state into a concrete left/right animation variant and applies it to the sprite set.
    /// </summary>
    private void ApplyPlayerState(PlayerState state)
    {
        var nextAnimation = state switch
        {
            PlayerState.Idle => _facingLeft ? PlayerSprite.PlayerAnimState.IdleLeft : PlayerSprite.PlayerAnimState.IdleRight,
            PlayerState.Run => _facingLeft ? PlayerSprite.PlayerAnimState.RunLeft : PlayerSprite.PlayerAnimState.RunRight,
            PlayerState.Jump => _facingLeft ? PlayerSprite.PlayerAnimState.JumpLeft : PlayerSprite.PlayerAnimState.JumpRight,
            _ => _facingLeft ? PlayerSprite.PlayerAnimState.AttackLeft : PlayerSprite.PlayerAnimState.AttackRight,
        };

        if (_heroineState == state && _heroine.State == nextAnimation)
        {
            return;
        }

        _heroineState = state;
        _heroine.State = nextAnimation;
    }

    /// <summary>
    /// Creates a horizontally repeating image strip for one parallax layer using authored repeat widths.
    /// </summary>
    private static RepeatingStripControl CreateParallaxLayer(string source, int zIndex, double opacity)
    {
        var sourceSize = SceneAssetDimensions[source];
        var segmentWidth = sourceSize.Width * EnvironmentScale;
        var repeatWidth = AuthoredRepeatWidths[source] * EnvironmentScale;
        var tileHeight = SceneHeight;

        return new RepeatingStripControl(source, repeatWidth, segmentWidth, 0, tileHeight)
        {
            Opacity = opacity,
            ZIndex = zIndex,
        };
    }

    /// <summary>
    /// Creates the floor-strip renderer used for the moving tileset band.
    /// </summary>
    private static TilesetStripControl CreateTilesetLayer()
    {
        return new TilesetStripControl("media/cold-corridors/tileset.png", 48f * EnvironmentScale)
        {
            ZIndex = 7,
        };
    }

    /// <summary>
    /// Creates one animated torch sprite positioned in authored near-layer scene coordinates.
    /// </summary>
    private static SkiaSprite CreateTorch(float sceneX, float sceneY)
    {
        var size = 32f * EnvironmentScale;
        var left = MathF.Round((sceneX * EnvironmentScale) - (size * 0.5f));
        var top = MathF.Round((sceneY * EnvironmentScale) - (size * 0.5f));
        return new SkiaSprite
        {
            UseCache = SkiaCacheType.GPU,
            AutoPlay = true,
            Repeat = -1,
            FramesPerSecond = 6,
            Columns = 4,
            Rows = 1,
            WidthRequest = size,
            HeightRequest = size,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = left,
            TranslationY = top,
            ZIndex = 14,
            Source = "media/cold-corridors/torch-sheet.png",
        };
    }

    /// <summary>
    /// High-level gameplay states that drive sprite animation selection.
    /// </summary>
    private enum PlayerState
    {
        Idle,
        Run,
        Jump,
        Attack,
    }

    /// <summary>
    /// High-level behavior states for the wandering ghost enemy.
    /// </summary>
    private enum GhostState
    {
        Appearing,
        Patrolling,
        Vanishing,
        Gone,
    }

    /// <summary>
    /// Sprite-set wrapper that exposes semantic player animation states and left/right mirroring.
    /// </summary>
    private sealed class PlayerSprite : SkiaSpriteSet
    {
        /// <summary>
        /// Concrete animation variants, split by action and facing direction.
        /// </summary>
        public enum PlayerAnimState
        {
            IdleRight,
            IdleLeft,
            RunRight,
            RunLeft,
            JumpRight,
            JumpLeft,
            AttackRight,
            AttackLeft,
        }

        private PlayerAnimState _state;

        /// <summary>
        /// Gets or sets the active semantic animation variant and maps it onto the underlying sprite-set state index.
        /// </summary>
        public PlayerAnimState State
        {
            get => _state;
            set
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
                base.State = value switch
                {
                    PlayerAnimState.IdleLeft or PlayerAnimState.IdleRight => 0,
                    PlayerAnimState.RunLeft or PlayerAnimState.RunRight => 1,
                    PlayerAnimState.JumpLeft or PlayerAnimState.JumpRight => 2,
                    _ => 3,
                };
                ApplyMirror();
            }
        }

        /// <summary>
        /// Registers the player's sprite sheets and default starting animation.
        /// </summary>
        public PlayerSprite()
        {
            UseCache = SkiaCacheType.GPU;
            Define(0, "media/gothicvania/heroine-idle.png", columns: 4, rows: 1, fps: 8);
            Define(1, "media/gothicvania/heroine-run.png", columns: 7, rows: 1, fps: 12);
            Define(2, "media/gothicvania/heroine-jump.png", columns: 4, rows: 1, fps: 10);
            Define(3, "media/gothicvania/heroine-attack.png", columns: 5, rows: 1, fps: 18);
            State = PlayerAnimState.IdleRight;
        }

        /// <summary>
        /// Reapplies horizontal mirroring whenever the underlying sprite-set changes animation.
        /// </summary>
        protected override void OnChangeState(int oldState, int newState)
        {
            base.OnChangeState(oldState, newState);
            ApplyMirror();
        }

        /// <summary>
        /// Mirrors the active sprite horizontally for left-facing animation variants.
        /// </summary>
        private void ApplyMirror()
        {
            if (CurrentSprite == null)
            {
                return;
            }

            var mirror = State is PlayerAnimState.IdleLeft
                or PlayerAnimState.RunLeft
                or PlayerAnimState.JumpLeft
                or PlayerAnimState.AttackLeft;

            CurrentSprite.ScaleX = mirror ? -1 : 1;
        }
    }

    /// <summary>
    /// Sprite-set wrapper for the corridor ghost enemy.
    /// </summary>
    private sealed class GhostSprite : SkiaSpriteSet
    {
        public enum GhostAnimState
        {
            AppearRight,
            AppearLeft,
            ChaseRight,
            ChaseLeft,
            IdleRight,
            IdleLeft,
            ShriekRight,
            ShriekLeft,
            VanishRight,
            VanishLeft,
        }

        private GhostAnimState _state;

        public GhostAnimState State
        {
            get => _state;
            set
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
                base.State = value switch
                {
                    GhostAnimState.AppearLeft or GhostAnimState.AppearRight => 0,
                    GhostAnimState.ChaseLeft or GhostAnimState.ChaseRight => 1,
                    GhostAnimState.IdleLeft or GhostAnimState.IdleRight => 2,
                    GhostAnimState.ShriekLeft or GhostAnimState.ShriekRight => 3,
                    _ => 4,
                };
                ApplyMirror();
            }
        }

        public GhostSprite()
        {
            UseCache = SkiaCacheType.GPU;
            Define(0, "media/gothicvania/ghost/ghost-Appear.png", columns: 6, rows: 1, fps: 10);
            Define(1, "media/gothicvania/ghost/ghost-Chase.png", columns: 4, rows: 1, fps: 10);
            Define(2, "media/gothicvania/ghost/ghost-Idle.png", columns: 7, rows: 1, fps: 10);
            Define(3, "media/gothicvania/ghost/ghost-Shriek.png", columns: 4, rows: 1, fps: 10);
            Define(4, "media/gothicvania/ghost/ghost-Vanish.png", columns: 7, rows: 1, fps: 10, repeat: 1);
            State = GhostAnimState.AppearLeft;
        }

        protected override void OnChangeState(int oldState, int newState)
        {
            base.OnChangeState(oldState, newState);
            ApplyMirror();
        }

        private void ApplyMirror()
        {
            if (CurrentSprite == null)
            {
                return;
            }

            var mirror = State is GhostAnimState.AppearLeft
                or GhostAnimState.ChaseLeft
                or GhostAnimState.IdleLeft
                or GhostAnimState.ShriekLeft
                or GhostAnimState.VanishLeft;

            CurrentSprite.ScaleX = mirror ? 1 : -1;
        }
    }

    /// <summary>
    /// Image control that repeats an authored texture segment horizontally across the scene.
    /// </summary>
    private sealed class RepeatingStripControl : SkiaImage
    {
        private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
        private readonly float _repeatWidth;
        private readonly float _segmentWidth;
        private float _offsetX;
        private SKImage _gpuImage;

        /// <summary>
        /// Horizontal scroll offset applied to the repeating strip.
        /// </summary>
        public float OffsetX
        {
            get => _offsetX;
            set
            {
                if (Math.Abs(_offsetX - value) < 0.001f)
                {
                    return;
                }

                _offsetX = value;
                Update();
            }
        }

        /// <summary>
        /// Creates a repeating strip with explicit authored repeat width, segment width, and scene placement.
        /// </summary>
        public RepeatingStripControl(string source, float repeatWidth, float segmentWidth, float top, float height)
        {
            _repeatWidth = repeatWidth;
            _segmentWidth = segmentWidth;
            Source = source;
            UseCache = SkiaCacheType.None;
            WidthRequest = -1;
            HeightRequest = height;
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Start;
            Top = top;
        }

        /// <summary>
        /// Releases any GPU image promoted for faster rendering.
        /// </summary>
        public override void OnDisposing()
        {
            _gpuImage?.Dispose();
            _gpuImage = null;
            base.OnDisposing();
        }

        /// <summary>
        /// Draws the repeating band by tiling the source image across each authored repeat segment.
        /// </summary>
        protected override void DrawSource(
            DrawingContext ctx,
            LoadedImageSource source,
            TransformAspect stretch,
            DrawImageAlignment horizontal = DrawImageAlignment.Center,
            DrawImageAlignment vertical = DrawImageAlignment.Center,
            SKPaint paint = null)
        {
            var activePaint = paint ?? new SKPaint();
            activePaint.IsAntialias = false;

            var dest = ctx.Destination;

            if (_gpuImage == null && Superview is DrawnView drawnView)
            {
                using var surface = drawnView.CreateSurface(source.Width, source.Height, true);
                if (surface?.Context != null)
                {
                    using var uploadPaint = new SKPaint { IsAntialias = false };
                    var uploadRect = new SKRect(0, 0, source.Width, source.Height);
                    if (source.Image != null)
                    {
                        surface.Canvas.DrawImage(source.Image, uploadRect, PixelSampling, uploadPaint);
                    }
                    else if (source.Bitmap != null)
                    {
                        surface.Canvas.DrawBitmap(source.Bitmap, uploadRect, uploadPaint);
                    }

                    surface.Canvas.Flush();
                    _gpuImage = surface.Snapshot();
                    drawnView.ReturnSurface(surface);
                }
            }

            var activeImage = _gpuImage ?? source.Image;
            var repeatWidth = MathF.Round(_repeatWidth);
            var segmentWidth = MathF.Round(_segmentWidth);
            var useOffsetX = -OffsetX % repeatWidth;
            var offsetX = useOffsetX > 0 ? useOffsetX : repeatWidth + useOffsetX;
            var startX = MathF.Round(dest.Left - offsetX);

            for (var bandX = startX; bandX < dest.Right + repeatWidth; bandX += repeatWidth)
            {
                for (var x = bandX; x < bandX + repeatWidth; x += segmentWidth)
                {
                    var left = MathF.Round(x);
                    var right = MathF.Round(x + segmentWidth);
                    var tileDest = new SKRect(left, dest.Top, right, dest.Bottom);

                    if (activeImage != null)
                    {
                        ctx.Context.Canvas.DrawImage(activeImage, tileDest, PixelSampling, activePaint);
                    }
                    else if (source.Bitmap != null)
                    {
                        ctx.Context.Canvas.DrawBitmap(source.Bitmap, tileDest, activePaint);
                    }
                }
            }

            if (paint == null)
            {
                activePaint.Dispose();
            }
        }
    }

    /// <summary>
    /// Custom floor-strip renderer that tiles the corridor floor texture along the bottom band of the scene.
    /// </summary>
    private sealed class TilesetStripControl : SkiaControl
    {
        private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
        private readonly string _source;
        private readonly float _tileWidth;
        private float _offsetX;
        private SKBitmap _bitmap;
        private SKImage _image;
        private SKImage _gpuImage;

        /// <summary>
        /// Horizontal scroll offset applied to the floor strip.
        /// </summary>
        public float OffsetX
        {
            get => _offsetX;
            set
            {
                if (Math.Abs(_offsetX - value) < 0.001f)
                {
                    return;
                }

                _offsetX = value;
                Update();
            }
        }

        /// <summary>
        /// Creates the floor-strip renderer with a fixed tile width in scene pixels.
        /// </summary>
        public TilesetStripControl(string source, float tileWidth)
        {
            _source = source.StartsWith("/", StringComparison.Ordinal) ? source : "/" + source;
            _tileWidth = tileWidth;
            WidthRequest = -1;
            HeightRequest = SceneHeight;
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Start;
        }

        /// <summary>
        /// Injects the preloaded floor bitmap and refreshes cached image resources.
        /// </summary>
        public void SetBitmap(SKBitmap bitmap)
        {
            if (bitmap == null)
            {
                return;
            }

            _bitmap?.Dispose();
            _image?.Dispose();
            _gpuImage?.Dispose();

            _bitmap = bitmap;
            _image = SKImage.FromBitmap(_bitmap);
            _gpuImage = null;
            Update();
        }

        /// <summary>
        /// Releases cached CPU and GPU image resources owned by the floor strip.
        /// </summary>
        public override void OnDisposing()
        {
            _gpuImage?.Dispose();
            _gpuImage = null;
            _image?.Dispose();
            _image = null;
            base.OnDisposing();
        }

        /// <summary>
        /// Draws the floor strip into its authored band using the current scroll offset.
        /// </summary>
        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);

            EnsureImageLoaded(ctx);

            var activeImage = _gpuImage ?? _image;
            if (activeImage == null && _bitmap == null)
            {
                return;
            }

            using var paint = new SKPaint { IsAntialias = false };
            var dest = ctx.Destination;
            var sceneTop = dest.Bottom - SceneHeight;
            var stripTop = sceneTop + TilesetTop;
            var stripBottom = stripTop + TilesetHeight;
            var useOffsetX = -OffsetX % _tileWidth;
            var offsetX = useOffsetX > 0 ? useOffsetX : _tileWidth + useOffsetX;

            for (var x = dest.Left - offsetX; x < dest.Right + _tileWidth; x += _tileWidth)
            {
                var tileDest = new SKRect(x, stripTop, x + _tileWidth, stripBottom);
                if (activeImage != null)
                {
                    ctx.Context.Canvas.DrawImage(activeImage, tileDest, PixelSampling, paint);
                }
                else
                {
                    ctx.Context.Canvas.DrawBitmap(_bitmap, tileDest, paint);
                }
            }
        }

        /// <summary>
        /// Lazily resolves the floor texture from cache and promotes it to GPU memory when possible.
        /// </summary>
        private void EnsureImageLoaded(DrawingContext ctx)
        {
            if (_bitmap == null)
            {
                _bitmap = SkiaImageManager.Instance.GetFromCache(_source);
                if (_bitmap != null)
                {
                    _image = SKImage.FromBitmap(_bitmap);
                }
            }

            if (_gpuImage == null && _image != null && Superview is DrawnView drawnView)
            {
                using var surface = drawnView.CreateSurface(_bitmap.Width, _bitmap.Height, true);
                if (surface?.Context != null)
                {
                    using var uploadPaint = new SKPaint { IsAntialias = false };
                    surface.Canvas.DrawImage(_image, 0, 0, PixelSampling, uploadPaint);
                    surface.Canvas.Flush();
                    _gpuImage = surface.Snapshot();
                    drawnView.ReturnSurface(surface);
                }
            }
        }
    }
}
