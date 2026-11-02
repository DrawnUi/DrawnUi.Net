# DrawnUI for Slint

`DrawnUi.Slint` hosts DrawnUI canvases in the windows of a [Slint for .NET](https://github.com/yurkinh/slint/tree/dotnet_bindings/api/dotnet) app.
A Slint UI and DrawnUI controls share a window: a `DrawnUiView` element shows a canvas, and passes its size, mouse, wheel, and keyboard input to it.

## Use It

Import the view, and export its `DrawnUiHost` global from the main `.slint` file:

```slint
import { DrawnUiView } from "drawnui.slint";
export { DrawnUiHost } from "drawnui.slint";

export component MainWindow inherits Window {
    DrawnUiView { view-id: 0; }
}
```

Attach a canvas to each view by its `view-id`, on the Slint thread:

```csharp
using var window = MainWindow.Create();
using var host = SlintCanvasHost.Attach(window.Instance, viewId: 0);
host.Canvas.Content = new SkiaStack
{
    Children = new List<SkiaControl> { new SkiaButton("Tap me") { Clicked = (me, args) => window.Title = "Tapped" } },
};
window.Run();
```

The app's project compiles `drawnui.slint` with its own files:

```xml
<SlintIncludePaths>path/to/src/Slint/DrawnUi</SlintIncludePaths>
<ItemGroup>
  <SlintFile Include="ui/main.slint" />
  <SlintFile Include="path/to/src/Slint/DrawnUi/drawnui.slint" Generate="false" />
</ItemGroup>
```

Register fonts and styles with `Super.UseDrawnUi()`, as on the other .NET heads.

## How It Works

The canvas draws when something changed, at most once a display frame, and the view shows the frame as an image.

- By default, the canvas draws into a raster surface, whose pixels the view shows, with any Slint renderer.
- With `SlintCanvasHost.UseGpu = true` and Slint's FemtoVG renderer on OpenGL, it draws with Skia on that context instead,
  into a texture that the view shows without copying pixels:

  ```csharp
  new Slint.BackendSelector().RendererName("femtovg").RequireOpenGL().Select();
  SlintCanvasHost.UseGpu = true;
  ```

  Not with Slint's Skia renderer, its default on the desktop and Android: it keeps its own Skia state of the context,
  which a second Skia on that context breaks, and the window shows nothing.

`SlintCanvasHost.IsGpu` tells which path a view takes.
On an Apple Silicon Mac, a frame of 1168×403 pixels takes about 1.5 ms of the CPU on the GPU path, and 1.8 ms on the raster path;
on an Android emulator at a scale of 2.625, the raster path takes about 2.6 ms a frame.

## Platforms

The macOS desktop, Android, and iOS run the sample, with the same scenes and `.slint` file, see `Samples/`.
On Android and iOS, Slint's platform starts the app: create the window and attach the hosts in `SlintActivity.SlintMain`,
or in the `Main` that calls `Run` on iOS.

## Input

- The mouse: every button, with the right button's release as the context-menu request, hover, and leaving the view.
- Touch: a finger's taps and drags, such as to scroll a list.
- The wheel, at 120 units a notch, horizontal on a touchpad's dominant axis.
- The keyboard: text and the editor keys for a focused `SkiaEditor`, and `OnKeyDown` / `OnKeyUp` events, once a click or tap gave the view focus.
- An input method, such as a phone's on-screen keyboard, writes into a hidden Slint `TextInput` that takes the focus while a DrawnUI editor has it;
  key presses still go to DrawnUI first.
  On Android, the keyboard opens for the editor; its text reaching the editor is still to be checked by hand.

`SlintCanvasHost.SaveWindow` saves the whole window, as Slint renders it, from the OpenGL framebuffer, for tests.

## Sample

`Samples/HelloSlint` shows two views next to a Slint header: a button whose taps reach the Slint UI, an editor, cards that light up on hover, and a ball that the arrow keys move.

```
dotnet run --project src/Slint/Samples/HelloSlint
```

`DRAWNUI_GPU=1` selects an OpenGL renderer, for the GPU path. `DRAWNUI_SELFTEST=<directory>` runs it as a test: it sends window events, as a user's mouse and keyboard would, saves frames as PNG files, and quits.
