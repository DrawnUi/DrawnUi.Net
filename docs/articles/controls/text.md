# Text Controls

DrawnUI offers text rendering capabilities through its specialized text controls. These controls provide text rendering with advanced formatting options while maintaining consistent appearance across all platforms.

## SkiaLabel

SkiaLabel is the primary text rendering control in DrawnUI, rendering text directly with SkiaSharp. Unlike traditional MAUI labels, SkiaLabel provides pixel-perfect text rendering with advanced formatting capabilities.

### Basic Usage

```xml
<draw:SkiaLabel
    Text="Hello World"
    TextColor="Black"
    FontSize="18"
    HorizontalTextAlignment="Center"
    VerticalTextAlignment="Center" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Text` | string | The text content to display |
| `TextColor` | Color | Text color |
| `FontFamily` | string | Font family name |
| `FontSize` | double | Font size in points (default 12) |
| `FontWeight` | int | Font weight (100-900 scale, 400=normal, 700=bold) |
| `FontAttributes` | FontAttributes | Bold/Italic/None |
| `HorizontalTextAlignment` | DrawTextAlignment | Text horizontal alignment (Start, Center, End, FillWords, FillWordsFull, FillCharacters, FillCharactersFull) |
| `VerticalTextAlignment` | TextAlignment | Text vertical alignment (Start, Center, End) |
| `LineBreakMode` | LineBreakMode | How text should wrap or truncate. `WordWrap` breaks at spaces and between Chinese and Japanese characters (a line never starts with 、。」ー or small kana), and breaks a word wider than the line, like a long URL, by characters |
| `MaxLines` | int | Maximum number of lines to display |
| `StrokeColor` | Color | Outline color |
| `StrokeWidth` | double | Outline width |
| `DropShadowColor` | Color | Shadow color |
| `DropShadowSize` | double | Shadow blur radius |
| `DropShadowOffsetX`/`DropShadowOffsetY` | double | Shadow offset |
| `AutoSize` | AutoSizeType | Auto-sizing mode |
| `AutoSizeText` | string | Text to use for auto-sizing calculations |
| `LineSpacing` | double | Line spacing multiplier |
| `ParagraphSpacing` | double | Paragraph spacing multiplier |
| `CharacterSpacing` | double | Character spacing multiplier |
| `MonoForDigits` | string | Use mono width for digits (e.g. "8") |

### Rich Text Formatting (Spans)

SkiaLabel supports rich text formatting through its `Spans` collection:

```xml
<draw:SkiaLabel>
    <draw:SkiaLabel.Spans>
        <draw:TextSpan Text="Hello " TextColor="Black" FontSize="18" />
        <draw:TextSpan Text="Beautiful " TextColor="Red" FontSize="20" FontWeight="700" />
        <draw:TextSpan Text="World!" TextColor="Blue" FontSize="18" IsItalic="True" />
    </draw:SkiaLabel.Spans>
</draw:SkiaLabel>
```

#### Interactive Spans

You can make any text span interactive by adding the `Tapped` event handler:

```xml
<draw:SkiaLabel FontSize="15" LineSpacing="1.5" TextColor="Black">
    <draw:TextSpan Text="Regular text " />
    <draw:TextSpan
        Text="tappable link"
        TextColor="Purple"
        Tapped="OnSpanTapped"
        Tag="link-id"
        Underline="True" />
    <draw:TextSpan Text=" more text..." />
</draw:SkiaLabel>
```

In your code-behind:

```csharp
private void OnSpanTapped(object sender, EventArgs e)
{
    var span = sender as TextSpan;
    string tag = span?.Tag?.ToString();
    // Handle the tap event based on the span or its tag
}
```

#### Styling Spans

TextSpan supports various styling options:

```xml
<draw:TextSpan Text="Bold text" IsBold="True" />
<draw:TextSpan Text="Italic text" IsItalic="True" />
<draw:TextSpan Text="Underlined text" Underline="True" />
<draw:TextSpan Text="Strikethrough text" Strikeout="True" />
<draw:TextSpan Text="Highlighted text" BackgroundColor="Yellow" />
```

#### Emoji Support

