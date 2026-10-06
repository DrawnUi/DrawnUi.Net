using System.Reflection;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using DrawnUi.OpenTk;
using DrawnUi.Views;
using HelloOpenTk.Pages;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;
using Color = DrawnUi.Color;
using Thickness = DrawnUi.Views.Thickness;

namespace HelloOpenTk;

/// <summary>
/// The drawn demo: a <see cref="SkiaShell"/> with the root menu underneath and the sample pages pushed on top,
/// wired as HelloWpf's MainWindow and the React demo's main.tsx do.
/// </summary>
public static class HelloApp
{
    /// <summary>The app window, for pages that follow its state (Pong pauses while minimized).</summary>
    public static HelloWindow Window { get; set; }

    /// <summary>Builds the canvas content.</summary>
    public static SkiaControl BuildContent()
    {
        var shell = new SkiaShell();
        // The Shapes and Shell pages need the shell itself, so the routes are attached after construction.
        shell.Routes = new Dictionary<string, Func<ShellArguments, SkiaControl>>
        {
            ["cells"] = _ => new CellsPage(),
            ["uneven"] = _ => new UnevenCellsPage(),
            ["images"] = _ => new ImagesPage(),
            ["shapes"] = _ => new ShapesPage(shell),
            ["svg"] = _ => new SvgPage(),
            ["text"] = _ => new TextPage(),
            ["layouts"] = _ => new LayoutsPage(),
            ["looks"] = _ => new LooksPage(),
            ["snapping"] = _ => new SnappingPage(),
            ["animations"] = _ => new AnimationsPage(),
            ["shell"] = _ => new ShellPage(shell),
            ["editor"] = _ => new EditorPage(),
            ["keyboard"] = _ => new KeyboardPage(),
            ["scroll"] = _ => new ScrollPage(),
            ["shaders"] = _ => new ShadersPage(),
            ["sprites"] = _ => new SpritesPage(),
            ["transforms"] = _ => new TransformsPage(),
            ["reorder"] = _ => new ReorderPage(),
            ["pong"] = _ => new PongPage(),
            ["a11y"] = _ => new AccessibilityPage(),
        };
        shell.Titles = Catalog.Samples.ToDictionary(s => s.Route, s => s.Title);

        shell.RootContent = new RootPage
        {
            SampleSelected = sample => _ = shell.GoToAsync(sample.Route),
        };

        // dev: HELLOOPENTK_ROOT=<route> hosts that page directly, without the shell
        var rootRoute = Environment.GetEnvironmentVariable("HELLOOPENTK_ROOT");
        var main = !string.IsNullOrEmpty(rootRoute) && shell.Routes.TryGetValue(rootRoute, out var build)
            ? build(new ShellArguments())
            : shell;

        return new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                main,
#if DEBUG
                // debug builds only, like the React demo's dev-server counter
                new SkiaLabelFps
                {
                    Margin = new Thickness(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    BackgroundColor = Color.Parse("#8B0000"),
                    TextColor = Colors.White,
                    ZIndex = 110,
                },
#endif
            },
        }
        // a right click (long press, Menu key) that no control took shows the library versions
        .OnContextMenu((me, e) =>
        {
            shell.ShowToast($"DrawnUi.OpenTk {VersionOf(typeof(DrawnUiWindow))} · SkiaSharp {VersionOf(typeof(SKCanvas))}", 3000);
            return true;
        });
    }

    private static string VersionOf(Type type)
    {
        var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? type.Assembly.GetName().Version?.ToString() ?? "?";
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }

    /// <summary>The DrawnUI icon for the title bar: decoded from the embedded icon.ico, 32x32, RGBA.</summary>
    public static OpenTK.Windowing.Common.Input.WindowIcon LoadWindowIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
            if (name == null)
                return null;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null)
                return null;

            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
                return null;

            var resized = bitmap.Width != 32 || bitmap.Height != 32
                ? bitmap.Resize(new SKImageInfo(32, 32), SkiaSamplingOptions.GetSamplingOptions(FilterQuality.Ultra, true))
                : bitmap;

            var pixels = resized.Bytes;
            // SKBitmap is BGRA, OpenTK wants RGBA
            for (var i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

            var image = new OpenTK.Windowing.Common.Input.Image(32, 32, pixels);
            if (!ReferenceEquals(resized, bitmap))
                resized.Dispose();
            return new OpenTK.Windowing.Common.Input.WindowIcon(image);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// The app window: DrawnUiWindow plus the window-level keyboard the Keyboard, Sprites and Pong pages listen to
/// (KeyboardManager KeyDown / KeyUp / KeyChar), as UseDesktopKeyboard does on the WPF and MAUI heads.
/// </summary>
public class HelloWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings, Canvas canvas)
    : DrawnUiWindow(gameSettings, nativeSettings, canvas)
{
    /// <inheritdoc/>
    [SupportedOSPlatform("windows")]
    protected override void ConfigureWindowChrome(IntPtr hwnd)
    {
        WindowChrome.SetCaptionColor(hwnd, 0x21, 0x25, 0x29);
        WindowChrome.SetBorderColor(hwnd, 0x21, 0x25, 0x29);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.IsRepeat && OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardPressed(key);
    }

    /// <inheritdoc/>
    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (OpenTkKeyMapper.Map(e.Key) is { } key)
            KeyboardManager.KeyboardReleased(key);
    }

    /// <inheritdoc/>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        var text = e.AsString;
        if (!string.IsNullOrEmpty(text) && !(text.Length == 1 && char.IsControl(text[0])))
            KeyboardManager.KeyboardChar(text);
    }
}
