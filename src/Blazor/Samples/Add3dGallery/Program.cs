using Add3dGallery;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

await Super.UseDrawnUi(builder)
    .WithBaseUrl(builder.HostEnvironment.BaseAddress)
    .WithOptions(o => o.UseDesktopKeyboard = true)
    // Labels without a FontFamily draw in FontText.
    .ConfigureStyles(styles => styles.AddStyle(new Style
    {
        ApplyToDerivedTypes = true,
        TargetType = typeof(SkiaLabel),
        Setters = { new Setter { Property = SkiaLabel.FontFamilyProperty, Value = "FontText" } },
    }))
    .ConfigureFonts(fonts =>
    {
        fonts.AddSymbols();
        fonts.AddFont("OpenSans-Regular.ttf", "FontText");
        fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
    })
    .BuildAndRunAsync();
