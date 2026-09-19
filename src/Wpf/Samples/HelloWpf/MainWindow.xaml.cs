using System.Windows;
using DrawnUi.Draw;
using HelloWpf.Pages;
using Color = DrawnUi.Color;

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
                ["shapes"] = _ => new ShapesPage(),
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
                ["a11y"] = _ => new AccessibilityPage(),
            };
        shell.Titles = Catalog.Samples.ToDictionary(s => s.Route, s => s.Title);

        shell.RootContent = new RootPage
        {
            SampleSelected = sample => _ = shell.GoToAsync(sample.Route),
        };

        // dev: HELLOWPF_ROOT=<route> hosts that page directly, without the shell
        var rootRoute = Environment.GetEnvironmentVariable("HELLOWPF_ROOT");
        Drawn.Content = !string.IsNullOrEmpty(rootRoute) && shell.Routes.TryGetValue(rootRoute, out var build)
            ? build(new ShellArguments())
            : shell;

        DevSnapshot.NavigateIfRequested(shell);
        DevSnapshot.ArmIfRequested(Drawn);
    }
}
