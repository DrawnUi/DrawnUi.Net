# OpenTK Resources (Images, Lottie, Fonts)

On the OpenTK head there is **no MAUI asset pipeline**. DrawnUI resolves a source string such as
`"Images/banana.gif"` as a path **relative to the application's output directory** (next to the
executable, e.g. `bin/Debug/net10.0/`). So every raw asset must be **copied to the output directory**.

## The rule

Add the asset to your `.csproj` as `Content` **with `CopyToOutputDirectory`**:

```xml
<ItemGroup>
  <Content Include="Images\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="Lottie\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="fonts\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Then reference it by its relative path, mirroring the folder layout:

```csharp
new SkiaImage("Images/banana.gif");
new SkiaGif    { Source = "Images/banana.gif" };
new SkiaLottie { Source = "Lottie/iosloader.json" };
```

Fonts are registered at startup and must likewise sit next to the executable:

```csharp
Super.UseDrawnUi()
    .ConfigureFonts(fonts => fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontGame"))
    .Build();
```

## Common gotcha — plain `<Content>` does NOT copy

> [!WARNING]
> In a `WinExe`/console SDK project, a bare `<Content Include="Images\banana.gif" />` is **not** copied
> to the output directory by default. The build succeeds, the file shows in the IDE, but at runtime the
> loader can't find it and the asset silently fails to render. **You must add `CopyToOutputDirectory`**
> (`PreserveNewest` or `Always`). This is the #1 reason an OpenTK asset "doesn't load".

Verify the copy landed:

```
ls bin/Debug/net10.0/Images        # banana.gif must be here
```

## Per-head differences

The same shared DrawnUI code runs on multiple heads, each with its own asset pipeline — keep the asset
in **all** the heads you ship:

| Head | Where raw assets live |
|---|---|
| **OpenTK** | `Content Include … CopyToOutputDirectory` → copied next to the exe |
| **.NET MAUI** | `Resources/Raw/**` (MauiAsset; bundled automatically) |
| **Web (Blazor / DrawnUi.Web)** | `wwwroot/**` (served as a static web asset) |

A source path like `"Images/banana.gif"` should resolve to the same relative location under each head's
asset root, so the shared code stays head-agnostic.

## `.sksl` shader files

Copying them out is the same `Content Include` rule (HelloOpenTk does it for `shaders\**`), but **loading them by
path does not work on this head**. `ShaderSource` / `TransitionShader` go through `SkSl.LoadFromResources`, which on
every non-MAUI .NET head (`DRAWNUI_NET`) refuses to read a file synchronously and throws unless the code is already in
`SkSl.LoadedCache`; the async path hands the relative path to a bare `HttpClient` and fails with *"An invalid request
URI was provided"*. WPF fills that cache at startup (`ShaderFiles.PreloadAll()`, every `.sksl` under the exe folder);
OpenTK has no equivalent, so nothing fills it.

Until the head gets one, give the shader its code instead of a path:

```csharp
// works on OpenTK today
new SkiaShaderEffect { ShaderCode = File.ReadAllText("shaders/blit.sksl") }

// SkiaShaderCarousel
carousel.TransitionShaderCode = File.ReadAllText("shaders/transitions/cube.sksl");
```

Or fill the loader cache yourself once at startup, which makes `ShaderSource` work as it does on WPF:

```csharp
foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.sksl", SearchOption.AllDirectories))
    SkSl.LoadedCache[Path.GetRelativePath(AppContext.BaseDirectory, file).Replace('\\', '/')] = File.ReadAllText(file);
```

## See also

- [DrawnUI for OpenTK](index.md) — initialization, fonts
- [OpenTK Samples](samples.md) — `OpenTkPong` uses exactly this `Content Include` pattern for `Images/**` and `fonts/**`
- [Platforms and Packages](../platforms.md)
