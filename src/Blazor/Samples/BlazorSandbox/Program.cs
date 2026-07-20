using BlazorSandbox;
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await Super.UseDrawnUi(builder)
    .WithBaseUrl(builder.HostEnvironment.BaseAddress)
    .WithOptions(o => o.UseDesktopKeyboard = true)
    .PreloadAssets(assets =>
    {
        assets.AddImage("favicon.png",        "favicon.png");
        assets.AddImage("icon-192.png",       "icon-192.png");
        assets.AddImage("dotnetbotcar.png",   "images/dotnetbotcar.png");
        assets.AddImage(@"Images\banana.gif", "media/banana.gif");
        assets.AddSvg("media/dotnet_bot.svg");
    })
    .ConfigureStyles(styles =>
    {

        styles.AddStyle(new Style()
        {
            ApplyToDerivedTypes = true,
            TargetType = typeof(SkiaLabel),
            Setters =
            {
                new Setter()
                {
                    Property = SkiaLabel.FontFamilyProperty,
                    Value = "FontText"
                }
            }
        });
    })
    .ConfigureFonts(fonts =>
    {
        // Lib-shipped subsets: emoji (~900 KB, alias FontEmoji) + text symbols
        // (~285 KB, arrows/math/geometric) instead of the 23.7 MB full emoji font
        fonts.AddEmojis();
        fonts.AddSymbols();
        fonts.AddFont("fonts/OpenSans-Regular.ttf",   "FontText",      FontWeight.Regular);
        fonts.AddFont("fonts/OpenSans-Semibold.ttf",  "FontTextBol");
        fonts.AddFont("fonts/OpenSans-Semibold.ttf",  "FontTextTitle");
        fonts.AddFont("fonts/DOM.TTF",                "FontBrand");
        fonts.AddFont("fonts/DOMB.TTF",               "FontBrandBold");
        fonts.AddFont("fonts/Orbitron-Regular.ttf",   "FontGame",      FontWeight.Regular);
        fonts.AddFont("fonts/Orbitron-Medium.ttf",    "FontGame",      FontWeight.Medium);
        fonts.AddFont("fonts/Orbitron-SemiBold.ttf",  "FontGame",      FontWeight.SemiBold);
        fonts.AddFont("fonts/Orbitron-Bold.ttf",      "FontGame",      FontWeight.Bold);
        fonts.AddFont("fonts/Orbitron-ExtraBold.ttf", "FontGame",      FontWeight.ExtraBold);
        fonts.AddFont("fonts/Orbitron-Medium.ttf",    "FontGameMedium");
        fonts.AddFont("fonts/Orbitron-SemiBold.ttf",  "FontGameSemiBold");
        fonts.AddFont("fonts/Orbitron-Bold.ttf",      "FontGameBold");
        fonts.AddFont("fonts/Orbitron-ExtraBold.ttf", "FontGameExtraBold");
    })
    .BuildAndRunAsync();
