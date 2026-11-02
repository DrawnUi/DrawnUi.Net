using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label that fills its box with a growing font keeps its own font size on an axis without a limit,
/// as inside a stack sized by its content.
/// </summary>
public class LabelAutoSizeUnboundedTests
{
    [Theory]
    [InlineData(AutoSizeType.FillHorizontal, true)]
    [InlineData(AutoSizeType.FitFillHorizontal, true)]
    [InlineData(AutoSizeType.FillVertical, false)]
    [InlineData(AutoSizeType.FitFillVertical, false)]
    public void UnboundedAxis_MeasureCompletes(AutoSizeType autoSize, bool unboundedWidth)
    {
        var label = new SkiaLabel("Some text") { FontSize = 16, AutoSize = autoSize };

        var measure = Task.Run(() => unboundedWidth
            ? label.Measure(float.PositiveInfinity, 100, 1f)
            : label.Measure(300, float.PositiveInfinity, 1f));

        Assert.True(measure.Wait(TimeSpan.FromSeconds(10)), "measure did not complete");
        Assert.True(measure.Result.Pixels.Width > 0 && measure.Result.Pixels.Height > 0);
    }
}
