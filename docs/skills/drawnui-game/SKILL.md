---
name: drawnui-game
description: Use when creating or modifying games built on DrawnUi.Gaming.DrawnGame for MAUI and Blazor WASM targets. Covers game loop, sprites, physics, canvas rendering, input, and cross-platform game patterns.
version: 1.2.0
tags: [drawnui, game, maui, skia, dotnet]
---

# DrawnUI Game Development Skill

Use this skill when creating or modifying games built on `DrawnUi.Gaming.DrawnGame` for MAUI and/or Blazor WASM targets.

> **IMPORTANT:** This skill must always be updated when new findings are discovered during game development - patterns, pitfalls, API quirks, project structure details, platform differences. Update this file before closing any game-related task that produced reusable knowledge.

> **IMPORTANT:** For game examples and samples, never keep the whole game in one file. Split immediately by concern using partials, starting with `GameName.cs`, `GameName.UI.cs`, and `GameName.Gestures.cs`, then add more slices like `GameName.Settings.cs`, `GameName.Theme.cs`, `GameName.Audio.cs`, or `GameName.Pieces.cs` as needed. If the project uses `.projitems`, add every new partial file explicitly.

> **IMPORTANT:** For puzzle trays or refill batches, do not generate the next pieces purely at random when the live board state matters. Build the next batch from the current occupancy so at least some pieces fit the available gaps, and prefer batches whose intended placements can clear a row or column when the board allows it. Only fall back to random fillers for leftover tray slots when the board is too constrained to support a full planned batch.

> **IMPORTANT:** When a puzzle tray is intentionally planned around the first two pieces clearing lines, do not evaluate the third fallback piece only against the current board. If the third piece cannot fit now, choose it against the post-collapse board state produced by those first two planned moves so the tray remains solvable through the intended clear sequence.

> **IMPORTANT:** Discuss DrawnUI game rendering and FX architecture like a senior game developer, not a cheerleader. Push back when the proposed approach is brittle, overfit, or engine-hostile. Prefer clean rendering contracts, explicit effect scopes, and theme material sets over giant all-in-one shaders or ad hoc control-tree hacks.

> **IMPORTANT:** During live gameplay, never create new controls on demand. Prewarm fixed-size pools for every gameplay visual that can appear at runtime: board cells, tray-piece tiles, preview tiles, FX tiles, and transient host layers. While the game is running, acquire from the pool, reconfigure, show, hide, and return to the pool. If the pool runs dry, treat that as a bug in pool sizing or release flow, not as a reason to allocate a new control.

> **IMPORTANT:** For board or field shader effects in grid-based puzzle games, separate source region from output scope. Use a dedicated field-sized FX layer when glow, particles, or dissolve can extend beyond the triggering tiles; keep local scope only for genuinely local effects. Prefer grid/cell masks or state textures over arrays of arbitrary rects.

---

## Solution / project templates

### A) MAUI only

```
MyGame/
  MyGame.Mobile.csproj   (Sdk="Microsoft.NET.Sdk", UseMaui=true)
  MainPage.cs
  Game/
    MyGame.cs
    MyGame.Loop.cs
    MyGame.Ui.cs
    MyGame.Input.cs
  Platforms/Android|iOS|Windows|MacCatalyst/...
```

**`MyGame.Mobile.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">
      $(TargetFrameworks);net10.0-windows10.0.19041.0
    </TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationTitle>My Game</ApplicationTitle>
    <ApplicationId>com.mycompany.mygame</ApplicationId>
  </PropertyGroup>

  <ItemGroup>
    <MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
    <MauiFont Include="Resources\Fonts\*" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="DrawnUi.Maui.Game" Version="*" />
  </ItemGroup>
</Project>
```

---

### B) Blazor WASM only

```
MyGame/
  MyGame.Web.csproj   (Sdk="Microsoft.NET.Sdk.BlazorWebAssembly")
  Program.cs
  App.razor
  Pages/
    GamePage.razor      ← entry point
  Game/
    MyGame.cs
    MyGame.Loop.cs
  wwwroot/
    Images/ Sounds/ Music/   ← assets go here
    index.html
```

**`MyGame.Web.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <WasmBuildNative>true</WasmBuildNative>
    <DefineConstants>$(DefineConstants);BROWSER</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />
    <PackageReference Include="DrawnUi.Blazor.Game" Version="*" />
  </ItemGroup>

  <ItemGroup>
    <TrimmerRootAssembly Include="SkiaSharp" />
  </ItemGroup>
</Project>
```

---

### C) MAUI + Blazor shared solution (Arkanoid pattern)

