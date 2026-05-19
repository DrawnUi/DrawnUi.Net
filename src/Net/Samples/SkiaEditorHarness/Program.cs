using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Views;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

Super.Init();

var outputDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts");
Directory.CreateDirectory(outputDirectory);

using var host = new HeadlessCanvasHost(900, 780);

var status = new SkiaLabel
{
    Tag = "status",
    Text = "SkiaEditorHarness ready",
    HorizontalOptions = LayoutOptions.Fill,
    FontSize = 18,
    TextColor = Colors.Black,
    Margin = new Thickness(0, 0, 0, 12)
};

var editor = new SkiaEditor
{
    Tag = "editor",
    HorizontalOptions = LayoutOptions.Fill,
    HeightRequest = 280,
    FontSize = 24,
    MaxLines = 8,
    TextColor = Colors.Black,
    CursorColor = Colors.DodgerBlue,
    BackgroundColor = Colors.White,
    SelectionColor = DrawnUi.Color.FromArgb("#5590CFFE")
};

var centeredPlaceholderEditor = new SkiaEditor
{
    Tag = "centered-ph",
    HorizontalOptions = LayoutOptions.Fill,
    MaxLines = 1,
    FontSize = 20,
    TextColor = Colors.Black,
    CursorColor = Colors.OrangeRed,
    BackgroundColor = Colors.LightYellow,
    HorizontalTextAlignment = DrawTextAlignment.Center,
    PlaceholderText = "Search…",
    PlaceholderColor = Colors.Gray,
    PlaceholderHorizontalAlignment = DrawTextAlignment.Center,
};

var richEditor = new SkiaRichEditor
{
    Tag = "rich-editor",
    HorizontalOptions = LayoutOptions.Fill,
    HeightRequest = 200,
    FontSize = 20,
    MaxLines = 8,
    TextColor = Colors.DarkBlue,
    CursorColor = Colors.DarkBlue,
    BackgroundColor = Colors.AliceBlue,
    SelectionColor = DrawnUi.Color.FromArgb("#5590CFFE")
};

var scene = new SkiaLayout
{
    Tag = "scene",
    Type = LayoutType.Column,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Padding = new Thickness(40),
    Spacing = 0,
    Children =
    {
        status,
        editor,
        centeredPlaceholderEditor,
        richEditor
    }
};

host.Canvas.Children = new List<SkiaControl> { scene };

var context = new HarnessContext(host, scene, "editor", new IHarnessAdapter[]
{
    new SkiaRichEditorHarnessAdapter(),
    new SkiaEditorHarnessAdapter()
});

host.Render();

TrySetFocus(editor, true);
editor.Text = string.Empty;
editor.CursorPosition = 0;
editor.SelectionLength = 0;

var steps = ParseSteps(args).ToList();
if (steps.Count == 0)
{
    steps = CreateDefaultScenario();
}

WriteSnapshot(context, outputDirectory, 0, "initial");

for (var index = 0; index < steps.Count; index++)
{
    var step = steps[index];
    ExecuteStep(context, step);
    WriteSnapshot(context, outputDirectory, index + 1, step.Name);
}

var finalImagePath = Path.Combine(outputDirectory, "final.png");
host.SavePng(finalImagePath);

var summaryPath = Path.Combine(outputDirectory, "summary.txt");
File.WriteAllText(summaryPath, Describe(context));

Console.WriteLine("DrawnUi.Net harness complete.");
Console.WriteLine($"Artifacts: {outputDirectory}");
Console.WriteLine($"FinalImage: {finalImagePath}");
Console.WriteLine($"Summary: {summaryPath}");
Console.WriteLine($"Steps: {steps.Count}");
Console.WriteLine(Describe(context));

static void ExecuteStep(HarnessContext context, HarnessStep step)
{
    switch (step.Command)
    {
        case HarnessCommand.Target:
            context.CurrentTargetTag = string.IsNullOrWhiteSpace(step.TargetTag) ? context.DefaultTargetTag : step.TargetTag;
            return;
        case HarnessCommand.Render:
        case HarnessCommand.Snapshot:
            return;
    }

    var target = ResolveTarget(context, step);

    switch (step.Command)
    {
        case HarnessCommand.Focus:
            if (!TrySetFocus(target, true))
                throw new InvalidOperationException($"Target '{target.Tag}' does not support focus.");
            return;
        case HarnessCommand.Blur:
            if (!TrySetFocus(target, false))
                throw new InvalidOperationException($"Target '{target.Tag}' does not support blur.");
            return;
        case HarnessCommand.SetProperty:
            ApplyProperty(target, step.PropertyName, step.Text ?? string.Empty);
            return;
        default:
            foreach (var adapter in context.Adapters)
            {
                if (adapter.CanHandle(target) && adapter.TryExecute(target, step, context))
                    return;
            }

            throw new InvalidOperationException($"Unsupported command '{step.Command}' for target '{target.Tag}' ({target.GetType().Name}).");
    }
}

static SkiaControl ResolveTarget(HarnessContext context, HarnessStep step)
{
    var targetTag = !string.IsNullOrWhiteSpace(step.TargetTag) ? step.TargetTag : context.CurrentTargetTag;
    var target = context.Root.FindViewByTag(targetTag);

    if (target == null)
        throw new InvalidOperationException($"Could not find harness target '{targetTag}'.");

    return target;
}

