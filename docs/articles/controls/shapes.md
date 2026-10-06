# SkiaShape

SkiaShape is a versatile control for rendering various geometric shapes in DrawnUI. Unlike traditional shape controls, SkiaShape offers high-performance rendering through SkiaSharp while supporting advanced features like custom paths, shadows, gradients, and content hosting.

## Basic Usage

SkiaShape supports various shape types through its `Type` property:

```xml
<DrawUi:SkiaShape 
    Type="Rectangle" 
    WidthRequest="200" 
    HeightRequest="100"
    BackgroundColor="Blue" 
    StrokeColor="White" 
    StrokeWidth="2" 
    CornerRadius="10" />
```

## Shape Types

SkiaShape supports the following shape types:

- **Rectangle**: A basic rectangle, optionally with rounded corners
- **Circle**: A perfect circle that maintains 1:1 aspect ratio
- **Ellipse**: An oval shape that can have different width and height
- **Path**: A custom shape defined by SVG path data
- **Polygon**: A shape defined by a collection of points
- **Line**: A series of connected line segments
- **Arc**: A circular arc segment, set by `Value1` and `Value2`

## Common Properties

### Visual Properties

| Property | Type | Description |
|----------|------|-------------|
| `BackgroundColor` | Color | Fill color of the shape |
| `StrokeColor` | Color | Outline color of the shape |
| `StrokeWidth` | double | Width of the outline stroke |
| `CornerRadius` | CornerRadius | Rounded corner radius for rectangles, one value or one per corner |
| `StrokeCap` | SKStrokeCap | End cap style for lines (Round, Butt, Square), default Round |
| `StrokePath` | double[] | Dash pattern for creating dashed lines, `"5,5"` in XAML |
| `StrokeBlendMode` | SKBlendMode | Controls how strokes blend with underlying content |
| `ClipBackgroundColor` | bool | If true, creates a "hollow" shape with just shadows and strokes |

### Shape-Specific Properties

| Property | Type | Description |
|----------|------|-------------|
| `PathData` | string | SVG path data for Path type shapes |
| `Points` | IList\<SkiaPoint\> | Points for Polygon or Line shapes, relative to the shape size (0.0-1.0) |
| `SmoothPoints` | float | Level of smoothing for Polygon/Line shapes (0.0-1.0) |
| `Value1` | double | Start angle in degrees for Arc shapes |
| `Value2` | double | Sweep angle in degrees for Arc shapes |

## Advanced Features

### Shadow Effects

SkiaShape supports multiple shadows through the `Shadows` collection property:

```xml
<DrawUi:SkiaShape 
    Type="Rectangle" 
    BackgroundColor="White" 
    CornerRadius="20">
    <DrawUi:SkiaShape.Shadows>
        <DrawUi:SkiaShadow 
            Color="#80000000" 
            Blur="10" 
            Y="4" />
    </DrawUi:SkiaShape.Shadows>
</DrawUi:SkiaShape>
```

### Gradients

SkiaShape supports gradient fills via the `FillGradient` property (on every `SkiaControl`) and the `StrokeGradient` property (shapes only):

```xml
<DrawUi:SkiaShape Type="Rectangle">
    <DrawUi:SkiaShape.FillGradient>
        <DrawUi:SkiaGradient 
            Type="Linear" 
            StartXRatio="0" 
            StartYRatio="0" 
            EndXRatio="1" 
            EndYRatio="1">
            <DrawUi:SkiaGradient.Colors>
                <Color>Red</Color>
                <Color>Blue</Color>
            </DrawUi:SkiaGradient.Colors>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.FillGradient>
</DrawUi:SkiaShape>
```

### Custom Paths

For complex shapes, you can use SVG path data:

```xml
<DrawUi:SkiaShape 
    Type="Path" 
    PathData="M0,0L15.825,8.0 31.65,15.99 15.82,23.99 0,32 0,15.99z" 
    BackgroundColor="Red" />
```

The PathData property follows standard SVG path notation:
- M: Move to (absolute)
- m: Move to (relative)
- L: Line to (absolute)
- l: Line to (relative)
- H/h: Horizontal line
- V/v: Vertical line
- C/c: Cubic bezier curve
- S/s: Smooth cubic bezier
- Q/q: Quadratic bezier curve
- T/t: Smooth quadratic bezier
- A/a: Elliptical arc
- Z/z: Close path

