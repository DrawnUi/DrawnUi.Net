# Moving your DrawnUI app to .NET 11

Since DrawnUI 1.11.0.1, `DrawnUi.Maui` and its MAUI add-ons (`DrawnUi.Maui.Game`, `DrawnUi.Maui.MapsUi`, `DrawnUi.MauiGraphics`, `DrawnUi.Maui.Add3d`) ship .NET 11 targets next to the .NET 10 ones: `net11.0`, `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst` and `net11.0-windows10.0.19041.0`. Moving a DrawnUI MAUI app to .NET 11 is a short csproj change.

**.NET 9 apps:** use the 1.10.9.5 packages, the last ones with .NET 9 targets. Version 1.11 and later target .NET 10 and .NET 11.

## Checklist

**Before (.NET 10):**

```xml
<PropertyGroup>
  <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
  <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">21.0</SupportedOSPlatformVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.110" />
  <PackageReference Include="Microsoft.Maui.Controls.Compatibility" Version="10.0.110" />
  <PackageReference Include="DrawnUi.Maui" Version="..." />
</ItemGroup>
```

**After (.NET 11):**

```xml
<PropertyGroup>
  <TargetFrameworks>net11.0-android;net11.0-ios;net11.0-maccatalyst</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net11.0-windows10.0.19041.0</TargetFrameworks>
  <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">24.0</SupportedOSPlatformVersion>
  <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">17.0</SupportedOSPlatformVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Maui.Controls" Version="11.0.10" />
  <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="11.0.0" />
  <PackageReference Include="DrawnUi.Maui" Version="..." />
</ItemGroup>
```

1. **Target frameworks and packages:** change every `net10.0-*` to `net11.0-*`, and reference `Microsoft.Maui.Controls` 11.0.10 (or `$(MauiVersion)`) and `Microsoft.Extensions.Logging.Debug` 11.0.0.
2. **SDK:** build with the .NET 11 SDK (11.0.100 or newer). A repository that pins an SDK in `global.json` moves it to 11.0.100; the .NET 11 SDK builds .NET 10 and .NET 11 MAUI targets:

```json
{
  "sdk": {
    "version": "11.0.100",
    "rollForward": "latestFeature"
  }
}
```

3. **Compatibility package:** remove the `Microsoft.Maui.Controls.Compatibility` package and any `.UseMauiCompatibility()` call in `MauiProgram`. MAUI 11 has no such package, and DrawnUI does not need it.
4. **Android:** set `SupportedOSPlatformVersion` to 24 or higher (a lower value stops the build with XA4216).
5. **Mac Catalyst:** set `SupportedOSPlatformVersion` to 17.0 or higher, the minimum of the .NET 11 Mac Catalyst SDK. iOS keeps 15.0.
6. **iOS launch time:** to keep the fast .NET 10 launch on iPhone, set these in the iOS part of the csproj and measure launch before and after:

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <Registrar>managed-static</Registrar>
  <PrepareAssemblies>false</PrepareAssemblies>
  <PostProcessAssemblies>false</PostProcessAssemblies>
</PropertyGroup>
```

The app keeps the number of linked frameworks it had on .NET 10 (about 35) instead of about 150.

## iOS and Mac Catalyst: the scene lifecycle

The iOS 27 SDK builds apps with the UIKit scene lifecycle, on iOS and Mac Catalyst. The DrawnUI MAUI template and samples on .NET 11 carry all three parts:

1. A `SceneDelegate.cs` in `Platforms/iOS` (and `Platforms/MacCatalyst`):

```csharp
using Foundation;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
```

2. A scene manifest pointing at it, in the `Info.plist` of `Platforms/iOS` and of `Platforms/MacCatalyst` (without it the build stops with MT7199):

```xml
<key>UIApplicationSceneManifest</key>
<dict>
    <key>UIApplicationSupportsMultipleScenes</key>
    <false/>
    <key>UISceneConfigurations</key>
    <dict>
        <key>UIWindowSceneSessionRoleApplication</key>
        <array>
            <dict>
                <key>UISceneConfigurationName</key>
                <string>__MAUI_DEFAULT_SCENE_CONFIGURATION__</string>
                <key>UISceneDelegateClassName</key>
                <string>SceneDelegate</string>
            </dict>
        </array>
    </dict>