static void WriteSnapshot(HarnessContext context, string outputDirectory, int index, string name)
{
    context.Host.Render();

    var safeName = SanitizeFileName(name);
    var imagePath = Path.Combine(outputDirectory, $"{index:00}-{safeName}.png");
    var statePath = Path.Combine(outputDirectory, $"{index:00}-{safeName}.txt");

    context.Host.SavePng(imagePath);
    File.WriteAllText(statePath, Describe(context));
}

static List<HarnessStep> CreateDefaultScenario()
{
    return new List<HarnessStep>
    {
        // --- plain editor scenario ---
        new(HarnessCommand.Target, "target-status", TargetTag: "status"),
        new(HarnessCommand.SetProperty, "set-status-text", Text: "Editor target follows below", PropertyName: "Text"),
        new(HarnessCommand.Target, "target-editor", TargetTag: "editor"),
        new(HarnessCommand.Type, "type-line-1", Text: "first line"),
        new(HarnessCommand.Enter, "enter-1"),
        new(HarnessCommand.Type, "type-line-2", Text: "second line"),
        new(HarnessCommand.Enter, "enter-2"),
        new(HarnessCommand.Type, "type-line-3", Text: "third line that should stay visible while typing"),
        new(HarnessCommand.Left, "move-left-8", Count: 8),
        new(HarnessCommand.Type, "insert-middle", Text: "[MID]"),
        new(HarnessCommand.SelectLineColumn, "select-second-line", Line: 1, Column: 0, EndLine: 1, EndColumn: 11),
        new(HarnessCommand.Type, "replace-selection", Text: "SECOND"),

        // --- centered-placeholder scenario ---
        new(HarnessCommand.Target, "target-centered-ph", TargetTag: "centered-ph"),
        new(HarnessCommand.AssertPlaceholderVisible, "ph-visible-initial"),    // must show when empty
        new(HarnessCommand.Type, "ph-type-hello", Text: "hello"),
        new(HarnessCommand.AssertPlaceholderHidden, "ph-hidden-after-type"),   // must hide when text present
        new(HarnessCommand.Backspace, "ph-backspace-all", Count: 5),
        new(HarnessCommand.AssertPlaceholderVisible, "ph-visible-after-clear"), // must reappear when empty again

        // --- rich editor scenario ---
        new(HarnessCommand.Target, "target-rich-editor", TargetTag: "rich-editor"),
        new(HarnessCommand.Type, "rich-type-hello", Text: "Hello world"),
        // Select "world" (chars 6-11) and apply bold
        new(HarnessCommand.Select, "rich-select-world", Start: 6, Count: 5),
        new(HarnessCommand.Bold, "rich-bold-world"),
        new(HarnessCommand.AssertFormat, "assert-bold-on", Text: "Bold=true"),
        // Move caret after "world" then type italic text
        new(HarnessCommand.Right, "rich-move-after-world"),
        new(HarnessCommand.Type, "rich-type-space", Text: " "),
        new(HarnessCommand.Type, "rich-type-italic-word", Text: "italic"),
        new(HarnessCommand.Select, "rich-select-italic", Start: 12, Count: 6),
        new(HarnessCommand.Italic, "rich-italic-italic"),
        new(HarnessCommand.AssertFormat, "assert-italic-on", Text: "Italic=true"),
        // Undo italic
        new(HarnessCommand.Undo, "rich-undo-italic"),
        new(HarnessCommand.AssertFormat, "assert-italic-off", Text: "Italic=false"),
        // Redo italic
        new(HarnessCommand.Redo, "rich-redo-italic"),
        new(HarnessCommand.AssertFormat, "assert-italic-back", Text: "Italic=true"),
        // Underline the entire text
        new(HarnessCommand.SelectAll, "rich-select-all"),
        new(HarnessCommand.Underline, "rich-underline-all"),
        // Strikethrough "Hello"
        new(HarnessCommand.Select, "rich-select-hello", Start: 0, Count: 5),
        new(HarnessCommand.Strikethrough, "rich-strikethrough-hello"),
        // Delete "world" bold span via backspace
        new(HarnessCommand.Select, "rich-select-world-2", Start: 6, Count: 5),
        new(HarnessCommand.Backspace, "rich-delete-world"),

        // --- multiline + glyph assertions ---
        new(HarnessCommand.Target, "target-rich-editor-2", TargetTag: "rich-editor"),
        // Clear and type two lines
        new(HarnessCommand.SelectAll, "rich-ml-select-all"),
        new(HarnessCommand.Backspace, "rich-ml-clear"),
        new(HarnessCommand.Type, "rich-ml-line1", Text: "first line"),
        new(HarnessCommand.Enter, "rich-ml-enter1"),
        new(HarnessCommand.Type, "rich-ml-line2", Text: "second line"),
        new(HarnessCommand.Enter, "rich-ml-enter2"),
        new(HarnessCommand.Type, "rich-ml-line3", Text: "third line"),
        // Cursor is now at end of "third line" (char 33)
        new(HarnessCommand.AssertChar, "assert-cursor-end", Count: 33),
        // Verify 3 rendered lines
        new(HarnessCommand.AssertLinesCount, "assert-3-lines", Count: 3),
        // Verify glyph positions were computed
        new(HarnessCommand.AssertGlyphsOk, "assert-glyphs-ok"),
        // Move to start and verify line 0
        new(HarnessCommand.Select, "rich-ml-move-start", Start: 0, Count: 0),
        new(HarnessCommand.AssertChar, "assert-cursor-0", Count: 0),
        new(HarnessCommand.AssertLine, "assert-on-line-0", Count: 0),
        // Move to start of second line (char 11 = after "first line\n")
        new(HarnessCommand.Select, "rich-ml-move-line2", Start: 11, Count: 0),
        new(HarnessCommand.AssertChar, "assert-cursor-11", Count: 11),
        new(HarnessCommand.AssertLine, "assert-on-line-1", Count: 1),
        // Move to start of third line (char 23 = after "first line\nsecond line\n")
        new(HarnessCommand.Select, "rich-ml-move-line3", Start: 23, Count: 0),
        new(HarnessCommand.AssertChar, "assert-cursor-23", Count: 23),
        new(HarnessCommand.AssertLine, "assert-on-line-2", Count: 2),
    };
}

