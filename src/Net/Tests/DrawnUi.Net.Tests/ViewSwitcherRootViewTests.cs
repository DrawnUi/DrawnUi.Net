using System.Diagnostics;
using System.Drawing;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaViewSwitcher with SelectedIndex set BEFORE its children exist (the usual initializer
/// order). GetRootView / GetTopView used to index an empty subview list and throw + trace an
/// ArgumentOutOfRangeException on every call until the tabs were added; they must answer null
/// quietly, and once the tabs are there the selected one must show as before.
/// </summary>
public class ViewSwitcherRootViewTests
{
    private readonly ITestOutputHelper _output;

    public ViewSwitcherRootViewTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class TraceCapture : TraceListener
    {
        public readonly List<string> Lines = new();
        public override void Write(string? message) { if (message != null) Lines.Add(message); }
        public override void WriteLine(string? message) { if (message != null) Lines.Add(message); }
    }

    private static SkiaControl Tab(string tag) => new SkiaShape
    {
        Tag = tag,
        Type = ShapeType.Rectangle,
        BackgroundColor = Colors.DarkSlateBlue,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };

    /// <summary>The switcher applies indices on its own async queue, so wait in real time.</summary>
    private static bool WaitTop(HeadlessCanvasHost host, SkiaViewSwitcher switcher, SkiaControl expected, int maxMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < maxMs)
        {
            host.AdvanceFrames(3);
            if (ReferenceEquals(switcher.TopView, expected))
                return true;
            Thread.Sleep(20);
        }
        return ReferenceEquals(switcher.TopView, expected);
    }

    [Fact]
    public void SelectedIndexBeforeChildren_NoExceptionTraced_FirstTabShown()
    {
        var capture = new TraceCapture();
        Trace.Listeners.Add(capture);
        try
        {
            var host = new HeadlessCanvasHost(300, 500, scale: 1f, background: Colors.Black);

            var switcher = new SkiaViewSwitcher
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                SelectedIndex = 0, // before any child
            };

            // no tabs yet: "no view" is a normal answer, not an exception
            Assert.Null(switcher.GetRootView(0));
            Assert.Null(switcher.GetTopView(0));
            Assert.Null(switcher.GetRootView(-1));

            var a = Tab("A");
            var b = Tab("B");
            switcher.Children = new List<SkiaControl> { a, b };

            host.Canvas.Content = switcher;
            host.AdvanceFrames(6);

            Assert.True(WaitTop(host, switcher, a), $"expected tab A on top, got {switcher.TopView?.Tag}");
            Assert.Same(a, switcher.GetRootView(0)?.View);
            Assert.Same(b, switcher.GetRootView(1)?.View);
            Assert.Null(switcher.GetRootView(2));

            switcher.SelectedIndex = 1;
            Assert.True(WaitTop(host, switcher, b), $"expected tab B on top, got {switcher.TopView?.Tag}");

            var thrown = capture.Lines.Where(x => x.Contains("ArgumentOutOfRange")).ToList();
            foreach (var line in thrown) _output.WriteLine(line);
            Assert.True(thrown.Count == 0, $"{thrown.Count} out-of-range exceptions traced by the switcher");
        }
        finally
        {
            Trace.Listeners.Remove(capture);
        }
    }
}
