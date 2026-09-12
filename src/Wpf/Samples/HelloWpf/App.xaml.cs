using System.Windows;
using DrawnUi.Draw;
using Style = DrawnUi.Draw.Style;
using Setter = DrawnUi.Draw.Setter;

namespace HelloWpf;

/// <summary>
/// Startup, same shape as the React demo's main.tsx and as DrawnUi.Net / OpenTK:
/// Super.UseDrawnUi().ConfigureFonts(...).ConfigureStyles(...).Build().
/// </summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Super.UseDrawnUi()
            .ConfigureFonts(fonts => fonts
                .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextBold"))
            // Without this every control that leaves FontFamily empty draws in Skia's built-in face,
            // on this head as on the others. A SkiaLabel style does not reach button captions,
            // because SkiaButton pushes its own FontFamily onto its label — so style buttons too.
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
    }
}