static IEnumerable<HarnessStep> ParseSteps(string[] args)
{
    foreach (var arg in args)
    {
        if (string.IsNullOrWhiteSpace(arg))
            continue;

        var commandSeparator = arg.IndexOf(':');
        var commandToken = commandSeparator >= 0 ? arg[..commandSeparator] : arg;
        var payload = commandSeparator >= 0 ? arg[(commandSeparator + 1)..] : string.Empty;

        switch (commandToken.Trim().ToLowerInvariant())
        {
            case "target":
                yield return new HarnessStep(HarnessCommand.Target, $"target-{payload}", TargetTag: payload.Trim());
                break;
            case "render":
                yield return new HarnessStep(HarnessCommand.Render, string.IsNullOrWhiteSpace(payload) ? "render" : payload.Trim());
                break;
            case "snapshot":
                yield return new HarnessStep(HarnessCommand.Snapshot, string.IsNullOrWhiteSpace(payload) ? "snapshot" : payload.Trim());
                break;
            case "setprop":
            {
                var separator = payload.IndexOf('=');
                if (separator <= 0)
                    throw new ArgumentException($"Invalid setprop payload: {arg}");

                var propertyName = payload[..separator].Trim();
                var propertyValue = payload[(separator + 1)..];
                yield return new HarnessStep(HarnessCommand.SetProperty, $"set-{propertyName.ToLowerInvariant()}", Text: propertyValue, PropertyName: propertyName);
                break;
            }
            case "type":
                yield return new HarnessStep(HarnessCommand.Type, $"type-{payload.Length}", Text: Unescape(payload));
                break;
            case "markdown":
                yield return new HarnessStep(HarnessCommand.UseMarkdown, $"markdown-{payload}", Enabled: ParseBool(payload));
                break;
            case "richtext":
                yield return new HarnessStep(HarnessCommand.UseMarkdown, $"markdown-{payload}", Enabled: ParseBool(payload));
                break;
            case "settext":
                yield return new HarnessStep(HarnessCommand.SetText, "set-text", Text: Unescape(payload));
                break;
            case "enter":
                yield return new HarnessStep(HarnessCommand.Enter, "enter");
                break;
            case "backspace":
                yield return new HarnessStep(HarnessCommand.Backspace, "backspace", Count: ParseInt(payload, 1));
                break;
            case "delete":
                yield return new HarnessStep(HarnessCommand.Delete, "delete", Count: ParseInt(payload, 1));
                break;
            case "left":
                yield return new HarnessStep(HarnessCommand.Left, "left", Count: ParseInt(payload, 1));
                break;
            case "right":
                yield return new HarnessStep(HarnessCommand.Right, "right", Count: ParseInt(payload, 1));
                break;
            case "shiftleft":
                yield return new HarnessStep(HarnessCommand.Left, "shift-left", Count: ParseInt(payload, 1), ExtendSelection: true);
                break;
            case "shiftright":
                yield return new HarnessStep(HarnessCommand.Right, "shift-right", Count: ParseInt(payload, 1), ExtendSelection: true);
                break;
            case "select":
            {
                var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var start = parts.Length > 0 ? ParseInt(parts[0], 0) : 0;
                var length = parts.Length > 1 ? ParseInt(parts[1], 0) : 0;
                yield return new HarnessStep(HarnessCommand.Select, "select", Start: start, Count: length);
                break;
            }
            case "movelc":
            {
                var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var line = parts.Length > 0 ? ParseInt(parts[0], 0) : 0;
                var column = parts.Length > 1 ? ParseInt(parts[1], 0) : 0;
                yield return new HarnessStep(HarnessCommand.MoveLineColumn, $"move-lc-{line}-{column}", Line: line, Column: column);
                break;
            }
            case "shiftmovelc":
            {
                var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var line = parts.Length > 0 ? ParseInt(parts[0], 0) : 0;
                var column = parts.Length > 1 ? ParseInt(parts[1], 0) : 0;
                yield return new HarnessStep(HarnessCommand.MoveLineColumn, $"shift-move-lc-{line}-{column}", Line: line, Column: column, ExtendSelection: true);
                break;
            }
            case "selectlc":
            {
                var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var startLine = parts.Length > 0 ? ParseInt(parts[0], 0) : 0;
                var startColumn = parts.Length > 1 ? ParseInt(parts[1], 0) : 0;
                var endLine = parts.Length > 2 ? ParseInt(parts[2], 0) : startLine;
                var endColumn = parts.Length > 3 ? ParseInt(parts[3], 0) : startColumn;
                yield return new HarnessStep(HarnessCommand.SelectLineColumn, "select-lc", Line: startLine, Column: startColumn, EndLine: endLine, EndColumn: endColumn);
                break;
            }
            case "selectall":
                yield return new HarnessStep(HarnessCommand.SelectAll, "select-all");
                break;
            case "focus":
                yield return new HarnessStep(HarnessCommand.Focus, "focus");
                break;
            case "blur":
                yield return new HarnessStep(HarnessCommand.Blur, "blur");
                break;
            case "bold":
                yield return new HarnessStep(HarnessCommand.Bold, "bold");
                break;
            case "italic":
                yield return new HarnessStep(HarnessCommand.Italic, "italic");
                break;
            case "underline":
                yield return new HarnessStep(HarnessCommand.Underline, "underline");
                break;
            case "strikethrough":
                yield return new HarnessStep(HarnessCommand.Strikethrough, "strikethrough");
                break;
            case "undo":
                yield return new HarnessStep(HarnessCommand.Undo, "undo");
                break;
            case "redo":
                yield return new HarnessStep(HarnessCommand.Redo, "redo");
                break;
            case "assertformat":
                yield return new HarnessStep(HarnessCommand.AssertFormat, $"assert-{payload.ToLowerInvariant()}", Text: payload);
                break;
            case "assertchar":
                yield return new HarnessStep(HarnessCommand.AssertChar, $"assert-char-{payload}", Count: ParseInt(payload, 0));
                break;
            case "assertline":
                yield return new HarnessStep(HarnessCommand.AssertLine, $"assert-line-{payload}", Count: ParseInt(payload, 0));
                break;
            case "assertlinescount":
                yield return new HarnessStep(HarnessCommand.AssertLinesCount, $"assert-lines-{payload}", Count: ParseInt(payload, 0));
                break;
            case "assertglyphsok":
                yield return new HarnessStep(HarnessCommand.AssertGlyphsOk, "assert-glyphs-ok");
                break;
            default:
                throw new ArgumentException($"Unknown harness step: {arg}");
        }
    }
}

