using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A label with CharacterSpacing in a padded, cached chip (FiltersCamera My Shots filter chips, MAUI Windows at 1.25)
/// drew its last letter cut flat at the right: the round S of FAVORITES lost its edge. 14 pt padding is 17.5 px, the
/// label was placed from 17.5 to 94.5 and Math.Round took the halves to the even pixel: 18 to 94, one pixel narrower
/// than measured, and the label clips to that rect. The text must draw with the same ink in the chip as with room.
/// </summary>
public class LabelSpacedClipTests
{
    readonly ITestOutputHelper _out;
    public LabelSpacedClipTests(ITestOutputHelper output) => _out = output;

    const string FontPath = @"C:\Dev\Cases\GitHub\FiltersCamera\src\app\Resources\Fonts\BarlowCondensed-SemiBold.ttf";
    const float Scale = 1.25f;

    static string Font()
    {
        if (!File.Exists(FontPath))
            return null;
        SkiaFontManager.Instance.RegisterFont("ChipTestDisplay", FontPath);
        SkiaFontManager.Instance.Initialize();
        return "ChipTestDisplay";
    }

    static SkiaRichLabel Label(string text, string font) => new SkiaRichLabel(text)
    {
        FontFamily = font,
        FontSize = 12,
        CharacterSpacing = 3,
        TextColor = Colors.White,
    };

    /// <summary>Left and right ink columns of the white text on the black canvas.</summary>
    static (int left, int right) Ink(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        int left = int.MaxValue, right = -1;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).Red > 24)
            {
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        return (left, right);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("FAVORITES")]
    [InlineData("DIGICAM")]
    [InlineData("CLASSIC")]
    [InlineData("ZOOM")]
    [InlineData("VINTAGE")]
    [InlineData("MONO 125")]
    [InlineData("SKETCH")]
    public void SpacedLabelInChip_DrawsAllItsInk(string text)
    {
        var font = Font();
        if (font == null)
            return;

        int reference;
        using (var host = new HeadlessCanvasHost(300, 80, Scale, Colors.Black))
        {
            var wide = Label(text, font);
            wide.WidthRequest = 200;
            host.Canvas.Content = new SkiaLayer { Children = { wide } };
            host.AdvanceFrames(3);
            var (l, r) = Ink(host);
            reference = r - l;
        }

        using (var host = new HeadlessCanvasHost(300, 80, Scale, Colors.Black))
        {
            SkiaLabel label = null;
            host.Canvas.Content = new SkiaScroll
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.Fill,
                Content = new SkiaRow
                {
                    Children =
                    {
                        new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            CornerRadius = 15,
                            Padding = new Thickness(14, 7),
                            UseCache = SkiaCacheType.Image,
                            Children = { Label(text, font).Assign(out label) },
                        },
                    },
                },
            };
            host.AdvanceFrames(3);
            var (l, r) = Ink(host);
            _out.WriteLine($"{text}: measured {label.MeasuredSize.Pixels.Width} rect {label.DrawingRect.Width} line {label.Lines[0].Width:0.00} ink {r - l} reference {reference}");
            Assert.Equal(reference, r - l);
        }
    }

    /// <summary>Without the app font: a child at a half-pixel offset keeps its measured width.</summary>
    [Theory]
    [InlineData(61.6)] // 77 px
    [InlineData(45.6)] // 57 px
    [InlineData(48)]   // 60 px
    public void ChildAtHalfPixelOffset_KeepsItsWidth(double width)
    {
        using var host = new HeadlessCanvasHost(300, 80, Scale, Colors.Black);
        SkiaShape child = null;
        host.Canvas.Content = new SkiaRow
        {
            Children =
            {
                new SkiaShape
                {
                    Padding = new Thickness(14, 7),
                    Children = { new SkiaShape { WidthRequest = width, HeightRequest = 10 }.Assign(out child) },
                },
            },
        };
        host.AdvanceFrames(2);

        Assert.Equal(child.MeasuredSize.Pixels.Width, child.DrawingRect.Width);
    }
}
