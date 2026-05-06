# DrawnUI for Blazor

DrawnUI for Blazor brings the DrawnUI rendering and control model to Blazor WebAssembly using SkiaSharp.

This target is under active development. The current validated slice covers startup, `Canvas` hosting, font registration, keyboard support, and a growing set of shared controls and probes.

## Package

Install the package:

```bash
dotnet add package DrawnUi.Blazor
```

The current project target is `net10.0`.

## Quick Start

Initialize DrawnUI in `Program.cs`:

```csharp
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
    UseDesktopKeyboard = true
});

await host.RunAsync();
```

Use DrawnUI in a Razor page:

```razor
@page "/"
@using DrawnUi.Draw
@using DrawnUi.Views
@using Microsoft.Maui.Controls

<Canvas WidthRequest="400"
        HeightRequest="220"
        BackgroundColor="#F4F1E8"
        RootControl="@RootControl" />

@code {
    private readonly SkiaControl RootControl = new SkiaLayout()
    {
        Margin = new Thickness(16),
        Type = LayoutType.Column,
        Children =
        {
            new SkiaLabel()
            {
                Text = "Hello from DrawnUI Blazor",
                FontFamily = "FontText",
                FontSize = 24
            }
        }
    };
}
```

## Fonts

In Blazor, fonts should be registered with URL paths that are reachable as static web assets:

```csharp
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");
```

## Trimming

Blazor WebAssembly apps using DrawnUI may also need to root `SkiaSharp` for trimming:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

This is currently used by the Blazor sandbox sample.

## Sandbox Reference

The best working reference in this repository is:

- `src/Blazor/Samples/BlazorSandbox/`

That sample currently validates the main startup flow plus cards, scrolling, lottie, and keyboard probes.

## Status

Blazor support is in progress, not yet full MAUI parity.

For current progress and validated slices, see:

- `docs/articles/blazor.md`
- `docs/shared-maui-blazor-port-status.md`