# Getting Started with DrawnUI

This guide will help you get started with DrawnUI in your .NET MAUI application.

> This page is the .NET MAUI-specific setup path. If you are deciding between MAUI, Blazor, or `DrawnUi.Net`, start with [Platforms and Packages](../platforms.md).

See also [MAUI Tutorials](tutorials.md).

## Installation

### Prerequisites

Target .NET 9.

To make everything compile from first attempt You might also need at least the following MAUI setup inside your csproj:

```
	<PropertyGroup>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">15.0</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">15.2</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">21.0</SupportedOSPlatformVersion>
        <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.19041.0</SupportedOSPlatformVersion>
        <TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.19041.0</TargetPlatformMinVersion>
	</PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Maui.Controls" Version="9.0.70" />
        <PackageReference Include="Microsoft.Maui.Controls.Compatibility" Version="9.0.70" />
    </ItemGroup>

```

### Add the NuGet Package

Install the **DrawnUi.Maui** NuGet package in your .NET MAUI project:

```bash
dotnet add package DrawnUi.Maui
```

> **Important**: Please install stable versions only.

Alternatively, you can fork the DrawnUi repo and _reference the main project directly_. In that case if you are on Windows x64 please disable MSIX packaging, but no need to to that when using nuget package.

### Additional Packages

There are additional packages supporting optional features:
- **DrawnUi.Maui.Camera** - Camera implementations for all platforms
- **DrawnUi.Maui.Game** - Gaming helpers and frame time interpolators
- **DrawnUi.Maui.MapsUi** - Integration with MapsUi
- **DrawnUi.MauiGraphics** - Integration with Maui.Graphics

These must be referenced separately if needed.

### 2. Initialize in Your MAUI App

Update your `MauiProgram.cs` file to initialize DrawnUI. When working on desktop you'll normally want to set your app window to a phone-like size, to be consistent with mobile platforms:

```csharp
using DrawnUi.Draw;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()

            .UseDrawnUi(new DrawnUiStartupSettings
            {
                DesktopWindow = new()
                {
                    Width = 375,
                    Height = 800
                }
            })

            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
```

See the full list of options in [Startup Settings](../startup-settings.md).

### Add Namespace to XAML

Add the DrawnUi namespace to your XAML files:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
             x:Class="YourNamespace.YourPage">
    <!-- Page content -->
</ContentPage>
```

### Using DrawnUi Controls

Now you can add DrawnUi controls to your page. You have two main options:

#### Option 1: Use Canvas inside a regular ContentPage

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
             x:Class="YourNamespace.YourPage">

    <draw:Canvas HorizontalOptions="Fill" VerticalOptions="Fill">
        <draw:SkiaLayout Type="Column" Spacing="16" Padding="32">
            <draw:SkiaLabel
                Text="Hello DrawnUi!"
                FontSize="24"
                HorizontalOptions="Center"
                VerticalOptions="Center" />

            <draw:SkiaButton
                Text="Click Me"
                WidthRequest="120"
                HeightRequest="40"
                CornerRadius="8"
                BackgroundColor="Blue"
                TextColor="White"
                VerticalOptions="Center"
                HorizontalOptions="Center"
                Clicked="OnButtonClicked" />

        </draw:SkiaLayout>
    </draw:Canvas>
</ContentPage>
```

#### Option 2: Use DrawnUiBasePage (for keyboard support)

```xml
<draw:DrawnUiBasePage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                      xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
                      x:Class="YourNamespace.YourPage">

    <draw:Canvas
        RenderingMode="Accelerated"
        Gestures="Lock"
        HorizontalOptions="Fill" VerticalOptions="Fill">
        <draw:SkiaLayout Type="Column" Spacing="16" Padding="32">
            <draw:SkiaLabel
                Text="Hello DrawnUi!"
                FontSize="24"
                HorizontalOptions="Center"
                VerticalOptions="Center" />

            <draw:SkiaButton
                Text="Click Me"
                WidthRequest="120"
                HeightRequest="40"
                CornerRadius="8"
                BackgroundColor="Blue"
                TextColor="White"
                VerticalOptions="Center"
                HorizontalOptions="Center"
                Clicked="OnButtonClicked" />
        </draw:SkiaLayout>
    </draw:Canvas>
</draw:DrawnUiBasePage>
```

### Setup Canvas

If you indend to process gestures inside your canvas setup the `Gestures` property accordingly.
If you would have animated content or use shaders set `RenderingMode` to `Accelerated`. Otherwise leave it as it is to use the default lightweight `Software` mode, it is still perfect for rendering static content.

See [Handling Gestures](../gestures.md) for the canvas-level setup before you wire control handlers.

### Handling Events

Handle control events in your code-behind:

```csharp
private void OnButtonClicked(SkiaButton sender, SkiaGesturesParameters e)
{
    // Handle button click
    DisplayAlert("DrawnUi", "Button clicked!", "OK");
}
```

> **Important**: DrawnUi button events use `Action<SkiaButton, SkiaGesturesParameters>` instead of the standard EventHandler pattern. The first parameter is the specific control type (SkiaButton), and the second contains gesture information.

## Using Styles

DrawnUi supports MAUI styling, You can set default properties for drawn controls all like you would do it for MAUI controls.
Add this to `Resources/Styles.xaml` to set default fonts for all SkiaLabel and SkiaRichLabel controls:

```xml
 ...

 <ResourceDictionary
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw">

    <Style ApplyToDerivedTypes="True" TargetType="draw:SkiaLabel">
        <Setter Property="TextColor" Value="Black" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontFamily" Value="FontText" />
    </Style>

...
```

## Using Platform-Specific Styles

Some of DrawnUi controls support platform-specific styling:

```xml
<draw:SkiaButton
    Text="Platform Style"
    ControlStyle="Material"
    WidthRequest="150"
    HeightRequest="40" />

<draw:SkiaSwitch
    ControlStyle="Cupertino"
    IsToggled="true"
    Margin="0,20,0,0" />
```

## Important Differences from Standard MAUI

When working with DrawnUI, keep these key differences in mind:

* **Layout Options**: `HorizontalOptions` and `VerticalOptions` defaults are `Start`, not `Fill`. Request size explicitly or set options to `Fill`, otherwise your control will take zero space.
* **Grid Defaults**: `Grid` layout type default Row- and Column are "Auto" and not "*".
* **Grid Spacing**: `Grid` layout type default col/row Spacing is 1, not 8.
* **Canvas Behavior**: The `Canvas` control is aware of its children's size and will resize accordingly. You can also set a fixed size for the `Canvas` and its children will adapt to it.

## Next steps

- Explore the [MAUI Tutorials](tutorials.md) for full app examples.
- Review [Handling Gestures](../gestures.md) before building gesture-heavy UI.
- Check [Sample Apps](../sample-apps.md) for larger references.