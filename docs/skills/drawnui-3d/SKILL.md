---
name: drawnui-3d
description: Real 3D inside a DrawnUI screen with the 3D add-on — SkiaModel3D (one glTF / GLB model with its skeleton animations, a model viewer that turns, zooms and resets) and SkiaScene3D (many 3D characters, "mobs", on a 2D game field, drawn in one pass), shapes made in code, images and colors on materials, the Lit / Toon / Flat looks with an outline, loading from the app or a URL, how a model behaves inside a scroll (drag lock, wheel and pinch zoom, double tap), rendering ahead and performance numbers, and what assets to use. Load before adding 3D, a model viewer, glTF / add3d models or 3D mobs to any DrawnUI app.
version: 1.0.0
tags: [drawnui, 3d, gltf, dotnet, react, rust]
---

# DrawnUI 3D

The 3D add-on draws real 3D (depth buffer, textures, a light, skeletal animation) through the GPU
context Skia already uses, into a texture the control then shows like an image. Everything around it
(layout, caching of the cards around it, gestures, text, buttons) works as for any DrawnUI control.

Load `drawnui` for engine semantics and the skill of your engine (`drawnui-rust`, `drawnui-react`, ...) for its build.

## Install

| Engine | Install | Import |
|---|---|---|
| DrawnUI for .NET | NuGet `DrawnUi.Maui.Add3d`, `DrawnUi.Wpf.Add3d`, `DrawnUi.OpenTk.Add3d`, `DrawnUi.Blazor.Add3d` or `DrawnUi.Web.Add3d`, version 1.10.9.2 (the one matching your head) | `using DrawnUi.Add3d;` |
| DrawnUI for React | `npm i drawnui-react drawnui-react-add3d` (`drawnui-react-add3d` ^1.0.1) | `import { SkiaModel3D } from "drawnui-react-add3d";` |
| DrawnUI for Rust | `drawnui-add3d = "1.0"` in `[dependencies]` | `use drawnui_add3d::prelude::*;` |

