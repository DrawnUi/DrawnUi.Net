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

using var host = new HeadlessCanvasHost(900, 520);

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
    HeightRequest = 320,
    FontSize = 24,
    MaxLines = 8,
    TextColor = Colors.Black,
    CursorColor = Colors.DodgerBlue,
    BackgroundColor = Colors.White,
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
        editor
    }
};

host.Canvas.Children = new List<SkiaControl> { scene };

var context = new HarnessContext(host, scene, "editor", new IHarnessAdapter[]
{
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
        new(HarnessCommand.Type, "replace-selection", Text: "SECOND")
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
    SetText
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

internal sealed class SkiaEditorHarnessAdapter : IHarnessAdapter
{
    public bool CanHandle(SkiaControl target)
    {
        return target is SkiaEditor;
    }

    public bool TryExecute(SkiaControl target, HarnessStep step, HarnessContext context)
    {
        if (target is not SkiaEditor editor)
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
            default:
                return false;
        }
    }

    public void AppendSummary(HarnessContext context, StringBuilder builder)
    {
        foreach (var tagged in HarnessUtilities.EnumerateTaggedControls(context.Root).OfType<SkiaEditor>())
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