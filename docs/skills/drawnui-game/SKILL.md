---
name: drawnui-game
description: Use when creating or modifying games built on DrawnUi.Gaming.DrawnGame for MAUI, Blazor WASM, pure WebAssembly (DrawnUi.Web), OpenTK and WPF targets. Covers game loop, sprites, physics, canvas rendering, input, and cross-platform game patterns.
version: 1.2.0
tags: [drawnui, game, maui, skia, dotnet]
---

# DrawnUI Game Development Skill

Use this skill when creating or modifying games built on `DrawnUi.Gaming.DrawnGame` for MAUI and/or Blazor WASM targets (the same base class ships for pure WebAssembly, OpenTK and WPF).

| Head | Game package | Pong sample (shared `src/Shared/Samples/Pong.Shared`) |
|---|---|---|
| MAUI | `DrawnUi.Maui.Game` | `src/Maui/Samples/MauiPong` |
| Blazor WASM | `DrawnUi.Blazor.Game` | `src/Blazor/Samples/BlazorSandbox` (`Pages/PongPage.razor`) |
| Pure WebAssembly | `DrawnUi.Web.Game` | `src/Wasm/Samples/PongWeb` (see `drawnui-web-app` skill) |
| OpenTK | `DrawnUi.OpenTk.Game` | `src/OpenTk/Samples/OpenTkPong` |
| WPF | `DrawnUi.Wpf.Game` | `src/Wpf/Samples/WpfPong` |

Every addon compiles the same `src/SharedGame` sources (`DrawnGame.cs`, `Game.Input.cs`, `IGame.cs`).

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
    <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.80" />
    <PackageReference Include="DrawnUi.Maui.Game" Version="1.10.8.26" />
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
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.7" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.7" PrivateAssets="all" />
    <PackageReference Include="DrawnUi.Blazor.Game" Version="1.10.8.26" />
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
  <PackageReference Include="DrawnUi.Maui.Game" Version="1.10.8.26" />
</ItemGroup>
```

**Blazor head imports shared:**
```xml
<!-- In MyGame.Web.csproj -->
<DefineConstants>$(DefineConstants);BROWSER</DefineConstants>
<!-- ... -->
<Import Project="../../Shared/MyGame.Shared.projitems" Label="Shared" />
<ItemGroup>
  <PackageReference Include="DrawnUi.Blazor.Game" Version="1.10.8.26" />
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
| `Pause()` / `Resume()` | App lifecycle hooks; set `IsPaused`, `Resume()` also resets `LastFrameTimeNanos` |
| `OnPaused()` / `OnResumed()` | Override for pause/resume logic |
| `LastFrameTimeNanos` | Protected - last frame timestamp (nanos) |
| `FrameInterpolatorDisabled` | Static - `true` = raw frame delta (frame-skip), `false` = delta smoothed by `FrameTimeInterpolator`. Default `true` on Android, OpenTK and browser builds (Blazor, pure Web), `false` elsewhere |
| `OnKeyDown(InputKey)` / `OnKeyUp(InputKey)` | Override - keyboard wired by base, while the game is on a canvas (a game removed from the screen gets no keys and is released) |
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
@using DrawnUi
@using DrawnUi.Draw
@using DrawnUi.Views
@implements IAsyncDisposable
@inject IJSRuntime JS