Live galleries: [.NET WebAssembly](https://add3dnet.drawnui.net), [Blazor](https://add3dblazor.drawnui.net),
[React](https://add3dreact.drawnui.net), [Rust](https://add3d.drawnui.net).

## Engines

| | DrawnUI for Rust | DrawnUI for .NET | DrawnUI for React |
|---|---|---|---|
| Package | crate `drawnui-add3d` (`use drawnui_add3d::prelude::*`) | `DrawnUi.Maui.Add3d`, `DrawnUi.Wpf.Add3d`, `DrawnUi.OpenTk.Add3d`, `DrawnUi.Blazor.Add3d`, `DrawnUi.Web.Add3d`; namespace `DrawnUi.Add3d` | npm `drawnui-react-add3d` (components; `drawnui-react-add3d/core` for the classes) |
| Draws on | the browser (WebGL2), Windows and Linux (desktop OpenGL), macOS and iOS (Metal), Android (Vulkan, or GL ES) | Android (Vulkan), iOS and Mac (Metal), Windows MAUI and WPF (GL ES through ANGLE), OpenTK on Windows and Linux (desktop OpenGL 3.3+), Blazor and pure Wasm (WebGL2) | the browser (WebGL2, on CanvasKit's own context) |
| Setup | add the crate, use the controls | add the package, use the controls (no startup registration) | add the package, use the components |

The API is the same in all three; names follow each language (`play_once` / `PlayOnce`,
`on_tapped_3d` / `Tapped3D`; React uses the .NET names as props). The examples below are Rust; the .NET and React
names are at the end.

## Where it fits

- Product viewers (furniture, shoes, gadgets), a model that answers a tap, an avatar, a reward chest,
  a globe with pins, a card that tilts toward the hand.
- Games: many animated characters on a 2D field, moved like sprites.
- Not for: a full 3D game world with a free camera, physics, shadows or post effects. Use a game
  engine for that. Vector illustrations and designer animations stay Lottie / SVG: the Flat look is
  a style for 3D models, not a replacement for those.

## The two controls

- `SkiaModel3D`: one scene framed by its own camera. Drag to turn it.
- `SkiaScene3D`: one 3D layer over a 2D game field. Objects are placed in the layer's own points like
  sprites, each with its own animations. All of them are drawn in one pass, and each model is loaded
  once whatever the number of objects.
- `Model3D` / `Mesh3D`: shapes made in code: `sphere`, `cylinder`, `torus`, `cuboid`, `lathe` (a
  turned profile), `extrude` (any 2D path made 3D). Parts by name, placed with `at`, `turned`,
  `scaled`, `inside(parent)`. Shown with `SkiaModel3D::from_model(&model)` or
  `SkiaScene3D::model_from(name, &model)`. In .NET also `Mesh3D.RoundedCuboid(w, h, d, radius, atlas)`: a box with
  rounded edges and corners (a die, a phone, a soap bar); with `atlas: true` one image split 3 x 2 gives each face its
  own cell (top row +X, -X, +Y; bottom row -Y, +Z, -Z), e.g. a die's six faces. Production look: never a sharp cube
  where the real object is rounded.

```rust
use drawnui::prelude::*;
use drawnui_add3d::prelude::*;

SkiaModel3D::new(include_bytes!("assets/boss.glb"))
    .animation("Idle")
    .view_angles(25.0, 10.0)
    .on_tapped_3d(|me, _app: &mut App, _cx, hit| {
        me.play_once("Hit");
    })
```

## Loading

- glTF 2.0 binary (`.glb`). Embedded in the app: `SkiaModel3D::new(bytes)`. Controls showing the
  same bytes share one copy.
- From a URL: `SkiaModel3D::from_url(url)`, later `set_source(url)`. Nothing is drawn while it loads.
  `load_state()` / `on_load_state` say `Loading`, `Ready` or `Failed`; `error()` says why. Show your
  own message for `Failed` (a broken link, a file that is not a model): the control stays empty.
  Controls asking for the same url share one load and one copy (30 list rows of 6 pieces: 6 fetches).
  What you set before it arrives (`animation`, `playing(false)`, `speed`) holds once it is there.
  A `SkiaScene3D` takes a model from a url with `model_url(name, url)`; `add` returns false until it
  is loaded, so place objects again when it fails.
- Web apps that start fast: embed only what the first screen shows and load the rest from urls. The
  app's download shrinks by the models' size and the page shows sooner; models below the first
  screen arrive while the visitor looks at it.
- Warmed ahead automatically, no call to make: a model within one screen height above or below the
  visible area is rendered once off screen while the frame has time left, so it shows at once when it
  scrolls in, and the 3D program is compiled early, before the first model draws (games precompile
  shaders while loading for the same reason). Measured in DrawnUI for Rust: first render of a viewer
  26-48 ms instead of 184-300 ms.

## Animations, nodes, materials

- `animation(name)` plays in a loop from the start; `play`, `play_once` (plays, then returns to the
  loop), `hold` (plays and stays on its last frame), `play_layer` / `stop_layer` (on top of the loop),
  `crossfade_ms`, `speed`, `set_playing`. `on_animation_ended` runs after `play_once` / `hold`.
- Names come from the file: `animations()`, `node_names()`, `material_names()`. Look them up there,
  never guess them.
- Nodes by name: `set_node_visible`, `set_node_translation`, `set_node_rotation`, `set_node_scale`,
  `reset_node`.
- Materials by name: `set_material_color` (a swatch), `set_material_roughness` (glTF roughness:
  0 shiny, 1 matte; leather a little shiny, fabric matte; shows in the Lit look), `set_material_image` with a `Texture3D`
  (`Texture3D::decode(png_or_jpeg_bytes)`, or any Skia image: a photo, a pattern drawn in code),
  `set_material_visible`. `None` puts back what the file had.
- Taps: `on_tapped_3d` tells the node, the material and the 3D point hit; a tap on empty space around
  the model is not a 3D tap. `screen_position(point)` gives where a 3D point is on screen and whether
  the model hides it: anchor labels and pins there in `on_view_changed`.

## Framing

A viewer fits its model by itself, like an image in fit mode, with a little air, and never cuts it:
- A view the user turns fits the model's real outline turned around the vertical (per height band, how far it
  reaches), at the current tilt: turning never cuts it, and a ball keeps its size at every tilt (turning a globe to a
  city never zooms it).
- A view nobody turns (orbit off, no tilt to pointer, no auto turn) fits its box exactly as seen.
- The fit covers the model at rest and through every animation of the file. When the file has big animations the app
  never plays (a jump, a fall), name the ones it plays: `FrameAnimations` (.NET) and the model shows larger.
- Parts the app moves itself past that (dice it tumbles, a node it turns) need room: `FrameMargin` (.NET, model units).
- `Zoom` above 1 sets a model back with more room around it (a globe in space); never set `Zoom` below 1 to make a
  model bigger: it cuts it in narrow views.

## Light and looks

A key light, a soft fill from the other side and the sky, set by the add-on. The look is chosen per
control:

| Look | What it gives |
|---|---|
| `Look::Lit` (default) | shaded by the light and the sky |
| `Look::Toon` | two tones, lit and shade, like a cartoon |
| `Look::Flat` | each color as it is, no shading: an illustration |

- `outline(width_pt, color)`: an ink line around every shape (0 = none). Toon or Flat plus an outline
  gives a drawn look.
- "Drawn" preset used in the gallery: `Look::Toon`, outline 2 pt, color #2B2540.
- Switch live with `set_look` / `set_outline` (both controls): models, places and animations stay.
- `antialias(true)` smooths edges (multisampling); it costs GPU time, so keep it for large viewers.

## Interaction

A model viewer behaves the same everywhere:

- Inside a scroll, a drag decides its direction at the start: vertical scrolls the page, sideways
  turns the model (yaw, with tilt). Without a scroll around it the drag turns freely in every
  direction (full orbit).
- A plain mouse wheel scrolls the page. Ctrl / Cmd + wheel, or a touchpad / two-finger pinch, zooms
  (`pinch_zoom(true)`, `min_zoom`, `max_zoom`).
- Double tap resets the view (`reset_on_double_tap(true)`), with a short glide.
- `lock_pitch(true)` keeps the horizon level; `orbit(false)` turns dragging off (a model that only
  plays); `auto_rotate(degrees_per_second)` turns it slowly at rest.
- `tilt_to_pointer(degrees)`: a card that turns toward the mouse or finger, up to `degrees` at its
  edge, and glides back when the pointer leaves. Drags no longer turn it, so the scroll keeps them.
- Tell the user how to use it, under the viewer: "Drag to turn · pinch or Ctrl+scroll to zoom ·
  double-tap to reset".

## Many objects (games, mobs)

```rust
SkiaScene3D::new()
    .model("slime", include_bytes!("assets/slime.glb"))
    .tilt(30.0)            // how much of their tops the player sees
    .fill()
    .assign(&mut app.field)
    .on_tapped_3d(|me, _app: &mut App, _cx, hit| {
        me.play_once(&hit.object, "Hit");
    })

// later, from the game loop:
field.add("mob7", "slime", x, y, 48.0);      // name, model, place in points, height
field.set_position("mob7", x, y);
field.set_facing("mob7", 90.0);
field.play("mob7", "Walk");
```

- One layer, one pass, each model loaded once. Objects are moved by points, like sprites; the layer
  never re-measures for that.
- Measured, browser, 100 animated mobs over a parallax field: CPU frame median 3.0 ms, p99 5.6 ms
  (Lit) and 5.9 ms (Toon + outline).
- Off screen, a layer stops its animations and asks for no frames until a scroll brings it back.

## Physics-driven motion (dice, tossed objects)

Motion that must look physical comes from a small simulation, not from eased angles: easing each axis on its own
and adding a scripted hop reads as fake, because the spin never answers the bounces. Pattern (the gallery's dice):
rigid bodies with velocity and spin, contacts at the corners against the table (impulse with restitution, then
Coulomb friction), fixed physics steps advanced inside the animation callback by the real elapsed time, each body
written with `SetNodeTranslation` + the quaternion `SetNodeRotation`. Advance the simulation frame by frame instead
of computing the whole throw on the tap, so the page stays responsive (on the web a full throw up front costs about
half a second). Let the outcome be what the physics
lands on; a body propped on an edge gets a tiny nudge so it tips over as a real one would.

## iOS Release: float structs as arguments

On arm64 Release builds (iOS AOT, Android LLVM) a method that takes more float-struct values by value than fit the 8
float argument registers (Vector2 = 2, Vector3 = 3, Quaternion / Vector4 = 4, float = 1, counted left to right) can
receive the later ones as garbage (dotnet/runtime#135197). Desktop and Debug builds are fine, so it shows only on the
phone: a mesh built from code shows broken triangles. Pass such parameters with `in`
(`void Quad(in Vector3 a, in Vector3 b, ...)`), in add-on code and in app code that builds meshes or poses.

## Performance rules

- Cache the cards around a resting model the usual way. Never cache a control that changes every
  frame (an animated model, a turning viewer): its cache would be rebuilt every frame for nothing.
- A model that is still (no animation, no drag, no auto-rotate) asks for no frames.
- Size the viewer to what the design needs: render cost grows with pixels (about 1 ms plus 1 ms per
  30 000 pixels for a first render).
- Layers behind a game field (parallax scenery): draw each as one continuous cached strip that slides,
  not two copies side by side, or a line shows where the copies meet.

## Assets

- Production-quality models only: realistic (Poly Haven class) or smooth, well-made stylized
  characters. Never blocky low-poly kits or placeholder art in a showcase.
- Check the license of every model and texture and keep a license file next to the assets.
- Keep files small: one `.glb` per model, textures at the size they show (1-2K), unused animations
  removed.

## Worked example: the 3D gallery

Every engine ships the same gallery: [Rust `examples/gallery3d`](https://github.com/DrawnUi/DrawnUi.Rust/tree/main/examples/gallery3d),
[.NET `src/Shared/Samples/Add3dGallery.Shared`](https://github.com/DrawnUi/DrawnUi.Net/tree/main/src/Shared/Samples/Add3dGallery.Shared)
(one shared page, run by the MAUI, WPF, OpenTK, Blazor and WebAssembly samples), [React `samples/add3d`](https://github.com/DrawnUi/DrawnUi.React/tree/master/samples/add3d): a single scrolling page with every pattern above, in this order:
- A globe with flashing city lights; tap one for its callout (city and country).
- A card that flips and tilts toward the hand, both faces drawn in code.
- A product viewer: a sofa with fabric swatches (image and roughness), hotspots, a price, a model
  loaded from a URL with its loading and failed states.
- A furniture shop list with a live 3D thumbnail per row.
- Small 3D touches: confetti falling, a product box wearing a label drawn in code, a reward chest.
- A sign-up form with a 3D mascot that watches what you type.
- Games: a field of 10 / 40 / 100 mobs over parallax scenery, with a "3D | Drawn" switch (a segmented
  control) that flips every mob between `Look::Lit` and the Drawn preset in place. A Boss fight tab.

### Lights on a 3D surface (the globe's flashing cities)

A spot of light is a 2D flare drawn at the point's screen position (`screen_position`, hidden when
the model covers it), not a 3D object: a small lit sphere reads as a ball, never as light. The flare
is a white core, a tight warm radial bloom (gone by 30% of its radius) and four thin rays fading at
their ends, every part drawn in the SCREEN blend mode so it only brightens what is under it. A normal
blend mixes the gold into a dark blue ocean and draws a grey smudge around the light. Animate the
flash with scale and rotation, never with opacity: an opacity layer draws the flare offscreen first,
where the screen blend has nothing under it.

The 3D gallery shows its FPS label in every build, Release included, bottom right: it is a performance
showcase, so the number stays visible.

## DrawnUI for .NET

Same contract: looks, interaction rules, warming ahead, one-pass scenes. Properties instead of
builder calls, events instead of closures, fluent helpers in `Add3dFluent`.

```csharp
using DrawnUi.Add3d;

new SkiaModel3D(glbBytes)
{
    Animation = "Idle",
    Yaw = 25, Pitch = 10,
    PinchZoom = true,
    ResetOnDoubleTap = true,
}
.OnTapped3D((me, hit) => me.PlayOnce("Hit"));
```

`SkiaModel3D` (constructors `(byte[] glb)`, `(Model3D model)`, `()`):
- Loading: `Source` (URL or app file, loads async), `LoadState` (`LoadState3D.Loading` / `Ready` /
  `Failed`), `Error`, event `LoadStateChanged`.
- Names: `Animations`, `NodeNames`, `MaterialNames`, `CurrentAnimation`.
- Playing: `Animation`, `IsPlaying`, `Speed`, `CrossfadeMs`, `AutoRotate` (degrees per second),
  `Play`, `PlayOnce`, `Hold`, `PlayLayer`, `StopLayer`, event `AnimationEnded`, `IsAnimating`.
- View: `Yaw`, `Pitch`, `Zoom`, `Orbit` (default true), `PinchZoom` (default false), `MinZoom` (0.5),
  `MaxZoom` (2.5), `ResetOnDoubleTap`, `LockPitch`, `TiltToPointer` (degrees, 0 = off), `ResetView()`,
  event `ViewChanged`.
- Looks: `Look` (`Look.Lit` / `Toon` / `Flat`), `OutlineWidth` (points), `OutlineColor` (black),
  `Antialias`.
- Nodes: `SetNodeVisible`, `SetNodeTranslation(name, x, y, z)`, `SetNodeRotation` (x, y, z degrees, or a
  `Quaternion` for turns worked out in code), `SetNodeScale`, `ResetNode`.
- Materials: `SetMaterialColor`, `SetMaterialImage(name, Texture3D)`, `SetMaterialRoughness(name,
  float?)` (null = the file's), `SetMaterialVisible`; fluent `.MaterialImage(name, texture)`.
- Taps: event `Tapped3D` with `ModelHit(Node, Material, Point)`, `HitTest(x, y)`,
  `ScreenPosition(Vector3)` returning `PointOnScreen(X, Y, Occluded)`.

`SkiaScene3D`:
- Models: `.Model(name, byte[] glb)` / `.Model(name, Model3D)` (chainable).
- Objects: `Add(name, model, x, y, size)`, `Remove`, `ClearObjects`, `SetPosition`, `SetFacing`, `SetSize`,
  `SetVisible`, `ObjectNames`, `Errors`.
- Playing: `Play(name, animation)`, `PlayOnce`, `Hold`, `SetSpeed`, `IsPlaying`, `SetPlaying`,
  `CurrentAnimation(obj)`, event `AnimationEnded` (`SceneAnimationEnded(Object, Animation)`).
- Materials: `SetMaterialColor(name, material, color)`, `SetMaterialImage(name, material, image)`.
- Looks: `Tilt` (degrees, default 15), `Look`, `OutlineWidth`, `OutlineColor`, `Antialias`.
- Taps: event `Tapped3D` with `SceneHit(Object, Node, Material)`, `HitTest(x, y)`.
- Fluent on both controls: `.OnAnimationEnded((me, name) => ...)`, `.OnTapped3D((me, hit) => ...)`.

Shapes: `Mesh3D.Lathe(profile, segments)`, `Cylinder`, `Sphere`, `Torus`, `Cuboid`, `Extrude(SKPath,
depth)`, `TriangleCount`; `new Model3D().Part(name, mesh, color).At(x, y, z).Turned(x, y, z)
.Scaled(x, y, z).Inside(parent)` (each applies to the last part). `Texture3D.Decode(bytes)`,
`Texture3D.FromImage(SKImage)`.

Desktop heads: draw with `RenderingMode = Accelerated` (the 3D pass uses the canvas's GPU context; a CPU canvas
shows no 3D). WPF needs nothing more (its ANGLE build comes with DrawnUi.Wpf). OpenTK: the window's OpenGL context
must be 3.3 core or newer (Linux 3.3, Windows 4.6, macOS 4.1 forward-compatible, as the OpenTK samples ask for).

Zoom with the wheel: Ctrl / Cmd + wheel and touchpad pinch arrive as a zoom wheel
(`WheelEventArgs.IsZoom` in AppoMobi.Gestures); a `SkiaScroll` offers such a wheel to the control
under the pointer first, so a viewer with `PinchZoom` zooms while a plain wheel scrolls the page.

## DrawnUI for React

The .NET names, as component props; methods through a ref.

```tsx
import { SkiaModel3D } from "drawnui-react-add3d";

<SkiaModel3D
  Glb={avatarGlb}                     // the .glb file's bytes (keep the same array between renders)
  Animation="Idle"
  Yaw={25} Pitch={10}
  PinchZoom={true}
  ResetOnDoubleTap={true}
  HorizontalOptions="Fill"
  HeightRequest={320}
  Tapped3D={(me, hit) => me.PlayOnce("Wave")}
/>
```

- Sources: `Glb` (bytes), `Source` (an address, with `LoadState` / `LoadStateChanged`), `Model` (a `Model3D` made in
  code).
- `SkiaScene3D` for many objects in one pass, driven through its ref (`Add`, `SetPosition`, `Play`, ...).
- Classes (`Model3D`, `Mesh3D`, `Texture3D`, `Look`, `LoadState3D`) also come from `drawnui-react-add3d/core`.
