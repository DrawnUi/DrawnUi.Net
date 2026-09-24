using SkiaSharp;
using DrawnUi.Draw;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests
{
    /// <summary>
    /// A Fill label with a left Margin inside an absolute layer must wrap at (layer width - margin),
    /// otherwise every line overflows the arranged rect by the margin and the parent clips the last glyphs.
    /// </summary>
    public class LabelMarginWrapTests : DrawnTestsBase
    {
        private readonly ITestOutputHelper _out;
        public LabelMarginWrapTests(ITestOutputHelper output) { _out = output; }

        const string Text = "We have added Looks! Save your favorite combinations of background, face, filter and adjustments, and switch between them with one tap, right from the main bar.";

        static void Render(SkiaControl control, float scale)
        {
            using var recorder = new SKPictureRecorder();
            var area = new SKRect(0, 0, control.MeasuredSize.Pixels.Width, control.MeasuredSize.Pixels.Height);
            var canvas = recorder.BeginRecording(area);
            var ctx = new DrawingContext(new SkiaDrawingContext
            {
                Superview = null,
                FrameTimeNanos = Super.GetCurrentTimeNanos(),
                Canvas = canvas,
                Width = canvas.DeviceClipBounds.Width,
                Height = canvas.DeviceClipBounds.Height
            }, area, scale);
            control.Render(ctx.WithDestination(control.DrawingRect));
            recorder.EndRecording().Dispose();
        }

        static SkiaLayer Bullet(out SkiaLabel label)
        {
            SkiaLabel assigned;
            var layer = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Start,
                Children =
                {
                    new SkiaLabel { Text = "•", FontSize = 15, VerticalOptions = LayoutOptions.Start },
                    new SkiaLabel
                    {
                        Text = Text,
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(16, 0, 0, 0),
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaxLines = -1,
                    }.Assign(out assigned),
                }
            };
            label = assigned;
            return layer;
        }

        /// <summary>
        /// Window moved to a monitor with another scale between the first measure and the draw:
        /// the label must re-wrap for the new scale, not draw the old lines into a differently sized rect.
        /// </summary>
        [Theory]
        [InlineData(1.25f, 1.5f)]
        [InlineData(1f, 1.75f)]
        [InlineData(1.5f, 1f)]
        public void FillLabelWithLeftMargin_RewrapsAfterScaleChange(float firstScale, float secondScale)
        {
            var layer = Bullet(out var label);

            var first = 432 * firstScale;
            layer.CommitInvalidations();
            layer.Measure(first, float.PositiveInfinity, firstScale);
            layer.Arrange(new SKRect(0, 0, layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height),
                layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height, firstScale);
            Render(layer, firstScale);
            var firstLines = label.Lines.Select(l => l.Value).ToArray();

            // DPI change: the canvas measures and draws at the new scale
            var width = 432 * secondScale;
            layer.CommitInvalidations();
            layer.Measure(width, float.PositiveInfinity, secondScale);
            layer.Arrange(new SKRect(0, 0, layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height),
                layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height, secondScale);
            Render(layer, secondScale);

            var rect = label.DrawingRect;
            var contentWidth = width - 16 * secondScale;
            _out.WriteLine($"{firstScale}->{secondScale}: rect {rect.Left}..{rect.Right} content {contentWidth} lines {string.Join(" | ", label.Lines.Select(l => $"{l.Width:0}"))}");
            Assert.Equal(16 * secondScale, rect.Left, 0.5);
            Assert.True(rect.Right <= width + 0.5f, $"label rect overflows the layer: right {rect.Right} > {width}");
            foreach (var line in label.Lines)
            {
                Assert.True(line.Width <= contentWidth + 0.5f, $"line {line.Width} wider than content {contentWidth} after {firstScale}->{secondScale}");
            }
        }

        [Theory]
        [InlineData(1f)]
        [InlineData(1.25f)]
        [InlineData(1.5f)]
        [InlineData(1.75f)]
        [InlineData(2f)]
        [InlineData(2.25f)]
        public void FillLabelWithLeftMargin_WrapsInsideArrangedRect(float scale)
        {
            SkiaLabel label;
            var layer = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Start,
                Children =
                {
                    new SkiaLabel { Text = "•", FontSize = 15, VerticalOptions = LayoutOptions.Start },
                    new SkiaLabel
                    {
                        Text = Text,
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(16, 0, 0, 0),
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaxLines = -1,
                    }.Assign(out label),
                }
            };

            var width = 432 * scale; // 480 window - 2 * 24 padding
            layer.CommitInvalidations();
            layer.Measure(width, float.PositiveInfinity, scale);
            layer.Arrange(new SKRect(0, 0, layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height),
                layer.MeasuredSize.Pixels.Width, layer.MeasuredSize.Pixels.Height, scale);

            Render(layer, scale);
            var rect = label.DrawingRect;
            _out.WriteLine($"scale {scale}: layer {layer.MeasuredSize.Pixels.Width}x{layer.MeasuredSize.Pixels.Height} label measured {label.MeasuredSize.Pixels.Width} rect {rect.Left}..{rect.Right} w={rect.Width}");
            Assert.NotNull(label.Lines);
            Assert.True(label.Lines.Length > 1, "text did not wrap");
            foreach (var line in label.Lines)
            {
                _out.WriteLine($"  line w={line.Width} bounds={line.Bounds}");
            }

            var contentWidth = width - 16 * scale;
            Assert.Equal(16 * scale, rect.Left, 0.5);
            Assert.True(rect.Right <= width + 0.5f, $"label rect overflows the layer: right {rect.Right} > {width}");
            foreach (var line in label.Lines)
            {
                Assert.True(line.Width <= contentWidth + 0.5f, $"line {line.Width} wider than content {contentWidth} at scale {scale}");
            }
        }
    }
}