```
MyGame.sln
src/
  Shared/                         ← shared game logic, no output
    MyGame.Shared.shproj
    MyGame.Shared.projitems
    GlobalAliases.cs              ← global using MauiGame = DrawnUi.Gaming.DrawnGame;
    Game/
      MyGame.cs
      MyGame.Loop.cs
      MyGame.Ui.cs
      MyGame.Input.cs
      MyGame.Sound.cs             ← uses IAudioService interface
      Internals/
        GameState.cs
        IReusableSprite.cs
      Sprites/
        BallSprite.cs  ...
      Sound/
        IAudioService.cs          ← interface; impl stays in platform project
  Mobile/                         ← MAUI head project
    MyGame.Mobile.csproj
    MauiProgram.cs
    MainPage.cs
    Game/
      Sound/
        MobileAudioService.cs     ← IAudioService impl for MAUI
    Platforms/...
    Resources/Raw/...             ← MauiAsset (audio, images)
  Web/MyGame.Web/                 ← Blazor head project
    MyGame.Web.csproj
    Program.cs
    Pages/
      GamePage.razor
    Sound/
      WebAudioService.cs          ← IAudioService impl using JS interop
    wwwroot/
      Images/ Sounds/ Music/      ← assets (copied manually from Mobile/Raw)
```

**`MyGame.Shared.shproj`** (boilerplate, one GUID)
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Label="Globals">
    <ProjectGuid>YOUR-GUID-HERE</ProjectGuid>
    <MinimumVisualStudioVersion>14.0</MinimumVisualStudioVersion>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"
          Condition="Exists(...)" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\...\Microsoft.CodeSharing.Common.Default.props"
          Condition="Exists(...)" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\...\Microsoft.CodeSharing.Common.props"
          Condition="Exists(...)" />
  <PropertyGroup />
  <Import Project="MyGame.Shared.projitems" Label="Shared" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\...\Microsoft.CodeSharing.CSharp.targets"
          Condition="Exists(...)" />
</Project>
```

**`MyGame.Shared.projitems`** (lists every shared `.cs` file)
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
    <HasSharedItems>true</HasSharedItems>
    <SharedGUID>YOUR-GUID-HERE</SharedGUID>
  </PropertyGroup>
  <PropertyGroup Label="Configuration">
    <Import_RootNamespace>MyGame</Import_RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)GlobalAliases.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\MyGame.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\MyGame.Loop.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\MyGame.Ui.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\MyGame.Input.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\MyGame.Sound.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\Internals\GameState.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\Internals\IReusableSprite.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Game\Sprites\BallSprite.cs" />
    <!-- add every shared .cs file here -->
    <Compile Include="$(MSBuildThisFileDirectory)Game\Sound\IAudioService.cs" />
  </ItemGroup>
</Project>
```

**MAUI head imports shared:**
```xml
<!-- In MyGame.Mobile.csproj -->
<Import Project="../Shared/MyGame.Shared.projitems" Label="Shared" />
<ItemGroup>
  <PackageReference Include="DrawnUi.Maui.Game" Version="*" />
</ItemGroup>
```

**Blazor head imports shared:**
```xml
<!-- In MyGame.Web.csproj -->
<DefineConstants>$(DefineConstants);BROWSER</DefineConstants>
<!-- ... -->
<Import Project="../../Shared/MyGame.Shared.projitems" Label="Shared" />
<ItemGroup>
  <PackageReference Include="DrawnUi.Blazor.Game" Version="*" />
</ItemGroup>
```

**`GlobalAliases.cs`** (in Shared root)
```csharp
global using MauiGame = DrawnUi.Gaming.DrawnGame;
```

---

### Platform guards in shared code

```csharp
// Audio init example in shared game class:
#if BROWSER
    BeginStartupAssetLoading();   // Blazor: JS-interop preload
#else
    if (USE_SOUND)
        _ = InitializeAudioAsync();  // MAUI: native audio
#endif

// Focus (keyboard) - Blazor doesn't support Focus():
#if !BROWSER
    Focus();
#endif

// Blazor-only compat helpers go in a file guarded by #if BROWSER
```

---

### Asset management: shared vs platform

| Asset type | MAUI | Blazor |
|---|---|---|
| Images/Sprites | `Resources/Raw/Images/` as `MauiAsset` | `wwwroot/Images/` (static file) |
| Audio files | `Resources/Raw/Sounds/` as `MauiAsset` | `wwwroot/Sounds/` (JS fetch) |
| Fonts | `Resources/Fonts/` as `MauiFont` | `wwwroot/fonts/` in `index.html` |
| Loading | `FileSystem.OpenAppPackageFileAsync(path)` | `SkiaImageManager.PreloadImages(paths)` |

Maintain both `wwwroot/` and `Resources/Raw/` as parallel copies (no symlink; just duplicate).

---

## Base class

```csharp
public class MyGame : DrawnUi.Gaming.DrawnGame   // alias: MauiGame = DrawnUi.Gaming.DrawnGame
```

`DrawnUi.Gaming.DrawnGame` (source `src/SharedGame/DrawnGame.cs`) extends `SkiaLayout`. Key members:

