# Image Controls

DrawnUI provides powerful image controls for high-performance image rendering with advanced features like effects, transformations, and sophisticated caching. This article covers the image components available in the framework.

## SkiaImage

SkiaImage is the core image control in DrawnUI, providing efficient image loading, rendering, and manipulation capabilities with direct SkiaSharp rendering. It supports multiple image sources, advanced rescaling algorithms, built-in effects, and comprehensive caching strategies.

### Basic Usage

```xml
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectCover"
    HorizontalOptions="Center"
    VerticalOptions="Center"
    WidthRequest="200"
    HeightRequest="200" />
```

> **Note:** The default `Aspect` is `AspectCover`, which maintains aspect ratio while filling the entire space.

### Key Properties

#### Core Properties
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | ImageSource | null | Source of the image (URL, file, resource, stream) |
| `Aspect` | TransformAspect | AspectCover | How the image scales to fit (AspectFit, AspectFill, etc.) |
| `HorizontalAlignment` | DrawImageAlignment | Center | Horizontal positioning of the image |
| `VerticalAlignment` | DrawImageAlignment | Center | Vertical positioning of the image |
| `UseAssembly` | object | null | An `Assembly` or an assembly name. A plain file path in `Source` then loads as an embedded resource of that assembly, see [Loading from Different Sources](#loading-from-different-sources) |

#### Loading & Performance
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LoadSourceOnFirstDraw` | bool | false | Whether to defer loading until first render |
| `PreviewBase64` | string | empty | Base64 encoded preview image to show while loading (plain base64, no `data:` prefix) |
| `RescalingQuality` | FilterQuality | Low | Quality of image rescaling (None, Low, Medium, High, Ultra) |
| `EraseChangedContent` | bool | false | Erase existing image when new source is set but not loaded yet |
| `DrawWhenEmpty` | bool | true | Whether to draw when no source is set |

#### Effects & Adjustments
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AddEffect` | SkiaImageEffect | None | Built-in image effect (None, Sepia, Tint, BlackAndWhite, etc.) |
| `ColorTint` | Color | Transparent | Tint color for image effect |
| `EffectBlendMode` | SKBlendMode | SrcIn | Blend mode for effects |
| `Brightness` | double | 1.0 | Adjusts image brightness (≥1.0) |
| `Contrast` | double | 1.0 | Adjusts image contrast (≥1.0) |
| `Saturation` | double | 0.0 | Adjusts image saturation (≥0) |
| `Blur` | double | 0.0 | Applies blur effect |
| `Gamma` | double | 1.0 | Adjusts gamma (≥0) |
| `Darken` | double | 5.0 | Darkens the image |
| `Lighten` | double | 5.0 | Lightens the image |

`Brightness`, `Contrast`, `Saturation`, `Gamma`, `Darken` and `Lighten` are read only by the matching `AddEffect` value. `Blur` works on its own.

#### Transformations
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ZoomX`/`ZoomY` | double | 1.0 | Zoom/scaling factors |
| `HorizontalOffset`/`VerticalOffset` | double | 0.0 | Offset for image position |

#### Sprite Sheets
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SpriteWidth`/`SpriteHeight` | double | 0.0 | Size of one cell of a sprite sheet, in source pixels. When both are above 0 the image shows one cell |
| `SpriteIndex` | int | -1 | Cell to show, counted from 0 left to right, then top to bottom. Outside the sheet (the default -1 too) nothing is drawn |

#### Gradient Overlay
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseGradient` | bool | false | Colors the image with a top to bottom gradient, keeping its transparency |
| `StartColor` | Color | DarkGray | Gradient color at the top |
| `EndColor` | Color | Gray | Gradient color at the bottom |

#### State Properties (Read-only)
| Property | Type | Description |
|----------|------|-------------|
| `LoadedSource` | LoadedImageSource | Currently loaded image source |
| `IsLoading` | bool | Whether image is currently loading |
| `HasError` | bool | Whether last load attempt failed |

> **Note:** Caching is handled by the SkiaControl base class. You can set `UseCache` on SkiaImage for caching strategies (e.g., `UseCache="Image"`).

> **Note:** The `VisualEffects` property is inherited from SkiaControl. You can use `<draw:SkiaControl.VisualEffects>` in XAML to apply effects like drop shadow or color presets.

### Aspect Modes

The `Aspect` property controls how the image is sized and positioned within its container. This is a critical property for ensuring that your images display correctly while maintaining their proportions when appropriate.

#### Available Aspect Modes

| Aspect Mode | Description | Visual Effect |
|-------------|-------------|---------------|
| `None` | No scaling or positioning | Image displayed at original size |
| `Fill` | Enlarges to fill the viewport without maintaining aspect ratio if smaller, but does not scale down if larger | May distort proportions |
| `Fit` | Fit without maintaining aspect ratio and without enlarging if smaller | May distort proportions |
| `AspectFit` | Fit inside viewport respecting aspect, scaling up or down as needed | May leave empty space around |
| `AspectFill` | Covers viewport respecting aspect without scaling down if bigger | May crop portions of the image |
| `FitFill` | Enlarges to fill the viewport if smaller and reduces size if larger, all without respecting aspect ratio | May distort proportions |
| `AspectFitFill` | Enlarges to fit the viewport if smaller and reduces size if larger, all while respecting aspect ratio | Maintains proportions |
| `Cover` | Stretches to the exact viewport size, scaling up or down, without respecting aspect ratio | May distort proportions |
| `AspectCover` | **Default.** Covers viewport respecting aspect, scales both up and down as needed | May crop portions, maintains aspect |
| `Tile` | Repeats the image at its natural size across the whole control | Pattern starts from one copy placed by the alignment |

#### Rescaling Quality

The `RescalingQuality` property (`FilterQuality`, default `Low`) sets the sampling used when the image is drawn at another size:

| Quality | Sampling |
|---------|----------|
| `None` | Nearest neighbor. Fastest, right for 1:1 drawing and pixel art |
| `Low` | Linear filtering. **Default** |
| `Medium` | Linear filtering with nearest mipmaps |
| `High` | Linear filtering with linear mipmaps, smooth when scaling down a lot |
| `Ultra` | Cubic (Mitchell) when scaling up, same as `High` when scaling down |

With any quality above `None`, `CacheRescaledSource` (default `true`) keeps a rescaled copy of the source instead of resampling on every draw. Set it to `false` for sources that change every frame, like a camera feed.

#### Examples and Visual Guide

```xml
<!-- Maintain aspect ratio, fit within bounds -->
<draw:SkiaImage Source="image.png" Aspect="AspectFit" />
```
This ensures the entire image is visible, possibly with letterboxing (empty space) on the sides or top/bottom.

```xml
<!-- Maintain aspect ratio, fill bounds (may crop) - DEFAULT -->
<draw:SkiaImage Source="image.png" Aspect="AspectCover" />
```
This fills the entire control with the image, possibly cropping parts that don't fit. Great for background images or thumbnails. This is the default behavior.

```xml
<!-- Stretch to fill bounds (may distort) -->
<draw:SkiaImage Source="image.png" Aspect="Fill" />
```
This stretches the image to fill the control exactly, potentially distorting the image proportions.

```xml
<!-- Tile the image at its natural size -->
<draw:SkiaImage Source="pattern.png" Aspect="Tile" />
```
This repeats the image to fill the entire control. Perfect for background patterns. Like `None`, the natural size is one source pixel per screen pixel: a 64 px image repeats every 32 points at a rendering scale of 2. The pattern starts from one copy placed by `HorizontalAlignment` and `VerticalAlignment` (centered by default) and runs out from it in every direction. `ZoomX`/`ZoomY` and the offsets move and scale that copy, and with it the whole pattern.

```xml
<!-- Tiles of a size you choose, each tile drawn with its own aspect -->
<draw:SkiaImageTiles Source="pattern.png" TileWidth="64" TileHeight="64" />
```
`SkiaImageTiles` sets the tile size in points and draws the image into each tile with `TileAspect`.

```xml
<!-- High-quality rescaling for photos -->
<draw:SkiaImage
    Source="photo.jpg"
    Aspect="AspectCover"
    RescalingQuality="High" />
```
This uses mipmaps, so a large photo stays smooth when it is drawn much smaller.

#### Combining Aspect and Alignment

You can combine `Aspect` with `HorizontalAlignment` and `VerticalAlignment` for precise control:

```xml
<!-- AspectFit with custom alignment -->
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectFit"
    HorizontalAlignment="Start"
    VerticalAlignment="End" />
```
This would fit the image within bounds while aligning it to the bottom-left corner of the available space.

#### Choosing the Right Aspect Mode

- **For user photos or content images**: `AspectFit` ensures the entire image is visible
- **For backgrounds or covers**: `AspectCover` (default) ensures no empty space is visible
- **For thumbnails and cards**: `AspectCover` provides consistent sizing
- **For patterns and textures**: `Tile` repeats the image at its natural size; `SkiaImageTiles` repeats it at a tile size you choose
- **For icons that need exact sizing**: `Fill` stretches to exact dimensions
- **For pixel-perfect graphics**: `None` maintains original size and quality

### Image Alignment

Control the alignment of the image within its container:

```xml
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectFit"
    HorizontalAlignment="End"
    VerticalAlignment="Start" />
```

This will position the image at the top-right of its container.

### Image Effects

SkiaImage supports various built-in effects through the `AddEffect` property. Effects can be combined with blend modes for advanced visual results.

#### Available Effects

| Effect | Description | Additional Properties |
|--------|-------------|----------------------|
| `None` | No effect applied | - |
| `BlackAndWhite` | Converts to grayscale (NTSC weights 0.2989, 0.587, 0.114) | - |
| `Grayscale` | Converts to grayscale (weights 0.21, 0.72, 0.07) | - |
| `Pastel` | Applies pastel color effect | - |
| `Tint` | Applies color tint | `ColorTint`, `EffectBlendMode` |
| `Darken` | Darkens the image | `Darken` (amount) |
| `Lighten` | Lightens the image | `Lighten` (amount) |
| `Sepia` | Applies sepia tone | - |
| `InvertColors` | Inverts all colors | - |
| `Contrast` | Adjusts contrast | `Contrast` (≥1.0) |
| `Saturation` | Adjusts saturation | `Saturation` (≥0) |
| `Brightness` | Adjusts brightness | `Brightness` (≥1.0) |
| `Gamma` | Adjusts gamma correction | `Gamma` (≥0) |
| `TSL` | Tint with Saturation and Lightness | `BackgroundColor`, `Saturation`, `Brightness`, `EffectBlendMode` |
| `HSL` | Hue, Saturation, Lightness adjustment | `Gamma` (hue), `Saturation`, `Brightness`, `EffectBlendMode`; applies only when `BackgroundColor` is set |
| `Custom` | Use custom effects via VisualEffects | - |

#### Basic Effects Examples

```xml
<!-- Apply a sepia effect -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="Sepia" />

<!-- Apply a tint effect with custom blend mode -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="Tint"
    ColorTint="Red"
    EffectBlendMode="Multiply" />

<!-- Apply grayscale effect -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="BlackAndWhite" />

<!-- Invert colors -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="InvertColors" />
```

### Image Adjustments

Fine-tune image appearance with adjustment properties. `Blur` works on its own; `Brightness`, `Contrast`, `Saturation` and `Gamma` take effect only with the matching `AddEffect` value, one at a time:

```xml
<draw:SkiaImage
    Source="image.png"
    AddEffect="Brightness"
    Brightness="1.2"
    Blur="2" />
```

#### Advanced Effect Combinations

```xml
<!-- HSL effect with custom values -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="HSL"
    BackgroundColor="Blue"
    Gamma="0.8"
    Saturation="1.2"
    Brightness="1.1"
    EffectBlendMode="Overlay" />

<!-- TSL effect with background color -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="TSL"
    BackgroundColor="Blue"
    Saturation="0.7"
    Brightness="1.3"
    EffectBlendMode="SoftLight" />
```

### Advanced Effects

For more complex effects, use the VisualEffects collection:

```xml
<draw:SkiaImage Source="image.png">
    <draw:SkiaControl.VisualEffects>
        <draw:DropShadowEffect
            Blur="8"
            X="2"
            Y="2"
            Color="#80000000" />
        <draw:ChainColorPresetEffect Preset="Sepia" />
    </draw:SkiaControl.VisualEffects>
</draw:SkiaImage>
```

### Gradient Tint

With `UseGradient="True"` the image is painted with a vertical gradient from `StartColor` at the top to `EndColor` at the bottom. The image keeps only its transparency, so this is made for icons and shapes on a transparent background:

```xml
<draw:SkiaImage
    Source="icon.png"
    Aspect="AspectFit"
    UseGradient="True"
    StartColor="Orange"
    EndColor="Red" />
```

The gradient runs over the visible part of the image: the icon itself with `AspectFit`, the control with a cropping aspect, the whole control with `Tile`. `AddEffect` filters still apply on top of it. To fade a photo instead, put a layer with a `FillGradient` over it (see [Gradients](../advanced/gradients.md)).

### Sprite Sheets

To show one cell of a sprite sheet, give the cell size in source pixels and the cell index:

```xml
<!-- 64x64 cells, the 6th one -->
<draw:SkiaImage
    Source="sheet.png"
    SpriteWidth="64"
    SpriteHeight="64"
    SpriteIndex="5"
    Aspect="AspectFit" />
```

Cells are numbered from 0, left to right, then top to bottom. Partial cells at the right and bottom edges do not count. The cell is laid out as if it were the whole image: `Aspect`, alignment, zoom and auto-size all use the cell size, and `Aspect="Tile"` repeats the cell. An index outside the sheet, the default -1 included, draws nothing. Neighbor cells never bleed in at the cell edges, whatever `RescalingQuality` is. To animate the frames of a sheet use `SkiaSprite`, see [Sprite Controls](sprites.md).

### Preview Images

Show a low-resolution placeholder while loading the main image:

```xml
<DrawUi:SkiaImage
    Source="https://example.com/large-image.jpg"
    PreviewBase64="iVBORw0KGgoAA..."
    Aspect="AspectFit" />
```

### Loading Options

Control how and when images are loaded:

```xml
<!-- Immediate loading (default) -->
<draw:SkiaImage
    Source="image.png"
    LoadSourceOnFirstDraw="False" />

<!-- Deferred loading (load when first rendered) -->
<draw:SkiaImage
    Source="image.png"
    LoadSourceOnFirstDraw="True" />

<!-- Erase content when source changes -->
<draw:SkiaImage
    Source="{Binding ImageUrl}"
    EraseChangedContent="True" />
```

#### Loading from Different Sources

```xml
<!-- From URL -->
<draw:SkiaImage Source="https://example.com/image.jpg" />

<!-- From file -->
<draw:SkiaImage Source="Images/local-image.png" />

<!-- From embedded resource: resource://<path inside the assembly>?assembly=<assembly name> -->
<draw:SkiaImage Source="resource://Images.embedded-image.png?assembly=MyApp" />

<!-- Same embedded resource with a plain path: UseAssembly takes an assembly name or an Assembly -->
<draw:SkiaImage UseAssembly="MyApp" Source="Images/embedded-image.png" />

<!-- From stream (in code-behind) -->
```

```csharp
// Load from stream
myImage.SetSource(async (cancellationToken) =>
{
    var stream = await GetImageStreamAsync();
    return stream;
});
```

With `UseAssembly`, the folders of the plain path become dots, so `Images/embedded-image.png` in `MyApp` loads the resource `MyApp.Images.embedded-image.png` (the default name of a file marked `EmbeddedResource`, as long as the root namespace is the assembly name). Urls and `resource://` sources are not affected. Set `UseAssembly` before `Source`, or the image loads a second time. In code you can pass the assembly itself: `UseAssembly = typeof(App).Assembly`.

### Caching Strategies

Optimize performance with various caching options:

```xml
<!-- Use double-buffered image caching (good for changing content) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="ImageDoubleBuffered" />

<!-- Use simple image caching (good for static content) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="Image" />

<!-- Cache drawing operations rather than bitmap (memory efficient) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="Operations" />

<!-- No caching (for frequently changing images) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="None" />
```

### Handling Load Events

You can respond to image load success or failure in code-behind:

```csharp
public MainPage()
{
    InitializeComponent();

    MyImage.Success += (sender, e) => {
        // Image loaded successfully
        Console.WriteLine($"Loaded: {e.Content}");
    };

    MyImage.Error += (sender, e) => {
        // Image failed to load
        Console.WriteLine($"Failed to load: {e.Content}");
    };

    MyImage.Cleared += (sender, e) => {
        // Image was cleared/unloaded
    };
}
```

#### Monitoring Load State

`IsLoading` and `HasError` are plain properties, not bindable ones, so XAML cannot bind to them. They raise `PropertyChanged`, so read or observe them from code (see [Working with Image Sources](#working-with-image-sources)).

#### Manual Loading Control

```csharp
// Stop current loading
myImage.StopLoading();

// Reload the current source
myImage.ReloadSource();

// Clear the current image
myImage.ClearBitmap();
```

## Image Management

### SkiaImageManager

DrawnUI includes a powerful image management system through the `SkiaImageManager` class. This provides centralized image loading, caching, and resource management.

#### Preloading Images

Preload images to ensure they're ready when needed:

```csharp
// Preload a single image
await SkiaImageManager.Instance.PreloadImage("Images/my-image.jpg");

// Preload multiple images
await SkiaImageManager.Instance.PreloadImages(new List<string> 
{
    "Images/image1.jpg",
    "Images/image2.jpg",
    "Images/image3.jpg"
});

// Same, with a priority: Low waits behind the images on screen (Normal), High goes first
await SkiaImageManager.Instance.PreloadImages(urls, LoadPriority.Low);
```

Network images load `SkiaImageManager.MaxParallelLoads` at a time and the others wait in line by priority; files on disk or in the app package load at once. `RunningCount` and `QueuedCount` tell how many network loads run and wait right now, and `RemoveFromCache(url)` drops one image so the next load reads it again.

#### Managing Memory Usage

Configure the image manager for optimal memory usage:

```csharp
// Enable bitmap reuse for better memory usage
SkiaImageManager.ReuseBitmaps = true;

// Set cache longevity (in seconds)
SkiaImageManager.CacheLongevitySecs = 1800; // 30 minutes

// Enable async loading for local images
SkiaImageManager.LoadLocalAsync = true;

// Drop one image from the cache
SkiaImageManager.Instance.RemoveFromCache("https://example.com/image.jpg");

// Add image to cache manually
SkiaImageManager.Instance.AddToCache("my-key", bitmap, 3600); // 1 hour

// Get image from cache
var cachedBitmap = SkiaImageManager.Instance.GetFromCache("my-key");
```

## Advanced Usage

### Loading from Base64

Load images directly from base64 strings:

```csharp
var base64String = "iVBORw0KGgoAA..."; // plain base64, no "data:" prefix
myImage.SetFromBase64(base64String);
```

### Applying Transformations

Apply transformations to the displayed image:

```xml
<DrawUi:SkiaImage
    Source="image.png"
    ZoomX="1.2"
    ZoomY="1.2"
    HorizontalOffset="10"
    VerticalOffset="-5" />
```

### Creating Images in Code

Create and configure SkiaImage controls programmatically:

```csharp
var image = new SkiaImage
{
    Source = "Images/my-image.jpg",
    LoadSourceOnFirstDraw = false,
    Aspect = TransformAspect.AspectCover,
    RescalingQuality = FilterQuality.Medium,
    AddEffect = SkiaImageEffect.Sepia,
    ColorTint = Colors.Brown,
    EffectBlendMode = SKBlendMode.Multiply,
    Brightness = 1.1,
    Contrast = 1.05,
    WidthRequest = 200,
    HeightRequest = 200,
    HorizontalOptions = LayoutOptions.Center,
    VerticalOptions = LayoutOptions.Center
};

// Subscribe to events
image.Success += (s, e) => Console.WriteLine("Image loaded");
image.Error += (s, e) => Console.WriteLine("Image failed to load");

myLayout.AddSubView(image); // the layout is already on screen
```

#### Advanced Programmatic Usage

```csharp
// Large hero photo, smooth when drawn much smaller
var heroImage = new SkiaImage
{
    Source = "hero-background.jpg",
    Aspect = TransformAspect.AspectCover,
    RescalingQuality = FilterQuality.High
};

// Sprite sheet animation: 8 frames in one row, see Sprite Controls
var sprite = new SkiaSprite
{
    Source = "character-sprites.png",
    Columns = 8,
    Rows = 1,
    FramesPerSecond = 10,
    Repeat = -1
};
```

## Performance Considerations

### Optimization Tips

1. **Image Size**
   - Resize images to their display size before including in your app
   - Use compressed formats (WebP, optimized PNG/JPEG) when possible
   - Consider providing different image sizes for different screen densities

2. **Caching**
   - Use `UseCache="Image"` for static images that don't change
   - Use `UseCache="ImageDoubleBuffered"` for images that change occasionally
   - Use `UseCache="Operations"` for images with effects but static content
   - Use `UseCache="None"` only for frequently changing images
   - An image carrying a shader effect that samples `iImage1` needs an image-backed cache — `Image`, `ImageDoubleBuffered`, `GPU` or `ImageComposite`. With `Operations` the cache is a picture and holds no image, so the effect snapshots the canvas instead of reading the image

3. **Loading Strategy**
   - Use `LoadSourceOnFirstDraw="True"` for off-screen images
   - Preload important images with SkiaImageManager.PreloadImages()
   - Provide preview images with `PreviewBase64` for large remote images

4. **Rendering Quality**
   - Set appropriate `RescalingQuality` based on your needs:
     - `None`: Fastest, nearest neighbor; right for pixel art and 1:1 drawing
     - `Low`: Linear filtering, good for scrolling content (default)
     - `Medium`: Linear with nearest mipmaps
     - `High`: Linear with mipmaps, smooth when scaling down a lot
     - `Ultra`: Cubic when scaling up, slowest (use sparingly)

5. **Memory Management**
   - Enable bitmap reuse with `SkiaImageManager.ReuseBitmaps = true`
   - Set reasonable cache longevity with `SkiaImageManager.CacheLongevitySecs`
   - Call `SkiaImageManager.Instance.RemoveFromCache(url)` for images you no longer need
   - Use `EraseChangedContent="True"` for dynamic image sources

### Examples of Optimized Image Loading

#### For Lists/Carousels

```xml
<draw:SkiaImage
    Source="{Binding ImageUrl}"
    LoadSourceOnFirstDraw="True"
    UseCache="ImageDoubleBuffered"
    RescalingQuality="Low"
    Aspect="AspectCover" />
```

#### For Hero/Cover Images

```xml
<draw:SkiaImage
    Source="{Binding CoverImage}"
    PreviewBase64="{Binding CoverImagePreview}"
    LoadSourceOnFirstDraw="False"
    UseCache="Image"
    RescalingQuality="Medium"
    Aspect="AspectCover" />
```

#### For Professional Photography

```xml
<draw:SkiaImage
    Source="{Binding HighResPhoto}"
    RescalingQuality="High"
    Aspect="AspectFit"
    UseCache="Image" />
```

#### For Icons and UI Graphics

```xml
<draw:SkiaImage
    Source="icon.png"
    RescalingQuality="None"
    Aspect="None"
    UseCache="Operations" />
```

#### For Image Galleries

```xml
<draw:SkiaScroll Orientation="Horizontal">
    <draw:SkiaLayout Type="Row" Spacing="10">
        <!-- Images that are initially visible -->
        <draw:SkiaImage
            Source="{Binding Images[0]}"
            LoadSourceOnFirstDraw="False"
            UseCache="Image"
            Aspect="AspectCover"
            WidthRequest="300"
            HeightRequest="200" />

        <!-- Images that may be scrolled to -->
        <draw:SkiaImage
            Source="{Binding Images[1]}"
            LoadSourceOnFirstDraw="True"
            UseCache="Image"
            Aspect="AspectCover"
            WidthRequest="300"
            HeightRequest="200" />

        <!-- More images... -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Advanced Features

### Custom Image Rendering

Get a rendered version of the image with all effects applied:

```csharp
// Get the image with all effects and transformations applied
var renderedImage = mySkiaImage.GetRenderedSource();
if (renderedImage != null)
{
    // Use the rendered image
    // Remember to dispose when done
    renderedImage.Dispose();
}
```

### Image Transformations and Offsets

```xml
<draw:SkiaImage
    Source="image.png"
    ZoomX="1.5"
    ZoomY="1.2"
    HorizontalOffset="20"
    VerticalOffset="-10"
    Aspect="AspectCover" />
```

### Working with Image Sources

```csharp
// Check if image is currently loading
if (myImage.IsLoading)
{
    // Show loading indicator
}

// Check for errors
if (myImage.HasError)
{
    // Show error state
}

// Access the loaded source
var loadedSource = myImage.LoadedSource;
if (loadedSource != null)
{
    var width = loadedSource.Width;
    var height = loadedSource.Height;
}
```

## Best Practices

### 1. Choose the Right Aspect Mode
- Use `AspectCover` (default) for most scenarios
- Use `AspectFit` when you need to see the entire image
- Use `SkiaImageTiles` for patterns and backgrounds
- Use `None` for pixel-perfect icons

### 2. Optimize Rescaling
- Keep the default `RescalingQuality="Low"` for general performance
- Use `High` for photos drawn much smaller than their source
- Use `None` for pixel art and 1:1 graphics
- Set `CacheRescaledSource="False"` for sources that change every frame

### 3. Manage Memory Efficiently
- Enable `SkiaImageManager.ReuseBitmaps = true` for shared images
- Set appropriate cache longevity with `CacheLongevitySecs`
- Use `EraseChangedContent="True"` for dynamic content
- Remove images you no longer need with `RemoveFromCache`

### 4. Handle Loading States
- Use `LoadSourceOnFirstDraw="True"` for off-screen images
- Provide preview images for large remote images
- Subscribe to `Success` and `Error` events for user feedback
- Monitor `IsLoading` and `HasError` properties

### 5. Apply Effects Wisely
- Use built-in effects for common adjustments
- Combine effects with appropriate blend modes
- Use `Custom` effect type with VisualEffects for complex scenarios
- Consider performance impact of multiple effects

This comprehensive guide covers all aspects of using SkiaImage in DrawnUI, from basic usage to advanced optimization techniques. The control provides powerful image handling capabilities while maintaining excellent performance through intelligent caching and rendering strategies.


## SkiaGif

SkiaGif is a dedicated control for displaying animated GIF files with playback control and optimization features.

### Basic Usage

```xml
<draw:SkiaGif
    Source="animation.gif"
    AutoPlay="True"
    Repeat="-1"
    WidthRequest="200"
    HeightRequest="200" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | string | empty | Path or URL of the GIF |
| `AutoPlay` | bool | true | Whether the animation starts when loaded |
| `Repeat` | int | 0 | Extra cycles after the first one, -1 loops forever |
| `SpeedRatio` | double | 1.0 | Playback speed: 0.5 is half speed (twice as long), 2 is double speed |
| `DefaultFrame` | int | 0 | Frame index shown when not playing, -1 is the last frame |
| `Aspect` | TransformAspect | AspectFitFill | How the frames scale to fit |
| `IsPlaying` | bool | - | Whether the animation is playing (read-only) |

Use `Start()` and `Stop()` to control playback from code. `Seek(ms)` jumps to a time position in milliseconds; to show a given frame while stopped, set `DefaultFrame`.

### Examples

```xml
<!-- Auto-playing GIF, looping -->
<draw:SkiaGif
    Source="loading.gif"
    AutoPlay="True"
    Repeat="-1" />

<!-- Started from code with Start(), plays 4 times -->
<draw:SkiaGif
    Source="animation.gif"
    AutoPlay="False"
    SpeedRatio="0.5"
    Repeat="3" />
```

## SkiaMediaImage

SkiaMediaImage (MAUI only) is a `SkiaImage` that also plays animations. When the source path contains `.gif` or `.webp`, it loads the frames and plays them in a loop. Any other source loads as a normal image. Its default cache is `ImageDoubleBuffered`.

### Basic Usage

```xml
<draw:SkiaMediaImage
    Source="{Binding MediaUrl}"
    WidthRequest="300"
    HeightRequest="200" />
```

### Key Properties

SkiaMediaImage adds no properties of its own: use the `SkiaImage` ones, such as `Source` and `Aspect`.

### Examples

```xml
<!-- Static image or looping animation, depending on the source -->
<draw:SkiaMediaImage
    Source="{Binding MediaSource}"
    Aspect="AspectCover" />
```