<AspectLockedViewportHost
    AspectWidth="@ViewportWidth"
    AspectHeight="@ViewportHeight"
    BackgroundColor="#020816"
    IsFullscreen="@_isFullscreen"
    FitVisibleHeight="true">

    <AspectLockedCanvas
        @ref="_canvas"
        Content="_canvasContent"
        LogicalWidth="@ViewportWidth"
        LogicalHeight="@ViewportHeight"
        HorizontalOptions="LayoutOptions.Fill"
        VerticalOptions="LayoutOptions.Fill"
        BackgroundColor="@Color.Parse("#020816")"
        UpdateMode="@UpdateModeType.Constant"
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

    private Task OnFullscreenChanged(bool isFullscreen)
    {
        _isFullscreen = isFullscreen;
        _canvas?.InvalidateChildren();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() { /* dispose JS modules */ }
}
```

Host (`AspectLockedViewportHost.razor`, HTML space provider) and canvas (`AspectLockedCanvas`, letterboxing in C#) live in `src/Blazor/DrawnUi/Views`; design notes in `AspectLockedCanvas.md` there. `AspectLockedViewportHost.BackgroundColor` is a CSS string, `Canvas.BackgroundColor` is a `Color`. Other heads (MAUI, pure Web, OpenTK, WPF): Pong uses the shared-source `RescalingCanvas` (`Pong.Shared/Views/RescalingCanvas.cs`, a `Canvas` with `LogicalWidth`/`LogicalHeight`) for the same fixed-viewport fit.

### Parallax / platformer

Same `AspectLockedViewportHost` + `AspectLockedCanvas` as above (the parallax sample uses them too). Wrap the game in a clipping `SkiaLayer`:

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

        // Frame-skip vs interpolation: DrawnGame.FrameInterpolatorDisabled (already true on Android/OpenTK/browser)

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

## Sprites moved by Left / Top stay cached

**Rule:** `Left` and `Top` position a control only when it is cached (any `UseCache` other than `None`). A sprite moved by `Left`/`Top` with `UseCache = None` draws at the top-left corner of its parent and never moves. Never drop the cache of such a sprite, even for a cheap shape or one whose child animates every frame. Concrete case (2026-11-07): power-ups set to `SkiaCacheType.None` "to save redraws" all sat motionless at the top-left corner.

## Hidden pop-ups: switch them off, never "park" them at a near-zero opacity

Keeping pop-up labels drawn every frame at Opacity 0.004 (so their first show costs no first draw) is a trap: about 20 styled labels plus 3 ray images drawn every frame took a phone (BV8800, Android Debug) from 90 to 23 fps, and labels that had been shown once kept costing even at Opacity 0. Pattern that works (measured): put the pop-ups in their own small Fill layer, cache them as Image, let them be drawn during the first ~1.5 s (cache made), then park them with `SkipRendering = true` while not showing and `false` just before showing. `SkipRendering` keeps the control's layout and its cache; `IsVisible` toggling re-runs layout and drops the cache, so every show records the pop-up again (Blazor WASM Debug, 2026-11-09). `SkipRendering` is a plain property that requests no redraw: the game loop redraws every frame anyway; outside a loop call `Repaint()` after setting it. Never toggle IsVisible on a child of the big game field (that relayout was the original "EDGE!" hitch).

Park with `SkipRendering` only a control that was laid out and drawn once. A control with `SkipRendering = true` from the start is left out of the measure: its `MeasuredSize` and `DrawingRect` stay 0, and setting `SkipRendering = false` later does not lay it out, so it never appears.

Opacity 0 does not skip drawing: children at `Opacity = 0` are still drawn every frame, their caches included. A victory screen whose parts (title, score plate, 3 chips, button) waited at Opacity 0 cost ~55 ms in its first frame and ~10 ms per frame afterwards, against 7-8 ms when they were not drawn (Blazor WASM Debug, 2026-11-09).

**Build a big overlay over several game frames.** Build part of the tree in one frame, the rest in the next, and `AddSubView` it in a third: await a `TaskCompletionSource` that the game loop completes each frame, and keep a show counter so a dismiss in between drops the half-built screen. If your game loop drains a queue of posted actions, take the count first and run only the actions queued before the frame started (`for (var pending = queue.Count; pending > 0 && queue.TryDequeue(out var a); pending--) a();`): a drain-until-empty loop runs an action that posts itself again in the same frame. Measured: the victory screen's single ~200 ms frame became ~38 / 20 / 85 ms (Blazor WASM Debug, 2026-11-09).

## A cached layer moved by Left: its children's GetHitBox goes stale

Children of a cached layer (ImageCompositeGPU) are not drawn again when only the layer moves, so their own `GetHitBox()` stays where they were last drawn. Compute their hit boxes from the layer's own `GetHitBox()` (the layer itself is drawn every frame) plus the child's Left/Top. Move such a layer with `Left`, not `TranslationX` (TranslationX moves the picture only). Measured on Windows on a sliding row.

Removing a child from an `ImageCompositeGPU` layer costs one partial redraw of that layer (~2-3 ms on Blazor WASM Debug, a brick wall, 2026-11-09). Remove it directly: setting `Opacity = 0` first and removing it later costs two partial redraws.

## Slow idle motion: animate Scale, not Translation

An `Image`-cached control animated by `TranslationY` (or X) is drawn at whole pixels: its edge pixels stay identical and jump 1 px at a time. A slow, small move (a ±4 pt "float" over 1.4 s) then steps every 70-140 ms and looks jaggy. The same control animated by `Scale` renders at subpixel precision (its edge pixels change smoothly from frame to frame). Measured on the OpenTK head, DrawnUi 1.10.8.25 (2026-11-08). For idle "alive" effects use a scale pulse, opacity, or lights drawn with float transforms (DrawAtlas); keep translations for fast moves, where whole-pixel steps happen every frame and do not show.

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

### IWithHitBox interface (sample-level, define it in your game)
```csharp
public interface IWithHitBox
{
    void UpdateState(long time, bool forceRecalculate = false); // recompute HitBox for this frame
    SKRect HitBox { get; }                                       // precalculated
}
```

### Getting canvas position
```csharp
// For TranslationX/Y based sprites (SpaceShooter):
var pos = sprite.GetPositionOnCanvasInPoints();
var hitBox = new SKRect(pos.X, pos.Y, pos.X + sprite.Width, pos.Y + sprite.Height);

