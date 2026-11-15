# 3D in DrawnUI

DrawnUI comes with a separate, optional 3D add-on, for every head DrawnUI runs on. It draws real 3D inside a DrawnUI screen: models from glTF files with their skeleton animations, shapes made in code, and images on 3D surfaces. It draws through the GPU context Skia already uses, so a 3D control sits in a layout, scrolls, takes taps and gets cached around like any other control.

<a href="https://add3d.drawnui.net"><img src="../images/add3d-gallery.jpg" alt="The 3D gallery: a sofa product viewer with fabric swatches and hotspots" width="720" /></a>

[See it live: the 3D gallery](https://add3d.drawnui.net)

## Where it fits

- Product viewers: furniture, shoes, gadgets you turn with a finger, with fabric swatches and prices.
- A model that answers a tap, an avatar beside a form, a reward chest, a globe with pins, a card that flips.
- Games: 3D characters and elements inside a 2D game.

For a full 3D world with a free camera, physics and shadows, use a game engine. Vector illustrations and designer animations stay Lottie or SVG.

## Install

Add the package for your framework and head, then use the controls. There is no startup registration.

| Framework | Head | Package | Draws with |
|---|---|---|---|
| DrawnUI for .NET | .NET MAUI: Android, iOS, Mac Catalyst, Windows | `DrawnUi.Maui.Add3d` | Vulkan on Android, Metal on iOS and Mac, ANGLE on Windows |
| | WPF | `DrawnUi.Wpf.Add3d` | ANGLE |
| | OpenTK: Windows, Linux | `DrawnUi.OpenTk.Add3d` | OpenGL 3.3 or newer |
| | Blazor WebAssembly | `DrawnUi.Blazor.Add3d` | WebGL2 |
| | Pure WebAssembly | `DrawnUi.Web.Add3d` | WebGL2 |
| DrawnUI for Rust | Windows, Linux, macOS, iOS, Android, Web | crate `drawnui-add3d` | OpenGL, Metal, Vulkan, WebGL2 |
| DrawnUi.React | Web | npm `drawnui-react-add3d` | WebGL2 |

The .NET namespace is `DrawnUi.Add3d`. The controls and their properties have the same names in every framework, in that language's style (`PlayOnce` in C#, `play_once` in Rust). The examples below are C#.

On desktop heads the canvas draws with `RenderingMode = RenderingModeType.Accelerated`: the 3D pass uses the canvas's GPU context.

## Two controls

- **`SkiaModel3D`**: one model framed by its own camera.
- **`SkiaScene3D`**: one 3D layer over 2D content. Objects are placed in the layer's points like sprites, each with its own animation, and the whole layer draws in one pass.

## Showing a model

```csharp
using DrawnUi.Add3d;

new SkiaModel3D(avatarGlb)            // the .glb file's bytes
{
    Animation = "CharacterArmature|Idle",
    Yaw = 25, Pitch = 10,
    PinchZoom = true,
    ResetOnDoubleTap = true,
    HorizontalOptions = LayoutOptions.Fill,
    HeightRequest = 320,
}
.Assign(out _avatar)
.OnTapped3D((me, hit) => me.PlayOnce("CharacterArmature|Wave"));
```

Names come from the file: `Animations`, `NodeNames` and `MaterialNames` list them. Look them up there, never guess them.

### Loading

- Pass the bytes of a glTF 2.0 binary (`.glb`), or set `Source` to a URL or an app file to load it in the background.
- `LoadState` tells `Loading`, `Ready` or `Failed` (`LoadStateChanged`, `Error`). Show your own message on `Failed`: the control stays empty.
- Controls showing the same model share one copy. What you set before it arrives (`Animation`, `IsPlaying`, `Speed`) applies once it is there.

### Animations

- `Animation` plays in a loop. `PlayOnce` plays and returns to the loop, `Hold` plays and stays on the last frame, `PlayLayer` / `StopLayer` play on top of the loop.
- `Speed`, `CrossfadeMs`, `IsPlaying`, and the `AnimationEnded` event after `PlayOnce` or `Hold`.

### Parts and materials

- Nodes by name: `SetNodeVisible`, `SetNodeTranslation`, `SetNodeRotation`, `SetNodeScale`, `ResetNode`.
- Materials by name: `SetMaterialColor`, `SetMaterialRoughness` (0 shiny, 1 matte), `SetMaterialVisible`, and `SetMaterialImage` with a `Texture3D` to put any image on a surface. `null` puts back what the file had.

### Taps

- `Tapped3D` tells the node, the material and the 3D point hit. A tap on empty space around the model is not a 3D tap.
- `ScreenPosition(point)` tells where a 3D point is on screen and whether the model hides it: pin labels to a model from `ViewChanged`.

### Turning and zooming

The control behaves the same on every platform:

- Inside a scroll, a drag decides its direction at the start: up and down scrolls the page, sideways turns the model. Without a scroll around it, a drag turns it freely.
- A plain mouse wheel scrolls the page. Ctrl or Cmd + wheel, a touchpad pinch or a two-finger pinch zooms (`PinchZoom`, `MinZoom`, `MaxZoom`).
- A double tap resets the view (`ResetOnDoubleTap`), and `ResetView()` does it from code.
- `LockPitch` keeps the horizon level, `Orbit = false` turns dragging off, `AutoRotate` turns the model slowly at rest, and `TiltToPointer` makes it lean toward the mouse or finger.

Tell people how to use it, under the control: "Drag to turn · pinch or Ctrl+scroll to zoom · double-tap to reset".

## Shapes made in code

Simple objects need no file: build a `Model3D` from parts.

```csharp
new SkiaModel3D(new Model3D()
    .Part("earth", Mesh3D.Sphere(1), Colors.White))
    .MaterialImage("earth", Texture3D.Decode(earthJpg));
```

Meshes: `Sphere`, `Cylinder`, `Torus`, `Cuboid`, `Lathe` (a turned profile) and `Extrude` (any `SKPath` made 3D). Each part is placed with `At`, `Turned`, `Scaled` and `Inside(parent)`. `Texture3D.FromImage` takes any Skia image: a photo, or a pattern drawn in code.

## A 3D layer: SkiaScene3D

```csharp
new SkiaScene3D
{
    Tilt = 30,                        // how much of the objects' tops is seen
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
}
.Model("character", characterGlb)
.Assign(out _layer)
.OnTapped3D((me, hit) => me.PlayOnce(hit.Object, "CharacterArmature|HitRecieve"));

// place and drive objects from code
_layer.Add("player", "character", x, y, 48);   // object name, model, position in points, height
_layer.SetPosition("player", x, y);
_layer.SetFacing("player", 90);
_layer.Play("player", "CharacterArmature|Walk");
```

- `Model(name, ...)` registers a model once; `Add` places an object that uses it, `Remove` and `ClearObjects` take objects away.
- `SetPosition`, `SetFacing`, `SetSize` and `SetVisible` move and change an object; moving it never re-measures the layout.
- `Play`, `PlayOnce`, `Hold`, `SetSpeed` and `AnimationEnded` work per object, `SetMaterialColor` and `SetMaterialImage` per object and material.
- `Tapped3D` tells which object, node and material was hit.

## Looks

| `Look` | What it gives |
|---|---|
| `Look.Lit` (default) | shaded by a key light, a soft fill and the sky |
| `Look.Toon` | two tones, lit and shade, like a cartoon |
| `Look.Flat` | each color as it is: an illustration |

`OutlineWidth` and `OutlineColor` draw an ink line around every shape; Toon or Flat with an outline gives a drawn look. Looks switch live: models, positions and animations stay. `Antialias = true` smooths edges per control; it costs GPU time, so keep it for large controls.

## Performance

- A model that is still (no animation, no drag, no auto-rotate) asks for no frames. A layer off screen stops its animations.
- A model near the screen is rendered ahead while the frame has time left, so it shows at once when it scrolls in.
- Cache the cards around a resting model as usual. Do not cache a control that changes every frame, such as an animated model.
- Size the control to the design: the cost grows with its pixels.
- In web apps, embed only the models of the first screen and load the rest from URLs.
- The 3D GPU memory of a window is freed when the window closes.

## Assets

Use production-quality glTF models (realistic or well-made stylized), keep each file small (textures at the size they show, unused animations removed), and keep a license file next to them.

## Samples

- **3D gallery** in the DrawnUi repository, `src/Shared/Samples/Add3dGallery.Shared`, with a head for MAUI, WPF, OpenTK, Blazor and pure WebAssembly.
- **[add3d.drawnui.net](https://add3d.drawnui.net)**: the same gallery built with DrawnUI for Rust, running in the browser.
