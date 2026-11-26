# Moving your DrawnUI app to .NET 11

Since DrawnUI 1.11.0.1, `DrawnUi.Maui` and its MAUI add-ons (`DrawnUi.Maui.Game`, `DrawnUi.Maui.MapsUi`, `DrawnUi.MauiGraphics`, `DrawnUi.Maui.Add3d`) ship .NET 11 targets next to the .NET 10 ones: `net11.0`, `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst` and `net11.0-windows10.0.19041.0`. Moving a DrawnUI MAUI app to .NET 11 is a short csproj change.

**.NET 9 apps:** use the 1.10.9.4 packages, the last ones with .NET 9 targets. Version 1.11 and later target .NET 10 and .NET 11.

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
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Maui.Controls" Version="11.0.10" />
  <PackageReference Include="DrawnUi.Maui" Version="..." />
</ItemGroup>
```

1. **Target frameworks:** change every `net10.0-*` to `net11.0-*`, and reference `Microsoft.Maui.Controls` 11.0.10 (or `$(MauiVersion)`).
2. **SDK:** build with the .NET 11 SDK (11.0.100 or newer). If the repository pins an SDK in `global.json`, move it to 11.0.100. The .NET 11 SDK builds .NET 10 and .NET 11 MAUI targets.
3. **Compatibility package:** remove the `Microsoft.Maui.Controls.Compatibility` package and any `.UseMauiCompatibility()` call in `MauiProgram`. MAUI 11 has no such package, and DrawnUI does not need it.
4. **Android:** set `SupportedOSPlatformVersion` to 24 or higher (a lower value stops the build with XA4216).
5. **iOS launch time:** to keep the fast .NET 10 launch on iPhone, set these in the iOS part of the csproj and measure launch before and after:

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <Registrar>managed-static</Registrar>
  <PrepareAssemblies>false</PrepareAssemblies>
  <PostProcessAssemblies>false</PostProcessAssemblies>
</PropertyGroup>
```

The app keeps the number of linked frameworks it had on .NET 10 (about 35) instead of about 150.

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

## Android Debug builds by hand

A Debug APK built with plain `dotnet build` and installed by hand needs `-p:EmbedAssembliesIntoApk=true`: the default fast deployment works through `dotnet run` and the IDE. This is how .NET for Android works, on every app.
