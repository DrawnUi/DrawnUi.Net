namespace EmptyCode;

/// <summary>
/// Starts DrawnUI, then opens the window. The window is built in code (MainWindow.cs),
/// so the whole UI stays in one C# method you can paste into DrawnUI Fiddle and back.
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        Super.UseDrawnUi()
            .ConfigureFonts(fonts =>
            {
                // Same aliases the DrawnUI Fiddle registers, so exported snippets
                // find the fonts they were written against.
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
            })
            .Build();

        new MainWindow().Show();
    }
}
