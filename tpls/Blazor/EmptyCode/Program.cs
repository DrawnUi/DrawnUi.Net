using EmptyCode;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

await Super.UseDrawnUi(builder)
    .WithBaseUrl(builder.HostEnvironment.BaseAddress)
    .WithOptions(o => o.UseDesktopKeyboard = true)
    // Same default as the DrawnUI Fiddle: labels without a FontFamily draw in FontText.
    .ConfigureStyles(styles => styles.AddStyle(new Style
    {
        ApplyToDerivedTypes = true,
        TargetType = typeof(SkiaLabel),
        Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
    }))
    .ConfigureFonts(fonts =>
    {
        // Emoji and symbol subsets shipped with DrawnUI: a browser has no system fonts to fall back on.
        fonts.AddEmojis();
        fonts.AddSymbols();
        // Same aliases the DrawnUI Fiddle registers, so exported snippets
        // find the fonts they were written against.
        fonts.AddFont("OpenSans-Regular.ttf", "FontText");
        fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
    })
    .BuildAndRunAsync();