For emoji rendering, use the `AutoFindFont` property of the span:

```xml
<draw:TextSpan Text="Regular text " />
<draw:TextSpan AutoFindFont="True" Text="🌐🚒🙎🏽👻🤖" />
<draw:TextSpan Text=" more text..." />
```

This ensures proper emoji rendering by finding and using appropriate fonts.

#### Fallback fonts

Without spans, name the fonts to use for glyphs the label's own font does not have. Each missing glyph is drawn with the first of them that has it, the rest of the text keeps the label's font:

```xml
<draw:SkiaLabel Text="Rating ★★★★☆ → 4/5" FontFamilyFallback="FontSymbols, FontSymbols2" />
```

A glyph that none of the fonts has becomes `FallbackCharacter`. With `AutoFont="True"` the whole label switches to the font of its first glyph instead.

### Text Effects

SkiaLabel supports various text effects:

#### Drop Shadow

Use the following properties for shadow effects:
- `DropShadowColor`: Shadow color
- `DropShadowSize`: Blur radius
- `DropShadowOffsetX`, `DropShadowOffsetY`: Shadow offset

```xml
<draw:SkiaLabel
    Text="Shadowed Text"
    FontSize="24"
    TextColor="White"
    DropShadowColor="#80000000"
    DropShadowSize="3"
    DropShadowOffsetX="1"
    DropShadowOffsetY="1" />
```

#### Outlined Text

```xml
<draw:SkiaLabel
    Text="Outlined Text"
    FontSize="24"
    TextColor="White"
    StrokeColor="Black"
    StrokeWidth="1" />
```

#### Gradient Text

```xml
<draw:SkiaLabel
    Text="Gradient Text"
    FontSize="24"
    FillGradient="{StaticResource MyGradient}" />
```

### Auto-sizing Text

SkiaLabel features powerful automatic font sizing capabilities that can dynamically adjust text to fit your container:

```xml
<draw:SkiaLabel
    Text="This text will resize to fit the available space"
    AutoSize="FitFillHorizontal"
    FontSize="24"
    MaxLines="1" />
```

- `AutoSize`: Controls auto-sizing mode: `None` (default), `FitHorizontal` / `FitVertical` (make the font smaller until the text fits), `FillHorizontal` / `FillVertical` (make it bigger to fill the space), `FitFillHorizontal` / `FitFillVertical` (both).
- `AutoSizeText`: Text to use for sizing calculations

### Monospaced Text Rendering

SkiaLabel can give every digit the same width, regardless of the font used, so changing numbers (counters, timers) do not jump around:

```xml
<draw:SkiaLabel
    Text="12:30:45"
    FontSize="18"
    MonoForDigits="8" />
```

- `MonoForDigits`: Use mono width for digits (e.g. "8")

### Selectable text

A label can let people select and copy its text. It is off by default, turn it on with `AccessibilityTextSelectable`:

```csharp
new SkiaLabel("You can select and copy this paragraph.")
{
    AccessibilityTextSelectable = true,
}
```

- Mouse: drag over the text to select it, double click selects a word. Ctrl+C (Cmd+C on Mac) copies, Ctrl+A selects the whole text.
- Touch: a long press selects a word, then drag to extend it. A Copy button appears next to the selection.
- A click or tap anywhere else clears the selection.
- From code: `Select(start, length)`, `SelectAll()`, `ClearSelection()`, `CopySelection()`, `SelectedText`, `SelectionStart`, `SelectionLength`.
- Look: the static `SkiaLabel.TextSelectionColor` and `SkiaLabel.CopyButtonText`.

Copying goes through `Super.SetClipboardText`, which every head fills (MAUI, WPF, OpenTK, Blazor, WebAssembly); set it yourself to route copies elsewhere. On .NET MAUI Windows the Ctrl+C / Ctrl+A keys need `UseDesktopKeyboard = true` in `DrawnUiStartupSettings`. A selectable label takes the mouse press for itself, so a parent does not get a drag that starts on its text.

### Performance Considerations

