using DrawnUi.Controls;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game;

public sealed class BridgeHeroineParallaxGame : DrawnUi.Gaming.Game
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

    private const float SceneWidth = 940f;
    private const float SceneHeight = 430f;
    private const float SourceSceneHeight = 239f;
    private const float EnvironmentScale = SceneHeight / SourceSceneHeight;
    private const float HeroineSize = 233.28f;
    private const float HeroineBaseX = 271.36f;
    private const float HeroineGroundTop = 164.00f;
    private const float MoveSpeed = 240f;
    private const float JumpImpulse = 520f;
    private const float Gravity = 1450f;
    private const float ScreenDriftFactor = 0.16f;
    private const float MaxScreenDrift = 92f;
    private const float AttackDuration = 0.32f;
    private const float LayerScreenHeight = SceneHeight;
    private const float TilesetTop = 197f * EnvironmentScale;
    private const float TilesetHeight = 42f * EnvironmentScale;
    private const float TorchBandWidth = 896f * EnvironmentScale;

    private readonly RepeatingStripControl _back;
    private readonly RepeatingStripControl _far;
    private readonly RepeatingStripControl _middle;
    private readonly RepeatingStripControl _near;
    private readonly TilesetStripControl _tileset;
    private readonly RepeatingStripControl _foreground;
    private readonly SkiaLayer _torchBand;
    private readonly HeroineSprite _heroine;

    private bool _initialized;
    private bool _moveLeft;
    private bool _moveRight;
    private bool _jumpRequested;
    private bool _facingLeft;
    private float _worldPosition;
    private float _heroineVelocityY;
    private float _heroineJumpOffset;
    private float _attackTimeRemaining;
    private HeroineState _heroineState;

    public BridgeHeroineParallaxGame()
    {
        Tag = nameof(BridgeHeroineParallaxGame);
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

        _heroine = new HeroineSprite
        {
            WidthRequest = HeroineSize,
            HeightRequest = HeroineSize,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Left = HeroineBaseX,
            Top = HeroineGroundTop,
            ZIndex = 7,
        };
        _heroine.State = HeroineSprite.HeroineAnimState.IdleRight;

        Children =
        [
            _back,
            _far,
            _middle,
            _near,
            _torchBand,
            _tileset,
            _heroine,
            _foreground,
        ];

        ApplyHeroineState(HeroineState.Idle);
        UpdateWorldVisuals();
    }

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

    public override void OnDisposing()
    {
        StopLoop();
        base.OnDisposing();
    }

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

        ApplyHeroineState(ResolveState(moveInput));
        UpdateWorldVisuals();
    }

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
        _heroine.Left = HeroineBaseX + drift;
        _heroine.Top = HeroineGroundTop + _heroineJumpOffset;

        _torchBand.TranslationX = nearTileOffset;
    }

    private HeroineState ResolveState(int moveInput)
    {
        if (_attackTimeRemaining > 0)
        {
            return HeroineState.Attack;
        }

        if (_heroineJumpOffset != 0 || _heroineVelocityY != 0)
        {
            return HeroineState.Jump;
        }

        return moveInput == 0 ? HeroineState.Idle : HeroineState.Run;
    }

    private void ApplyHeroineState(HeroineState state)
    {
        var nextAnimation = state switch
        {
            HeroineState.Idle => _facingLeft ? HeroineSprite.HeroineAnimState.IdleLeft : HeroineSprite.HeroineAnimState.IdleRight,
            HeroineState.Run => _facingLeft ? HeroineSprite.HeroineAnimState.RunLeft : HeroineSprite.HeroineAnimState.RunRight,
            HeroineState.Jump => _facingLeft ? HeroineSprite.HeroineAnimState.JumpLeft : HeroineSprite.HeroineAnimState.JumpRight,
            _ => _facingLeft ? HeroineSprite.HeroineAnimState.AttackLeft : HeroineSprite.HeroineAnimState.AttackRight,
        };

        if (_heroineState == state && _heroine.State == nextAnimation)
        {
            return;
        }

        _heroineState = state;
        _heroine.State = nextAnimation;
    }

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

    private static TilesetStripControl CreateTilesetLayer()
    {
        return new TilesetStripControl("media/cold-corridors/tileset.png", 48f * EnvironmentScale)
        {
            ZIndex = 7,
        };
    }

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

    private enum HeroineState
    {
        Idle,
        Run,
        Jump,
        Attack,
    }

    private sealed class HeroineSprite : SkiaSpriteSet
    {
        public enum HeroineAnimState
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

        private HeroineAnimState _state;

        public HeroineAnimState State
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
                    HeroineAnimState.IdleLeft or HeroineAnimState.IdleRight => 0,
                    HeroineAnimState.RunLeft or HeroineAnimState.RunRight => 1,
                    HeroineAnimState.JumpLeft or HeroineAnimState.JumpRight => 2,
                    _ => 3,
                };
                ApplyMirror();
            }
        }

        public HeroineSprite()
        {
            UseCache = SkiaCacheType.GPU;
            Define(0, "media/gothicvania/heroine-idle.png", columns: 4, rows: 1, fps: 8);
            Define(1, "media/gothicvania/heroine-run.png", columns: 7, rows: 1, fps: 12);
            Define(2, "media/gothicvania/heroine-jump.png", columns: 4, rows: 1, fps: 10);
            Define(3, "media/gothicvania/heroine-attack.png", columns: 5, rows: 1, fps: 18);
            State = HeroineAnimState.IdleRight;
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

            var mirror = State is HeroineAnimState.IdleLeft
                or HeroineAnimState.RunLeft
                or HeroineAnimState.JumpLeft
                or HeroineAnimState.AttackLeft;

            CurrentSprite.ScaleX = mirror ? -1 : 1;
        }
    }

    private sealed class RepeatingStripControl : SkiaImage
    {
        private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
        private readonly float _repeatWidth;
        private readonly float _segmentWidth;
        private float _offsetX;
        private SKImage _gpuImage;

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

        public override void OnDisposing()
        {
            _gpuImage?.Dispose();
            _gpuImage = null;
            base.OnDisposing();
        }

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

    private sealed class TilesetStripControl : SkiaControl
    {
        private static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
        private readonly string _source;
        private readonly float _tileWidth;
        private float _offsetX;
        private SKBitmap _bitmap;
        private SKImage _image;
        private SKImage _gpuImage;

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

        public TilesetStripControl(string source, float tileWidth)
        {
            _source = source.StartsWith("/", StringComparison.Ordinal) ? source : "/" + source;
            _tileWidth = tileWidth;
            WidthRequest = -1;
            HeightRequest = SceneHeight;
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Start;
        }

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

        public override void OnDisposing()
        {
            _gpuImage?.Dispose();
            _gpuImage = null;
            _image?.Dispose();
            _image = null;
            base.OnDisposing();
        }

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