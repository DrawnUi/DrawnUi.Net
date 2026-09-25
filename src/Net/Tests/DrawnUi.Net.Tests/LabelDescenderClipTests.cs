using DrawnUi;
using DrawnUi.Controls;
using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Glyph ink can reach below FontMetrics.Descent (Inter: FontMetrics.Bottom is ~1.5px past Descent at 18px).
/// The label's line box is ascent + descent, so that ink lands below DrawingRect, and the default Operations
/// cache draws its picture clipped to the recording rect: the last line's g/p/y lost their bottom pixels
/// (DrawnCamera What's New, Windows 125%). The label must report the overshoot as its effects margin so the
/// cache area includes it.
/// </summary>
public class LabelDescenderClipTests
{
    const string InterPath = @"C:\Dev\Cases\GitHub\DrawnCamera\src\DrawnCamera\Resources\Fonts\Inter-Regular.ttf";
    const string Text = "We have added Looks! Save your favorite combinations of background, face, filter and adjustments, and switch between them with one tap, right from the main bar.";

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void LastLineDescenders_SurviveOperationsCache(float scale)
    {
        if (!File.Exists(InterPath))
            return; // font not on this machine, nothing to prove

        SkiaFontManager.Instance.RegisterFont("FontText", InterPath);
        SkiaFontManager.Instance.Initialize();

        var background = Color.FromRgb(0x1a, 0x1b, 0x1e);
        using var host = new HeadlessCanvasHost((int)(480 * scale), (int)(320 * scale), scale, background);

        SkiaLabel label = null;
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 12,
            Padding = new Thickness(24, 22),
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Start,
                    Children =
                    {
                        new SkiaLabel { Text = "\u2022", FontFamily = "FontText", FontSize = 15, VerticalOptions = LayoutOptions.Start },
                        new SkiaLabel
                        {
                            Text = Text,
                            FontFamily = "FontText",
                            FontSize = 14,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Start,
                            Margin = new Thickness(16, 0, 0, 0),
                            LineBreakMode = LineBreakMode.WordWrap,
                            MaxLines = -1,
                        }.Assign(out label),
                    }
                }
            }
        };

        for (int i = 0; i < 3; i++) host.RenderFrame(16);

        Assert.Equal(SkiaCacheType.Operations, label.UsingCacheType);
        Assert.True(label.Lines.Length > 1, "text did not wrap");
        Assert.True(label.EffectsMarginPixels.Bottom >= 1, $"no bottom overshoot reported: {label.EffectsMarginPixels}");

        // the expansion the cache surface / clip / dirty region get: FontMetrics.Bottom past Descent, whole pixels
        var expected = Math.Ceiling(label.FontMetrics.Bottom - label.FontMetrics.Descent);
        Assert.True(expected >= 1, $"Inter should overshoot its descent at this size, metrics {label.FontMetrics.Descent}/{label.FontMetrics.Bottom}");
        Assert.Equal(expected, label.EffectsMarginPixels.Bottom, 0.01);
    }
}