// For Left/Top based sprites (Pong `GameExtensions.GetHitBox`, sample helper):
var hitBox = sprite.GetHitBox();  // Left/Top + Width/Height = field-space (logical game coords)
```

Keep collisions in field-space when the game is drawn through a rescaling canvas: canvas-space positions stop matching game coords once the field is scaled or letterboxed.

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

No binding: the strip's `OffsetX` setter calls `Update()` itself and skips unchanged values (`ParallaxGame.RepeatingStripControl.cs`).

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

**Spin the cached sprite, not its child.** A child's transform (a letter with `RotationX` += per frame inside an `Operations`-cached SkiaShape) costs the sprite a cache re-record every frame: cheap on Android Release (BV8800: 5-10 falling power-ups = 11.1-11.4 ms/frame, a steady 90 FPS), ~1.5 ms per sprite on Blazor WASM Debug. `RotationX` on the cached sprite itself costs no re-record and draws correctly with `Operations` (2026-11-09); collisions are unaffected because `GetHitBox()` uses the layout box. A label with `Image` cache drew nothing under a 3D `RotationX`; `Operations` draws it. Before calling a cache setting slow, A/B ONE variable with the sprites behaving normally: a stress phase that also spawns many sprites and bursts in one frame, or sprites stuck in a corner (see "Sprites moved by Left / Top stay cached"), confounds the measurement.

**Timed effects (level reveal etc.) run on their own animator, not the game loop** (`control.AnimateRangeAsync(v => ..., 0, 1, ms, easing, cancel: token)`): a level can be built while the loop is stopped (new game, reset). Cancelling that token fires the callback once more SYNCHRONOUSLY — a stop method called from the callback must clear its fields BEFORE cancelling, or the nested call races it (2026-11-07: NRE left a disposed SkiaShaderEffect attached → the whole bricks layer drew nothing). Remove + dispose the effect when done so normal play blits the plain cache.

**Particles / glow bursts:** one overlay control with fixed struct pools, drawn with ONE `canvas.DrawAtlas(sheet, sprites[], RSXform[], colors[], SKBlendMode.Modulate, paint{BlendMode=Plus})` from a single WHITE sprite sheet tinted per sprite (dead slots = zero-scale RSXform). Per-sprite `DrawImage` cost ~1 ms CPU/frame for ~190 sprites on Android; the atlas ~0.1 ms. Never bake per-color textures at runtime (first use hitched 228 ms; colors that change per hit grow the cache). Draw the atlas once in black on the first frame to warm the GPU path. `SKRotationScaleMatrix` ctor = `(scale*cos, scale*sin, tx, ty)` (no `CreateScaleTranslation` in SkiaSharp 4.148).

**Text that changes every frame (score count-up) inside a cached plate:** with the plate on `Image`, every text change re-rasters the whole plate (12 ms per frame on Blazor WASM Debug). Put the plate on `Operations` and give its static labels their own `Operations` caches: a text change then costs 6 ms and idle frames 0.4-0.8 ms (2026-11-09).

**Blurred art (light rays, glows): bake the blur once, tint at draw time.** A control with `UseCache = Image` whose content draws with a blur `MaskFilter` paints the blur again every time its cache is made, so each show cost a 20-109 ms frame on Blazor WASM Debug. Instead, paint the blurred art once per pixel size into a shared static WHITE `SKImage`, and in the control's `Paint` override draw only that image, tinted: `_paint.ColorFilter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.Modulate); ctx.Context.Canvas.DrawImage(image, ctx.Destination, _paint);`. Keep the filter in a field and rebuild it only when the tint changes (release the old one with `DisposeObject`). Keep `UseCache = Operations` on the control: its cache records only that one `DrawImage`, it re-records cheaply on a tint change, and `Left`/`Top` keep placing it (see "Sprites moved by Left / Top stay cached"). The first frame of a burst went to 16-19 ms (2026-11-09).

**Adaptive render scale for a full-screen 3D or shader scene (fill-bound phones).** When the cost is per pixel (a mesh fragment program, a post effect), draw the scene with fewer pixels and scale it up, as a video player adapts its quality; HUD, texts and dialogs stay at full resolution. Measured on a 1080x2408 Mali-G57 phone (Dungeon Run, real SkMesh): title 35 FPS at 100 %, 52-56 at 75 %, 89-92 (the 90 Hz cap) at 50 %; 75 % looks the same as 100 % on a 480 dpi screen, 50 % a little softer. Recipe:
- Put only the scene (with its post `VisualEffects`) in one layer with `HorizontalFillRatio = VerticalFillRatio = scale`, `ScaleX = ScaleY = 1 / scale`, `AnchorX = AnchorY = 0`: the scene and its effect lay out and render at the small size, the transform stretches the result over the screen.
- Below 1 the layer overrides `Paint`: its children draw into a persistent GPU `SKSurface` of the layer's size (`CreateSurface(w, h, true)`, made again only when the size changes; one `SkiaDrawingContext { IsVirtual = true, Surface, Canvas }` kept with it, `Superview` and `FrameTimeNanos` updated per frame; translate by `-Destination.Left/Top` while drawing), then the surface is drawn into the destination with `new SKSamplingOptions(SKFilterMode.Linear)` (`SKSurface.Draw` has no sampling and would upscale blocky). For zero managed allocation per frame, call SkiaSharp's own bindings `sk_surface_new_image_snapshot`, `sk_canvas_draw_image_rect`, `sk_image_unref` through function pointers taken once from `SkiaSharp.SkiaApi` (`MethodHandle.GetFunctionPointer()`), releasing the image before the next frame draws into the surface; this works on every backend and keeps `Snapshot()` as the fallback. At 1 it calls `base.Paint` and costs nothing. Do not use `UseCache = GPU` for this: a GPU cache makes a new surface every time it is drawn again, i.e. every frame for a live scene.
- Measure the intervals between the scene's own draws (a `Stopwatch` timestamp in its `Paint`, on the render thread), never the `DrawnGame.GameLoop` delta: the game loop ticks at the display's rate even while the drawing falls behind. The game thread then reads the chosen scale and applies a change.
- Pick the scale from frame intervals, not single spikes: steps 1, 0.75, 0.5; step down when more than half of a 2 s window misses the display's rate (`interval > 1.5 / Hz`, `Hz = Super.MaxFps > 0 ? Super.MaxFps : Super.GetDisplayRefreshRate(60)`), step up after 10 s of windows with under 10 % misses, and keep the lower step for the session when it has to step down within 10 s of a step up (no pumping). Skip intervals over 0.25 s (pauses, loads, the step's own resize). Give tests a forced scale and log one line per step.
- Example: DrawnUi.Dungeon.Net `src/Dungeon.Shared/UI/RenderScale.cs` (`RenderScale`, `OffscreenLayer`).

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

Set only `Text`, and only when it differs from the current one. Never assign style objects (a new `FillGradient`, shadows) in the same callback: a new object re-measures and redraws the label and its cached layer every time, even with equal contents. Style the label once, or observe the rarely changing property separately (see the `drawnui` skill, Invalidation).

---

## Android-specific

Frame-skip is already the default on Android (`DrawnGame.FrameInterpolatorDisabled` is `true` under `ANDROID`, also `OPENTK` and `BROWSER`). Set it only to opt into interpolation (`false`), or to force frame-skip on iOS/Windows/WPF (`true`).

---

## Common mistakes

- **Creating sprites in GameLoop** → GC spikes, frame drops. Always pool.
- **Forgetting `IgnoreChildrenInvalidations = true`** → massive CPU waste per frame.
- **Iterating `Views` while adding/removing** → crash. Use `_spritesToBeAdded/Removed` queues.
- **Updating score label via binding every hit** → UI thread saturation. Batch once per frame.
- **`UseCache = GPU` on scrolling parallax layer** → cache invalidated every frame = wasted GPU upload.
- **Not waiting for `Superview.HasHandler`** → premature GPU operations crash.
- **Not calling `UpdateState(LastFrameTimeNanos)` before collision checks** → stale hitboxes.
- **Positioning a pooled sprite from its own `Width`/`Height`** → a sprite never laid out yet reports `-1` (`SkiaControl._width = -1` until its first arrange), so its first spawn lands off by half the other sprite's size, later spawns are right ("happens only sometimes"). Measured 2026-11-06: `bullet.Left = Paddle.Left + (Paddle.Width - bullet.Width)/2` sent every fresh bullet ~43 units right of the paddle. When spawner and sprite share the same alignment (both `HorizontalOptions.Center`), `Left`/`Top` are offsets from that anchor: copy the offset (`bullet.Left = Paddle.Left`) and use `WidthRequest`/`HeightRequest` for size math.
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
const float MAX_DEV = MathF.PI * 0.27f; // about ±49° from vertical

// Bottom paddle (player) - ball goes UP (sin < 0):
var hitPos = (ballHit.MidX - paddleHit.Left) / paddleHit.Width; // 0..1
Ball.Angle = BallSprite.ClampAngleFromHorizontal(-MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
if (MathF.Sin(Ball.Angle) > 0) Ball.Angle = -Ball.Angle; // ensure upward

// Top paddle (AI) - ball goes DOWN (sin > 0):
Ball.Angle = BallSprite.ClampAngleFromHorizontal(MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
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
    if (_autoServeTimer <= 0) Serve(triggeredByAi: true); // a tap cannot serve while _aiServes
}
```

