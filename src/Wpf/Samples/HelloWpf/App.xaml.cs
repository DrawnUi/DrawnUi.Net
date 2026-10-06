using System.Globalization;
using System.Windows;
using DrawnUi.Draw;
using DrawnUi.Wpf;
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

        // Numbers in captions and status lines read the same on every machine and as in the React demo
        // ("0.005", never "0,005" on a comma-decimal Windows locale).
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Super.UseDrawnUi()
            // Same set as the React demo's main.tsx. This head has no AddSymbols()/AddEmojis(), so the
            // Noto subsets it ships are registered by hand under the aliases FontFamilyFallback expects.
            .ConfigureFonts(fonts => fonts
                .AddFont("fonts/OpenSans-Regular.ttf", "FontText")
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontText", DrawnUi.Draw.FontWeight.SemiBold) // FontAttributes=Bold / FontWeight=600 pick this face
                .AddFont("fonts/OpenSans-Semibold.ttf", "FontTextBold")
                .AddFont("fonts/Orbitron-Regular.ttf", "FontGame") // Pong score and messages, as in the .NET Pong samples
                .AddFont("fonts/NotoSansMathSymbols-Subset.ttf", "FontSymbols")
                .AddFont("fonts/NotoSansSymbols2-Subset.ttf", "FontSymbols2")
                // The web heads' Noto emoji subset flattened to COLRv0 (dev/fonts/colrv0_emoji.py): SkiaSharp
                // on Windows draws text through DirectWrite, which renders COLRv0 but draws the COLRv1
                // original as nothing (measured 2026-10-02, SkiaSharp 4.148).
                .AddFont("fonts/NotoColorEmoji-Subset-COLRv0.ttf", "FontEmoji"))
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
            // Same settings class as a MAUI app: keys pressed anywhere in the window reach KeyboardManager.
            .WithSettings(new DrawnUiStartupSettings { UseDesktopKeyboard = true })
            .Build();

        // Every label is read as text, every button is a button, as in the React demo.
        SkiaLabel.DefaultAccessibilityRole = DrawnUi.Models.Aria.RoleText;
        SkiaButton.DefaultAccessibilityRole = DrawnUi.Models.Aria.RoleButton;
    }
}
