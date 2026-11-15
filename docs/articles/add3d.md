# 3D in DrawnUI

The DrawnUI 3D add-on draws real 3D inside a DrawnUI screen: models from glTF files with their skeleton animations, shapes made in code, images on 3D surfaces, and many animated characters on a 2D game field. It draws through the GPU context Skia already uses, so a 3D control sits in a layout, scrolls, takes taps and gets cached around like any other control.

[See it live: the 3D gallery](https://add3d.drawnui.net)

## Where it fits

- Product viewers: furniture, shoes, gadgets you turn with a finger, with fabric swatches and prices.
- A model that answers a tap, an avatar beside a form, a reward chest, a globe with pins, a card that flips.
- Games: dozens of animated characters on a 2D field, moved like sprites.

For a full 3D world with a free camera, physics and shadows, use a game engine. Vector illustrations and designer animations stay Lottie or SVG.

## Install

One package per head, namespace `DrawnUi.Add3d`. There is no startup registration: add the package and use the controls.

| Head | Package | Draws with |
|---|---|---|
| .NET MAUI | `DrawnUi.Maui.Add3d` | Vulkan on Android, Metal on iPhone and Mac, ANGLE on Windows |
| WPF | `DrawnUi.Wpf.Add3d` | ANGLE |
| OpenTK (Windows, Linux) | `DrawnUi.OpenTk.Add3d` | OpenGL 3.3 or newer |
| Blazor WebAssembly | `DrawnUi.Blazor.Add3d` | WebGL2 |
| Pure WebAssembly | `DrawnUi.Web.Add3d` | WebGL2 |

On desktop heads the canvas draws with `RenderingMode = RenderingModeType.Accelerated`: the 3D pass uses the canvas's GPU context.

The same add-on exists for DrawnUI for Rust (crate `drawnui-add3d`), with the same controls and property names in Rust style. It is also in DrawnUi.React.

## Two controls

- **`SkiaModel3D`**: one model framed by its own camera. Drag to turn it.
- **`SkiaScene3D`**: one 3D layer over a 2D game field. Objects are placed in the layer's points like sprites, each with its own animation, and all of them draw in one pass.

## A model viewer

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

Animation names come from the file: `Animations` lists them. Look them up there, never guess them.

- **Loading:** pass the bytes of a glTF 2.0 binary (`.glb`), or set `Source` to a URL or an app file to load it in the background. `LoadState` tells `Loading`, `Ready` or `Failed` (`LoadStateChanged`, `Error`); show your own message on `Failed`. Controls showing the same model share one copy.
- **Animations:** `Animation` plays in a loop; `PlayOnce` plays and returns to the loop, `Hold` stays on the last frame, `PlayLayer` plays on top of the loop. `Speed`, `CrossfadeMs`, `IsPlaying`, `AnimationEnded`.
- **Names come from the file:** read `Animations`, `NodeNames` and `MaterialNames` instead of guessing them.
- **Nodes and materials by name:** show, hide, move, turn or scale a part (`SetNodeVisible`, `SetNodeRotation`, ...), recolor a material, change its roughness or put any image on it (`SetMaterialColor`, `SetMaterialRoughness`, `SetMaterialImage` with a `Texture3D`).
- **Taps:** `Tapped3D` tells the node, the material and the 3D point hit. `ScreenPosition(point)` tells where a 3D point is on screen and whether the model hides it, to pin labels to a model.

### How people turn and zoom it

The viewer behaves the same on every platform:

- Inside a scroll, a drag decides its direction at the start: up and down scrolls the page, sideways turns the model.
- A plain mouse wheel scrolls the page. Ctrl or Cmd + wheel, a touchpad pinch or a two-finger pinch zooms (`PinchZoom`, `MinZoom`, `MaxZoom`).
- A double tap resets the view (`ResetOnDoubleTap`).
- `LockPitch` keeps the horizon level, `Orbit = false` turns dragging off, `AutoRotate` turns the model slowly at rest, and `TiltToPointer` makes a card lean toward the mouse or finger.

Tell people how to use it, under the viewer: "Drag to turn · pinch or Ctrl+scroll to zoom · double-tap to reset".

## Shapes made in code

No file needed for simple objects: build a `Model3D` from parts.

```csharp
new SkiaModel3D(new Model3D()
    .Part("earth", Mesh3D.Sphere(1), Colors.White))
    .MaterialImage("earth", Texture3D.Decode(earthJpg));
```

Meshes: `Sphere`, `Cylinder`, `Torus`, `Cuboid`, `Lathe` (a turned profile) and `Extrude` (any `SKPath` made 3D). Each part is placed with `At`, `Turned`, `Scaled` and `Inside(parent)`. `Texture3D.FromImage` takes any Skia image, a photo or a pattern drawn in code.

## Many characters on a game field

```csharp
new SkiaScene3D
{
    Tilt = 30,                        // how much of their tops the player sees
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
}
.Model("slime", slimeGlb)
.Assign(out _field)
.OnTapped3D((me, hit) => me.PlayOnce(hit.Object, "CharacterArmature|HitRecieve"));

// from the game loop
_field.Add("mob7", "slime", x, y, 48);   // name, model, position in points, height
_field.SetPosition("mob7", x, y);
_field.SetFacing("mob7", 90);
_field.Play("mob7", "CharacterArmature|Walk");
```

Each model loads once, however many objects use it, and the whole layer draws in one pass. Moving an object never re-measures the layout. 100 animated characters run at 60 FPS in desktop Chrome; on a mid-range Android phone in Chrome (BV8800, 90 Hz screen) at about 79 FPS, with the Blazor build running without AOT.

## Looks

| `Look` | What it gives |
|---|---|
| `Look.Lit` (default) | shaded by a key light, a soft fill and the sky |
| `Look.Toon` | two tones, lit and shade, like a cartoon |
| `Look.Flat` | each color as it is: an illustration |

`OutlineWidth` and `OutlineColor` draw an ink line around every shape; Toon or Flat with an outline gives a drawn look. Switch looks live: models, positions and animations stay. `Antialias = true` smooths edges per control; it costs GPU time, so keep it for large viewers.

## Performance

- A model that is still (no animation, no drag, no auto-rotate) asks for no frames.
- A model near the screen is rendered ahead while the frame has time left, so it shows at once when it scrolls in.
- Cache the cards around a resting model as usual. Do not cache a control that changes every frame, such as an animated model.
- Size the viewer to the design: the cost grows with its pixels.
- For web apps, embed only the models of the first screen and load the rest from URLs.
- The 3D GPU memory of a window is freed when the window closes.

## Assets

Use production-quality glTF models (realistic or well-made stylized), keep each file small (textures at the size they show, unused animations removed), and keep a license file next to them.

## Samples

- **3D gallery** in the DrawnUi repository, `src/Shared/Samples/Add3dGallery.Shared`, with a head for MAUI, WPF, OpenTK, Blazor and pure WebAssembly: a product viewer with fabrics and hotspots, a 3D list, a card flip, a globe with pins, a mascot beside a form, micro-interactions, images on 3D, model files, 100 mobs on a 2D field and a boss fight.
- **[add3d.drawnui.net](https://add3d.drawnui.net)**: the same gallery built with DrawnUI for Rust, running in the browser.
