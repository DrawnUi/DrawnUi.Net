# SkiaEditor

`SkiaEditor` is a fully drawn text editor rendered entirely on the SkiaSharp canvas. It uses a hidden native control (Android `EditText`, iOS `UITextView`, Windows `TextBox`) purely as a keyboard sink — all text rendering, cursor drawing, and selection happen in SkiaSharp. This gives pixel-perfect, fully styleable text input on every platform including Blazor WASM.

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
| `Text` | string | `""` | Current text value |
| `MaxLines` | int | `1` | `1` = single-line; `>1` = multiline with that many visible lines |
| `IsMultiline` | bool | — | Read-only; `true` when `MaxLines != 1` |

### Appearance

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FontSize` | double | `14` | Font size in logical pixels |
| `FontFamily` | string | `null` | Font family name |
| `FontWeight` | int | `400` | Weight (400 normal, 700 bold) |
| `TextColor` | Color | Black | Drawn text color |
| `CursorColor` | Color | Black | Blinking cursor color |
| `SelectionColor` | Color | — | Selection highlight color |
| `LineHeight` | double | `1.3` | Line height multiplier |
| `HorizontalTextAlignment` | DrawTextAlignment | `Start` | Text alignment |
| `Padding` | Thickness | `0` | Inner padding around text |
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
| `Done` | Done | Closes keyboard, fires `TextSubmitted` |
| `Go` | Go | Fires `TextSubmitted` |
| `Next` | Next | Fires `TextSubmitted` |
| `Search` | Search | Fires `TextSubmitted` |
| `Send` | Send | Fires `TextSubmitted` |

On multiline editors the confirm key inserts a newline instead of submitting, regardless of `ReturnType`.

## Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `TextChanged` | `EventHandler<string>` | Fires on every keystroke |
| `FocusChanged` | `EventHandler<bool>` | Fires when keyboard opens or closes |
| `TextSubmitted` | `EventHandler<string>` | Fires when IME action button is tapped |

## Commands

| Command | Argument | Description |
|---------|----------|-------------|
| `CommandOnSubmit` | `string` (current text) | Executed on submit |
| `CommandOnFocusChanged` | `bool` (focus state) | Executed on focus change |
| `CommandOnTextChanged` | `string` (current text) | Executed on every keystroke |

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
    ReturnType="Send"
    KeyboardType="Default"
    FontSize="16"
    TextColor="Black"
    CursorColor="Black"
    Padding="12,8"
    TextSubmitted="OnSendMessage" />
```

`MaxLines="4"` caps visible height at 4 lines; the editor scrolls internally if the user types more. `ReturnType="Send"` shows the Send button on the IME.

## Notes

- Cursor height adapts automatically to the rendered line height. Cursor width defaults to 2 px.
- `IsPassword` masking (`•`) is drawn at the canvas layer. The hidden native control is always invisible, so OS-level password masking only affects Android's native `TransformationMethod` (irrelevant for display but prevents text leaking into autocomplete).
- On Blazor WASM the editor works without a native control — keyboard routing uses JS global key listeners.
- Embedding inside a `SkiaScroll` works — the editor scroll and the outer scroll coexist via gesture routing.
