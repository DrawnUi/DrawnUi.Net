using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using System.Text;

Super.Init();

var outputDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts");
Directory.CreateDirectory(outputDirectory);

using var host = new HeadlessCanvasHost(900, 520);

var editor = new SkiaEditor
{
    Tag = "editor",
    WidthRequest = 760,
    HeightRequest = 320,
    Margin = new Thickness(40),
    FontSize = 24,
    MaxLines = 8,
    TextColor = Colors.Black,
    CursorColor = Colors.DodgerBlue,
    BackgroundColor = Colors.White,
    SelectionColor = DrawnUi.Color.FromArgb("#5590CFFE")
};

host.Canvas.Children = new List<SkiaControl> { editor };
host.Render();

editor.SetFocus(true);
editor.Text = string.Empty;
editor.CursorPosition = 0;
editor.SelectionLength = 0;

var steps = ParseSteps(args).ToList();
if (steps.Count == 0)
{
    steps = CreateDefaultScenario();
}

WriteSnapshot(host, editor, outputDirectory, 0, "initial");

for (var index = 0; index < steps.Count; index++)
{
    var step = steps[index];
    ExecuteStep(editor, step);
    WriteSnapshot(host, editor, outputDirectory, index + 1, step.Name);
}

var finalImagePath = Path.Combine(outputDirectory, "final.png");
host.SavePng(finalImagePath);

var summaryPath = Path.Combine(outputDirectory, "summary.txt");
File.WriteAllText(summaryPath, Describe(editor));

Console.WriteLine("SkiaEditor multiline harness complete.");
Console.WriteLine($"Artifacts: {outputDirectory}");
Console.WriteLine($"FinalImage: {finalImagePath}");
Console.WriteLine($"Summary: {summaryPath}");
Console.WriteLine($"Steps: {steps.Count}");
Console.WriteLine(Describe(editor));

static void ExecuteStep(SkiaEditor editor, HarnessStep step)
{
    switch (step.Command)
    {
        case HarnessCommand.Type:
            editor.StubTypeText(step.Text ?? string.Empty);
            break;
        case HarnessCommand.Enter:
            editor.StubPressEnter();
            break;
        case HarnessCommand.Backspace:
            editor.StubBackspace(step.Count);
            break;
        case HarnessCommand.Delete:
            editor.StubDelete(step.Count);
            break;
        case HarnessCommand.Left:
            editor.StubMoveCursor(-step.Count, step.ExtendSelection);
            break;
        case HarnessCommand.Right:
            editor.StubMoveCursor(step.Count, step.ExtendSelection);
            break;
        case HarnessCommand.Select:
            editor.StubSelectRange(step.Start, step.Count);
            break;
        case HarnessCommand.MoveLineColumn:
            editor.StubMoveCursorToLineColumn(step.Line, step.Column, step.ExtendSelection);
            break;
        case HarnessCommand.SelectLineColumn:
            editor.StubSelectLineColumnRange(step.Line, step.Column, step.EndLine, step.EndColumn);
            break;
        case HarnessCommand.SelectAll:
            editor.StubSelectAll();
            break;
        case HarnessCommand.Focus:
            editor.SetFocus(true);
            break;
        case HarnessCommand.Blur:
            editor.SetFocus(false);
            break;
        case HarnessCommand.SetText:
            editor.Text = NormalizeLineBreaks(step.Text);
            editor.CursorPosition = editor.Text?.Length ?? 0;
            editor.SelectionLength = 0;
            break;
        default:
            throw new InvalidOperationException($"Unsupported command: {step.Command}");
    }
}

static void WriteSnapshot(HeadlessCanvasHost host, SkiaEditor editor, string outputDirectory, int index, string name)
{
    host.Render();

    var safeName = SanitizeFileName(name);
    var imagePath = Path.Combine(outputDirectory, $"{index:00}-{safeName}.png");
    var statePath = Path.Combine(outputDirectory, $"{index:00}-{safeName}.txt");

    host.SavePng(imagePath);
    File.WriteAllText(statePath, Describe(editor));
}

static List<HarnessStep> CreateDefaultScenario()
{
    return new List<HarnessStep>
    {
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
            case "type":
                yield return new HarnessStep(HarnessCommand.Type, $"type-{payload.Length}", Text: Unescape(payload));
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

static string Unescape(string value)
{
    return NormalizeLineBreaks(value
        .Replace("\\r", "\r", StringComparison.Ordinal)
        .Replace("\\n", "\n", StringComparison.Ordinal)
        .Replace("\\t", "\t", StringComparison.Ordinal));
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

static string Describe(SkiaEditor editor)
{
    var scrollField = editor.GetType().GetField("_scroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    var scroll = scrollField?.GetValue(editor) as SkiaScroll;

    var builder = new StringBuilder();
    builder.AppendLine($"Text: {Escape(editor.Text)}");
    builder.AppendLine($"CursorPosition: {editor.CursorPosition}");
    builder.AppendLine($"SelectionLength: {editor.SelectionLength}");
    builder.AppendLine($"IsMultiline: {editor.IsMultiline}");
    builder.AppendLine($"MeasuredLines: {editor.Label?.LinesCount ?? 0}");
    builder.AppendLine($"ScrollOffsetX: {scroll?.ViewportOffsetX ?? 0}");
    builder.AppendLine($"ScrollOffsetY: {scroll?.ViewportOffsetY ?? 0}");
    return builder.ToString();
}

static string Escape(string? value)
{
    return value?
        .Replace("\r", "\\r")
        .Replace("\n", "\\n") ?? string.Empty;
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
    int Count = 1,
    int Start = 0,
    int Line = 0,
    int Column = 0,
    int EndLine = 0,
    int EndColumn = 0,
    bool ExtendSelection = false);