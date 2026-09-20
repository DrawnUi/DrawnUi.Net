using DrawnUi.Draw;
using DrawnUi.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox;

/// <summary>
/// Repro: SkiaScroll with Orientation=Neither must not scroll by pan, fling or mouse wheel.
/// Top scroll is Neither (offsets must stay 0), bottom one is Vertical (offsets must move).
/// </summary>
public class ScrollNeitherPage : BasePageReloadable, IDisposable
{
    private Canvas? _canvas;
    private SkiaScroll _neither = null!;
    private SkiaScroll _vertical = null!;

    static SkiaLayout TallContent(Color color) => new SkiaLayout()
    {
        Type = LayoutType.Column,
        Spacing = 4,
        Padding = 8,
        HorizontalOptions = LayoutOptions.Fill,
        Children = Enumerable.Range(0, 30).Select(i => (SkiaControl)new SkiaLabel($"row {i}")
        {
            FontSize = 14,
            TextColor = Colors.White,
            BackgroundColor = color,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Fill,
        }).ToList()
    };

    public override void Build()
    {
        _canvas?.Dispose();

        _canvas = new Canvas()
        {
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Lock,
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout()
            {
                Type = LayoutType.Column,
                Spacing = 8,
                Padding = new Thickness(16, 32),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaLabel("Orientation = Neither (must NOT scroll)")
                    {
                        FontSize = 15, TextColor = Colors.Black
                    },
                    new SkiaScroll()
                    {
                        Orientation = ScrollOrientation.Neither,
                        HeightRequest = 200,
                        HorizontalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.LightGray,
                        Content = TallContent(Colors.OrangeRed)
                    }.Assign(out _neither),

                    new SkiaLabel("Orientation = Vertical (control)")
                    {
                        FontSize = 15, TextColor = Colors.Black
                    },
                    new SkiaScroll()
                    {
                        Orientation = ScrollOrientation.Vertical,
                        HeightRequest = 200,
                        HorizontalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.LightGray,
                        Content = TallContent(Colors.SeaGreen)
                    }.Assign(out _vertical),

                    new SkiaLabel("Drag inside each block. The orange one must not move at all, not even rubber-band.")
                    {
                        FontSize = 13, TextColor = Colors.Black
                    },
                }
            }
        };

        this.Content = _canvas;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _canvas?.Dispose();
        }

        base.Dispose(isDisposing);
    }
}