### As a Content Container

SkiaShape can function as a container, clipping child elements to its shape boundaries:

```xml
<DrawUi:SkiaShape 
    Type="Circle" 
    BackgroundColor="Green" 
    WidthRequest="200" 
    HeightRequest="200">
    <DrawUi:SkiaImage 
        Source="background.jpg" 
        VerticalOptions="Fill" 
        HorizontalOptions="Fill" />
    <DrawUi:SkiaLabel 
        Text="Circular Content" 
        TextColor="White" 
        HorizontalOptions="Center" 
        VerticalOptions="Center" />
</DrawUi:SkiaShape>
```

Children are placed on top of each other, like in an absolute layout: `Type` on a shape selects the shape, not the layout. To arrange children in a column or row, put a `SkiaLayout` inside the shape.

## Creating Polygons

For polygon shapes, you can define points in various ways. Point coordinates are always relative (0.0-1.0) to the shape's size.

### Using SkiaPoint Collection

```xml
<DrawUi:SkiaShape 
    Type="Polygon" 
    BackgroundColor="Purple">
    <DrawUi:SkiaShape.Points>
        <DrawUi:SkiaPoint X="0" Y="0" />
        <DrawUi:SkiaPoint X="1" Y="0" />
        <DrawUi:SkiaPoint X="1" Y="1" />
        <DrawUi:SkiaPoint X="0" Y="1" />
    </DrawUi:SkiaShape.Points>
</DrawUi:SkiaShape>
```

### Using Relative Coordinates

Relative coordinates (0.0-1.0) automatically scale to the shape's dimensions:

```xml
<DrawUi:SkiaShape 
    Type="Polygon" 
    BackgroundColor="CornflowerBlue">
    <DrawUi:SkiaShape.Points>
        <DrawUi:SkiaPoint X="0.0" Y="0.8" />
        <DrawUi:SkiaPoint X="0.0" Y="0.7" />
        <DrawUi:SkiaPoint X="1.0" Y="0.2" />
        <DrawUi:SkiaPoint X="1.0" Y="0.3" />
    </DrawUi:SkiaShape.Points>
</DrawUi:SkiaShape>
```

### Using String Definition

You can also use a converter for inline point definitions. Points are separated with `;`:

```xml
<DrawUi:SkiaShape 
    Type="Polygon" 
    BackgroundColor="Purple"
    Points="0,0; 1,0; 1,1; 0,1" />
```

### Predefined Shapes

SkiaShape provides predefined point collections for common shapes:

```xml
<DrawUi:SkiaShape 
    Type="Polygon" 
    BackgroundColor="Yellow"
    Points="{x:Static DrawUi:SkiaShape.PolygonStar}" />
```

### Smooth Curves

For smoother, curved polygons, adjust the `SmoothPoints` property (0.0-1.0):

```xml
<DrawUi:SkiaShape 
    Type="Polygon" 
    BackgroundColor="#220000FF" 
    SmoothPoints="0.9"
    Points="0.0,0.8; 0.0,0.7; 1.0,0.2; 1.0,0.3" />
```

A value of 0 creates sharp corners, while a value of 1.0 creates maximally smooth curves.

## Creating Lines

Lines can be created using the same point collection approach:

```xml
<DrawUi:SkiaShape 
    Type="Line" 
    StrokeColor="Black" 
    StrokeWidth="2"
    Points="0,0; 0.33,1; 0.66,0; 1,1" />
```

Customize line appearance with:
- `StrokeCap`: Controls how line ends appear
- `StrokePath`: Define dash patterns ("5,5" creates 5-point dashes with 5-point gaps)

## Practical Examples

### Card with Shadow

