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
| **Web (Blazor / DrawnUi.Wasm)** | `wwwroot/**` (served as a static web asset) |

A source path like `"Images/banana.gif"` should resolve to the same relative location under each head's
asset root, so the shared code stays head-agnostic.

## See also

- [DrawnUI for OpenTK](index.md) — initialization, fonts
- [OpenTK Samples](samples.md) — `OpenTkPong` uses exactly this `Content Include` pattern for `Images/**` and `fonts/**`
- [Platforms and Packages](../platforms.md)
