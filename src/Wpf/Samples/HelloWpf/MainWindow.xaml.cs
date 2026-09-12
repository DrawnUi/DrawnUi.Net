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

        Drawn.Canvas.BackgroundColor = Color.Parse("#212529");

        var shell = new SkiaShell
        {
            // Ported pages register here as each module lands.
            Routes = new Dictionary<string, Func<ShellArguments, SkiaControl>>
            {
                ["cells"] = _ => new CellsPage(),
                ["uneven"] = _ => new UnevenCellsPage(),
                ["images"] = _ => new ImagesPage(),
                ["shapes"] = _ => new ShapesPage(),
                ["svg"] = _ => new SvgPage(),
            },
            Titles = Catalog.Samples.ToDictionary(s => s.Route, s => s.Title),
        };

        shell.RootContent = new RootPage
        {
            SampleSelected = sample => _ = shell.GoToAsync(sample.Route),
        };

        Drawn.Content = shell;

        DevSnapshot.NavigateIfRequested(shell);
        DevSnapshot.ArmIfRequested(Drawn);
    }
}