| Member | Purpose |
|--------|---------|
| `StartLoop(int delayMs=0)` | Begin game tick |
| `StopLoop()` | Stop game tick |
| `GameLoop(float deltaSeconds)` | Override - called every frame |
| `Pause()` / `Resume()` | App lifecycle hooks |
| `OnPaused()` / `OnResumed()` | Override for pause/resume logic |
| `LastFrameTimeNanos` | Protected - last frame timestamp (nanos) |
| `FrameInterpolatorDisabled` | Static - set `true` on Android to prefer frame-skip |
| `OnKeyDown(InputKey)` / `OnKeyUp(InputKey)` | Override - keyboard wired by base |
| `ProcessGestures(...)` | Override - touch/gesture input |
| `IgnoreChildrenInvalidations` | Set `true` after init - **critical for perf** |
| `Update()` | Force one re-render |

---

## MAUI entry point (XAML)

```xml
<draw:Canvas
    Gestures="Enabled"
    RenderingMode="Accelerated"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">

    <draw:SkiaLayout HorizontalOptions="Fill" VerticalOptions="Fill">
        <game:MyGame />
        <draw:SkiaLabelFps
            Margin="0,0,4,24"
            BackgroundColor="DarkRed" TextColor="White"
            HorizontalOptions="End" VerticalOptions="End"
            Rotation="-45" ZIndex="100" />
    </draw:SkiaLayout>
</draw:Canvas>
```

---

## Blazor entry point

### Fixed-aspect game (portrait/landscape locked)

```razor
@page "/my-game"
@using DrawnUi.Draw
@using DrawnUi.Views
@implements IAsyncDisposable
@inject IJSRuntime JS

<AspectLockedViewportHost
    AspectWidth="@ViewportWidth"
    AspectHeight="@ViewportHeight"
    BackgroundColor="#020816"
    IsFullscreen="@_isFullscreen">

    <AspectLockedCanvas
        @ref="_canvas"
        Content="_canvasContent"
        LogicalWidth="@ViewportWidth"
        LogicalHeight="@ViewportHeight"
        HorizontalOptions="LayoutOptions.Fill"
        VerticalOptions="LayoutOptions.Fill"
        BackgroundColor="#020816"
        RenderingMode="@RenderingModeType.Accelerated"
        Gestures="@GesturesMode.Enabled"
        IsFullscreen="@_isFullscreen"
        IsFullscreenChanged="OnFullscreenChanged" />
</AspectLockedViewportHost>

@code {
    private const float ViewportWidth = 480f;
    private const float ViewportHeight = 800f;
    private readonly SkiaControl _canvasContent;
    private AspectLockedCanvas _canvas;
    private bool _isFullscreen;

    public MyGamePage()
    {
        _canvasContent = new SkiaLayer()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new MyGame()
                {
                    WidthRequest = ViewportWidth,
                    HeightRequest = ViewportHeight,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                },
                new SkiaLabelFps()
                {
                    UseCache = SkiaCacheType.GPU,
                    Margin = new(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    BackgroundColor = Colors.DarkRed,
                    TextColor = Colors.White,
                    ZIndex = 110,
                }
            }
        };
    }

    private async Task OnFullscreenChanged(bool isFullscreen)
    {
        _isFullscreen = isFullscreen;
        _canvas?.InvalidateChildren();
    }

    public async ValueTask DisposeAsync() { /* dispose JS modules */ }
}
```

### Parallax / platformer (rescaling canvas)

Use `ParallaxRescalingCanvas` instead of `AspectLockedCanvas`. Wrap game in a clipping `SkiaLayer`:

```csharp
_canvasContent = new SkiaLayer()
{
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    BackgroundColor = Colors.Black,
    Children =
    {
        new SkiaLayer()
        {
            WidthRequest = ViewportWidth,
            HeightRequest = ViewportHeight,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsClippedToBounds = true,
            Children = { _stage }
        },
        new SkiaLabelFps() { ... }
    }
};
```

---

## Game class initialization template

