# Button Controls

DrawnUi provides highly customizable button controls with platform-specific styling and support for custom content.

## SkiaButton

`SkiaButton` is a versatile button control supporting different button styles, platform-specific appearance, and custom content. You can use the default content or provide your own child views. If you use conventional tags (`BtnText`, `BtnShape`), SkiaButton will apply its properties (like `Text`, `TextColor`, etc.) to those views automatically.

> **Note:** If you provide custom content, use the tags `BtnText` for your main label and `BtnShape` for the button background to enable property binding.

### Basic Usage

```xml
<draw:SkiaButton
    Text="Click Me"
    WidthRequest="120"
    HeightRequest="40"
    BackgroundColor="Blue"
    TextColor="White"
    CornerRadius="8"
    Clicked="OnButtonClicked" />
```

### Custom Content Example

```xml
<draw:SkiaButton>
    <draw:SkiaShape Tag="BtnShape" BackgroundColor="Red" CornerRadius="12" />
    <draw:SkiaLabel Tag="BtnText" Text="Custom" TextColor="Yellow" />
</draw:SkiaButton>
```

### Button Style Types

SkiaButton supports multiple style variants through the `ButtonStyle` property:

- `Contained`: Standard filled button with background color (default)
- `Outlined`: Button with outline border and transparent background
- `Text`: Button with no background or border, only text

```xml
<draw:SkiaButton
    Text="Outlined Button"
    ButtonStyle="Outlined"
    BackgroundColor="Blue"
    TextColor="Blue" />
```

### Platform-Specific Styling

Set the bindable `ControlStyle` property (XAML or code) to pick a look; `UsingControlStyle` is the resolved read-only value (`Platform` resolved to the running OS). Styles include:

- `Unset`: the DrawnUI default look
- `Platform`: the style matching the current OS
- `Cupertino`: iOS-style button
- `Material`: Android Material Design 2 button
- `Material3`: Android Material Design 3 (Material You) button
- `Windows`: Windows-style button

```xml
<draw:SkiaButton Text="Platform" ControlStyle="Platform" />
```

`ControlStyle` can be changed at runtime, the button rebuilds its content for the new style (see [platform styling](../advanced/platform-styling.md#changing-the-style-at-runtime)).

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Text` | string | The text displayed on the button |
| `TextColor` | Color | The color of the button text |
| `BackgroundColor` | Color | The background color of the button |
| `CornerRadius` | float | The corner radius of the button (applied via `BtnShape`) |
| `ButtonStyle` | ButtonStyleType | The button style (Contained, Outlined, Text) |
| `ElevationEnabled` | bool | Whether the button has a shadow effect |
| `TextCase` | TextTransform | The text case transformation (None, Uppercase, Lowercase) |
| `FontSize` | double | The font size of the button text |
| `FontFamily` | string | The font family of the button text |
| `IsDisabled` | bool | Disables the button if true |
| `IsPressed` | bool | True while the button is pressed |
| `IconPosition` | IconPositionType | Position of icon (icon support planned) |
| `ApplyEffect` | SkiaTouchAnimation | Touch animation effect (Ripple, Shimmer, etc.) |

### Events

- `Clicked`: Raised when the button is clicked/tapped
- `Pressed`: Raised when the button is pressed down
- `Released`: Raised when the button is released
- `Up`, `Down`, `Tapped`: Additional gesture events

### Icon Support

Icon support is planned. The `IconPosition` property exists, but icon rendering is not yet implemented.

---

## API XML Documentation

> The following methods in SkiaButton have been updated with XML documentation in the codebase:
> - `OnDown`, `OnUp`, `OnTapped`, `ApplyProperties`, `CreateDefaultContent`, `CreateCupertinoStyleContent`, `CreateMaterialStyleContent`, `CreateWindowsStyleContent`, `OnButtonPropertyChanged`, `FindViews`, `CreateClip`.

For more details, see the source code in `src/Engine/Maui/Controls/Button/SkiaButton.cs`.