- For static text, set `UseCache="Image"` to render once and cache as bitmap
- For frequently updated text, use `UseCache="Operations"` (the label default) for best performance
- Consider setting `MaxLines` when appropriate to avoid unnecessary layout calculations
- For large blocks of text, monitor performance and consider breaking into multiple labels
- Use monospaced features only when needed as it adds some calculation overhead
- For complex shadow effects, consider using `UseCache="Image"` to optimize rendering

## SkiaRichLabel

SkiaRichLabel extends SkiaLabel to provide Markdown formatting capabilities. It parses Markdown syntax and renders properly formatted text.

### Basic Usage

Set the markdown in `Text`. In XAML put a single line in the `Text` attribute; multi-line markdown is easiest from code:

```csharp
new SkiaRichLabel
{
    Text = """
           # Markdown Title

           This is a paragraph with **bold** and *italic* text.

           - List item 1
           - List item 2

           [Visit Documentation](https://link.example.com)

           `Inline code` looks like this.
           """
};
```

### Supported Markdown Features

- **Headings** (# H1, ## H2)
- **Text formatting** (bold, italic, strikethrough)
- **Lists** (bulleted and numbered)
- **Links** (with customizable styling)
- **Code** (inline and blocks)
- **Paragraphs** (with proper spacing)

### Customizing Markdown Style

```xml
<draw:SkiaRichLabel
    LinkColor="Blue"
    CodeTextColor="DarkGreen"
    CodeBackgroundColor="#EEEEEE"
    StrikeoutColor="Red"
    PrefixBullet="• "
    PrefixNumbered="{}{0}. "
    UnderlineLink="True"
    UnderlineWidth="1"
    Text="This has **custom** styling for [links](https://example.com) and `code blocks`." />
```

### Link Handling

SkiaRichLabel provides built-in support for handling link taps:

```xml
<draw:SkiaRichLabel
    LinkTapped="OnLinkTapped"
    CommandLinkTapped="{Binding OpenLinkCommand}"
    Text="Check out [this link](https://example.com)!" />
```

In your code-behind:

```csharp
private void OnLinkTapped(object sender, string url)
{
    // url parameter contains the link URL
    Browser.OpenAsync(url);
}
```

### Implementation Notes

- SkiaRichLabel parses Markdown with CommonMark.NET (plus `~~strikethrough~~`) and draws only the features listed above
- The parser focuses on the most commonly used Markdown syntax for mobile applications
- For more complex Markdown rendering needs, consider creating a custom renderer

## Special Labels

### SkiaLabelFps

A specialized label for displaying frames-per-second (FPS) metrics, useful for performance monitoring during development:

```xml
<draw:SkiaLabelFps
    TextColor="Green"
    FontSize="12"
    HorizontalOptions="End"
    VerticalOptions="Start"
    Margin="0,20,20,0" />
```

## Example: Text Card

```xml
<draw:SkiaShape
    Type="Rectangle"
    BackgroundColor="White"
    CornerRadius="8"
    Padding="16"
    WidthRequest="300">

    <draw:SkiaShape.Shadows>
        <draw:SkiaShadow
            Color="#22000000"
            Blur="10"
            X="0"
            Y="2" />
    </draw:SkiaShape.Shadows>

    <draw:SkiaLayout Type="Column" Spacing="8">
        <draw:SkiaLabel
            Text="Article Title"
            FontSize="20"
            FontWeight="700"
            TextColor="#333333" />

        <draw:SkiaLabel
            Text="Published on April 3, 2025"
            FontSize="12"
            TextColor="#666666" />

        <draw:SkiaLabel
            Text="Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam in dui mauris. Vivamus hendrerit arcu sed erat molestie vehicula. Sed auctor neque eu tellus rhoncus ut eleifend nibh porttitor."
            FontSize="14"
            TextColor="#444444"
            LineHeight="1.5" />

        <draw:SkiaLabel>
            <draw:SkiaLabel.Spans>
                <draw:TextSpan Text="Read more " TextColor="#444444" FontSize="14" />
                <draw:TextSpan Text="here" TextColor="Blue" FontSize="14" Underline="True" />
            </draw:SkiaLabel.Spans>
        </draw:SkiaLabel>
    </draw:SkiaLayout>
</draw:SkiaShape>
```