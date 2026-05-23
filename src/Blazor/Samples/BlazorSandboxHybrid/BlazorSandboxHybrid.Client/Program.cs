using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

await Super.UseDrawnUi(builder)
    .WithOptions(o => o.UseDesktopKeyboard = true)
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("/fonts/OpenSans-Regular.ttf",  "FontText",      FontWeight.Regular);
        fonts.AddFont("/fonts/OpenSans-Semibold.ttf", "FontTextTitle");
    })
    .BuildAndRunAsync();
