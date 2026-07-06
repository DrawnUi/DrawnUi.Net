using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LoadMoreRepro;
using DrawnUi.Draw;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await Super.UseDrawnUi(builder)
	.WithBaseUrl(builder.HostEnvironment.BaseAddress)
	.WithOptions(o => o.UseDesktopKeyboard = true)
	.BuildAndRunAsync();
