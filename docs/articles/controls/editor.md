# SkiaEditor

`SkiaEditor` is a fully drawn text editor rendered entirely on the SkiaSharp canvas. On .NET MAUI it uses a hidden native control (Android `EditText`, iOS `UITextView`, Windows `TextBox`) purely as a keyboard sink — all text rendering, cursor drawing, and selection happen in SkiaSharp. This gives pixel-perfect, fully styleable text input on every platform including Blazor WASM.

## How it works

```
User tap → SkiaEditor gesture handler
             ↓ positions drawn cursor
             ↓ focuses hidden native control (1×1 px, invisible)
                   ↓ platform IME opens
                   ↓ keystrokes → native TextWatcher / delegate
                         ↓ Text property updated
                         ↓ SkiaLabel re-renders with new text
                         ↓ cursor repositioned
```

The native control is never visible — it exists solely so the platform IME has a valid input target.

## Basic usage

### Single-line

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

### Multiline

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

### Numeric input

```xml
<draw:SkiaEditor
    MaxLines="1"
    KeyboardType="Numeric"
    ReturnType="Done"
    Text="{Binding Amount}" />
```

### Password

```xml
<draw:SkiaEditor
    MaxLines="1"
    IsPassword="True"
    ReturnType="Done"
    Text="{Binding Password}" />
```

## Properties

### Text

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | string | `null` | Current text value |
| `MaxLines` | int | `1` | `1` = single-line; `>1` = multiline with that many visible lines |
| `IsMultiline` | bool | — | Read-only; `true` when `MaxLines != 1` |
| `AutoHeight` | bool | `false` | Multiline only: start one line tall and grow with the text up to `MaxLines`, then scroll. Ignored when `HeightRequest` is set |

### Appearance

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FontSize` | double | `12` | Font size in points |
| `FontFamily` | string | `""` | Font family name |
| `FontWeight` | int | `0` (not set) | Weight (400 normal, 700 bold) |
| `TextColor` | Color | Black | Drawn text color |
| `CursorColor` | Color | style accent | Blinking cursor color; when not set, the `ControlStyle` accent (crimson for the default look) |
| `SelectionColor` | Color | `#5590CFFE` | Selection highlight color |
| `LineHeight` | double | `1.0` | Line height multiplier |
| `HorizontalTextAlignment` | DrawTextAlignment | `Start` | Text alignment |
| `Padding` | Thickness | `12,8` | Inner padding around text (`12,8` when not set) |
| `PlaceholderText` | string | `null` | Text shown while the editor is empty |
| `PlaceholderColor` | Color | `#9AA0A6` | Placeholder text color |
| `UseMarkdown` | bool | `false` | Render text as Markdown |

### Keyboard and input

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyboardType` | `SkiaEditorKeyboard` | `Default` | Software keyboard layout |
| `ReturnType` | `ReturnType` | `Done` | IME action button label and behavior |
| `IsPassword` | bool | `false` | Masks drawn text with `•`; disables autocorrect |

### Focus and selection

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsFocused` | bool | `false` | Set to `true` to open keyboard programmatically |
| `CursorPosition` | int | `0` | Character index of the cursor |
| `SelectionLength` | int | `0` | Number of selected characters |

## KeyboardType

`SkiaEditorKeyboard` enum controls the software keyboard shown when the editor is focused.

| Value | Android `InputType` | iOS `UIKeyboardType` | Description |
|-------|---------------------|----------------------|-------------|
| `Default` | `TYPE_CLASS_TEXT` | `Default` | Standard QWERTY, autocorrect on |
| `Numeric` | `TYPE_CLASS_NUMBER` | `NumberPad` | Integers only |
| `Decimal` | `TYPE_CLASS_NUMBER \| FLAG_DECIMAL` | `DecimalPad` | Numbers with decimal point |
| `Phone` | `TYPE_CLASS_PHONE` | `PhonePad` | Phone number layout |
| `Email` | `TYPE_TEXT_VARIATION_EMAIL_ADDRESS` | `EmailAddress` | Email keyboard with `@` and `.` keys |

