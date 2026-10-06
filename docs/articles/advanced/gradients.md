# Using Gradients in DrawnUi

DrawnUi provides gradient support for shapes, text, and SVG icons, enabling visually rich and modern UI designs. This article covers the types of gradients available, how to apply them, and practical examples for common scenarios.

## Gradient Types

`SkiaGradient.Type` (`GradientType`) supports several gradient types:

- **Linear** (default): Colors transition along a straight line, from (`StartXRatio`, `StartYRatio`) to (`EndXRatio`, `EndYRatio`), as ratios of the control size.
- **Circular**: Colors radiate outward from (`StartXRatio`, `StartYRatio`) as a circle.
- **Oval**: Like `Circular`, stretched to the control's aspect ratio.
- **Sweep**: Colors sweep around the control's center. The start angle is the control's `Value1`, the sweep angle is its `Value2`.

`Conical` is in the enum but currently draws as `Linear`. Colors go in `Colors`; optional stop offsets (0 to 1) go in `ColorPositions`, one per color.

## Applying Gradients to Shapes

You can apply gradients to the fill or stroke of any `SkiaShape` using the `FillGradient` and `StrokeGradient` properties.

### Linear Gradient Example

```xml
<DrawUi:SkiaShape Type="Rectangle" CornerRadius="16" WidthRequest="200" HeightRequest="100">
    <DrawUi:SkiaShape.FillGradient>
        <DrawUi:SkiaGradient
            Type="Linear"
            StartXRatio="0"
            StartYRatio="0"
            EndXRatio="1"
            EndYRatio="1">
            <DrawUi:SkiaGradient.Colors>
                <Color>#FF6A00</Color>
                <Color>#FFD800</Color>
            </DrawUi:SkiaGradient.Colors>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.FillGradient>
</DrawUi:SkiaShape>
```

Maybe you have colors defined in a static class?

```xml
<draw:SkiaFrame
    AnimationTapped="Ripple"
    Tapped="OnTapped_Item"
    WidthRequest="60"
    StrokeWidth="1"
    HeightRequest="28"
    StrokeColor="{Binding SelectionColor}"
    CornerRadius="4">
    <draw:SkiaControl.FillGradient>
        <draw:SkiaGradient
            Opacity="0.1"
            EndXRatio="0"
            EndYRatio="1"
            StartXRatio="0"
            StartYRatio="0"
            Type="Linear">
            <draw:SkiaGradient.Colors>
                <x:Static Member="xam:BackColors.GradientStartNav"/>
                <x:Static Member="xam:BackColors.GradientEndNav"/>
            </draw:SkiaGradient.Colors>
        </draw:SkiaGradient>
    </draw:SkiaControl.FillGradient>
    <draw:SkiaRichLabel
        HorizontalOptions="Center"
        HorizontalTextAlignment="Center"
        Text="{Binding Title}"
        TextColor="{Binding SelectionColor}"
        VerticalOptions="Center" />
</draw:SkiaFrame>
```

### Radial Gradient Example

```xml
<DrawUi:SkiaShape Type="Circle" WidthRequest="120" HeightRequest="120">
    <DrawUi:SkiaShape.FillGradient>
        <DrawUi:SkiaGradient
            Type="Circular"
            StartXRatio="0.5"
            StartYRatio="0.5">
            <DrawUi:SkiaGradient.Colors>
                <Color>#00C3FF</Color>
                <Color>#FFFF1C</Color>
            </DrawUi:SkiaGradient.Colors>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.FillGradient>
</DrawUi:SkiaShape>
```

### Sweep Gradient Example

```xml
<DrawUi:SkiaShape Type="Ellipse" WidthRequest="180" HeightRequest="100" Value1="0" Value2="360">
    <DrawUi:SkiaShape.FillGradient>
        <DrawUi:SkiaGradient Type="Sweep">
            <DrawUi:SkiaGradient.Colors>
                <Color>#FF0080</Color>
                <Color>#7928CA</Color>
            </DrawUi:SkiaGradient.Colors>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.FillGradient>
</DrawUi:SkiaShape>
```

