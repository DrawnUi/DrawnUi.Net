# Layout Controls

DrawnUI provides a powerful and flexible layout system for arranging controls with high performance. The system is similar to MAUI's native layout system but optimized for direct rendering with SkiaSharp.

## Core Layout Types

DrawnUI offers several core layout types:

### SkiaLayout

The base layout control for measurement, arrangement, and rendering of child elements. It supports different layout strategies via the `Type` property:

- Managing child controls
- Performance optimizations (see below)

```xml
<draw:SkiaLayout
    Type="Absolute"
    WidthRequest="400"
    HeightRequest="300">
    <!-- Child controls here -->
</draw:SkiaLayout>
```

## Layout Types

The `Type` property on `SkiaLayout` supports:

- **Absolute**: Free positioning, default. Think of it like a MAUI Grid with a single column and a single row.
- **Grid**: Row and column-based layout, classic MAUI Grid.
- **Column**: Vertical stacking (like MAUI VerticalStackLayout)
- **Row**: Horizontal stacking (like MAUI HorizontalStackLayout)
- **Wrap**: Items wrap to new lines when space runs out (similar to WPF WrapPanel)

```xml
<draw:SkiaLayout Type="Wrap" Spacing="5">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
    <!-- More items -->
</draw:SkiaLayout>
```

> **Note**: The `Spacing` property takes a single double value that applies to both horizontal and vertical spacing between items.

## Specialized Layout Controls

### ContentLayout

A specialized layout for hosting a single content element is `ContentLayout`: it holds a single child in its `Content` property, instead of using `Children`. `SkiaBackdrop` and `SkiaSpriteSet` subclass it. (`SkiaShape` subclasses `SkiaLayout` and uses `Children`.)

### SnappingLayout

Supports snap points for controlled scrolling, ideal for carousels or paginated interfaces. `SkiaDrawer`, `SkiaCarousel` are deriving from it.

### SkiaDecoratedGrid

`SkiaDecoratedGrid` is a specialized grid that can draw shapes between rows and columns, providing visual separation and decoration. The lines are drawn in the `RowSpacing` / `ColumnSpacing` gaps, filled with the `HorizontalLine` / `VerticalLine` gradients (`SkiaGradient`, a soft fading line by default; set one to `null` to hide those lines).

```xml
<draw:SkiaDecoratedGrid
    ColumnDefinitions="*,*,*"
    RowDefinitions="Auto,Auto,Auto"
    RowSpacing="1"
    ColumnSpacing="1">

    <draw:SkiaLabel Text="Cell 1,1" Grid.Column="0" Grid.Row="0" />
    <draw:SkiaLabel Text="Cell 1,2" Grid.Column="1" Grid.Row="0" />
    <draw:SkiaLabel Text="Cell 1,3" Grid.Column="2" Grid.Row="0" />
    <draw:SkiaLabel Text="Cell 2,1" Grid.Column="0" Grid.Row="1" />
    <draw:SkiaLabel Text="Cell 2,2" Grid.Column="1" Grid.Row="1" />
    <draw:SkiaLabel Text="Cell 2,3" Grid.Column="2" Grid.Row="1" />
</draw:SkiaDecoratedGrid>
```

### SkiaHotspot

`SkiaHotspot` provides a way to handle gestures in a lazy way, creating invisible touch-sensitive areas.

```xml
<draw:SkiaLayout Type="Absolute">
    <draw:SkiaImage Source="background.png" />

    <draw:SkiaHotspot
        Margin="100,100,0,0"
        WidthRequest="50" HeightRequest="50"
        Tapped="OnHotspotTapped" />

    <draw:SkiaHotspot
        Margin="200,150,0,0"
        WidthRequest="80" HeightRequest="30"
        Tapped="OnAnotherHotspotTapped" />
</draw:SkiaLayout>
```

### SkiaBackdrop

`SkiaBackdrop` applies effects to the background below, like blur and other visual effects.

```xml
<draw:SkiaLayout Type="Absolute">
    <draw:SkiaImage Source="background.png" />

    <draw:SkiaBackdrop
        Margin="50,50,0,0"
        WidthRequest="200" HeightRequest="150"
        Blur="10"
        BackgroundColor="#80FFFFFF" />

    <draw:SkiaLabel
        Text="Text over blurred background"
        Margin="60,100,0,0"
        TextColor="Black" />
</draw:SkiaLayout>
```

