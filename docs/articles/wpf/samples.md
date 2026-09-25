# WPF Samples

Three sample projects live in the repository under [src/Wpf/Samples](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples). Clone the repo and open the one you need; each is a plain `dotnet run`.

## HelloWpf

[src/Wpf/Samples/HelloWpf](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/HelloWpf)

The full demo: one page per feature, navigated with a drawn `SkiaShell`.

- Cells and uneven cells (recycled templated lists), images, SVG, shapes, text, layouts
- Platform looks (`ControlStyle`), snapping carousels, Lottie and GIF
- Shell: pages, nested tabbed shell, popups, modals, toasts, cancellable navigation
- `SkiaEditor`, keyboard input through `KeyboardManager`
- `SkiaScroll` headers, footers, scroll bars, pull to refresh, snapping
- Shaders: `SkiaShaderCarousel` transitions, `SkiaShaderEffect`, touch ripples
- Sprites and a `SkiaSpriteSet` warrior moved with the keyboard
- Transforms and `*ToAsync` animations, drag to reorder, accessibility

Runs with `RenderingMode="Accelerated"`. Open it to see a specific control or feature working on WPF.

## WpfSandbox

[src/Wpf/Samples/WpfSandbox](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfSandbox)

The smallest useful window: a drawn tree declared in WPF XAML, bound to a view model with `{Binding}` and an `ICommand`, and styled by a WPF `Style` from `App.xaml`. References the head project from source.

## WpfPackageDemo

[src/Wpf/Samples/WpfPackageDemo](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfPackageDemo)

The same window as `WpfSandbox`, consuming the `DrawnUi.Wpf` NuGet package with a `PackageReference` only. Copy it as the starting point for a new app.

## WpfPong

[src/Wpf/Samples/WpfPong](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Wpf/Samples/WpfPong)

The Pong game from the OpenTK and WebAssembly samples, hosted in a WPF window. The game code is shared (`src/Shared/Samples/Pong.Shared`); this project adds the window, the fonts and the background image. It uses the `DrawnUi.Wpf.Game` addon for the `DrawnGame` base class (fixed-step game loop, keyboard state) and hosts a `RescalingCanvas` through the `DrawnUiElement(Func<Canvas>)` constructor, so the game's logical viewport scales with the window and keeps its aspect ratio. Tap or click to serve, arrow keys move the paddle.

## Which sample to start with

- `WpfSandbox` if you are adding a drawn canvas to an existing WPF app.
- `WpfPackageDemo` if you want a project that references the package.
- `HelloWpf` to see a specific control or feature working on WPF.
- `WpfPong` if you are writing a game.

- **Snippets**: more DrawnUI snippets you can run and edit right in the browser at [drawfiddle.com](https://drawfiddle.com).