```xml
<DrawUi:SkiaShape 
    Type="Rectangle" 
    BackgroundColor="White" 
    CornerRadius="12" 
    Padding="16"
    WidthRequest="300" 
    HeightRequest="150">
    
    <DrawUi:SkiaShape.Shadows>
        <DrawUi:SkiaShadow 
            Color="#22000000" 
            Blur="20" 
            Y="4" />
    </DrawUi:SkiaShape.Shadows>
    
    <DrawUi:SkiaLayout Type="Column" HorizontalOptions="Fill">
        <DrawUi:SkiaLabel 
            Text="Card Title" 
            FontSize="18" 
            FontAttributes="Bold" />
        
        <DrawUi:SkiaLabel 
            Text="This is a card with rounded corners and a shadow effect. SkiaShape makes it easy to create modern UI components." 
            TextColor="#666666" 
            Margin="0,10,0,0" />
    </DrawUi:SkiaLayout>
</DrawUi:SkiaShape>
```

### Progress Indicator

```xml
<DrawUi:SkiaShape 
    Type="Arc" 
    StrokeColor="#EEEEEE" 
    StrokeWidth="10" 
    BackgroundColor="Transparent"
    Value1="0" 
    Value2="360" 
    WidthRequest="100" 
    HeightRequest="100">
    
    <DrawUi:SkiaShape 
        Type="Arc" 
        StrokeColor="Blue" 
        StrokeWidth="10" 
        BackgroundColor="Transparent"
        Value1="0" 
        Value2="{Binding Progress}" 
        WidthRequest="100" 
        HeightRequest="100" />
    
    <DrawUi:SkiaLabel 
        Text="{Binding ProgressText}" 
        HorizontalOptions="Center" 
        VerticalOptions="Center" />
</DrawUi:SkiaShape>
```

### Custom Button

```xml
<DrawUi:SkiaShape 
    Type="Path" 
    PathData="M10,0 L90,0 C95,0 100,5 100,10 L100,40 C100,45 95,50 90,50 L10,50 C5,50 0,45 0,40 L0,10 C0,5 5,0 10,0 Z" 
    BackgroundColor="Blue" 
    WidthRequest="100" 
    HeightRequest="50">
    
    <DrawUi:SkiaLabel 
        Text="SUBMIT" 
        TextColor="White" 
        FontAttributes="Bold"
        HorizontalOptions="Center" 
        VerticalOptions="Center" />

    <!-- Tap area on top of the content -->
    <DrawUi:SkiaHotspot CommandTapped="{Binding ButtonCommand}" />
</DrawUi:SkiaShape>
```

## Performance Considerations

- For static shapes, set `UseCache="Image"` to render once and cache as bitmap
- For frequently animated shapes, use `UseCache="Operations"` for best performance
- Avoid excessive shadows or complex paths in performance-critical UI
- For very complex paths, pre-process SVG data when possible rather than computing at runtime

## SkiaHoverMask

`SkiaHoverMask` is a control deriving from SkiaShape that can be used to create hover effects. Think of it as an inverted shape: it paints its parent's whole area with its `BackgroundColor` (or `FillGradient`) and leaves a hole in its own shape. It has no hover logic of its own: show or hide it from your hover or selection handling.

### Basic Usage

```xml
<draw:SkiaHoverMask
    Type="Rectangle"
    CornerRadius="8"
    BackgroundColor="#40000000"
    WidthRequest="200"
    HeightRequest="100" />
```

### Properties

`SkiaHoverMask` adds no properties of its own. The mask uses `BackgroundColor` or `FillGradient`, and the hole uses the shape properties (`Type`, `CornerRadius` and the rest).

## Platform Specific Notes

SkiaShape is drawn by the shared engine, so it looks the same on every DrawnUI head: MAUI (Android, iOS, Mac Catalyst, Windows), WPF, OpenTK, Blazor and WebAssembly.

---

# SkiaSvg

SkiaSvg is a specialized control for rendering SVG (Scalable Vector Graphics) files with high performance and quality. As a vector graphics control, it shares many characteristics with SkiaShape but is specifically designed for displaying complex SVG artwork, icons, and illustrations.

## Why SVG in DrawnUi?

SVG (Scalable Vector Graphics) offers several advantages for modern mobile applications:
- **Resolution Independence**: SVGs scale perfectly to any size without pixelation
- **Small File Sizes**: Vector data is typically much smaller than equivalent raster images
- **Dynamic Styling**: SVG elements can be styled, tinted, and modified at runtime
- **Performance**: Hardware-accelerated vector rendering through SkiaSharp
- **Accessibility**: SVG content can include semantic information

## Basic Usage

