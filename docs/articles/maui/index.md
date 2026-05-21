# .NET MAUI

Use this section when DrawnUI is running inside a .NET MAUI app.

This is the native-host lane for DrawnUI: iOS, Android, MacCatalyst, and Windows, with MAUI providing the app shell and DrawnUI owning part or all of the rendered UI.

If you are building with **Blazor**, do not start with the MAUI tutorial list just because the tutorial titles look generic. Start with [Blazor](../blazor/index.md) for the correct host setup, or use the shared [Tutorial Host Guide](../tutorials.md) to see which tutorial concepts already have Blazor sandbox equivalents.

## Start here

- [Installation and Setup](getting-started.md)
- [MAUI Tutorials and Host Notes](tutorials.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)
- [Drawn Layouts](../layouts.md)
- [Porting Native to Drawn](../porting-maui.md)

## When MAUI is the right host

Choose the MAUI lane when you need:

- a native app host for mobile or desktop
- full control over gesture-heavy or animation-heavy screens
- a custom UI rendered by DrawnUI on top of MAUI app structure
- access to MAUI platform services while keeping the visible UI fully drawn

## Package

```bash
dotnet add package DrawnUi.Maui
```

## Related docs

- [Platforms and Packages](../platforms.md)
- [Tutorial Host Guide](../tutorials.md)
- [Installation and Setup](getting-started.md)
- [MAUI Tutorials and Host Notes](tutorials.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)
- [Porting Native to Drawn](../porting-maui.md)