static void ApplyProperty(SkiaControl target, string? propertyName, string rawValue)
{
    if (string.IsNullOrWhiteSpace(propertyName))
        throw new ArgumentException("Property name is required for setprop.");

    var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    if (property == null || !property.CanWrite)
        throw new InvalidOperationException($"Target '{target.Tag}' does not have a writable property named '{propertyName}'.");

    var value = ConvertValue(property.PropertyType, rawValue);
    property.SetValue(target, value);
}

static object? ConvertValue(Type propertyType, string rawValue)
{
    var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

    if (targetType == typeof(string))
        return Unescape(rawValue);

    if (targetType == typeof(bool))
        return ParseBool(rawValue);

    if (targetType == typeof(int))
        return ParseInt(rawValue, 0);

    if (targetType == typeof(float))
        return float.Parse(rawValue, CultureInfo.InvariantCulture);

    if (targetType == typeof(double))
        return double.Parse(rawValue, CultureInfo.InvariantCulture);

    if (targetType.Name == "Color")
    {
        var parseMethod = targetType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, binder: null, new[] { typeof(string) }, modifiers: null);
        if (parseMethod != null)
            return parseMethod.Invoke(null, new object[] { rawValue });
    }

    if (targetType == typeof(Thickness))
        return ParseThickness(rawValue);

    if (targetType.IsEnum)
        return Enum.Parse(targetType, rawValue, ignoreCase: true);

    throw new InvalidOperationException($"Unsupported property type '{targetType.Name}' for setprop.");
}

static Thickness ParseThickness(string rawValue)
{
    var parts = rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    return parts.Length switch
    {
        1 => new Thickness(double.Parse(parts[0], CultureInfo.InvariantCulture)),
        2 => new Thickness(
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture)),
        4 => new Thickness(
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture)),
        _ => throw new InvalidOperationException($"Invalid Thickness value '{rawValue}'. Expected 1, 2, or 4 comma-separated numbers.")
    };
}

static string Unescape(string value)
{
    return NormalizeLineBreaks(value
        .Replace("\\r", "\r", StringComparison.Ordinal)
        .Replace("\\n", "\n", StringComparison.Ordinal)
        .Replace("\\t", "\t", StringComparison.Ordinal));
}

static bool TrySetFocus(SkiaControl target, bool focused)
{
    var method = target.GetType().GetMethod("SetFocus", BindingFlags.Instance | BindingFlags.Public, binder: null, new[] { typeof(bool) }, modifiers: null);
    if (method != null)
    {
        method.Invoke(target, new object[] { focused });
        return true;
    }

    var property = target.GetType().GetProperty("IsFocused", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
    {
        property.SetValue(target, focused);
        return true;
    }

    return false;
}

static string NormalizeLineBreaks(string? value)
{
    return value?
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n') ?? string.Empty;
}

static int ParseInt(string value, int fallback)
{
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : fallback;
}

static bool ParseBool(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "1" => true,
        "true" => true,
        "on" => true,
        "yes" => true,
        _ => false
    };
}

