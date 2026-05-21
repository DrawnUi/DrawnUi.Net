using BlazorSandbox;
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var assetBaseUri = new Uri(builder.HostEnvironment.BaseAddress);

DrawnExtensions.RegisterFont("FontEmoji", BuildAssetUrl(assetBaseUri, "fonts/NotoColorEmoji-Regular.ttf"));

DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, BuildAssetUrl(assetBaseUri, "fonts/OpenSans-Regular.ttf"));
DrawnExtensions.RegisterFont("FontTextBol", BuildAssetUrl(assetBaseUri, "fonts/OpenSans-Semibold.ttf"));
DrawnExtensions.RegisterFont("FontTextTitle", BuildAssetUrl(assetBaseUri, "fonts/OpenSans-Semibold.ttf"));

DrawnExtensions.RegisterFont("FontBrand", BuildAssetUrl(assetBaseUri, "fonts/DOM.TTF"));
DrawnExtensions.RegisterFont("FontBrandBold", BuildAssetUrl(assetBaseUri, "fonts/DOMB.TTF"));

DrawnExtensions.RegisterFont("FontGame", FontWeight.Regular, BuildAssetUrl(assetBaseUri, "fonts/Orbitron-Regular.ttf"));
DrawnExtensions.RegisterFont("FontGame", FontWeight.Medium, BuildAssetUrl(assetBaseUri, "fonts/Orbitron-Medium.ttf"));
DrawnExtensions.RegisterFont("FontGame", FontWeight.SemiBold, BuildAssetUrl(assetBaseUri, "fonts/Orbitron-SemiBold.ttf"));
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, BuildAssetUrl(assetBaseUri, "fonts/Orbitron-Bold.ttf"));
DrawnExtensions.RegisterFont("FontGame", FontWeight.ExtraBold, BuildAssetUrl(assetBaseUri, "fonts/Orbitron-ExtraBold.ttf"));

DrawnExtensions.RegisterFont("FontGameMedium", BuildAssetUrl(assetBaseUri, "fonts/Orbitron-Medium.ttf"));
DrawnExtensions.RegisterFont("FontGameSemiBold", BuildAssetUrl(assetBaseUri, "fonts/Orbitron-SemiBold.ttf"));
DrawnExtensions.RegisterFont("FontGameBold", BuildAssetUrl(assetBaseUri, "fonts/Orbitron-Bold.ttf"));
DrawnExtensions.RegisterFont("FontGameExtraBold", BuildAssetUrl(assetBaseUri, "fonts/Orbitron-ExtraBold.ttf"));

DrawnExtensions.RegisterImage("favicon.png", BuildAssetUrl(assetBaseUri, "favicon.png"));
DrawnExtensions.RegisterImage("icon-192.png", BuildAssetUrl(assetBaseUri, "icon-192.png"));
DrawnExtensions.RegisterImage("dotnetbotcar.png", BuildAssetUrl(assetBaseUri, "images/dotnetbotcar.png"));
DrawnExtensions.RegisterImage(@"Images\banana.gif", BuildAssetUrl(assetBaseUri, "media/banana.gif"));
DrawnExtensions.RegisterSvg(BuildAssetUrl(assetBaseUri, "media/dotnet_bot.svg"));

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
	UseDesktopKeyboard = true
});

await host.RunAsync();

static string BuildAssetUrl(Uri baseUri, string relativePath)
{
    return new Uri(baseUri, relativePath).ToString();
}
