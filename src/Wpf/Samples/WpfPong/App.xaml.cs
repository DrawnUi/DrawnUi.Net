using System.Windows;
using DrawnUi.Draw;
using DrawnUi.Wpf;
using Pong.Game;
using FontWeight = DrawnUi.Draw.FontWeight;

namespace WpfPong;

/// <summary>
/// Registers the game fonts once, before the first canvas is shown (same set as the OpenTK Pong), and
/// the startup settings: window size from the game's logical size, keyboard for the whole window.
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
            .WithSettings(new DrawnUiStartupSettings
            {
                // Wider than the field on purpose: the arcade background shows in the side bands and
                // hints that the window can be resized, the field keeps its aspect ratio (RescalingCanvas).
                DesktopWindow = new WindowParameters
                {
                    Width = (int)(PongGame.HEIGHT * 1.22),
                    Height = (int)(PongGame.HEIGHT * 1.33),
                },
                UseDesktopKeyboard = true,
            })
            .Build();
    }
}