static string SanitizeFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var builder = new StringBuilder(value.Length);

    foreach (var ch in value)
    {
        builder.Append(invalid.Contains(ch) ? '-' : ch);
    }

    return builder.ToString();
}

static string Describe(HarnessContext context)
{
    var builder = new StringBuilder();
    builder.AppendLine($"CurrentTarget: {context.CurrentTargetTag}");
    builder.AppendLine($"TaggedControls: {string.Join(", ", EnumerateTaggedControls(context.Root).Select(x => $"{x.Tag}:{x.GetType().Name}"))}");

    var currentTarget = context.Root.FindViewByTag(context.CurrentTargetTag);
    if (currentTarget != null)
    {
        builder.AppendLine($"TargetType: {currentTarget.GetType().Name}");
        builder.AppendLine($"TargetTag: {currentTarget.Tag}");
    }

    foreach (var adapter in context.Adapters)
    {
        adapter.AppendSummary(context, builder);
    }

    return builder.ToString();
}

static IEnumerable<SkiaControl> EnumerateTaggedControls(SkiaControl root)
{
    if (!string.IsNullOrWhiteSpace(root.Tag))
        yield return root;

    foreach (var view in root.Views)
    {
        foreach (var child in EnumerateTaggedControls(view))
            yield return child;
    }
}

internal static class HarnessUtilities
{
    public static string NormalizeLineBreaks(string? value)
    {
        return value?
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n') ?? string.Empty;
    }

    public static IEnumerable<SkiaControl> EnumerateTaggedControls(SkiaControl root)
    {
        if (!string.IsNullOrWhiteSpace(root.Tag))
            yield return root;

        foreach (var view in root.Views)
        {
            foreach (var child in EnumerateTaggedControls(view))
                yield return child;
        }
    }

    public static string Escape(string? value)
    {
        return value?
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") ?? string.Empty;
    }
}

internal sealed class HeadlessCanvasHost : IDisposable
{
    private readonly SKSurface _surface;
    private readonly HeadlessDrawable _drawable;

    public HeadlessCanvasHost(int width, int height)
    {
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _drawable = new HeadlessDrawable(_surface);

        Canvas = new Canvas
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = Colors.LightGray
        };

        Canvas.AttachCanvasView(_drawable);
        Canvas.ConnectedHandler();
    }

    public Canvas Canvas { get; }

    public void Render()
    {
        _drawable.PrepareFrame();
        Canvas.RenderExternalSurface(_surface, new SKRect(0, 0, _drawable.CanvasSize.Width, _drawable.CanvasSize.Height), _drawable.FrameTime);
    }

    public void SavePng(string filePath)
    {
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        Canvas.Dispose();
        _drawable.Dispose();
        _surface.Dispose();
    }
}

internal sealed class HeadlessDrawable : ISkiaDrawable
{
    public HeadlessDrawable(SKSurface surface)
    {
        Surface = surface;
        CanvasSize = new SKSize(surface.Canvas.LocalClipBounds.Width, surface.Canvas.LocalClipBounds.Height);
        OnDraw = static (_, _) => false;
    }

    public Func<SKSurface, SKRect, bool> OnDraw { get; set; }

    public SKSurface Surface { get; }

    public bool IsHardwareAccelerated => false;

    public double FPS => 60;

    public bool IsDrawing { get; private set; }

    public bool HasDrawn { get; private set; }

    public long FrameTime { get; private set; }

    public Guid Uid { get; } = Guid.NewGuid();

    public SKSize CanvasSize { get; }

    public bool Update(long nanos = 0)
    {
        PrepareFrame(nanos);
        IsDrawing = true;
        try
        {
            HasDrawn = OnDraw?.Invoke(Surface, new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height)) ?? false;
            return HasDrawn;
        }
        finally
        {
            IsDrawing = false;
        }
    }

    public void SignalFrame(long nanoseconds)
    {
        FrameTime = nanoseconds;
    }

    public void PrepareFrame(long nanos = 0)
    {
        FrameTime = nanos > 0 ? nanos : GetFrameTimestampNanos();
    }

    public void Dispose()
    {
    }

    private static long GetFrameTimestampNanos()
    {
        var timestamp = Stopwatch.GetTimestamp();
        return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
    }
}

internal enum HarnessCommand
{
    Target,
    Render,
    Snapshot,
    SetProperty,
    UseMarkdown,
    Type,
    Enter,
    Backspace,
    Delete,
    Left,
    Right,
    Select,
    MoveLineColumn,
    SelectLineColumn,
    SelectAll,
    Focus,
    Blur,
    SetText,
    // Rich formatting
    Bold,
    Italic,
    Underline,
    Strikethrough,
    Undo,
    Redo,
    AssertFormat,
    // Structural assertions (both editor types)
    AssertChar,
    AssertLine,
    AssertLinesCount,
    AssertGlyphsOk,
    AssertPlaceholderVisible,
    AssertPlaceholderHidden,
}

