using System.Reflection;
using System.Windows;
using DrawnUi.Draw;
using DrawnUi.Wpf;
using HelloWpf.Pages;
using SkiaSharp;
using DrawnUi.Views;
using Color = DrawnUi.Color;
using Thickness = DrawnUi.Views.Thickness;

namespace HelloWpf;

/// <summary>
/// Hosts the drawn demo: a <see cref="SkiaShell"/> with the root menu underneath and the sample
/// pages pushed on top, exactly as the React demo wires its own shell in main.tsx.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and mounts the shell.</summary>
    public MainWindow()
    {
        InitializeComponent();
        if (Environment.GetEnvironmentVariable("HELLOWPF_RETAINED") == "1")
            Drawn.RenderingMode = DrawnUi.Draw.RenderingModeType.AcceleratedRetained; // dev: retained GPU surface
        if (Environment.GetEnvironmentVariable("HELLOWPF_SOFTWARE") == "1")
            Drawn.RenderingMode = DrawnUi.Draw.RenderingModeType.Default; // dev: compare GPU vs software rendering

        Drawn.Canvas.BackgroundColor = Color.Parse("#212529");

        var shell = new SkiaShell();
        // Ported pages register here as each module lands. The shell page needs the shell itself, so
        // the routes are attached after construction.
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

        // dev: HELLOWPF_ROOT=<route> hosts that page directly, without the shell
        var rootRoute = Environment.GetEnvironmentVariable("HELLOWPF_ROOT");
        var main = !string.IsNullOrEmpty(rootRoute) && shell.Routes.TryGetValue(rootRoute, out var build)
            ? build(new ShellArguments())
            : shell;

        Drawn.Content = new SkiaLayer
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
            shell.ShowToast($"DrawnUi.Wpf {VersionOf(typeof(DrawnUiElement))} · SkiaSharp {VersionOf(typeof(SKCanvas))}", 3000);
            return true;
        });

        // The Images page photo (also the Shell backdrop and the Scroll header), warmed once the first
        // screen is up so the first visit to Images shows it at once instead of black tiles.
        ContentRendered += (_, _) => _ = Task.Run(() => SkiaImageManager.Instance.PreloadImages(new List<string> { "images/baboon.jpg" }));

        DevSnapshot.NavigateIfRequested(shell);
        DevSnapshot.ArmIfRequested(Drawn);
    }

    private static string VersionOf(Type type)
    {
        var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? type.Assembly.GetName().Version?.ToString() ?? "?";
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }
}