```csharp
public class MyGame : DrawnUi.Gaming.DrawnGame
{
    const int MAX_ENEMIES = 24;
    const int MAX_BULLETS = 64;

    private bool _initialized;
    private bool _needPrerender;

    public MyGame()
    {
        // Build UI tree here (before layout is ready)
        // Do NOT start loop here

        // Android: prefer frame-skip over interpolation for fast games
        // Game.FrameInterpolatorDisabled = true;  // uncomment for Android

        // Platform lifecycle
        Super.OnNativeAppResumed += (s, e) => Resume();
        Super.OnNativeAppPaused += (s, e) => Pause();
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        Task.Run(async () =>
        {
            while (Superview == null || !Superview.HasHandler)
                await Task.Delay(30);

            Initialize();
        }).ConfigureAwait(false);
    }

    void Initialize()
    {
        if (_initialized || !Superview.HasHandler) return;

        // Pre-build sprite pools in parallel (NEVER create sprites during gameplay)
        Parallel.Invoke(
            () => { for (int i = 0; i < MAX_ENEMIES; i++) AddToPool(CreateEnemy()); },
            () => { for (int i = 0; i < MAX_BULLETS; i++) AddToPool(CreateBullet()); }
        );

        IgnoreChildrenInvalidations = true;  // CRITICAL: stop child invalidations after init
        _needPrerender = true;
        _initialized = true;

        PresentGame();
    }

    protected override void Draw(DrawingContext context)
    {
        base.Draw(context);
        if (_needPrerender)
        {
            // warmup shaders / precompile here
            _needPrerender = false;
        }
    }

    public override void GameLoop(float deltaSeconds)
    {
        base.GameLoop(deltaSeconds);

        // ... game logic

        ProcessSpritesToBeRemoved();

        foreach (var add in _spritesToBeAdded)
            AddSubView(add);
        _spritesToBeAdded.Clear();
    }

    protected override void OnPaused()
    {
        StopLoop();
        // mute audio etc
    }

    protected override void OnResumed()
    {
        StartLoop();
        // unmute audio etc
    }

    public override void OnWillDisposeWithChildren()
    {
        Super.OnNativeAppResumed -= ...;
        Super.OnNativeAppPaused -= ...;
        base.OnWillDisposeWithChildren();
    }
}
```

---

## Sprite pooling

**Rule:** Pre-allocate all sprites before gameplay starts. Never `new` during `GameLoop`.

### Pool implementation (simple)
```csharp
// SpaceShooter pattern - Dictionary<Guid, T>
protected Dictionary<Guid, BulletSprite> BulletsPool = new(MAX_BULLETS);

// Take from pool
var sprite = BulletsPool.Values.FirstOrDefault();
if (sprite != null && BulletsPool.Remove(sprite.Uid))
{
    sprite.IsActive = true;
    sprite.TranslationX = ...;
    _spritesToBeAdded.Add(sprite);
}

// Return to pool (after AnimateDisappearing)
BulletsPool.TryAdd(sprite.Uid, sprite);
RemoveSubView(sprite);
```

### Pool implementation (typed, Arkanoid pattern)
```csharp
public class ReusableSpritePool<T> where T : IReusableSprite
{
    private Dictionary<Guid, T> Pool;
    public ReusableSpritePool(int size) => Pool = new(size);
    public void Return(T item) => Pool.TryAdd(item.Uid, item);
    public T Get()
    {
        var item = Pool.Values.FirstOrDefault();
        if (item != null && Pool.Remove(item.Uid)) return item;
        return default;
    }
    public int Count => Pool?.Count ?? 0;
}
```

### IReusableSprite interface
```csharp
public interface IReusableSprite
{
    Guid Uid { get; }
    bool IsActive { get; set; }
    void ResetAnimationState();
    Task AnimateDisappearing();
}
```

### Deferred add/remove pattern
```csharp
// Fields
private readonly ConcurrentQueue<SkiaControl> _spritesToBeRemovedLater = new();
private readonly List<SkiaControl> _spritesToBeAdded = new(128);

void RemoveReusable(IReusableSprite sprite)
{
    sprite.IsActive = false;
    sprite.AnimateDisappearing().ContinueWith(_ =>
    {
        _spritesToBeRemovedLater.Enqueue(sprite as SkiaControl);
    }).ConfigureAwait(false);
}

void ProcessSpritesToBeRemoved()
{
    while (_spritesToBeRemovedLater.TryDequeue(out var sprite))
        RemoveSprite(sprite);  // returns to pool + RemoveSubView
}
```

---

## Collision detection

### AABB (simple, desktop-safe)
```csharp
if (rectA.IntersectsWith(rectB))  { /* hit */ }
if (rectA.IntersectsWith(rectB, out SKRect overlap))  { /* hit with overlap info */ }
```

### Raycast (preferred on mobile - handles frame drops)
```csharp
// In SpaceShooter, hitbox is updated once per frame before collision checks:
sprite.UpdateState(LastFrameTimeNanos);  // caches HitBox

// RaycastCollision (Arkanoid):
var hit = RaycastCollision.CastRay(
    ballPosition,   // Vector2 center
    ballDirection,  // Vector2 normalized
    maxDistance,    // float: speed * deltaSeconds
    ballRadius,     // float
    collisionTargets  // IEnumerable<IWithHitBox>
);
if (hit.Collided) { /* hit.Target, hit.Face, hit.Distance */ }
```

### IWithHitBox interface
```csharp
public interface IWithHitBox
{
    SKRect HitBox { get; set; }
    void UpdateState(long frameTimeNanos, bool force = false);
}
```

### Getting canvas position
```csharp
// For TranslationX/Y based sprites (SpaceShooter):
var pos = sprite.GetPositionOnCanvasInPoints();
var hitBox = new SKRect(pos.X, pos.Y, pos.X + sprite.Width, pos.Y + sprite.Height);

// For Left/Top based sprites (Arkanoid):
var hitBox = sprite.GetHitBox();  // returns SKRect in canvas coords
```

---

## Sprite positioning: two patterns