### Multi-Stop Gradients

You can define gradients with multiple color stops:

```xml
<DrawUi:SkiaShape Type="Rectangle" WidthRequest="220" HeightRequest="60">
    <DrawUi:SkiaShape.FillGradient>
        <DrawUi:SkiaGradient Type="Linear" StartXRatio="0" StartYRatio="0" EndXRatio="1" EndYRatio="0">
            <DrawUi:SkiaGradient.Colors>
                <Color>#FF6A00</Color>
                <Color>#FFD800</Color>
                <Color>#00FFB4</Color>
            </DrawUi:SkiaGradient.Colors>
            <DrawUi:SkiaGradient.ColorPositions>
                <x:Double>0.0</x:Double>
                <x:Double>0.5</x:Double>
                <x:Double>1.0</x:Double>
            </DrawUi:SkiaGradient.ColorPositions>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.FillGradient>
</DrawUi:SkiaShape>
```

## Applying Gradients to Text

You can apply gradients to text using the `FillGradient` property on `SkiaLabel` (`GradientByLines`, default true, applies it per line; `StrokeGradient` paints the text stroke):

```xml
<DrawUi:SkiaLabel 
    Text="Gradient Text" 
    FontSize="32" 
    FillGradient="{StaticResource MyGradient}" />
```

Or define inline:

```xml
<DrawUi:SkiaLabel Text="Sunset" FontSize="40">
    <DrawUi:SkiaLabel.FillGradient>
        <DrawUi:SkiaGradient Type="Linear" StartXRatio="0" StartYRatio="0" EndXRatio="1" EndYRatio="0">
            <DrawUi:SkiaGradient.Colors>
                <Color>#FF6A00</Color>
                <Color>#FFD800</Color>
            </DrawUi:SkiaGradient.Colors>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaLabel.FillGradient>
</DrawUi:SkiaLabel>
```

## Applying Gradients to SVG, code behind

You can paint SVG icons with a gradient using the `FillGradient` property on `SkiaSvg` (`GradientBlendMode` sets how it blends with the icon):

```csharp
new SkiaSvg()
{
    HorizontalOptions = LayoutOptions.Center,
    HeightRequest = 20,
    LockRatio = 1,
    UseCache = SkiaCacheType.Image,
    FillGradient =
        new SkiaGradient()
        {
            StartXRatio = 1,
            EndXRatio = 0,
            StartYRatio = 0,
            EndYRatio = 1,
            Colors =
                new Color[] { BackColors.GradientStartNav,
                    BackColors.GradientEndNav }
        },
}
```

This paints the icon with a diagonal two-color gradient.

## Defining Gradients as Resources

For reuse, define gradients as resources:

```xml
<ContentPage.Resources>
    <DrawUi:SkiaGradient x:Key="MyGradient" Type="Linear" StartXRatio="0" StartYRatio="0" EndXRatio="1" EndYRatio="1">
        <DrawUi:SkiaGradient.Colors>
            <Color>#FF6A00</Color>
            <Color>#FFD800</Color>
        </DrawUi:SkiaGradient.Colors>
    </DrawUi:SkiaGradient>
</ContentPage.Resources>
```

Then reference with:

```xml
<DrawUi:SkiaLabel Text="Reusable Gradient" FillGradient="{StaticResource MyGradient}" />
```

## C# Example: Creating a Gradient in Code

```csharp
var gradient = new SkiaGradient
{
    Type = GradientType.Linear,
    Colors = new List<Color> { Colors.Red, Colors.Yellow },
    StartXRatio = 0,
    StartYRatio = 0,
    EndXRatio = 1,
    EndYRatio = 1
};

control.FillGradient = gradient;
```

## Tips and Best Practices

- Use gradients to add depth and visual interest to your UI.
- For performance, prefer simple gradients or reuse gradient resources.
- Gradients can be animated by changing their properties 
- Combine gradients with shadows for modern card and button designs.

