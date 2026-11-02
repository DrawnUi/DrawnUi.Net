using System.Globalization;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Slint;
using HelloOpenTk.Pages;
using SlintGallery;
using Color = DrawnUi.Color;
using Setter = DrawnUi.Draw.Setter;
using Style = DrawnUi.Draw.Style;

// Startup as in HelloOpenTk, whose pages this is.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Super.UseDrawnUi()
    .ConfigureFonts(fonts => fonts
        .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontText", FontWeight.SemiBold)
        .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextBold")
        .AddFont("fonts/Orbitron-Regular.ttf", "FontGame")
        .AddFont("fonts/NotoSansMathSymbols-Subset.ttf", "FontSymbols")
        .AddFont("fonts/NotoSansSymbols2-Subset.ttf", "FontSymbols2")
        .AddFont("fonts/NotoColorEmoji-Subset-COLRv0.ttf", "FontEmoji"))
    .ConfigureStyles(styles => styles
        .AddStyle(new Style
        {
            TargetType = typeof(SkiaLabel),
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
        })
        .AddStyle(new Style
        {
            TargetType = typeof(SkiaButton),
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = SkiaButton.FontFamilyProperty, Value = "FontText" } },
        }))
    .Build();
SkiaLabel.DefaultAccessibilityRole = Aria.RoleText;
SkiaButton.DefaultAccessibilityRole = Aria.RoleButton;

// DRAWNUI_GPU=1: Slint's FemtoVG renderer on OpenGL, whose context DrawnUI draws on.
if (Environment.GetEnvironmentVariable("DRAWNUI_GPU") == "1")
{
    new Slint.BackendSelector().RendererName("femtovg").RequireOpenGL().Select();
    SlintCanvasHost.UseGpu = true;
}

var shell = new SkiaShell();
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
    ["a11y"] = _ => new AccessibilityPage(),
};
var samples = Catalog.Samples.Where(s => shell.Routes.ContainsKey(s.Route)).ToList();
shell.Titles = samples.ToDictionary(s => s.Route, s => s.Title);

using var window = MainWindow.Create();
using var host = SlintCanvasHost.Attach(window.Instance, 0);
host.Canvas.BackgroundColor = Color.Parse("#212529");
host.Canvas.Content = shell;
window.Samples = new Slint.ListModel<Sample>(samples.Select(s => new Sample { Title = s.Title, Text = s.Text }));

// The sidebar and the drawn root menu both open a page on top of the menu.
async void Show(int index)
{
    window.Current = index;
    await shell.PopToRootAsync();
    await shell.GoToAsync(samples[index].Route);
}
window.OnSelect(Show);
shell.RootContent = new RootPage { SampleSelected = sample => Show(samples.FindIndex(s => s.Route == sample.Route)) };

if (Environment.GetEnvironmentVariable("DRAWNUI_SELFTEST") is { Length: > 0 } outDir)
    SelfTest.Run(window, host, samples.Select(s => s.Route).ToList(), outDir);

window.Run();