| Pattern | When | Position fields |
|---------|------|----------------|
| `TranslationX/Y` | Center-origin, relative to layout center | `sprite.TranslationX`, `sprite.TranslationY` |
| `Left/Top` | Top-left origin, absolute inside parent | `sprite.Left`, `sprite.Top` |

Player clamping example (TranslationX pattern):
```csharp
void UpdatePlayerPosition(double x)
{
    var left = -Width / 2f + Player.Width / 2f;
    var right = Width / 2f - Player.Width / 2f;
    Player.TranslationX = Math.Clamp(x, left, right);
}
```

---

## Input handling

### Keyboard
```csharp
// Base class auto-wires KeyboardManager - just override:
public override void OnKeyDown(InputKey key)
{
    switch (key)
    {
        case InputKey.ArrowLeft:  _moveLeft = true; break;
        case InputKey.ArrowRight: _moveRight = true; break;
        case InputKey.Space:      Fire(); break;
    }
}

public override void OnKeyUp(InputKey key)
{
    if (key == InputKey.ArrowLeft)  _moveLeft = false;
    if (key == InputKey.ArrowRight) _moveRight = false;
}

volatile bool _moveLeft, _moveRight;
```

Key mappings dictionary (rebindable):
```csharp
Dictionary<InputKey, GameKey> ActionKeys = new()
{
    { InputKey.Space,      GameKey.Fire  },
    { InputKey.ArrowLeft,  GameKey.Left  },
    { InputKey.ArrowRight, GameKey.Right },
};
```

### Touch / gestures
```csharp
const double thresholdNotPanning = 20.0;
bool _wasPanning, _isPressed;

public override ISkiaGestureListener ProcessGestures(
    SkiaGesturesParameters args, GestureEventProcessingInfo apply)
{
    if (State == GameState.Playing)
    {
        var velocityX = (float)(args.Event.Distance.Velocity.X / RenderingScale);

        if (args.Type == TouchActionResult.Panning)
        {
            _wasPanning = true;
            _moveLeft  = velocityX < 0;
            _moveRight = velocityX > 0;
            return this;
        }
        if (args.Type == TouchActionResult.Down)
        {
            _isPressed = true;
            _wasPanning = false;
        }
        if (args.Type == TouchActionResult.Tapped
            || (args.Type == TouchActionResult.Up
                && _isPressed
                && Math.Abs(args.Event.Distance.Total.X) < thresholdNotPanning * RenderingScale))
        {
            Fire();
        }
        if (args.Type == TouchActionResult.Up)
        {
            _isPressed = false;
            _moveLeft = _moveRight = false;
        }
        return this;
    }
    return base.ProcessGestures(args, apply);
}
```

---

## Parallax layers

Scroll scale determines depth. Typical scale set (farthest to nearest):
```
back:       0.18
far:        0.34
middle:     0.56
near:       0.82
tileset:    1.00 (world speed)
foreground: 1.25
```

Drive from `_worldPosition` accumulator:
```csharp
// In GameLoop:
_worldPosition += moveInput * MoveSpeed * deltaSeconds;

// Apply to layers:
_back.OffsetX   = -_worldPosition * 0.18f;
_far.OffsetX    = -_worldPosition * 0.34f;
_middle.OffsetX = -_worldPosition * 0.56f;
_near.OffsetX   = -_worldPosition * 0.82f;
_tileset.OffsetX = -_worldPosition;
_foreground.OffsetX = -_worldPosition * 1.25f;
```

Each layer must call `Update()` when `OffsetX` changes (no binding - call it directly).

`RepeatingStripControl` extends `SkiaImage`, overrides `DrawSource`, tiles image using GPU texture + offscreen repeat-band cache. Use `UseCache = SkiaCacheType.None` on scrolling layers.

---

## Animated sprite sets

```csharp
// SkiaSpriteSet - wraps multiple sprite-sheet animations
public class GhostSprite : SkiaSpriteSet
{
    public GhostSprite()
    {
        UseCache = SkiaCacheType.GPU;
        Define(0, "path/appear.png",  columns: 6, rows: 1, fps: 10);
        Define(1, "path/chase.png",   columns: 4, rows: 1, fps: 10);
        Define(2, "path/vanish.png",  columns: 7, rows: 1, fps: 10, repeat: 1);
    }
    // State = 0..N selects animation
    // Mirror: CurrentSprite.ScaleX = -1
}
```

---

## Caching rules for game sprites

| Control type | Cache setting | Reason |
|---|---|---|
| Static sprite (enemy, bullet, ball) | `GPU` | Blitted each frame, never redraws content |
| Scrolling / parallax layer | `None` | Redraws offset every frame |
| UI overlay (HUD, dialog) stable during pause | `GPU` | Blitted under pause; blur dialogs need post-render hack |
| Root game container | none (`IgnoreChildrenInvalidations = true`) | Manages its own invalidation via game loop |

**Never** set GPU cache nested inside a parent that already has GPU cache unless you know that subtree is fully stable.