internal sealed record HarnessStep(
    HarnessCommand Command,
    string Name,
    string? Text = null,
    string? TargetTag = null,
    string? PropertyName = null,
    bool Enabled = false,
    int Count = 1,
    int Start = 0,
    int Line = 0,
    int Column = 0,
    int EndLine = 0,
    int EndColumn = 0,
    bool ExtendSelection = false);

internal sealed class HarnessContext
{
    public HarnessContext(HeadlessCanvasHost host, SkiaControl root, string defaultTargetTag, IReadOnlyList<IHarnessAdapter> adapters)
    {
        Host = host;
        Root = root;
        DefaultTargetTag = defaultTargetTag;
        CurrentTargetTag = defaultTargetTag;
        Adapters = adapters;
    }

    public HeadlessCanvasHost Host { get; }

    public SkiaControl Root { get; }

    public string DefaultTargetTag { get; }

    public string CurrentTargetTag { get; set; }

    public IReadOnlyList<IHarnessAdapter> Adapters { get; }
}

internal interface IHarnessAdapter
{
    bool CanHandle(SkiaControl target);

    bool TryExecute(SkiaControl target, HarnessStep step, HarnessContext context);

    void AppendSummary(HarnessContext context, StringBuilder builder);
}

internal sealed class SkiaRichEditorHarnessAdapter : IHarnessAdapter
{
    public bool CanHandle(SkiaControl target) => target is SkiaRichEditor;

    public bool TryExecute(SkiaControl target, HarnessStep step, HarnessContext context)
    {
        if (target is not SkiaRichEditor editor)
            return false;

        switch (step.Command)
        {
            case HarnessCommand.Type:
                editor.StubTypeText(step.Text ?? string.Empty);
                return true;
            case HarnessCommand.Enter:
                editor.StubPressEnter();
                return true;
            case HarnessCommand.Backspace:
                editor.StubBackspace(step.Count);
                return true;
            case HarnessCommand.Delete:
                editor.StubDelete(step.Count);
                return true;
            case HarnessCommand.Left:
                editor.StubMoveCursor(-step.Count, step.ExtendSelection);
                return true;
            case HarnessCommand.Right:
                editor.StubMoveCursor(step.Count, step.ExtendSelection);
                return true;
            case HarnessCommand.Select:
                editor.StubSelectRange(step.Start, step.Count);
                return true;
            case HarnessCommand.MoveLineColumn:
                editor.StubMoveCursorToLineColumn(step.Line, step.Column, step.ExtendSelection);
                return true;
            case HarnessCommand.SelectLineColumn:
                editor.StubSelectLineColumnRange(step.Line, step.Column, step.EndLine, step.EndColumn);
                return true;
            case HarnessCommand.SelectAll:
                editor.StubSelectAll();
                return true;
            case HarnessCommand.SetText:
                editor.Document.SetText(HarnessUtilities.NormalizeLineBreaks(step.Text));
                editor.CursorPosition = editor.Document.Length;
                editor.SelectionLength = 0;
                return true;
            case HarnessCommand.Bold:
                editor.ToggleBold();
                return true;
            case HarnessCommand.Italic:
                editor.ToggleItalic();
                return true;
            case HarnessCommand.Underline:
                editor.ToggleUnderline();
                return true;
            case HarnessCommand.Strikethrough:
                editor.ToggleStrikethrough();
                return true;
            case HarnessCommand.Undo:
                editor.UndoRich();
                return true;
            case HarnessCommand.Redo:
                editor.RedoRich();
                return true;
            case HarnessCommand.AssertFormat:
                AssertFormat(editor, step.Text ?? string.Empty, step.Name);
                return true;
            case HarnessCommand.AssertChar:
                AssertEqual(step.Name, "CursorPosition", step.Count, editor.CursorPosition);
                return true;
            case HarnessCommand.AssertLine:
                context.Host.Render();
                AssertEqual(step.Name, "CursorLine", step.Count, editor.GetCursorLine());
                return true;
            case HarnessCommand.AssertLinesCount:
                context.Host.Render();
                AssertEqual(step.Name, "LinesCount", step.Count, editor.Label?.LinesCount ?? 0);
                return true;
            case HarnessCommand.AssertGlyphsOk:
                context.Host.Render();
                AssertGlyphsOk(editor, step.Name);
                return true;
            default:
                return false;
        }
    }

