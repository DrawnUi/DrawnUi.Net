using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Blazor.Server;

internal sealed class HeadlessCanvasHost : IDisposable
{
    private readonly SKSurface _surface;
    private readonly HeadlessDrawable _drawable;

    public HeadlessCanvasHost(int width, int height, Color backgroundColor)
    {
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _drawable = new HeadlessDrawable(_surface);

        Canvas = new Canvas
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = backgroundColor
        };

        Canvas.AttachCanvasView(_drawable);
        Canvas.ConnectedHandler();
    }

    public Canvas Canvas { get; }

    public void Render()
    {
        _drawable.PrepareFrame();
        Canvas.RenderExternalSurface(
            _surface,
            new SKRect(0, 0, _drawable.CanvasSize.Width, _drawable.CanvasSize.Height),
            _drawable.FrameTime);
    }

    public SKImage Snapshot()
    {
        return _surface.Snapshot();
    }

    public void Dispose()
    {
        Canvas.Dispose();
        _drawable.Dispose();
        _surface.Dispose();
    }
}