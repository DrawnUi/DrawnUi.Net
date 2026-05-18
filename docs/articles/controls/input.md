# Input Controls

DrawnUi.Maui provides various input controls for user interaction, including sliders, progress indicators, and specialized picker controls.

## SkiaSlider

`SkiaSlider` is a versatile slider control that supports both single value selection and range selection capabilities.

### Basic Usage

```xml
<draw:SkiaSlider
    Minimum="0"
    Maximum="100"
    Value="50"
    WidthRequest="300"
    HeightRequest="40"
    TrackColor="LightGray"
    ThumbColor="Blue"
    ValueChanged="OnSliderValueChanged" />
```

### Range Selection

```xml
<draw:SkiaSlider
    Minimum="0"
    Maximum="100"
    Value="25"
    ValueTo="75"
    IsRange="true"
    WidthRequest="300"
    HeightRequest="40"
    TrackColor="LightGray"
    ThumbColor="Blue" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Minimum` | double | Minimum value of the slider |
| `Maximum` | double | Maximum value of the slider |
| `Value` | double | Current value (or start value for range) |
| `ValueTo` | double | End value for range selection |
| `IsRange` | bool | Whether the slider supports range selection |
| `TrackColor` | Color | Color of the slider track |
| `ThumbColor` | Color | Color of the slider thumb |
| `Step` | double | Step increment for value changes |

### Events

- `ValueChanged`: Raised when the slider value changes
  - Event signature: `EventHandler<double>`

## SkiaProgress

`SkiaProgress` is a progress indicator control to show that you are actually doing something, with support for determinate and indeterminate progress.

### Basic Usage

```xml
<draw:SkiaProgress
    Progress="0.5"
    WidthRequest="200"
    HeightRequest="20"
    ProgressColor="Green"
    BackgroundColor="LightGray"
    CornerRadius="10" />
```

### Indeterminate Progress

```xml
<draw:SkiaProgress
    IsIndeterminate="true"
    WidthRequest="200"
    HeightRequest="20"
    ProgressColor="Blue"
    BackgroundColor="LightGray" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Progress` | double | Progress value (0.0 to 1.0) |
| `IsIndeterminate` | bool | Whether to show indeterminate progress |
| `ProgressColor` | Color | Color of the progress bar |
| `BackgroundColor` | Color | Background color of the progress track |
| `CornerRadius` | double | Corner radius for rounded progress bar |

## SkiaWheelPicker

`SkiaWheelPicker` provides an iOS-style picker wheel for selecting items from a list.

### Basic Usage

```xml
<draw:SkiaWheelPicker
    ItemsSource="{Binding Items}"
    SelectedItem="{Binding SelectedItem}"
    WidthRequest="200"
    HeightRequest="150"
    ItemHeight="40"
    VisibleItemsCount="5" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `ItemsSource` | IEnumerable | Collection of items to display |
| `SelectedItem` | object | Currently selected item |
| `SelectedIndex` | int | Index of the selected item |
| `ItemHeight` | double | Height of each item in the picker |
| `VisibleItemsCount` | int | Number of visible items |
| `IsLooped` | bool | Whether the picker loops infinitely |

### Events

- `SelectionChanged`: Raised when the selected item changes

## SkiaSpinner

`SkiaSpinner` is a spinner control to test your luck, providing a rotating wheel with customizable segments.

### Basic Usage

```xml
<draw:SkiaSpinner
    Segments="{Binding SpinnerSegments}"
    WidthRequest="200"
    HeightRequest="200"
    SpinDuration="3000"
    SpinCompleted="OnSpinCompleted" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Segments` | IEnumerable | Collection of spinner segments |
| `SelectedSegment` | object | Currently selected segment |
| `SpinDuration` | int | Duration of spin animation in milliseconds |
| `IsSpinning` | bool | Whether the spinner is currently spinning |
| `SpinVelocity` | double | Initial velocity for the spin |

### Methods

- `Spin()`: Start spinning the wheel
- `Stop()`: Stop the spinning animation

### Events

- `SpinCompleted`: Raised when the spin animation completes
  - Event signature: `EventHandler<object>` where object is the selected segment

## SkiaEditor

`SkiaEditor` is a fully drawn text editor rendered entirely on the SkiaSharp canvas. It uses a hidden native control (EditText / UITextView / TextBox) as a keyboard sink, so all text rendering, cursor drawing, and selection are handled in SkiaSharp — giving pixel-perfect, fully styleable text input on every platform including Blazor WASM.

### Single-line editor

```xml
<draw:SkiaEditor
    HorizontalOptions="Fill"
    MaxLines="1"
    FontSize="16"
    TextColor="Black"
    CursorColor="DodgerBlue"
    BackgroundColor="#F5F5F5"
    Padding="12,8"
    ReturnType="Done"
    Text="{Binding Username}"
    TextSubmitted="OnSubmit" />
