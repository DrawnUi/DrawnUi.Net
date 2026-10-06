# Input Controls

DrawnUI provides various input controls for user interaction, including sliders, progress indicators, and specialized picker controls.

## SkiaSlider

`SkiaSlider` is a versatile slider control that supports both single value selection and range selection capabilities.

The current value of a single-value slider is `End`. In range mode (`EnableRange="True"`) the selection is `Start`..`End`.

### Basic Usage

```xml
<draw:SkiaSlider
    Min="0"
    Max="100"
    End="50"
    WidthRequest="300"
    TrackSelectedColor="Red"
    ThumbColor="Blue"
    EndChanged="OnSliderValueChanged" />
```

### Range Selection

```xml
<draw:SkiaSlider
    Min="0"
    Max="100"
    Start="25"
    End="75"
    EnableRange="True"
    WidthRequest="300" />
```

### Platform Styles

`SkiaSlider` supports [platform-specific styling](../advanced/platform-styling.md) via `ControlStyle`: `Unset` (stock look), `Cupertino`, `Material`, `Material3`, `Windows`, or `Platform` (picks the style matching the current OS at runtime). The style can be changed after the slider was shown, the look is rebuilt.

```xml
<draw:SkiaSlider
    ControlStyle="Platform"
    Min="0"
    Max="100"
    End="30" />
```

`ThumbColor`, `TrackColor` and `TrackSelectedColor` work with every style. For the stock (`Unset`) look they are applied only when explicitly set — strokes and shadows follow automatically as darker shades of the set color.

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Min` | double | Minimum value (default 0) |
| `Max` | double | Maximum value (default 100) |
| `End` | double | Current value (single mode), or range end value |
| `Start` | double | Range start value, used when `EnableRange` is true |
| `EnableRange` | bool | Two thumbs selecting a `Start`..`End` range |
| `Step` | double | Step increment values snap to (default 1) |
| `RangeMin` | double | Minimum allowed distance between `Start` and `End` |
| `ControlStyle` | PrebuiltControlStyle | `Unset`, `Platform`, `Cupertino`, `Material`, `Material3`, `Windows`; changing it at runtime rebuilds the look |
| `ThumbColor` | Color | Color of the thumb(s) |
| `TrackColor` | Color | Color of the unselected track |
| `TrackSelectedColor` | Color | Color of the selected part of the track |
| `TrailStartOffset` | double | Fine-tune pts for where the selected trail starts relative to the start thumb center |
| `TrailEndOffset` | double | Fine-tune pts for where the selected trail ends relative to the end thumb center |
| `SliderHeight` | double | Height of the track area |
| `ClickOnTrailEnabled` | bool | Tapping the track jumps the nearest thumb there (default true) |
| `IgnoreWrongDirection` | bool | Ignore gestures along the wrong axis |
| `RespondsToGestures` | bool | Default true. False: thumbs cannot be dragged and the track ignores taps, `Start`/`End` change from code only |
| `Invert` | bool | Invert the direction of values |
| `ValueStringFormat` | string | Format for `EndDesc`/`StartDesc` readable value strings |

Cupertino style extras: `CupertinoTrackHeight` (default 2), `CupertinoThumbDiameter` (default 28), `CupertinoThumbBorderWidth` (default 0.5).

The selected trail is anchored under the thumb centers automatically for any thumb size; use `TrailStartOffset`/`TrailEndOffset` for per-design fine-tuning.

### Events

- `EndChanged`: Raised when the value (`End`) changes. Signature: `EventHandler<double>`
- `StartChanged`: Raised when the range start (`Start`) changes. Signature: `EventHandler<double>`

### Keyboard

A slider is an accessibility node by default (role slider). Its name is your `AccessibilityLabel`, so name it by purpose ("Volume"); its value (`End`, `Min`, `Max`, `Step`, and "20 – 80" for a range slider) and its orientation go to screen readers separately. A screen reader can step it up or down (one arrow-key step) or set a value, which snaps to `Step`. With keyboard focus, Right / Up and Left / Down step the value by `Step` (a hundredth of the range when `Step` is 0), PageUp / PageDown move a tenth of the range, Home / End go to `Min` / `Max`. A ranged slider moves `End`, which stops at `Start`. Enter and Space do nothing. `EndChanged` fires as for a drag. See [Accessibility](../advanced/accessibility.md#keyboard-navigation).

### Customizing (XAML subclass)

Subclass `SkiaSlider` and provide your own content: a child tagged `"Trail"` hosting the track, a `SliderTrail` tagged `"SelectedTrail"`, and a `SliderThumb` named/tagged `"EndThumb"` (plus `"StartThumb"` for range). See `Sandbox/Views/Controls/DrawnSlider.xaml` (visual reskin) and `Sandbox/Views/Controls/ColorPicker/SliderColor.xaml` (gradient color-picker slider) for working examples. User-provided content is never overridden by the built-in style logic.

## SkiaProgress

`SkiaProgress` is a progress indicator control to show that you are actually doing something. It shows a determinate value: there is no indeterminate mode.

### Basic Usage

```xml
<draw:SkiaProgress
    Min="0"
    Max="100"
    Value="50"
    WidthRequest="200"
    TrackHeight="8"
    ProgressColor="Green"
    TrackColor="LightGray" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | double | Current value, clamped to `Min`..`Max` and snapped to `Step` |
