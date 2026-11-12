using System.Globalization;
using System.Windows;
using DrawnUi.Wpf;
using Style = DrawnUi.Draw.Style;
using Setter = DrawnUi.Draw.Setter;

namespace Add3dGallery;

/// <summary>Startup: the font aliases the shared pages ask for, keys from the whole window.</summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prices and numbers read the same on every machine ("$1,190", "0.5").
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Super.UseDrawnUi()
            .ConfigureFonts(fonts => fonts
                .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextTitle"))
            // Labels without a FontFamily draw in FontText.
            .ConfigureStyles(styles => styles.AddStyle(new Style
            {
                ApplyToDerivedTypes = true,
                TargetType = typeof(SkiaLabel),
                Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
            }))
            // keys pressed anywhere in the window reach KeyboardManager (the boss fight, the form)
            .WithSettings(new DrawnUiStartupSettings { UseDesktopKeyboard = true })
            .Build();
    }
}