### AI while the player holds the serve
In `WaitingToStart` with `!_aiServes`, the AI paddle stays put until the player has moved (`_playerHasMoved`), then wanders at 30% speed (`MovePaddle(AiPaddle, _aiWanderDir * 0.3f, deltaSeconds)`, new random direction every 0.5–1.2 s, bounced off the walls).

### AI reaction time tuning (Medium difficulty)
Reaction time 0.25–0.7s is too slow - AI misses trivial balls right after serve. Medium params in `PongAI`:
```csharp
_reactionTimeMin = 0.06f; _reactionTimeMax = 0.22f;
_accuracy = 0.84f; _mistakeProbability = 0.10f;
_mistakeDurationMin = 0.25f; _mistakeDurationMax = 0.5f;
_decisionChangeInterval = 1.8f; _movementSmoothingTime = 0.10f;
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
| 400+ FPS in Constant mode | Mesa/WSLg ignores VSync swap interval | `DrawnUiWindow` detects it (first 60 frames) and paces frames on the refresh grid itself (`FramePacing`); a custom `GameWindow` must cap frames itself |

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

## References (DrawnUI repo, https://github.com/DrawnUi/DrawnUi.Net)

- `src/SharedGame/DrawnGame.cs` - base class (+ `Game.Input.cs`, `IGame.cs`)
- `src/Blazor/Samples/BlazorSandbox/Games/SpaceShooter/` - full shooter example (pooling, deferred add/remove)
- `src/Blazor/Samples/BlazorSandbox/Games/Parallax/` - parallax/platformer (strip controls, tileset, input)
- `src/Shared/Samples/Pong.Shared/` - Pong: `Game/PongGame.cs`, `Game/PongGame.Loop.cs` (loop + AI integration), `Game/Ai/PongAI.cs`, `Views/RescalingCanvas.cs`; heads listed in the table at the top
- `src/Maui/Samples/GameTemplate/` - MAUI game starter (pooled sprites, `IReusableSprite`, `IWithHitBox`)
- https://github.com/DrawnUi/DrawnUi.Net.Breakout - MAUI+Blazor shared game (Arkanoid pattern: shproj/projitems, IAudioService per head, raycast collision)
