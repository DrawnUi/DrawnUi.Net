using System.Collections.Generic;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Color = DrawnUi.Color;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaPicker with ControlStyle Material3 builds the Material 3 outlined field (metrics, floating label, notched
/// outline) instead of falling back to the default look, and a runtime style change drops it again.
/// </summary>
public class PickerMaterial3Tests
{
    private static readonly List<string> Items = new() { "Forest", "Wetlands", "Savanna" };

    private static (HeadlessCanvasHost host, SkiaPicker picker) Render(PrebuiltControlStyle style, float scale = 1f)
    {
        var host = new HeadlessCanvasHost((int)(320 * scale), (int)(120 * scale), scale, background: Colors.White);

        var picker = new SkiaPicker
        {
            ControlStyle = style,
            Placeholder = "Habitat",
            Items = Items,
            WidthRequest = 280,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(10),
        };

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { picker },
        };

        host.AdvanceFrames(5);
        return (host, picker);
    }

    private static bool Same(Color actual, string hex)
    {
        var expected = Color.FromArgb(hex);
        return Math.Abs(actual.Red - expected.Red) < 0.01
               && Math.Abs(actual.Green - expected.Green) < 0.01
               && Math.Abs(actual.Blue - expected.Blue) < 0.01
               && Math.Abs(actual.Alpha - expected.Alpha) < 0.01;
    }

    [Fact]
    public void Material3_UsesTheOutlinedFieldMetrics()
    {
        var (host, picker) = Render(PrebuiltControlStyle.Material3);
        using var _ = host;

        var frame = picker.FindView<SkiaShape>("PickerFrame");
        var text = picker.FindView<SkiaLabel>("PickerText");
        var label = picker.FindView<SkiaLabel>("PickerLabel");

        Assert.Equal(64, picker.HeightRequest); // 56 field + half of the floating label line
        Assert.Equal(4, frame.CornerRadius.TopLeft);
        Assert.Equal(1, frame.StrokeWidth);
        Assert.True(Same(frame.StrokeColor, "#79747E"), "outline");
        Assert.Equal(0, frame.BackgroundColor.Alpha); // outlined: no container
        Assert.Null(frame.Shadows);

        // empty: the placeholder rests inside at body large, the floating label is hidden
        Assert.NotNull(label);
        Assert.False(label.IsVisible);
        Assert.Equal("Habitat", text.Text);
        Assert.Equal(16, text.FontSize);
        Assert.True(Same(text.TextColor, "#49454F"), "placeholder color");

        // populated: the placeholder floats onto the outline at body small
        picker.SelectedIndex = 1;
        host.AdvanceFrames(3);

        Assert.True(label.IsVisible);
        Assert.Equal("Habitat", label.Text);
        Assert.Equal(12, label.FontSize);
        Assert.Equal("Wetlands", text.Text);
        Assert.Equal(16, text.FontSize);
        Assert.True(Same(text.TextColor, "#1D1B20"), "text color");
    }

    [Fact]
    public void Material3_FloatingLabel_CutsAGapInTheOutline()
    {
        var (host, picker) = Render(PrebuiltControlStyle.Material3, scale: 2f);
        using var _ = host;

        var frame = picker.FindView<SkiaShape>("PickerFrame");
        var label = picker.FindView<SkiaLabel>("PickerLabel");

        var outlineRow = (int)frame.DrawingRect.Top + 1;
        var gapX = (int)(frame.DrawingRect.Left + 13 * host.Scale);   // inside the label's leading padding
        var lineX = (int)(frame.DrawingRect.Left + 200 * host.Scale); // far from the label

        byte Luma(int x, int y)
        {
            using var image = host.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            var c = bitmap.GetPixel(x, y);
            return (byte)((c.Red + c.Green + c.Blue) / 3);
        }

        // empty: closed outline everywhere
        Assert.True(Luma(gapX, outlineRow) < 200, "outline expected where the label will float");
        Assert.True(Luma(lineX, outlineRow) < 200, "outline expected");

        // populated: the label floats and the cached outline is recorded again with the gap
        picker.SelectedIndex = 0;
        host.AdvanceFrames(3);

        Assert.True(label.IsVisible);
        Assert.True(Luma(gapX, outlineRow) > 240, "gap expected under the floating label");
        Assert.True(Luma(lineX, outlineRow) < 200, "outline expected outside the label");
    }

    [Fact]
    public void DefaultLook_HasNoFloatingLabel()
    {
        var (host, picker) = Render(PrebuiltControlStyle.Unset);
        using var _ = host;

        var frame = picker.FindView<SkiaShape>("PickerFrame");

        Assert.Equal(48, picker.HeightRequest);
        Assert.Equal(12, frame.CornerRadius.TopLeft);
        Assert.Null(picker.FindView<SkiaLabel>("PickerLabel"));
    }

    [Fact]
    public void ChangingStyleAtRuntime_RebuildsWithoutTheMaterial3Parts()
    {
        var (host, picker) = Render(PrebuiltControlStyle.Material3);
        using var _ = host;
        picker.SelectedIndex = 2;
        host.AdvanceFrames(3);
        Assert.NotNull(picker.FindView<SkiaLabel>("PickerLabel"));

        picker.ControlStyle = PrebuiltControlStyle.Windows;
        host.AdvanceFrames(5);

        var frame = picker.FindView<SkiaShape>("PickerFrame");
        Assert.Null(picker.FindView<SkiaLabel>("PickerLabel"));
        Assert.Equal(48, picker.HeightRequest);
        Assert.Equal(1.5, frame.StrokeWidth, 3); // the Windows outline
        Assert.Equal("Savanna", picker.FindView<SkiaLabel>("PickerText").Text);
    }
}
