# OpenTK Input and Window Features

---

## Keys Input

For controls driven by `KeyboardManager` (e.g., `DrawnGame` movement), route key events through `OpenTkKeyMapper`:

```csharp
protected override void OnKeyDown(KeyboardKeyEventArgs e)
{
    base.OnKeyDown(e);
    if (OpenTkKeyMapper.Map(e.Key) is { } key)
        KeyboardManager.KeyboardPressed(key);
}

protected override void OnKeyUp(KeyboardKeyEventArgs e)
{
    base.OnKeyUp(e);
    if (OpenTkKeyMapper.Map(e.Key) is { } key)
        KeyboardManager.KeyboardReleased(key);
}
```

`DrawnUiWindow` handles editor keys (backspace, arrows, home/end, ctrl+A, tab) automatically. Override `OnKeyDown` and call `base.OnKeyDown(e)` first, then add your key routing.

---

## Fullscreen

`DrawnUiWindow` supports fullscreen out of the box:

- **F11** — toggles fullscreen/windowed
- **ESC** — exits fullscreen (returns to windowed)
- **System menu** (right-click title bar or Alt+Space) — includes a "Fullscreen" item on Windows

To toggle programmatically:

```csharp
window.ToggleFullscreen();
```

---

## Window Centering

`DrawnUiWindow` centers on the primary monitor at startup with no visible flicker. The window starts hidden, positions itself, then becomes visible.

---

## DWM Title Bar Styling (Windows)

Override `ConfigureWindowChrome` in your `DrawnUiWindow` subclass to apply custom DWM colors:

```csharp
class MyWindow(GameWindowSettings gs, NativeWindowSettings ns, Canvas canvas)
    : DrawnUiWindow(gs, ns, canvas)
{
    [SupportedOSPlatform("windows")]
    protected override void ConfigureWindowChrome(IntPtr hwnd)
    {
        // Match your app's background color (0x1A, 0x1A, 0x2E = #1A1A2E)
        WindowChrome.SetCaptionColor(hwnd, 0x1A, 0x1A, 0x2E);
        WindowChrome.SetBorderColor(hwnd, 0x1A, 0x1A, 0x2E);
    }
}
```

`WindowChrome` helpers:

| Method | Effect | Min Windows |
|---|---|---|
| `SetCaptionColor(hwnd, r, g, b)` | Title bar background color | Win11 22000 |
| `SetBorderColor(hwnd, r, g, b)` | Window border color | Win11 22000 |
| `SetDarkMode(hwnd, bool)` | Force dark/light title text | Win10 20H1 |
| `SetRoundedCorners(hwnd, bool)` | Rounded/square corners | Win11 22000 |

> When a custom caption color is set, Windows automatically picks black or white title text based on luminance. You do not need `SetDarkMode`.

`ConfigureWindowChrome` is only called on Windows — no `OperatingSystem.IsWindows()` guard is needed inside the override.

---

## Publish — Self-Contained Single File

For a distributable release build targeting Windows x64:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

`TrimMode=full` will cut SkiaSharp font and image APIs unless you protect them:

```xml
<ItemGroup Condition="'$(PublishTrimmed)' == 'true'">
  <TrimmerRootAssembly Include="OpenTK.Graphics" />
  <TrimmerRootAssembly Include="OpenTK.Windowing.Desktop" />
  <TrimmerRootAssembly Include="SkiaSharp" />
  <TrimmerRootAssembly Include="HarfBuzzSharp" />
</ItemGroup>
```

To strip PDB files from the publish output:

```xml
<Target Name="ExcludePdbsFromPublish" AfterTargets="ComputeFilesToPublish">
  <ItemGroup>
    <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)"
                           Condition="'%(Extension)' == '.pdb'" />
  </ItemGroup>
</Target>
```

Suppress the console window on Windows:

```xml
<OutputType>WinExe</OutputType>
```

---

## Window Icon

Set the file system icon (Explorer, taskbar) via `ApplicationIcon` and embed it as a resource for the title bar:

```xml
<PropertyGroup>
  <ApplicationIcon>icon.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <EmbeddedResource Include="icon.ico" />
</ItemGroup>
```

Load the embedded ICO at runtime and pass it to `NativeWindowSettings.Icon`:

```csharp
OpenTK.Windowing.Common.Input.WindowIcon? LoadWindowIcon()
{
    try
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
        if (name == null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return null;

        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap == null) return null;

        var resized = bitmap.Width != 32 || bitmap.Height != 32
            ? bitmap.Resize(new SKImageInfo(32, 32), SKFilterQuality.High)
            : bitmap;

        var pixels = resized.Bytes;
        // SKBitmap is BGRA; OpenTK wants RGBA
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

        var image = new OpenTK.Windowing.Common.Input.Image(32, 32, pixels);
        if (!ReferenceEquals(resized, bitmap)) resized.Dispose();
        return new OpenTK.Windowing.Common.Input.WindowIcon(image);
    }
    catch { return null; }
}
```

Then:

```csharp
var nativeSettings = new NativeWindowSettings
{
    Icon = LoadWindowIcon(),
    // ...
};
```

---

## Game Timing

Frame time starts from `OnLoad()`, not system boot. `DrawnGame.LastFrameTimeNanos` begins near `0` and the first delta is a few milliseconds — matching MAUI and Blazor behavior.

---

## Related

- [OpenTK Guide](index.md)
- [Window Patterns](window.md)
- [OpenTK FAQ](faq.md)
