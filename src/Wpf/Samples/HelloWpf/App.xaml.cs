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
            // Same set as the React demo's main.tsx. This head has no AddSymbols()/AddEmojis(), so the
            // Noto subsets it ships are registered by hand under the aliases FontFamilyFallback expects.
            .ConfigureFonts(fonts => fonts
                .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontText", DrawnUi.Draw.FontWeight.SemiBold) // FontAttributes=Bold / FontWeight=600 pick this face
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextBold")
                .AddFont("fonts/NotoSansMathSymbols-Subset.ttf", "FontSymbols")
                .AddFont("fonts/NotoSansSymbols2-Subset.ttf", "FontSymbols2"))
            // No emoji font: the React demo's Noto Color Emoji subset is a COLR/SVG colour font that
            // SkiaSharp on Windows draws as nothing. AutoFont picks the system Segoe UI Emoji instead.
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