## ReturnType

Controls the label/action of the IME confirm button.

| Value | Key label | Behavior |
|-------|-----------|----------|
| `Done` | Done | Fires `TextSubmitted` |
| `Go` | Go | Fires `TextSubmitted` |
| `Next` | Next | Fires `TextSubmitted` |
| `Search` | Search | Fires `TextSubmitted` |
| `Send` | Send | Fires `TextSubmitted` |

On a single-line editor the confirm key always fires `TextSubmitted`; on Android and iOS it also removes focus, which closes the keyboard. On a multiline editor it inserts a line break, except with `ReturnType="Send"`: then Enter fires `TextSubmitted` and keeps focus, and Shift+Enter inserts a line break (iOS cannot tell Shift+Enter apart and always sends).

## Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `TextChanged` | `EventHandler<string>` | Fires on every keystroke |
| `FocusChanged` | `EventHandler<bool>` | Fires when keyboard opens or closes |
| `TextSubmitted` | `EventHandler<string>` | Fires when IME action button is tapped |
| `CursorMoved` | `EventHandler` | Fires when the cursor position changes |

## Commands

| Command | Argument | Description |
|---------|----------|-------------|
| `CommandOnSubmit` | `string` (current text) | Executed on submit |
| `CommandOnFocusChanged` | `bool` (focus state) | Executed on focus change |
| `CommandOnTextChanged` | `string` (current text) | Executed on every keystroke |

## Keyboard navigation

An editor is a Tab stop by default. Tab into it and it takes the caret, so typing goes into it; `IsFocused` and the canvas `FocusedChild` are set exactly as after a click. Tab and Shift+Tab leave the field and move on from it, also after a click into it (no tab characters, except in an OpenTK `CanvasHost` overlay), and while editing the arrows, Home / End and Enter work on the text. The next control keeps the keyboard, so Enter or Space presses a button reached this way. See [Accessibility](../advanced/accessibility.md#keyboard-navigation).

## Programmatic focus

```csharp
// Open keyboard
myEditor.IsFocused = true;

// Close keyboard
myEditor.IsFocused = false;

// Move cursor to end
myEditor.CursorPosition = myEditor.Text?.Length ?? 0;
```

## C# construction

```csharp
var editor = new SkiaEditor
{
    HorizontalOptions = LayoutOptions.Fill,
    MaxLines = 1,
    FontSize = 16,
    TextColor = Colors.Black,
    CursorColor = Colors.DodgerBlue,
    BackgroundColor = Color.Parse("#F5F5F5"),
    Padding = new Thickness(12, 8),
    ReturnType = ReturnType.Done,
    KeyboardType = SkiaEditor.SkiaEditorKeyboard.Default,
};
editor.TextSubmitted += (s, text) => Console.WriteLine(text);
```

## Chat input pattern

```xml
<draw:SkiaEditor
    MaxLines="4"
    AutoHeight="True"
    ReturnType="Send"
    KeyboardType="Default"
    FontSize="16"
    TextColor="Black"
    CursorColor="Black"
    Padding="12,8"
    TextSubmitted="OnSendMessage" />
```

`AutoHeight="True"` starts the editor one line tall and grows it with the text; `MaxLines="4"` caps the visible height at 4 lines, then the editor scrolls internally. Without `AutoHeight` the editor always reserves 4 lines. `ReturnType="Send"` shows the Send button on the IME, and Enter sends.

## Notes

- Cursor height adapts automatically to the rendered line height. Cursor width defaults to 2 points.
- `IsPassword` masking (`•`) is drawn at the canvas layer. The hidden native control is always invisible, so OS-level password masking only affects Android's native `TransformationMethod` (irrelevant for display but prevents text leaking into autocomplete).
- On Blazor, pure WebAssembly, WPF and OpenTK the editor works without a native control — keys come from the head's keyboard input (JS key listeners in the browser).
- Embedding inside a `SkiaScroll` works — the editor scroll and the outer scroll coexist via gesture routing.
