using System.Reflection;
using System.Text;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Markdown source indexes (SkiaRichLabel.SourceIndexes) exist for an editor's caret only: a label that only displays
/// markdown parses without them, allocating nothing for them.
/// </summary>
[Collection("Multithreaded")] // counts allocations: never next to the parallel load of other classes
public class MarkdownSourceIndexesTests
{
    class ParsingLabel : SkiaRichLabel
    {
        public ParsingLabel(bool track)
        {
            typeof(SkiaRichLabel).GetField("TrackSourceIndexes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, track);
        }

        public void Parse() => SetTextInternal();
    }

    static readonly string LongMarkdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i =>
        $"# Heading {i}\nSome **bold {i}** and *italic* text with a [link {i}](http://example.com/{i}) and `code`.\n- item one\n- item two"));

    static long ParseBytes(ParsingLabel label)
    {
        label.Parse(); // warm up
        long best = long.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            label.Parse();
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return best;
    }

    [Fact]
    public void DisplayOnlyLabel_ParsesWithoutSourceIndexes()
    {
        using var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        var display = new ParsingLabel(false) { Text = LongMarkdown };
        var tracked = new ParsingLabel(true) { Text = LongMarkdown };
        host.Canvas.Content = new SkiaStack { Children = { display, tracked } };
        host.AdvanceFrames(2);

        var displayBytes = ParseBytes(display);
        var trackedBytes = ParseBytes(tracked);
        Assert.Null(display.SourceIndexes);
        Assert.NotNull(tracked.SourceIndexes);
        // the map alone is 4 bytes per drawn character; source positions cost more on top
        Assert.True(trackedBytes - displayBytes >= 4L * tracked.SourceIndexes.Count,
            $"display {displayBytes} B, tracked {trackedBytes} B, {tracked.SourceIndexes.Count} drawn characters");
    }

    [Fact]
    public void EditorLabel_TracksSourceIndexes()
    {
        using var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        var editor = new SkiaEditor { WidthRequest = 300, UseMarkdown = true, MaxLines = 4, Text = "**bold** text" };
        host.Canvas.Content = new SkiaStack { Children = { editor } };
        host.AdvanceFrames(4);
        Assert.NotNull(((SkiaRichLabel)editor.Label).SourceIndexes);
    }
}
