# WPF Samples

Three sample projects under `src/Wpf/Samples/` show how DrawnUI is hosted in WPF.

## HelloWpf

Path: `src/Wpf/Samples/HelloWpf/`

The full demo: every page of the [React demo](../react/index.md) ported to WPF, navigated with a drawn `SkiaShell`.

- Cells and uneven cells (recycled templated lists), images, SVG, shapes, text, layouts
- Platform looks (`ControlStyle`), snapping carousels, Lottie and GIF
- Shell: pages, nested tabbed shell, popups, modals, toasts, cancellable navigation
- `SkiaEditor`, keyboard input through `KeyboardManager`
- `SkiaScroll` headers, footers, scroll bars, pull to refresh, snapping
- Shaders: `SkiaShaderCarousel` transitions, `SkiaShaderEffect`, touch ripples
- Sprites and a `SkiaSpriteSet` warrior moved with the keyboard
- Transforms and `*ToAsync` animations, drag to reorder, accessibility

Runs with `RenderingMode="Accelerated"`. Open it first to see every control on this head.

## WpfSandbox

Path: `src/Wpf/Samples/WpfSandbox/`

The smallest useful window: a drawn tree declared in WPF XAML, bound to a view model with `{Binding}` and an `ICommand`, and styled by a WPF `Style` from `App.xaml`. Project reference to the head.

## WpfPackageDemo

Path: `src/Wpf/Samples/WpfPackageDemo/`

The same window as `WpfSandbox`, consuming the `DrawnUi.Wpf` NuGet package with a `PackageReference` only. Use it as the template for a new app.

## Which sample to start with

- Start with `WpfSandbox` if you are adding a drawn canvas to an existing WPF app.
- Start with `WpfPackageDemo` if you want a project that references the package.
- Open `HelloWpf` to see a specific control or feature working on WPF.
