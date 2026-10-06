using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An image that loads asynchronously raises Success once and no Error. It used to raise Error right after
/// Success (the load action returned and the error call after it ran anyway), ending with HasError = true.
/// </summary>
public class SkiaImageLoadEventsTests
{
    [Fact]
    public void AsyncLoad_RaisesSuccessOnce_AndNoError()
    {
        var successes = 0;
        var errors = 0;
        var image = new SkiaImage
        {
            WidthRequest = 20,
            HeightRequest = 20,
            UseAssembly = typeof(SkiaImageLoadEventsTests).Assembly,
        };
        image.Success += (_, _) => Interlocked.Increment(ref successes);
        image.Error += (_, _) => Interlocked.Increment(ref errors);
        image.Source = "Resources/green.png"; // embedded as DrawnUi.Net.Tests.Resources.green.png

        using var host = new HeadlessCanvasHost(100, 100, 1f, Colors.Black);
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { image }
        };

        for (var i = 0; i < 100 && Volatile.Read(ref successes) == 0; i++)
        {
            host.RenderFrame();
            Thread.Sleep(20);
        }

        // give a stray Error after the Success time to arrive
        for (var i = 0; i < 10; i++)
        {
            host.RenderFrame();
            Thread.Sleep(20);
        }

        Assert.Equal(1, Volatile.Read(ref successes));
        Assert.Equal(0, Volatile.Read(ref errors));
        Assert.False(image.HasError);
    }
}