---

## Asset preloading (Blazor WASM)

```csharp
private static readonly string[] SceneAssets = [
    "media/sprites/player.png",
    "media/sprites/enemy.png",
];

private async Task InitializeAsync()
{
    await SkiaImageManager.Instance.PreloadImages(SceneAssets);

    // Access after preload:
    var bitmap = SkiaImageManager.Instance.GetFromCache("media/sprites/player.png");

    IgnoreChildrenInvalidations = true;
    LastFrameTimeNanos = SkiaControl.GetNanoseconds();
    _initialized = true;
    Update();
    StartLoop();
}
```

GPU texture upload pattern (for custom renderers):
```csharp
// Promote CPU image to GPU once
if (_gpuImage == null && Superview is DrawnView drawnView)
{
    using var surface = drawnView.CreateSurface(source.Width, source.Height, true);
    if (surface?.Context != null)
    {
        surface.Canvas.DrawImage(source.Image, rect, paint);
        surface.Canvas.Flush();
        _gpuImage = surface.Snapshot();
        drawnView.ReturnSurface(surface);
    }
}
```

---

## Game state machine

```csharp
public enum GameState { Ready, Playing, Paused, Ended, DemoPlay, LevelComplete }

GameState State { get; set; }  // OnPropertyChanged for UI binding

// State flow:
// Ready → Playing (StartNewGame)
// Playing → Paused (app background / pause button)
// Playing → Ended (GameLost / GameWon)
// Playing → LevelComplete (all targets cleared)
// LevelComplete → Playing (next level)
// Any → DemoPlay (AI attracts)
```

---

## Score update pattern

Score changes multiple times per frame → avoid `OnPropertyChanged` every hit. Update UI manually once per frame after loop body:

```csharp
void UpdateScore()
{
    LabelScore.Text = $"SCORE: {Score}";
}

// In GameLoop, call UpdateScore() last
```

---

## Android-specific

```csharp
// In game constructor or MAUI host:
#if ANDROID
    Game.FrameInterpolatorDisabled = true;  // prefer skip over interpolation
#endif
```

---

## Common mistakes

- **Creating sprites in GameLoop** → GC spikes, frame drops. Always pool.
- **Forgetting `IgnoreChildrenInvalidations = true`** → massive CPU waste per frame.
- **Iterating `Views` while adding/removing** → crash. Use `_spritesToBeAdded/Removed` queues.
- **Updating score label via binding every hit** → UI thread saturation. Batch once per frame.
- **`UseCache = GPU` on scrolling parallax layer** → cache invalidated every frame = wasted GPU upload.
- **Not waiting for `Superview.HasHandler`** → premature GPU operations crash.
- **Not calling `UpdateState(LastFrameTimeNanos)` before collision checks** → stale hitboxes.
- **Setting `BindingContext` to `this` inside MAUI game** - parent may override it; re-assert in `OnBindingContextChanged`.

---

---

## Pong / paddle game patterns

### SkiaLabel vertical positioning in absolute layout
`SkiaLabel` ignores `Top` for vertical positioning regardless of `LayoutOptions`. Must use `Margin`:
```csharp
new SkiaLabel()
{
    HorizontalOptions = LayoutOptions.Center,
    VerticalOptions = LayoutOptions.Start,
    Margin = new Thickness(0, targetY, 0, 0),
    HorizontalTextAlignment = DrawTextAlignment.Center,
}
```

### Ball follows paddle before serve (Breakout/Pong pattern)
In `WaitingToStart` game phase, set `Ball.Left` each frame to track the serving paddle:
```csharp
Ball.Left = ServingPaddle.Left + (PADDLE_WIDTH - 14) / 2.0; // center ball on paddle
```
Do this in GameLoop, NOT in the sprite itself.

### Ball angle formula for paddle bounce (clean Pong formula)
Standard Pong bounce - center hit = straight, edge hit = angled:
```csharp
const float MAX_DEV = MathF.PI * 0.20f; // ±36° from vertical, min 54° from horizontal

// Bottom paddle (player) - ball goes UP (sin < 0):
var hitPos = (ballHit.MidX - paddleHit.Left) / paddleHit.Width; // 0..1
Ball.Angle = ClampAngleFromHorizontal(-MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
if (MathF.Sin(Ball.Angle) > 0) Ball.Angle = -Ball.Angle; // ensure upward

// Top paddle (AI) - ball goes DOWN (sin > 0):
Ball.Angle = ClampAngleFromHorizontal(MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
if (MathF.Sin(Ball.Angle) < 0) Ball.Angle = -Ball.Angle; // ensure downward
```

### Dynamic ball speed per rally
Add `Speed` property to ball sprite, increase on each paddle hit, reset on serve:
```csharp
// BallSprite:
public float Speed { get; set; } = PongGame.BALL_SPEED;
// UpdatePosition uses Speed instead of BALL_SPEED constant

// In GameLoop on paddle hit:
Ball.Speed = MathF.Min(Ball.Speed + 20f, PongGame.BALL_SPEED * 2.0f);

// In Serve():
Ball.Speed = BALL_SPEED;
```