```xml
<draw:SkiaSvg
    Source="icon.svg"
    TintColor="Blue"
    WidthRequest="64"
    HeightRequest="64" />
```

## Key Properties

### Core Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | string | empty | Path to the SVG file (local or web URL) |
| `SvgString` | string | empty | SVG markup to draw, instead of a file |
| `TintColor` | Color | Transparent | Color to tint the entire SVG |
| `Aspect` | TransformAspect | AspectFitFill | How to scale the SVG within bounds |
| `LockRatio` | double | 0 | Inherited from SkiaControl: locks the final size to the smaller (-1) or larger (1) of the requested width and height |

### Styling Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FillGradient` | SkiaGradient | null | Gradient fill for the whole SVG, blended with `GradientBlendMode` |
| `ShadowColor` | Color | Transparent | Drop shadow color, no shadow while transparent |
| `ShadowX`/`ShadowY` | double | 2.0 | Drop shadow offset |
| `ShadowBlur` | double | 5.0 | Drop shadow blur |
| `FontAwesomePrimaryColor` | Color | Black | Fill for elements with `class="fa-primary"` (Font Awesome duotone icons) |
| `FontAwesomeSecondaryColor` | Color | Gray | Fill for elements with `class="fa-secondary"` |
| `Opacity` | double | 1.0 | Overall opacity of the SVG |

### Loading Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseCache` | SkiaCacheType | Operations | How to cache the rendered SVG |

The SVG text is cached by source, so every `SkiaSvg` with the same `Source` reuses one download.

## SVG Source Options

### Local Files
On MAUI, place SVG files in your project's `Resources/Raw` folder. On WPF and OpenTK the file is read next to the executable, and on Blazor and WebAssembly it is fetched from the app's base address (`wwwroot`):
```xml
<draw:SkiaSvg Source="icons/home.svg" />
```

### Web URLs
Load SVG files from the internet:
```xml
<draw:SkiaSvg Source="https://example.com/logo.svg" />
```

### Embedded Resources
`Source` does not read embedded resources. Read the file yourself and set `SvgString`:
```csharp
using var stream = typeof(App).Assembly.GetManifestResourceStream("MyAssembly.Icons.star.svg");
using var reader = new StreamReader(stream);
mySvg.SvgString = reader.ReadToEnd();
```

## Styling and Tinting

### Simple Tinting
Apply a single color tint to the entire SVG:
```xml
<draw:SkiaSvg
    Source="heart.svg"
    TintColor="Red"
    WidthRequest="32"
    HeightRequest="32" />
```

### Override Fill and Stroke
SkiaSvg has no per-element fill or stroke override. `TintColor` recolors the whole SVG, and Font Awesome duotone icons take two colors:
```xml
<draw:SkiaSvg
    Source="icon.svg"
    FontAwesomePrimaryColor="Navy"
    FontAwesomeSecondaryColor="White" />
```

### Dynamic Color Changes
Change colors based on state or themes:
```xml
<draw:SkiaSvg
    Source="star.svg"
    TintColor="{Binding IsSelected, Converter={StaticResource BoolToColorConverter}}" />
```

## Advanced Examples

### SVG with Visual Effects
Add shadows, glows, and other effects:
```xml
<draw:SkiaSvg Source="logo.svg" TintColor="DarkBlue">
    <draw:SkiaControl.VisualEffects>
        <draw:DropShadowEffect 
            Blur="8" 
            X="4" 
            Y="4" 
            Color="#40000000" />
        <draw:OuterGlowEffect 
            Color="CornflowerBlue" 
            Blur="6" />
    </draw:SkiaControl.VisualEffects>
</draw:SkiaSvg>
```

### Animated SVG Icon
Animate the control from code, for example on tap:
```xml
<draw:SkiaSvg 
    x:Name="AnimatedIcon"
    Source="heart.svg"
    TintColor="Gray"
    WidthRequest="48"
    HeightRequest="48" />
```

```csharp
AnimatedIcon.OnTapped(async me =>
{
    me.TintColor = Colors.Red;
    await me.ScaleToAsync(1.2, 1.2, 100);
    await me.ScaleToAsync(1, 1, 100);
});
```

