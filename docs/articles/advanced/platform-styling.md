# Platform-Specific Styling

Some of the DrawnUi controls support platform-specific styling to ensure your app looks and feels native on each platform. When you create your own controls you can support that feature too.

## Using Platform Styles

### The ControlStyle Property

Some DrawnUi controls include a `ControlStyle` property that determines their visual appearance:

- `Unset`: Default styling defined by the control
- `Platform`: Automatically selects the appropriate style for the current platform
- `Cupertino`: iOS-style appearance
- `Material`: Android Material Design 2 appearance
- `Material3`: Android Material Design 3 (Material You) appearance
- `Windows`: Windows-style appearance

### Basic Usage

```xml
<!-- Automatically use the platform-specific style -->
<draw:SkiaButton
    Text="Platform Button"
    ControlStyle="Platform" />

<!-- Explicitly use iOS style on any platform -->
<draw:SkiaSwitch
    ControlStyle="Cupertino"
    IsToggled="true" />
```

## Supported Controls

The following controls support platform-specific styling:

- `SkiaButton`: Different button appearances across platforms
- `SkiaSwitch`: Toggle switches with platform-specific track and thumb styling
- `SkiaCheckbox`: Checkbox controls with platform-appropriate checkmarks and animations
- `SkiaRadioButton`: Radio rings and dots per platform
- `SkiaSlider`: Change values with platform-specific track and thumb styling
- `SkiaProgress`: Track and trail per platform (Material3 adds the gap and stop indicator)
- `SkiaPicker`, `SkiaWheelPicker`: Field and wheel looks per platform
- `SkiaEditor`: Background, border and cursor per platform

## Changing the style at runtime

`ControlStyle` is not a one-shot: setting it after the control was measured rebuilds the default content for the new style. The control drops the children it built itself (children you provided stay), releases only the sizes and defaults the previous style pinned (`WidthRequest`, `HeightRequest`, minimum sizes, alignment or cache defaults set through `SetStyleDefault`), keeps every value you set yourself, and builds the new look at the next measure. A theme or platform switch in settings needs nothing more than assigning the property.

```csharp
foreach (var toggle in Views.OfType<SkiaToggle>())
    toggle.ControlStyle = PrebuiltControlStyle.Material3;
```

## Platform Style Characteristics

### Cupertino (iOS) Style

- Rounded corners and subtle shadows
- Blue accent color (#007AFF)
- Switches have pill-shaped tracks with shadows on the thumb
- Buttons typically have semibold text

### Material (Android) Style

- Less rounded corners
- More pronounced shadows
- Material blue accent color (#2196F3)
- Switches have track colors that match the thumb when active
- Buttons often use uppercase text

### Windows Style

- Minimal corner radius
- Subtle shadows
- Windows blue accent color (#0078D7)
- Switches and buttons have a more squared appearance

## Customizing Platform Styles

You can combine platform styles with custom styling. The platform style defines the base appearance, while your custom properties provide additional customization:

```xml
<draw:SkiaButton
    Text="Custom Platform Button"
    ControlStyle="Platform"
    BackgroundColor="Purple"
    TextColor="White" />
```

This creates a button with the platform-specific shape, shadow, and behavior, but with your custom colors.

## Creating Custom Platform-Styled Controls

`ControlStyle` already lives on `SkiaControl`, every control has it. To support it in your own control build the look in `CreateDefaultContent` (runs once, at the first measure, after the object initializer and XAML have been applied) and switch on `UsingControlStyle`, which already resolves `Platform` to the running OS. Apply layout or cache defaults through `SetStyleDefault` and sizes through `SetDefaultContentSize`, both leave alone anything the user set explicitly and both are undone by a rebuild. The base handles a runtime `ControlStyle` change for you; if you cache child references with a null-guard, reset them in `RebuildDefaultContent`.

```csharp
public class MyCustomControl : SkiaLayout
{
    SkiaShape _frame;

    protected override void CreateDefaultContent()
    {
        if (Views.Count > 0)
            return; // the user provided children, keep them

        SetStyleDefault(HorizontalOptionsProperty, LayoutOptions.Fill);

        switch (UsingControlStyle)
        {
            case PrebuiltControlStyle.Cupertino:
                SetDefaultContentSize(120, 44);
                AddSubView(new SkiaShape { CornerRadius = 12, BackgroundColor = Color.Parse("#007AFF") }.Assign(out _frame));
                break;
            case PrebuiltControlStyle.Material:
            case PrebuiltControlStyle.Material3:
                SetDefaultContentSize(120, 40);
                AddSubView(new SkiaShape { CornerRadius = 20, BackgroundColor = Color.Parse("#6750A4") }.Assign(out _frame));
                break;
            case PrebuiltControlStyle.Windows:
                SetDefaultContentSize(120, 32);
                AddSubView(new SkiaShape { CornerRadius = 4, BackgroundColor = Color.Parse("#0078D7") }.Assign(out _frame));
                break;
            default:
                SetDefaultContentSize(120, 41);
                AddSubView(new SkiaShape { CornerRadius = 8, BackgroundColor = Color.Parse("#DC143C") }.Assign(out _frame));
                break;
        }
    }

    public override void RebuildDefaultContent()
    {
        _frame = null; // disposed by the rebuild, CreateDefaultContent assigns the new one
        base.RebuildDefaultContent();
    }
}