```

### Multiline editor (chat-style)

```xml
<draw:SkiaEditor
    HorizontalOptions="Fill"
    MaxLines="4"
    FontSize="16"
    TextColor="White"
    CursorColor="White"
    BackgroundColor="#1E1E2E"
    Padding="12,8"
    ReturnType="Send"
    TextSubmitted="OnSend" />
```

### Numeric / Password

```xml
<!-- Numbers only -->
<draw:SkiaEditor
    MaxLines="1"
    KeyboardType="Numeric"
    ReturnType="Done"
    Placeholder="Enter amount" />

<!-- Password -->
<draw:SkiaEditor
    MaxLines="1"
    IsPassword="True"
    ReturnType="Done"
    Text="{Binding Password}" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | string | `""` | Current text value |
| `MaxLines` | int | `1` | `1` = single-line; `>1` = multiline with that many visible lines |
| `FontSize` | double | `14` | Font size in logical pixels |
| `FontFamily` | string | `null` | Font family name |
| `FontWeight` | int | `400` | Weight (400 normal, 700 bold) |
| `TextColor` | Color | Black | Drawn text color |
| `CursorColor` | Color | Black | Blinking cursor color |
| `SelectionColor` | Color | — | Selection highlight color |
| `KeyboardType` | `SkiaEditorKeyboard` | `Default` | Software keyboard layout |
| `ReturnType` | `ReturnType` | `Done` | IME action button label |
| `IsPassword` | bool | `false` | Masks text with `•`; disables autocorrect |
| `IsFocused` | bool | `false` | Focus state; set to `true` to open keyboard programmatically |
| `LineHeight` | double | `1.3` | Line height multiplier |
| `HorizontalTextAlignment` | DrawTextAlignment | `Start` | Text alignment |
| `Padding` | Thickness | `0` | Inner padding around text |
| `UseMarkdown` | bool | `false` | Render text as Markdown |

### `SkiaEditorKeyboard` values

| Value | Android | iOS | Description |
|-------|---------|-----|-------------|
| `Default` | `TYPE_CLASS_TEXT` | Default | Standard QWERTY, autocorrect on |
| `Numeric` | `TYPE_CLASS_NUMBER` | NumberPad | Integers only |
| `Decimal` | `TYPE_CLASS_NUMBER \| DECIMAL` | DecimalPad | Numbers with decimal point |
| `Phone` | `TYPE_CLASS_PHONE` | PhonePad | Phone number layout |
| `Email` | `TYPE_TEXT_VARIATION_EMAIL` | EmailAddress | Email layout with `@` and `.` keys |

### `ReturnType` values

`Done`, `Go`, `Next`, `Search`, `Send` — sets the IME action button label. Tapping it calls `Submit()` on single-line editors; on multiline editors it inserts a newline (except `ReturnType.Send` which also fires `TextSubmitted`).

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `TextChanged` | `EventHandler<string>` | Fires on every keystroke |
| `FocusChanged` | `EventHandler<bool>` | Fires when keyboard opens or closes |
| `TextSubmitted` | `EventHandler<string>` | Fires when IME action button tapped (single-line) |

### Commands

| Command | Description |
|---------|-------------|
| `CommandOnSubmit` | Executed with current `Text` on submit |
| `CommandOnFocusChanged` | Executed with `bool` focus state |
| `CommandOnTextChanged` | Executed with current `Text` on change |

### Programmatic focus

```csharp
// Open keyboard
myEditor.IsFocused = true;

// Close keyboard
myEditor.IsFocused = false;
```

### Notes

- Cursor renders using `SkiaCursor` — width defaults to 2 px, height tracks the measured line height.
- Password masking (`•`) is drawn by the canvas layer; the hidden native control is always invisible, so native password masking is irrelevant on Windows.
- `MaxLines="1"` with `ReturnType="Send"` is the typical chat input setup.
- Scroll inside a multiline editor is handled automatically via an internal `SkiaScroll`.
