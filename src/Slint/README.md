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

The canvas draws into a raster surface when something changed, at most once a display frame, and the view shows the frame as an image.
Since the view is an ordinary Slint image, it needs nothing of a particular Slint renderer.
On an Apple Silicon Mac, a frame of 1168×403 pixels takes about 1.4 ms to draw and pass on.

Input:

- The mouse: every button, with the right button's release as the context-menu request, hover, and leaving the view.
- The wheel, at 120 units a notch, horizontal on a touchpad's dominant axis.
- The keyboard: text and the editor keys for a focused `SkiaEditor`, and `OnKeyDown` / `OnKeyUp` events, once a click gave the view focus.

## Sample

`Samples/HelloSlint` shows two views next to a Slint header: a button whose taps reach the Slint UI, an editor, cards that light up on hover, and a ball that the arrow keys move.

```
dotnet run --project src/Slint/Samples/HelloSlint
```

`DRAWNUI_SELFTEST=<directory>` runs it as a test: it sends window events, as a user's mouse and keyboard would, saves frames as PNG files, and quits.
