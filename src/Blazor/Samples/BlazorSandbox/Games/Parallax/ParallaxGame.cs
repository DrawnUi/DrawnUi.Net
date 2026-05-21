using AppoMobi.Gestures;
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
public sealed partial class ParallaxGame : DrawnGame
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
    /// Width of the visible gameplay viewport in scene units.
    /// </summary>
    private const float SceneWidth = 940f;

    /// <summary>
    /// Height of the visible gameplay viewport in scene units.
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
    /// Authored frame width of the heroine sprite sheets.
    /// </summary>
    private const float PlayerFrameWidth = 128f;

    /// <summary>
    /// Authored frame height of the heroine sprite sheets.
    /// </summary>
    private const float PlayerFrameHeight = 64f;

    /// <summary>
    /// Stable actor slot height used for bottom-anchored player rendering.
    /// Individual animations can render shorter or taller inside this slot.
    /// </summary>
    private const float PlayerRenderHeight = 260f;

    /// <summary>
    /// Stable render width of the heroine derived from the authored frame aspect.
    /// Keeping width tied to height makes the sprite grow automatically when PlayerRenderHeight changes.
    /// </summary>
    private const float PlayerRenderWidth = PlayerRenderHeight * (PlayerFrameWidth / PlayerFrameHeight);

    /// <summary>
    /// Legacy grounded baseline preserved from the original top-anchored sample.
    /// Bottom-anchored sprites align their feet to this world Y.
    /// </summary>
    private const float PlayerGroundBaseline = 397.28f;

    /// <summary>
    /// Render height of the wandering ghost enemy.
    /// </summary>
    private const float GhostRenderHeight = 240f;

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
    /// Left inset that keeps the actor slot centered around the heroine's authored baseline X.
    /// </summary>
    private const float PlayerRenderLeftInset = PlayerRenderWidth * 0.5f;

    /// <summary>
    /// Baseline X position of the heroine before camera drift is applied.
    /// </summary>
    private const float PlayerBaseX = 271.36f;

    /// <summary>
    /// Original top position used by the old fixed-box sample.
    /// Kept for reference because other authored scene values were tuned against it.
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
    /// Authored torch mount centers within one near-layer repeat band.
    /// </summary>
    private static readonly (float X, float Y)[] TorchAnchors =
    [
        (143f, 112f),
        (369f, 111f),
        (591f, 112f),
        (817f, 110f),
    ];

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
    /// Current facing direction used to select left or right animation variants.
    /// </summary>
    private bool _facingLeft;
    /// <summary>
    /// Separate torch overlay band that moves with the near layer.
    /// </summary>
    private readonly TorchOverlayControl _torchBand;

    /// <summary>
    /// Player sprite-set used for the controllable actor.
    /// </summary>
    private readonly PlayerSprite _player;

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
    /// Pointer input latch for moving the player left while the mouse button stays down.
    /// </summary>
    private bool _pointerMoveLeft;

    /// <summary>
    /// Pointer input latch for moving the player right while the mouse button stays down.
    /// </summary>
    private bool _pointerMoveRight;

    /// <summary>
    /// Queued jump request consumed on the next gameplay tick.
    /// </summary>
    private bool _jumpRequested;


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





    public override void OnScaleChanged()
    {
        base.OnScaleChanged();

        InvalidateWithChildren();
    }

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

        _torchBand = new TorchOverlayControl("media/cold-corridors/torch-sheet.png")
        {
            HeightRequest = SceneHeight,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            ZIndex = 5,
        };

        _player = new PlayerSprite
        {
            WidthRequest = PlayerRenderWidth,
            HeightRequest = PlayerRenderHeight,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Left = PlayerBaseX - PlayerRenderLeftInset,
            Top = PlayerGroundBaseline - PlayerRenderHeight,
            ZIndex = 7,
        };
        //_player.State = PlayerAnimState.IdleRight;

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
        _ghost.AnimationState = GhostSprite.GhostAnimState.AppearLeft;
        _ghost.State = GhostState.Appearing;

        Children =
        [
            _back,
            _far,
            _middle,
            _near,
            _torchBand,
            _tileset,
            _ghost,
            _player,
            _foreground,
        ];

        _player.SetState(PlayerState.Idle, _facingLeft);

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
        _torchBand.OffsetX = nearTileOffset;
        _tileset.OffsetX = -_worldPosition;
        _foreground.OffsetX = -_worldPosition * 1.25f;

        var drift = Math.Clamp(_worldPosition * ScreenDriftFactor, -MaxScreenDrift, MaxScreenDrift);
        _player.Left = (PlayerBaseX - PlayerRenderLeftInset) + drift;
        _player.Top = (PlayerGroundBaseline - PlayerRenderHeight) + _heroineJumpOffset;

        var ghostWorldX = GetGhostWorldX();
        _ghost.Left = (ghostWorldX - _worldPosition) - (GhostRenderWidth * 0.5f);
        _ghost.Top = GhostBaseTop + (_ghost.State == GhostState.Gone ? 0f : MathF.Sin(_ghost._ghostBobTime) * GhostSprite.GhostBobAmplitude);

    }

    /// <summary>
    /// Advances the enemy patrol state machine and resolves attack kills against the ghost.
    /// </summary>
    private void UpdateGhost(float deltaSeconds)
    {
        _ghost.UpdateState(deltaSeconds);
    }

    /// <summary>
    /// Computes the ghost's world-space X position from its patrol origin and oscillation phase.
    /// </summary>
    private float GetGhostWorldX()
    {
        return GhostSpawnWorldX + (MathF.Sin(_ghost._ghostPatrolTime * GhostSprite.GhostPatrolSpeed) * GhostSprite.GhostPatrolHalfWidth);
    }

    /// <summary>
    /// Attempts to hit the ghost using the current melee attack window.
    /// Returns true when the ghost enters the vanish state.
    /// </summary>
    private bool TryAttackGhost()
    {
        if (_player.State != PlayerState.Attack)
        {
            return false;
        }

        if (_ghost.State is GhostState.Gone or GhostState.Vanishing or GhostState.Appearing)
        {
            return false;
        }

        var attackHitBox = _player.GetAttackHitBox();
        var ghostHitBox = _ghost.GetHitBox();

        if (!attackHitBox.IntersectsWith(ghostHitBox))
        {
            return false;
        }

        _ghost.SetState(GhostState.Vanishing);
        return true;
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
}