</dict>
```

3. A guard in `AppDelegate` for the app's exit, until MAUI ships its fix ([dotnet/maui #40438](https://github.com/dotnet/maui/issues/40438)):

```csharp
public override void WillTerminate(UIKit.UIApplication application)
{
    try
    {
        base.WillTerminate(application);
    }
    catch (InvalidOperationException e) when (e.Message == "Window was already destroyed")
    {
    }
}
```

With the scene lifecycle, the app creates its window in `App.CreateWindow` and changes pages with `Windows[0].Page = newPage`, never through the `Application.MainPage` setter:

```csharp
protected override Window CreateWindow(IActivationState activationState)
{
    return new Window(new MainPage());
}
```

## Shared Apple code in Platforms/Apple

MAUI 11 compiles the files of the platform folders it knows. Code shared by iOS and Mac Catalyst in `Platforms/Apple` (for example with `Plugin.Maui.ApplePlatform`) is declared with one item:

```xml
<ItemGroup>
  <MauiPlatformSpecificFolder Include="Platforms\Apple\" TargetPlatformIdentifiers="ios;maccatalyst" />
</ItemGroup>
```

On .NET 11 this item replaces the plugin, so keep the plugin only for the older targets: `Condition="!$(TargetFramework.StartsWith('net11.0'))"` on its `PackageReference`. Missing types or CS0115 ("no suitable method found to override") mean the Apple files were not compiled: add the item. CS2002 warnings mean they compile twice: limit the plugin to the older targets.

## XAML

MAUI 11 templates write XAML with implicit namespaces: the root element has no `xmlns` or `xmlns:x`. DrawnUI XAML keeps its `xmlns:draw` prefix as before. A project that targets .NET 10 and .NET 11 at the same time keeps the explicit `xmlns` lines, which both versions build.

MAUI 11 compiles XAML with its source generator, which checks more than XamlC did:

- Write paths with forward slashes: `Source="Images/car.png"`. A backslash stops the build.
- A binding to a property without a public setter takes `Mode=OneWay`.
- An `x:Name` must not repeat the name of a property of the page or control: bindings to that property would reach the named child. Give the child another name.

## SkiaSharp's own mesh types

On .NET 11, SkiaSharp has its own `SKMesh` types with the same names as DrawnUI's in `DrawnUi.Draw.Meshes`. Code that imports both namespaces names DrawnUI's types with aliases:

```csharp
using SKMesh = DrawnUi.Draw.Meshes.SKMesh;
using SKMeshMode = DrawnUi.Draw.Meshes.SKMeshMode;
using SKMeshSpecification = DrawnUi.Draw.Meshes.SKMeshSpecification;
using SKMeshVertexBuffer = DrawnUi.Draw.Meshes.SKMeshVertexBuffer;
using SKMeshIndexBuffer = DrawnUi.Draw.Meshes.SKMeshIndexBuffer;
```

Add the aliases for the other `SKMeshSpecification...` types the code uses the same way. In Razor, the same lines go into `_Imports.razor` as `@using`. On GPU canvases DrawnUI's meshes draw through Skia's own SkMesh, elsewhere through DrawnUI's C# path, with the same API.

## Blazor, pure WebAssembly and OpenTK apps

- Blazor and pure WebAssembly apps reference `Microsoft.AspNetCore.Components.WebAssembly` 11.0.0 (and its `DevServer` / `Server` packages at 11.0.0).
- Pure WebAssembly and OpenTK apps that pin SkiaSharp's native packages themselves use the versions DrawnUI 1.11 builds with on .NET 11: `SkiaSharp.NativeAssets.WebAssembly` / `SkiaSharp.NativeAssets.Linux` 4.159.0-rc.1.26567.13 and `HarfBuzzSharp.NativeAssets.WebAssembly` 15.0.0.100-rc.1.26567.13.

## Android Debug builds by hand

A Debug APK built with plain `dotnet build` and installed by hand needs `-p:EmbedAssembliesIntoApk=true`: the default fast deployment works through `dotnet run` and the IDE. This is how .NET for Android works, on every app.