    public void AppendSummary(HarnessContext context, StringBuilder builder)
    {
        foreach (var tagged in HarnessUtilities.EnumerateTaggedControls(context.Root).OfType<SkiaRichEditor>())
        {
            var scrollField = tagged.GetType().GetField("_scroll", BindingFlags.Instance | BindingFlags.NonPublic);
            var scroll = scrollField?.GetValue(tagged) as SkiaScroll;

            var docText = tagged.Document.GetText();
            builder.AppendLine($"[{tagged.Tag}] Text: {HarnessUtilities.Escape(docText)}");
            builder.AppendLine($"[{tagged.Tag}] CursorPosition: {tagged.CursorPosition}");
            builder.AppendLine($"[{tagged.Tag}] SelectionLength: {tagged.SelectionLength}");
            builder.AppendLine($"[{tagged.Tag}] IsMultiline: {tagged.IsMultiline}");
            builder.AppendLine($"[{tagged.Tag}] MeasuredLines: {tagged.Label?.LinesCount ?? 0}");
            builder.AppendLine($"[{tagged.Tag}] ScrollOffsetX: {scroll?.ViewportOffsetX ?? 0}");
            builder.AppendLine($"[{tagged.Tag}] ScrollOffsetY: {scroll?.ViewportOffsetY ?? 0}");

            var runs = tagged.Document.GetFormattingRuns();
            builder.AppendLine($"[{tagged.Tag}] FormatRuns: {runs.Count}");
            foreach (var (s, e, fmt) in runs)
            {
                var text = docText.Length >= e ? docText.Substring(s, e - s) : "?";
                builder.AppendLine($"  [{s},{e}) '{HarnessUtilities.Escape(text)}' B={fmt.Bold} I={fmt.Italic} U={fmt.Underline} S={fmt.Strikethrough}");
            }
        }
    }

    private static void AssertFormat(SkiaRichEditor editor, string assertion, string stepName)
    {
        var parts = assertion.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new ArgumentException($"AssertFormat '{stepName}': invalid assertion '{assertion}'. Expected 'Field=value'.");

        var field = parts[0];
        var expected = parts[1].ToLowerInvariant() == "true";

        var fmt = editor.SelectionFormat;
        bool? actual = field.ToLowerInvariant() switch
        {
            "bold"          => fmt.Bold,
            "italic"        => fmt.Italic,
            "underline"     => fmt.Underline,
            "strikethrough" => fmt.Strikethrough,
            _ => throw new ArgumentException($"AssertFormat '{stepName}': unknown field '{field}'.")
        };

        var actualBool = actual == true;
        if (actualBool != expected)
            throw new InvalidOperationException(
                $"AssertFormat '{stepName}': {field} expected {expected} but was {actual?.ToString() ?? "null"}. " +
                $"CursorPos={editor.CursorPosition} SelectionLength={editor.SelectionLength}");

        Console.WriteLine($"  ✓ {stepName}: {field}={actual}");
    }

    private static void AssertEqual(string stepName, string label, int expected, int actual)
    {
        if (actual != expected)
            throw new InvalidOperationException($"Assert '{stepName}': {label} expected {expected} but was {actual}.");
        Console.WriteLine($"  ✓ {stepName}: {label}={actual}");
    }

    private static void AssertGlyphsOk(SkiaRichEditor editor, string stepName)
    {
        var label = editor.Label;
        if (label == null || label.LinesCount == 0)
            throw new InvalidOperationException($"Assert '{stepName}': Label has no lines.");

        var line0 = label.Lines[0];
        if (line0.Spans.Count == 0)
            throw new InvalidOperationException($"Assert '{stepName}': Line 0 has no spans.");

        LineGlyph[]? glyphs = null;
        foreach (var span in line0.Spans)
        {
            if (span.Glyphs != null && span.Glyphs.Length > 0)
            {
                glyphs = span.Glyphs;
                break;
            }
        }

        if (glyphs == null || glyphs.Length == 0)
            throw new InvalidOperationException($"Assert '{stepName}': Line 0 spans have no glyphs. NeedsGlyphPositions may not be set.");

        // At least two glyphs so we can verify positions advance
        if (glyphs.Length > 1 && glyphs[glyphs.Length - 1].Position <= glyphs[0].Position)
            throw new InvalidOperationException(
                $"Assert '{stepName}': Glyph positions do not advance (first={glyphs[0].Position}, last={glyphs[glyphs.Length - 1].Position}). Positions may not be computed.");

        Console.WriteLine($"  ✓ {stepName}: {glyphs.Length} glyphs, pos[0]={glyphs[0].Position:F1} pos[last]={glyphs[glyphs.Length - 1].Position:F1}");
    }
}

internal sealed class SkiaEditorHarnessAdapter : IHarnessAdapter
{
    public bool CanHandle(SkiaControl target)
    {
        return target is SkiaEditor && target is not SkiaRichEditor;
    }