| `Min` | double | Minimum value (default 0) |
| `Max` | double | Maximum value (default 100) |
| `Step` | double | Step the value snaps to (default 0, no snapping) |
| `ProgressColor` | Color | Color of the progress bar; when not set, the style's color |
| `TrackColor` | Color | Color of the background track; when not set, the style's color |
| `TrackHeight` | double | Height of the track; when not set, the style's height |
| `ControlStyle` | PrebuiltControlStyle | `Unset`, `Platform`, `Cupertino`, `Material`, `Material3` (gap and stop indicator), `Windows`; can be changed at runtime |

`ValueChanged` (`EventHandler<double>`) is raised when `Value` changes.

### Accessibility

A progress bar is an accessibility node by default (role progressbar). Screen readers read its value as a percentage ("65%"); its name is your `AccessibilityLabel` ("Download"). It is read only. See [Range controls](../advanced/accessibility.md#range-controls-sliders-progress-bars).

## SkiaWheelPicker

`SkiaWheelPicker` provides an iOS-style picker wheel for selecting items from a list.

### Basic Usage

```xml
<draw:SkiaWheelPicker
    ItemsSource="{Binding Items}"
    SelectedIndex="{Binding SelectedIndex}"
    WidthRequest="200"
    HeightRequest="150"
    VisibleItems="5" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `ItemsSource` | IList | Collection of items to display |
| `ItemTemplate` | DataTemplate | Optional template for the items |
| `SelectedIndex` | int | Index of the selected item (two-way, default -1) |
| `VisibleItems` | int | Number of visible items (default 7) |
| `TextColor` | Color | Color of the item text (default Gray) |
| `TextSelectedColor` | Color | Color of the selected item text (default Red) |
| `LinesColor` | Color | Color of the selection lines (default White) |
| `BackgroundView` | SkiaControl | Optional extra view added to the picker next to the wheel |

### Events

- `SelectedIndexChanged`: Raised when `SelectedIndex` is set from code or a binding. Signature: `EventHandler<int>`. When the user scrolls the wheel, `SelectedIndex` changes (bind or observe it) but this event is not raised.

### Two-Way Binding `SelectedIndex` (C# code-behind)

Instead of wiring the `SelectedIndexChanged` event by hand, keep `SelectedIndex` in sync with a model both directions using the fluent `ObservePropertyTwoWay` extension. A separate one-way `ObserveProperty` then drives any UI (e.g. a label) from the model. The model must be `INotifyPropertyChanged`; the picker's `SelectedIndex` is a bindable property, so its setter raises change notifications.

```csharp
// model : INotifyPropertyChanged with an int SelectedIndex
new SkiaWheelPicker()
    {
        ItemsSource = _items,
        VisibleItems = 5,
    }
    .Assign(out _picker)
    .ObservePropertyTwoWay(model,
        nameof(model.SelectedIndex),  me   => me.SelectedIndex = model.SelectedIndex,    // model -> picker
        nameof(SkiaWheelPicker.SelectedIndex), (src, me) => src.SelectedIndex = me.SelectedIndex); // picker -> model

new SkiaLabel()
    .ObserveProperty(model, nameof(model.SelectedIndex),
        me => me.Text = $"Selected: {model.SelectedIndex}");
```

Scrolling the wheel updates `model.SelectedIndex`; setting `model.SelectedIndex` scrolls the wheel. Re-entrancy is guarded internally. See the [Fluent Extensions](../fluent-extensions.md) guide for more on `ObserveProperty` / `ObservePropertyTwoWay`.

## SkiaSpinner

`SkiaSpinner` is a spinner control to test your luck: a wheel of names that shows its items around a circle and is spun with a gesture or from code.

### Basic Usage

```xml
<draw:SkiaSpinner
    ItemsSource="{Binding SpinnerItems}"
    SelectedIndex="{Binding SelectedIndex}"
    WidthRequest="200"
    HeightRequest="200"
    SidePosition="Right" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `ItemsSource` | IList | Items shown on the wheel |
| `ItemTemplate` | DataTemplate | Optional template for the items |
| `SelectedIndex` | int | Index of the selected item (two-way, default -1) |
| `SidePosition` | SidePosition | Where on the wheel the selection is read: `Top`, `Right` (default), `Bottom`, `Left` |
| `WheelRotation` | double | Current rotation of the wheel in degrees |
| `Snap` | bool | Snap to an item after the wheel stops (default true) |
| `Velocity` | double | How much a gesture speeds up the wheel (default 2.0) |
| `Deceleration` | double | Friction applied while spinning (default 0.0003) |
| `InverseVisualRotation` | bool | Items readable on the left instead of the right |
| `RespondsToGestures` | bool | False: the wheel turns from code only (default true) |

### Methods

- `SpinToIndex(index, spins = 0, speed = 350)`: Animate to an item, with optional extra full turns
- `SpinToIndexShortest(index, speed = 350)`: Animate to an item by the shortest path
- `SpinToRandom()`: Spin to a random position
- `Rotate(targetRotation, durationMs = 500)`: Animate to a rotation
- `StopScrolling()`: Stop the spinning animation

### Events

- `SelectedIndexChanged`: Raised when `SelectedIndex` is set from code or a binding
  - Event signature: `EventHandler<int>` with the new index
  - When the user spins the wheel, `SelectedIndex` changes (bind or observe it) but this event is not raised
