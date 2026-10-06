# Tutorials

If you are choosing a host first, start with [Platforms and Packages](platforms.md).

## Available today

[MAUI Tutorials](maui/tutorials.md) are available today and include the step-by-step walkthroughs for the current full tutorial path. 

## Try it in the browser

[DrawnUI Fiddle](https://drawfiddle.com) runs DrawnUI snippets right in the browser, with nothing to install: open one, change the code and see the result at once. It is the quickest way to try what a tutorial shows.

## Other hosts

Blazor, OpenTK, WPF, and `DrawnUi.Net` use the same core DrawnUI concepts and control tree patterns, but the host setup is different.

For now, use the MAUI tutorials for the control and layout concepts, then switch to samples for your target host:

- [Blazor Samples](blazor/samples.md)
- [OpenTK Samples](opentk/samples.md)
- [WPF Samples](wpf/samples.md)
- [DrawnUi.Net Overview](net/index.md)
- [DrawnUI for React](react/index.md) and [DrawnUI for Rust](rust/index.md): their demo apps, one page per feature

## How to read the current tutorials

If a tutorial shows `ContentPage`, `MauiProgram.cs`, or MAUI XAML namespaces, that part is MAUI-specific host setup.

The drawn UI composition itself is still useful across hosts. In most cases, the same layouts, controls, and interaction patterns carry over with only the host bootstrap and container setup changed.

For example:

- MAUI uses `DrawnUi.Maui` with MAUI app startup and pages
- Blazor uses `DrawnUi.Blazor.*` with `UseDrawnUiAsync(...)` and Razor `<Canvas ... />`
- OpenTK uses `DrawnUi.OpenTk` inside an OpenTK `GameWindow`
- `DrawnUi.Net` uses the shared rendering/runtime layer without a MAUI or browser host

## Related

- [DrawnUI Fiddle](https://drawfiddle.com)
- [MAUI Tutorials](maui/tutorials.md)
- [Installation and Setup](maui/getting-started.md)
- [Blazor Overview](blazor/index.md)
- [Blazor Samples](blazor/samples.md)
- [DrawnUI for OpenTK](opentk/index.md)
- [OpenTK Samples](opentk/samples.md)
- [WPF Samples](wpf/samples.md)
- [DrawnUi.Net Overview](net/index.md)