    public bool TryExecute(SkiaControl target, HarnessStep step, HarnessContext context)
    {
        if (target is not SkiaEditor editor || target is SkiaRichEditor)
            return false;

        switch (step.Command)
        {
            case HarnessCommand.UseMarkdown:
                editor.UseMarkdown = step.Enabled;
                return true;
            case HarnessCommand.Type:
                editor.StubTypeText(step.Text ?? string.Empty);
                return true;
            case HarnessCommand.Enter:
                editor.StubPressEnter();
                return true;
            case HarnessCommand.Backspace:
                editor.StubBackspace(step.Count);
                return true;
            case HarnessCommand.Delete:
                editor.StubDelete(step.Count);
                return true;
            case HarnessCommand.Left:
                editor.StubMoveCursor(-step.Count, step.ExtendSelection);
                return true;
            case HarnessCommand.Right:
                editor.StubMoveCursor(step.Count, step.ExtendSelection);
                return true;
            case HarnessCommand.Select:
                editor.StubSelectRange(step.Start, step.Count);
                return true;
            case HarnessCommand.MoveLineColumn:
                editor.StubMoveCursorToLineColumn(step.Line, step.Column, step.ExtendSelection);
                return true;
            case HarnessCommand.SelectLineColumn:
                editor.StubSelectLineColumnRange(step.Line, step.Column, step.EndLine, step.EndColumn);
                return true;
            case HarnessCommand.SelectAll:
                editor.StubSelectAll();
                return true;
            case HarnessCommand.SetText:
                editor.Text = HarnessUtilities.NormalizeLineBreaks(step.Text);
                editor.CursorPosition = editor.Text?.Length ?? 0;
                editor.SelectionLength = 0;
                return true;
            case HarnessCommand.AssertChar:
                if (editor.CursorPosition != step.Count)
                    throw new InvalidOperationException($"Assert '{step.Name}': CursorPosition expected {step.Count} but was {editor.CursorPosition}.");
                Console.WriteLine($"  ✓ {step.Name}: CursorPosition={editor.CursorPosition}");
                return true;
            case HarnessCommand.AssertLine:
                context.Host.Render();
                var editorLine = editor.GetCursorLine();
                if (editorLine != step.Count)
                    throw new InvalidOperationException($"Assert '{step.Name}': CursorLine expected {step.Count} but was {editorLine}.");
                Console.WriteLine($"  ✓ {step.Name}: CursorLine={editorLine}");
                return true;
            case HarnessCommand.AssertLinesCount:
                context.Host.Render();
                var editorLinesCount = editor.Label?.LinesCount ?? 0;
                if (editorLinesCount != step.Count)
                    throw new InvalidOperationException($"Assert '{step.Name}': LinesCount expected {step.Count} but was {editorLinesCount}.");
                Console.WriteLine($"  ✓ {step.Name}: LinesCount={editorLinesCount}");
                return true;
            case HarnessCommand.AssertGlyphsOk:
                context.Host.Render();
                var editorLine0 = editor.Label?.Lines?.FirstOrDefault();
                var editorGlyphs = editorLine0?.Spans.Count > 0 ? editorLine0.Spans[0].Glyphs : null;
                if (editorGlyphs == null || editorGlyphs.Length == 0)
                    throw new InvalidOperationException($"Assert '{step.Name}': No glyphs found in line 0.");
                Console.WriteLine($"  ✓ {step.Name}: {editorGlyphs.Length} glyphs");
                return true;
            case HarnessCommand.AssertPlaceholderVisible:
            {
                context.Host.Render();
                var ph = GetPlaceholderLabel(editor);
                if (ph == null)
                    throw new InvalidOperationException($"Assert '{step.Name}': _placeholderLabel field not found.");
                if (!ph.IsVisible)
                    throw new InvalidOperationException($"Assert '{step.Name}': placeholder expected VISIBLE but IsVisible={ph.IsVisible}. Text='{editor.Text}' PlaceholderText='{editor.PlaceholderText}'");
                Console.WriteLine($"  ✓ {step.Name}: placeholder visible");
                return true;
            }
            case HarnessCommand.AssertPlaceholderHidden:
            {
                context.Host.Render();
                var ph = GetPlaceholderLabel(editor);
                if (ph == null)
                    throw new InvalidOperationException($"Assert '{step.Name}': _placeholderLabel field not found.");
                if (ph.IsVisible)
                    throw new InvalidOperationException($"Assert '{step.Name}': placeholder expected HIDDEN but IsVisible={ph.IsVisible}. Text='{editor.Text}' PlaceholderText='{editor.PlaceholderText}'");
                Console.WriteLine($"  ✓ {step.Name}: placeholder hidden");
                return true;
            }
            default:
                return false;
        }
    }

    private static SkiaLabel? GetPlaceholderLabel(SkiaEditor editor)
    {
        var field = editor.GetType().GetField("_placeholderLabel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(editor) as SkiaLabel;
    }

    public void AppendSummary(HarnessContext context, StringBuilder builder)
    {
        foreach (var tagged in HarnessUtilities.EnumerateTaggedControls(context.Root)
                     .OfType<SkiaEditor>()
                     .Where(e => e is not SkiaRichEditor))
        {
            var scrollField = tagged.GetType().GetField("_scroll", BindingFlags.Instance | BindingFlags.NonPublic);
            var scroll = scrollField?.GetValue(tagged) as SkiaScroll;

            builder.AppendLine($"[{tagged.Tag}] Text: {HarnessUtilities.Escape(tagged.Text)}");
            builder.AppendLine($"[{tagged.Tag}] CursorPosition: {tagged.CursorPosition}");
            builder.AppendLine($"[{tagged.Tag}] SelectionLength: {tagged.SelectionLength}");
            builder.AppendLine($"[{tagged.Tag}] IsMultiline: {tagged.IsMultiline}");
            builder.AppendLine($"[{tagged.Tag}] UseMarkdown: {tagged.UseMarkdown}");
            builder.AppendLine($"[{tagged.Tag}] MeasuredLines: {tagged.Label?.LinesCount ?? 0}");
            builder.AppendLine($"[{tagged.Tag}] ScrollOffsetX: {scroll?.ViewportOffsetX ?? 0}");
            builder.AppendLine($"[{tagged.Tag}] ScrollOffsetY: {scroll?.ViewportOffsetY ?? 0}");
        }
    }
}