### AI auto-serve
When AI scores and must re-serve, do NOT require player tap. In GameLoop WaitingToStart:
```csharp
if (_aiServes)
{
    // wander left/right
    _aiWanderTimer -= deltaSeconds;
    if (_aiWanderTimer <= 0) { /* pick random dir */ }
    if (atLeftWall && movingLeft) _aiWanderDir = 1;
    if (atRightWall && movingRight) _aiWanderDir = -1;
    MovePaddle(AiPaddle, _aiWanderDir, deltaSeconds);
    Ball.Left = AiPaddle.Left + (PADDLE_WIDTH - 14) / 2.0; // ball follows

    _autoServeTimer -= deltaSeconds;
    if (_autoServeTimer <= 0) Serve();
}
```

### AI pre-positioning during player's serve
AI should track ball X while player has it (ball is at player paddle):
```csharp
// In WaitingToStart, !_aiServes block:
var targetLeft = (float)(Ball.Left - (PADDLE_WIDTH - 14) / 2.0);
targetLeft = MathF.Max(0, MathF.Min(targetLeft, WIDTH - PADDLE_WIDTH));
var dist = targetLeft - (float)AiPaddle.Left;
if (MathF.Abs(dist) > PADDLE_WIDTH * 0.1f)
    MovePaddle(AiPaddle, dist > 0 ? 1f : -1f, deltaSeconds);
```

### AI reaction time tuning (Medium difficulty)
Reaction time 0.25–0.7s is too slow - AI misses trivial balls right after serve. Working Medium params:
```csharp
_reactionTimeMin = 0.05f; _reactionTimeMax = 0.2f;
_accuracy = 0.88f; _mistakeProbability = 0.08f;
_mistakeDurationMin = 0.25f; _mistakeDurationMax = 0.55f;
_movementSmoothingTime = 0.1f;
```

### AI wall-bounce prediction
Use game logical width constant, not `_game.Width` (which can be rendered pixel size):
```csharp
while (predicted < 0 || predicted > PongGame.WIDTH)
{
    if (predicted < 0) predicted = -predicted;
    else if (predicted > PongGame.WIDTH) predicted = 2 * PongGame.WIDTH - predicted;
}
```

### Reset paddles after each point
Call `ResetPaddles()` in the Scored→WaitingToStart transition (not in Score()) so paddles snap to center cleanly before ball reappears:
```csharp
// In Scored phase transition:
ResetPaddles();
ResetBall(playerServes);
_phase = GamePhase.WaitingToStart;
```

### AI IdleWander during game phases
`PongAI.Update()` is only called during Playing phase. For WaitingToStart behavior (wander, pre-position), implement directly in GameLoop - do NOT rely on PongAI.Update or IdleWander being called.

### WASM boot race condition
.NET 10 Blazor WASM uses fingerprinted files (e.g. `App.g1f0h62exp.wasm`). Server needs ~10–12s warmup after `dotnet run` before fingerprinted files are reliably served. Navigate after warmup; fresh browser context clears cached 404s.

---

## OpenTK Desktop target (Linux / WSL2)

### NativeWindowSettings — required Linux fixes

```csharp
var nativeSettings = new NativeWindowSettings
{
    API = ContextAPI.OpenGL,
    // Mesa D3D12 (WSLg) doesn't expose GL 4.6 via GLX — use 3.3 on Linux
    APIVersion = OperatingSystem.IsLinux() ? new Version(3, 3) : new Version(4, 6),
    Profile = ContextProfile.Core,
    // Required on WSLg: prevents fullscreen→windowed transition that kills D3D12 device
    WindowState = WindowState.Normal,
    ...
};
```

### WSL2 one-time setup

```bash
sudo apt install libglfw3 libopenal1 libgl1 libgles2-mesa unzip
glxinfo | grep "OpenGL renderer"   # verify GPU (should show D3D12/Intel/AMD, not llvmpipe)
```

### libglfw.so.3 — replace bundled with system GLX build

Self-contained publish bundles `libglfw.so.3` compiled with EGL. EGL fails to create desktop OpenGL Core context on WSLg. Replace with Ubuntu `libglfw3` package (X11/GLX build):

```bash
cd /path/to/publish
mv libglfw.so.3 libglfw.so.3.bak
ln -s /usr/lib/x86_64-linux-gnu/libglfw.so.3 libglfw.so.3
```

### Launching

```bash
DISPLAY=:0 ./YourApp     # WSLg: set X11 display
```

Audio (OpenAL/libopenal1) works automatically via WSLg PulseAudio — no extra config.

### Crash signatures and causes

