# DrawnUI Blazor Boot Loader

Replaces the default Blazor SVG spinner with a branded animated loading screen.  
Shipped inside `DrawnUi.Blazor.Core` — no extra package needed.

---

## Files (served at `_content/DrawnUi.Blazor.Core/`)

| File | Description |
|---|---|
| `drawnui-loader.css` | All `.boot-*` styles and animations |
| `drawnui-logo.svg` | Gray DrawnUI logo — for dark backgrounds |
| `drawnui-logo-black.svg` | Black DrawnUI logo — for light backgrounds |

---

## Setup

### 1. `wwwroot/index.html` — add to `<head>`

```html
<link rel="stylesheet" href="_content/DrawnUi.Blazor.Core/drawnui-loader.css" />
```

Optional — use the DrawnUI logo as favicon:

```html
<link rel="icon" type="image/svg+xml" href="_content/DrawnUi.Blazor.Core/drawnui-logo.svg" />
```

### 2. `wwwroot/index.html` — replace `<div id="app">` content

```html
<div id="app">
    <div class="boot-shell" aria-label="Loading MyApp">
        <div class="boot-stage">
            <div class="boot-logo-wrap">
                <img class="boot-logo" src="_content/DrawnUi.Blazor.Core/drawnui-logo.svg" alt="DrawnUI" />
                <span class="boot-spark" style="--a:15deg;--r:6.8rem;--du:2.1s;--de:0s;--c:#4b8ef9"></span>
                <span class="boot-spark" style="--a:72deg;--r:6.4rem;--du:1.8s;--de:0.38s;--c:#f25536"></span>
                <span class="boot-spark" style="--a:138deg;--r:7rem;--du:2.4s;--de:0.72s;--c:#e4a130"></span>
                <span class="boot-spark" style="--a:195deg;--r:6.2rem;--du:1.9s;--de:1.08s;--c:#78549e"></span>
                <span class="boot-spark" style="--a:252deg;--r:6.8rem;--du:2.2s;--de:1.44s;--c:#4b8ef9"></span>
                <span class="boot-spark" style="--a:315deg;--r:6rem;--du:1.7s;--de:0.56s;--c:#f25536"></span>
                <span class="boot-spark" style="--a:42deg;--r:7.4rem;--du:2.6s;--de:0.92s;--c:#e4a130"></span>
                <span class="boot-spark" style="--a:172deg;--r:7.6rem;--du:2s;--de:1.6s;--c:#78549e"></span>
            </div>
            <div class="boot-foot">
                <div class="boot-copy">My App Name</div>
                <div class="boot-progress" aria-hidden="true">
                    <div class="boot-progress-track">
                        <div class="boot-progress-fill"></div>
                    </div>
                    <div class="boot-progress-text"></div>
                </div>
            </div>
        </div>
    </div>
</div>
```

Change `My App Name` and the `aria-label` to match your app.

### 3. `wwwroot/css/app.css` — set background to match your app

`.boot-shell` uses `min-height: 100svh` so it centers correctly regardless of whether the parent `#app` has an explicit height. It is transparent — it inherits the page background.

Set `html/body` background to your app's theme color so there is no flash before CSS is parsed:

```css
html, body {
    background: #090b10; /* dark app — adjust to your theme */
}
```

Light-themed apps can skip this or use their own color; the loader will pick up whatever background is set.

---

## Progress bar

The progress fill reads `--blazor-load-percentage` and the text reads  
`--blazor-load-percentage-text` — both set automatically by Blazor during WASM download.  
No JS needed.

To preview the loader at a specific progress without running the app, open:

```
http://localhost:PORT/?loader-preview&progress=65&label=Loading+assets
```

This requires the preview JS block from the Breakout `index.html` (optional, copy as needed).

---

## Custom logo

Replace the `<img>` `src` with your own asset:

```html
<img class="boot-logo" src="images/my-logo.svg" alt="My App" />
```

---

## Spark colors

Each `<span class="boot-spark">` takes four CSS custom properties:

| Property | Description |
|---|---|
| `--a` | angle on the orbit ring (deg) |
| `--r` | orbit radius (rem) |
| `--du` | animation duration |
| `--de` | animation delay (stagger) |
| `--c` | color (hex or any CSS color) |

Add, remove, or recolor sparks freely.

---

## Reference implementations

- `src/Blazor/Samples/BlazorSandbox/wwwroot/index.html` — sandbox example
- `src/Web/Breakout.Web/wwwroot/index.html` — game app example (includes loader-preview JS)