### SVG in Lists and Grids
Optimize SVG rendering in collections:
```xml
<draw:SkiaScroll>
    <draw:SkiaLayout Type="Column" ItemsSource="{Binding MenuItems}">
        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <draw:SkiaLayout Type="Row" Spacing="12">
                    <draw:SkiaSvg 
                        Source="{Binding IconPath}"
                        TintColor="{Binding IconColor}"
                        WidthRequest="24"
                        HeightRequest="24"
                        UseCache="Image" />
                    <draw:SkiaLabel 
                        Text="{Binding Title}"
                        VerticalOptions="Center" />
                </draw:SkiaLayout>
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Performance Optimization

### Caching Strategies
Choose the right caching strategy for your use case:

```xml
<!-- For static icons (best memory efficiency) -->
<draw:SkiaSvg UseCache="Operations" Source="static-icon.svg" />

<!-- For frequently changing colors/effects -->
<draw:SkiaSvg UseCache="Image" Source="dynamic-icon.svg" />

<!-- For complex animations -->
<draw:SkiaSvg UseCache="GPU" Source="animated-icon.svg" />
```

### Loading Optimization
Load SVG files once at startup, before the first screen needs them. Blazor startup calls `SkiaSvg.InitializeAsync` for you; on other heads call it yourself:
```csharp
SkiaSvg.RegisterSource("large-illustration.svg");
await SkiaSvg.InitializeAsync();
```

## SVG Compatibility

### Supported SVG Features
- **Paths**: All path commands (M, L, C, Q, A, Z, etc.)
- **Basic Shapes**: rect, circle, ellipse, line, polyline, polygon
- **Styling**: fill, stroke, stroke-width, opacity
- **Transforms**: translate, rotate, scale, matrix
- **Groups**: `<g>` elements with nested content
- **Text**: Basic text rendering (limited font support)

### Limitations
- **Animations**: SVG animations are not supported (use DrawnUi animations instead)
- **Scripting**: JavaScript in SVG is ignored
- **External References**: External image/font references may not work
- **Complex Filters**: Some SVG filter effects are not supported

## Troubleshooting

### Common Issues

**SVG not displaying:**
- Verify the file path is correct
- Check that the SVG file is valid
- On MAUI, ensure the file is in the `Resources/Raw` folder (see [Local Files](#local-files) for other heads)
- Handle the `Error` event: it receives the load exception

**Colors not working:**
- Some SVGs have `fill="currentColor"` which requires explicit styling
- Use `TintColor` for simple color changes
- Use `FontAwesomePrimaryColor`/`FontAwesomeSecondaryColor` for duotone icons

**Performance issues:**
- Use appropriate `UseCache` for your scenario
- Avoid very complex SVGs with thousands of paths
- Consider simplifying SVG artwork for mobile use

**Sizing problems:**
- Set explicit `WidthRequest`/`HeightRequest`
- Use `Aspect` (default `AspectFitFill`) to keep proportions
- Check the original SVG viewBox dimensions

## Best Practices

### 1. SVG Optimization
- Use tools like SVGO to optimize SVG files
- Remove unnecessary metadata and comments
- Simplify complex paths where possible
- Use appropriate precision for coordinates

### 2. Color Management
- Design SVGs with `currentColor` for easy theming
- Use consistent color naming in your design system
- Test with different `TintColor` values during design

### 3. Performance
- Cache frequently used icons with `UseCache="Operations"`
- Use `UseCache="Image"` for icons that change colors often
- Preload critical SVGs during app startup with `SkiaSvg.RegisterSource` and `SkiaSvg.InitializeAsync`

### 4. Accessibility
- Screen readers do not read SVG metadata: give meaningful icons an `AccessibilityRole` (for example `Aria.RoleImg`) and an `AccessibilityLabel`, see [Accessibility](../advanced/accessibility.md#accessibility-props)
- Use semantic naming for SVG files
- Ensure sufficient color contrast when tinting

### 5. Responsive Design
- Design SVGs to work at multiple sizes
- Test icon legibility at small sizes (16x16, 24x24)
- Use consistent visual weight across icon sets

This comprehensive guide covers all aspects of using SkiaSvg in DrawnUI, from basic usage to advanced optimization techniques. The control provides powerful SVG rendering capabilities while maintaining excellent performance through intelligent caching.