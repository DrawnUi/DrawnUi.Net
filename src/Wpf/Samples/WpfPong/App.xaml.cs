using System.Windows;
using DrawnUi.Draw;
using FontWeight = DrawnUi.Draw.FontWeight;

namespace WpfPong;

/// <summary>
/// Registers the game fonts once, before the first canvas is shown. Same set as the OpenTK Pong.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Super.UseDrawnUi()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("fonts/NotoColorEmoji-Regular.ttf", "FontEmoji", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontGame", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-Regular.ttf", "FontText", FontWeight.Regular);
                fonts.AddFont("fonts/Orbitron-SemiBold.ttf", "FontTextBold", FontWeight.SemiBold);
                fonts.AddFont("fonts/Orbitron-ExtraBold.ttf", "FontTextTitle");
            })
            .Build();
    }
}
