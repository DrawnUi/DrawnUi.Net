# Platform-Specific Styling

Some of the DrawnUi controls support platform-specific styling to ensure your app looks and feels native on each platform. When you create your own controls you can support that feature too.

## Using Platform Styles

### The ControlStyle Property

Some DrawnUi controls include a `ControlStyle` property that determines their visual appearance:

- `Unset`: Default styling defined by the control
- `Platform`: Automatically selects the appropriate style for the current platform
- `Cupertino`: iOS-style appearance
- `Material`: Android Material Design appearance
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
- `SkiaSlider`: Change values with platform-specific track and thumb styling

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

If you're creating custom controls, you can leverage the same platform styling system:

```csharp
public class MyCustomControl : SkiaControl
{
    public static readonly BindableProperty ControlStyleProperty = BindableProperty.Create(
        nameof(ControlStyle),
        typeof(PrebuiltControlStyle),
        typeof(MyCustomControl),
        PrebuiltControlStyle.Unset);

    public PrebuiltControlStyle ControlStyle
    {
        get { return (PrebuiltControlStyle)GetValue(ControlStyleProperty); }
        set { SetValue(ControlStyleProperty, value); }
    }
    
    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        
        if (propertyName == nameof(ControlStyle))
        {
            ApplyPlatformStyle();
        }
    }
    
    private void ApplyPlatformStyle()
    {
        switch (ControlStyle)
        {
            case PrebuiltControlStyle.Cupertino:
                // Apply iOS-specific styling
                break;
            case PrebuiltControlStyle.Material:
                // Apply Material Design styling
                break;
            case PrebuiltControlStyle.Windows:
                // Apply Windows styling
                break;
            case PrebuiltControlStyle.Platform:
                #if IOS || MACCATALYST
                // Apply iOS styling
                #elif ANDROID
                // Apply Material styling
                #elif WINDOWS
                // Apply Windows styling
                #endif
                break;
        }
    }
}