On every frame it copies the screen area it covers, applies `Blur` and its other effects, and draws the result in its place. Things to know:

- **Transformed parents.** Inside a parent that is scaled, moved, rotated or turned in 3D (a popup that zooms in, for example), it copies the area it really covers on screen and draws it back exactly there, so the glass lines up with what is behind it. `Blur` stays the same on screen while the parent scales. Up to 1.10.7.14 it copied its layout rect, so in such a parent the glass showed a shifted, wrongly scaled part of the screen.
- **Cached parents.** A backdrop must draw live on every frame, so none of its parents may keep an old copy:
  - A picture cache (`Operations`, `OperationsFull`, the default of `SkiaShape`) records the backdrop's copy once and replays it frozen: a frosted `SkiaShape` panel in a zooming popup kept the glass of its first frame. Set `UseCache = None` on every picture-cached parent of a backdrop, a `SkiaShape` holding it included. DrawnUI logs a warning once when a backdrop is recorded into a picture; it never changes a parent's cache for you.
  - Inside an `Image` or `GPU` cache the backdrop copies that cache's own surface, not the screen behind it, and the copy is redrawn only when that cache is. That is right when what should show through the glass is inside the same cache (a cached card whose picture lies under its glass panel). When the glass should show content outside it, keep such caches off its parents.
- **Frosted glass panel:** put the backdrop as the first child of a `SkiaShape` (for its corners and stroke), then the panel's content after it:

```csharp
new SkiaShape
{
    CornerRadius = 24,
    UseCache = SkiaCacheType.None, // SkiaShape defaults to Operations, which would freeze the backdrop
    StrokeColor = Colors.White.WithAlpha(0.3f),
    StrokeWidth = 1,
    Children =
    {
        new SkiaBackdrop { Blur = 6, BackgroundColor = Colors.White.WithAlpha(0.1f) }.Fill(),
        new SkiaStack { Padding = 20, Children = { /* content */ } },
    }
}
```

## Example: Creating a Grid Layout

```xml
<draw:SkiaLayout Type="Grid"
    ColumnDefinitions="Auto,*,100"
    RowDefinitions="Auto,*,50">
    <!-- Header spanning all columns -->
    <draw:SkiaLabel 
        Text="Grid Header" 
        Grid.Column="0" 
        Grid.ColumnSpan="3"
        Grid.Row="0" 
        HorizontalOptions="Center" />
    <!-- Sidebar -->
    <draw:SkiaLayout
        Type="Column"
        Grid.Column="0"
        Grid.Row="1"
        Grid.RowSpan="2"
        BackgroundColor="LightGray"
        Padding="10">
        <draw:SkiaLabel Text="Menu Item 1" />
        <draw:SkiaLabel Text="Menu Item 2" />
        <draw:SkiaLabel Text="Menu Item 3" />
    </draw:SkiaLayout>
    <!-- Main content -->
    <draw:ContentLayout 
        Grid.Column="1" 
        Grid.Row="1">
        <draw:SkiaLabel 
            Text="Main Content Area" 
            HorizontalOptions="Center" 
            VerticalOptions="Center" />
    </draw:ContentLayout>
    <!-- Right panel -->
    <draw:SkiaLayout
        Type="Column"
        Grid.Column="2"
        Grid.Row="1"
        BackgroundColor="LightBlue"
        Padding="5">
        <draw:SkiaLabel Text="Panel Info" />
    </draw:SkiaLayout>
    <!-- Footer spanning columns 1-2 -->
    <draw:SkiaLabel 
        Text="Footer" 
        Grid.Column="1" 
        Grid.ColumnSpan="2"
        Grid.Row="2" 
        HorizontalOptions="Center"
        VerticalOptions="Center" />
</draw:SkiaLayout>
```

All grid functionality is handled by SkiaLayout with `Type="Grid"`.

## Under the Hood

The layout system is built on top of the `SkiaControl` base class. Layout controls extend this for child management, measurement, and arrangement. Internally, structures like `LayoutStructure` and `GridStructure` efficiently track and manage layout information.

### Caching

Caching options help balance CPU and memory usage:

- **None**: No caching, recalculated every frame
- **Operations**: Caches drawing commands
- **Image**: Caches as bitmap (more memory, less CPU)
- **GPU**: Uses hardware acceleration where available

There are more types (`OperationsFull`, `ImageDoubleBuffered`, `ImageComposite`, `ImageCompositeGPU`, `Auto`), see [Caching](../advanced/caching.md).