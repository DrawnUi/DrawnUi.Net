# Range Controls

DrawnUI provides two range controls: `SkiaSlider` to pick a value and `SkiaProgress` to show progress. `SkiaProgress` shares its range functionality through the `SkiaRangeBase` base class. `SkiaSlider` is a `SkiaLayout` with its own range properties.

## Architecture

```
SkiaLayout
├── SkiaRangeBase (abstract base class)
│   └── SkiaProgress (progress bar)
└── SkiaSlider (interactive slider with thumb)
```

## SkiaRangeBase

The base class provides common functionality for range controls:

### Properties
- `Value` - Current value within the range (default: 0, two-way). It is snapped to `Step` and clamped to `Min`..`Max`
- `Min` - Minimum value (default: 0)
- `Max` - Maximum value (default: 100)
- `Step` - Value stepping increment (default: 0 = no stepping)
- `TrackColor` - Background track color
- `ProgressColor` - Progress/selected portion color
- `TrackHeight` - Height of the track (default: 4)

### Events
- `ValueChanged` - Fired when the value changes

## SkiaSlider

Interactive slider control with draggable thumb.

### Properties
- `End` - The slider's value (default: 100); the upper value of a range slider
- `Start` - For range sliders (when `EnableRange` is true)
- `Min` - Minimum value (default: 0)
- `Max` - Maximum value (default: 100)
- `Step` - Value stepping increment (default: 1)
- `EnableRange` - Enable dual-thumb range selection
- `SliderHeight` - Overall slider height
- `ThumbColor` - Color of the draggable thumb
- `TrackColor` - Background track color
- `TrackSelectedColor` - Color of the selected part of the track
- `Invert` - Reverse the direction (inherited from `SkiaLayout`)

### Events
- `EndChanged` - Fired when End value changes
- `StartChanged` - Fired when Start value changes

### Platform Styles
- `Cupertino` - iOS-style slider (2pt track, 28pt thumb, system blue)
- `Material` - Material Design (4dp track, 20dp thumb, Material blue). `Material3` uses the same shape with the Material 3 primary color
- `Windows` - Windows Fluent Design (4pt rounded track, 20pt white thumb with an accent inner dot)
- `Default` - Generic style

### Example Usage

```xml
<!-- Basic slider -->
<draw:SkiaSlider 
    Min="0" 
    Max="100" 
    End="50" 
    ControlStyle="Cupertino" />

<!-- Range slider -->
<draw:SkiaSlider 
    Min="0" 
    Max="100" 
    Start="20" 
    End="80" 
    EnableRange="True" />
```

## SkiaProgress

Linear progress bar control for showing progress or completion status. It fills the track from `Min` to `Value`.

### Properties
All properties from `SkiaRangeBase` plus:
- Platform-specific styling through `ControlStyle`. An explicitly set `TrackColor`, `ProgressColor` or `TrackHeight` wins over the style's value

### Platform Styles
- `Cupertino` - iOS-style progress bar (4pt height, rounded, system blue #007AFF)
- `Material` - Material Design progress bar (4pt height, slight rounding, Material blue #2196F3)
- `Material3` - Material 3 progress bar (4pt height, a gap before the remaining track and a stop dot at its end, primary #6750A4)
- `Windows` - Windows Fluent Design progress bar (6pt height, moderate rounding, Fluent blue #0078D4)
- `Default` - Generic style (8pt height, crimson #DC143C)

### Example Usage

```xml
<!-- Basic progress bar -->
<draw:SkiaProgress 
    Min="0" 
    Max="100" 
    Value="75" 
    ControlStyle="Cupertino" />

<!-- Custom colors -->
<draw:SkiaProgress 
    Min="0" 
    Max="100" 
    Value="50" 
    TrackColor="LightGray" 
    ProgressColor="Green" />
```

## Backward Compatibility

`SkiaSlider` is not built on `SkiaRangeBase`, so its API is unchanged:

- `End` is the slider's value: it has no `Value` property
- `TrackSelectedColor` colors the selected track: it has no `ProgressColor` property
- `EndChanged` and `StartChanged` report changes: it has no `ValueChanged` event
- Existing XAML and code-behind require no changes

## Migration Guide

### For existing SkiaSlider usage:
No changes required! Your existing code will continue to work.

### For new development:
- Use `SkiaProgress` for progress indicators
- Use `SkiaSlider` for interactive value selection

## Implementation Details

### SkiaRangeBase
- Provides value-to-position conversion methods
- Handles property coercion and validation
- Manages track and progress visual elements
- Implements platform styling infrastructure

### ProgressTrail
- Specialized component for progress visualization
- Similar to `SliderTrail` but optimized for progress display
- Handles width calculation based on progress value

### Platform Styling
Each control implements platform-specific `CreateXXXStyleContent()` methods based on official design guidelines:

#### iOS (Cupertino)
- **Slider**: 2pt track height, 28pt thumb diameter, system blue (#007AFF)
- **Progress**: 4pt height, fully rounded corners, system gray background
- **Colors**: iOS system blue, system gray 5 background

#### Material Design
- **Slider**: 4dp track, 20dp thumb
- **Progress**: 4dp height, slight rounding (2dp)
- **Colors**: Material blue (#2196F3), surface variant background. `Material3` uses the Material 3 primary (#6750A4)

#### Windows (Fluent Design)
- **Slider**: 4pt rounded track, 20pt white thumb with an accent dot
- **Progress**: 6pt height, moderate rounding (3dp)
- **Colors**: Fluent accent blue (#0078D4), neutral background

#### Implementation Methods
- `CreateCupertinoStyleContent()` - iOS styling
- `CreateMaterialStyleContent()` - Material Design
- `CreateMaterial3StyleContent()` - Material 3 (`SkiaProgress` only; `SkiaSlider` uses `CreateMaterialStyleContent()` for both)
- `CreateWindowsStyleContent()` - Windows styling
- `CreateDefaultStyleContent()` - Generic styling