| Error | Cause | Fix |
|---|---|---|
| `EGL: Failed to create context: Arguments are inconsistent` | Bundled libglfw uses EGL; Mesa EGL can't create desktop GL Core context | Replace libglfw.so.3 with system GLX build |
| `GLX: Failed to create context: GLXBadFBConfig` | GL 4.6 not available on Mesa D3D12 | Use GL 3.3 on Linux |
| `D3D12: Removing Device` + segfault | WSLg fullscreen→windowed mode switch resets D3D12 device | Set `WindowState = WindowState.Normal` |
| 400+ FPS in Constant mode | Mesa/WSLg ignores VSync swap interval | `DrawnUiWindow` auto-caps via `UpdateFrequency` on Linux; custom `GameWindow` must set it manually |

---

## Blazor WASM startup optimization (slow boot, "initializing" hang)

Root cause of a frozen boot loader is almost always `UseDrawnUiAsync` → `SkiaFontManager.InitializeAsync` downloading every registered font BEFORE `host.RunAsync()` — no progress shown. Full CJK/emoji fonts are 10–26 MB each. Diagnose first: `wwwroot/fonts` sizes, `ConfigureFonts` registrations, Network tab during the frozen phase (also catch 404s of registered-but-missing fonts).

Two publish-level checks that dominate everything:
1. **AOT** — without `<RunAOTCompilation>true</RunAOTCompilation>` Release runs on the Mono interpreter (`WasmBuildNative=true` is NOT AOT). Measured: main-thread blocked 41%→17%, ~2x FPS under 6x CPU throttle. Dev `dotnet run` stays interpreter — never judge runtime perf from the dev loop.
2. **Server compression** — publish emits `.br`/`.gz` siblings; default nginx ignores them. AOT grows `dotnet.native.wasm` ~5x raw (~10MB as .br) — without `brotli_static on; gzip_static on;` boot looks frozen for minutes. Verify: `curl -sI -H "Accept-Encoding: br" .../dotnet.native.<hash>.wasm` → `Content-Encoding: br`. See `docs/articles/blazor/publishing.md`.

Three-stage font architecture:
1. **Boot (blocking, light)**: register only small per-language subset fonts covering 100% of that language's UI strings + used emojis. Language switch on web = page reload (persist choice), so no runtime font switching needed.
2. **In-app loading screen**: app starts, splash hides, real game stays hidden behind a full-screen loading layer with progress until ALL heavy resources are in (never show partial content then re-render).
3. **Deferred streaming**: loading screen streams full fonts (replacing subset aliases — same alias, transparent swap) + preloads audio, then reveals the app.

Font subsetting (`fonttools`, `pip install fonttools lxml`): Latin subset = ASCII + Latin ext + Greek + Cyrillic + punctuation/symbols ranges (26 MB CJK font → ~260 KB); CJK subsets add glyphs extracted from that language's .resx + kana for ja; emoji subset = scan source/resx for actual emoji + U+FE0F/U+200D (22.6 MB → ~160 KB). Traps: do NOT union a companion font's cmap into a subset (keeps the whole font); plain `SkiaLabel` does NOT per-character-fallback — a subset missing a script silently drops glyphs (not tofu); rerun subsetting when resx strings/emojis change.

Framework APIs (`src/Blazor/DrawnUi/Internals/Core/SkiaFontManager.cs`): `InitializeAsync` parallelizes startup downloads (`Task.WhenAll`); `LoadFontAsync(alias, url, IProgress<float>?, ct)` = runtime load with real progress (`SetBrowserResponseStreamingEnabled(true)` + `ResponseHeadersRead` + chunked read — without streaming, Blazor buffers whole body and progress jumps 0→100), replaces typeface under the same alias.

Audio preloading batches (Breakout pattern — `BreakoutGame.StartupAssets.cs`): `record StartupAssetBatch(string StatusText, int TotalItems, Func<Action<int,int,string>, Task> LoadAsync)` so fonts+audio+images share one progress bar; show overlay only if still pending after ~250 ms grace (cached revisits skip the flash); on failure continue without the resource, dialog after presenting.

Validation: Network tab filtered to `fonts/` — boot fetches only subsets, full fonts after app start; visual check hanzi + hangul (fallback trap above); loading progress moves; measure payload before/after (real case: 61 MB → 1.2 MB for en).

## References (DrawnUI repo, https://github.com/taublast/DrawnUi)

- `src/SharedGame/DrawnGame.cs` - base class (+ `Game.Input.cs`, `IGame.cs`)
- `src/Blazor/Samples/BlazorSandbox/Games/SpaceShooter/` - full shooter example (pooling, deferred add/remove)
- `src/Blazor/Samples/BlazorSandbox/Games/Parallax/` - parallax/platformer (strip controls, tileset, input)
- `src/Shared/Samples/Pong.Shared/Game/` - Pong: `PongGame.cs`, `PongGame.Loop.cs` (loop + AI integration), `Ai/PongAI.cs`
- https://github.com/taublast/DrawnUi.Breakout - MAUI+Blazor shared game (Arkanoid pattern: shproj/projitems, IAudioService per head, raycast collision)
