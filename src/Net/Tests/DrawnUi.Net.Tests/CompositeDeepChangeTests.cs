using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// ImageComposite redraws a change at any depth: a card inside an uncached stack, the stack a child of the
/// composite (a list whose cards sit in an inner stack). Only the card's area is erased and redrawn, the other cards
/// are not; the pixels equal a full render of the same state. A transform on the way falls back to redrawing the
/// direct child whole; too many changes at once fall back to a full record.
/// </summary>
public class CompositeDeepChangeTests
{
    static readonly Color[] Colors5 = { Colors.SteelBlue, Colors.Orange, Colors.SeaGreen, Colors.Purple, Colors.Gold };

    /// <summary>A caption, then the cards in an uncached inner stack: the Background panel's shape.</summary>
    static (SkiaStack List, SkiaStack Inner, List<SkiaShape> Cards, SkiaLabel Caption) Build(int count = 5, bool composite = true)
    {
        var cards = new List<SkiaShape>();
        var inner = new SkiaStack
        {
            Spacing = 8,
            Children = Enumerable.Range(0, count).Select(i => (SkiaControl)new SkiaShape
            {
                CornerRadius = 10,
                HeightRequest = count > 5 ? 10 : 40,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors5[i % Colors5.Length],
                StrokeColor = Colors.White,
                StrokeWidth = 1,
                UseCache = SkiaCacheType.Image,
            }.Adapt(me => cards.Add(me))).ToList(),
        };
        SkiaLabel caption = null;
        var list = new SkiaStack
        {
            Padding = new Thickness(12),
            Spacing = 8,
            UseCache = composite ? SkiaCacheType.ImageComposite : SkiaCacheType.None,
            Children = new List<SkiaControl>
            {
                new SkiaLabel("Presets") { TextColor = Colors.White, FontSize = 14 }.Assign(out caption),
                inner,
            },
        };
        return (list, inner, cards, caption);
    }

    static SKBitmap Pixels(HeadlessCanvasHost host)
    {
        using var image = host.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>The same final state drawn from scratch: the partial record must give these pixels.</summary>
    static void AssertSameAsFullRender(HeadlessCanvasHost host, Action<List<SkiaShape>> finalState, int count = 5)
    {
        using var reference = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var built = Build(count, composite: false);
        finalState(built.Cards);
        reference.Canvas.Content = built.List;
        reference.AdvanceFrames(3);
        Assert.True(reference.NonBackgroundFraction(Colors.Black) > 0.2); // a real picture, not two blank ones

        using var a = Pixels(host);
        using var b = Pixels(reference);
        var different = 0;
        for (var y = 0; y < a.Height; y++)
        for (var x = 0; x < a.Width; x++)
        {
            var p = a.GetPixel(x, y);
            var q = b.GetPixel(x, y);
            if (Math.Abs(p.Red - q.Red) > 2 || Math.Abs(p.Green - q.Green) > 2 || Math.Abs(p.Blue - q.Blue) > 2)
                different++;
        }
        Assert.Equal(0, different);
    }

    [Fact]
    public void CardInUncachedStack_OnlyItsAreaIsRedrawn()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, inner, cards, _) = Build();
        host.Canvas.Content = list;
        host.AdvanceFrames(3);
        Assert.False(list.LastCompositeRecord.Partial);

        cards[2].BackgroundColor = Colors.Red;
        host.AdvanceFrames(2);

        var record = list.LastCompositeRecord;
        Assert.True(record.Partial);
        Assert.Equal(new SkiaControl[] { cards[2] }, record.Changed);
        Assert.Single(record.Areas);
        Assert.Equal(cards[2].DirtyRegion, record.Areas[0]);
        Assert.Contains(inner, record.Redrawn);

        AssertSameAsFullRender(host, c => c[2].BackgroundColor = Colors.Red);
    }

    [Fact]
    public void TwoCardsChanged_TwoAreas()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, _, cards, _) = Build();
        host.Canvas.Content = list;
        host.AdvanceFrames(3);

        cards[0].BackgroundColor = Colors.Red;
        cards[4].BackgroundColor = Colors.White;
        host.AdvanceFrames(2);

        var record = list.LastCompositeRecord;
        Assert.True(record.Partial);
        Assert.Equal(2, record.Areas.Count);
        Assert.Equal(2, record.Changed.Count);

        AssertSameAsFullRender(host, c =>
        {
            c[0].BackgroundColor = Colors.Red;
            c[4].BackgroundColor = Colors.White;
        });
    }

    [Fact]
    public void TransformOnTheWay_RedrawsTheDirectChildWhole()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, inner, cards, _) = Build();
        host.Canvas.Content = list;
        host.AdvanceFrames(3);

        cards[1].TranslationX = 20;
        host.AdvanceFrames(2);

        var record = list.LastCompositeRecord;
        Assert.True(record.Partial);
        Assert.Empty(record.Changed); // by area only when nothing on the way moves the content
        Assert.Contains(inner, record.Redrawn);

        AssertSameAsFullRender(host, c => c[1].TranslationX = 20);
    }

    [Fact]
    public void DirectChildChange_AsBefore()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, _, _, caption) = Build();
        host.Canvas.Content = list;
        host.AdvanceFrames(3);

        caption.TextColor = Colors.Yellow;
        host.AdvanceFrames(2);

        var record = list.LastCompositeRecord;
        Assert.True(record.Partial);
        Assert.Empty(record.Changed);
        Assert.Contains(caption, record.Redrawn);
    }

    /// <summary>
    /// A child whose glow comes and goes (a button's hover) changes the effects margin the composite's surface is
    /// recorded with. The old surface was reused at the old margin's offset and the whole list showed shifted
    /// (DrawnCamera 1.0.2, Reset under the Background sliders).
    /// </summary>
    [Fact]
    public void ChildGlowComesAndGoes_NoShift()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, _, cards, caption) = Build();
        host.Canvas.Content = list;
        host.AdvanceFrames(3);

        var glow = new OuterGlowEffect { Color = Colors.White, Blur = 10 };
        cards[4].VisualEffects.Add(glow);
        cards[4].Update();
        host.AdvanceFrames(3);
        cards[4].VisualEffects.Remove(glow);
        cards[4].Update();
        host.AdvanceFrames(3);

        AssertSameAsFullRender(host, c => { });
    }

    /// <summary>The Background panel as shipped: the composite list in a scroll scrolled to its end, a button that glows
    /// on hover as a direct child at the list's left edge.</summary>
    static (SkiaScroll Scroll, SkiaStack List, SkiaShape Button) BuildScrolledWithButton(bool composite)
    {
        var (list, _, _, _) = Build(count: 8, composite: composite);
        SkiaShape button = null;
        list.AddSubView(new SkiaShape
        {
            CornerRadius = 14,
            WidthRequest = 120,
            HeightRequest = 40,
            BackgroundColor = Colors.DimGray,
            StrokeColor = Colors.Gray,
            StrokeWidth = 1,
            HorizontalOptions = LayoutOptions.Start,
        }.Assign(out button));
        var scroll = new SkiaScroll { VerticalOptions = LayoutOptions.Fill, Content = list };
        return (scroll, list, button);
    }

    [Fact]
    public void DirectChildGlowInScrolledComposite_NoShift()
    {
        using var host = new HeadlessCanvasHost(300, 300, scale: 1f, background: Colors.Black);
        var (scroll, list, button) = BuildScrolledWithButton(composite: true);
        host.Canvas.Content = scroll;
        host.AdvanceFrames(3);
        scroll.ScrollToBottom(0);
        host.AdvanceFrames(5);

        var glow = new OuterGlowEffect { Color = Colors.White, Blur = 10 };
        button.VisualEffects.Add(glow);
        button.Update();
        host.AdvanceFrames(3);
        button.VisualEffects.Remove(glow);
        button.Update();
        host.AdvanceFrames(3);

        using var reference = new HeadlessCanvasHost(300, 300, scale: 1f, background: Colors.Black);
        var built = BuildScrolledWithButton(composite: false);
        reference.Canvas.Content = built.Scroll;
        reference.AdvanceFrames(3);
        built.Scroll.ScrollToBottom(0);
        reference.AdvanceFrames(5);

        using var a = Pixels(host);
        using var b = Pixels(reference);
        var different = 0;
        for (var y = 0; y < a.Height; y++)
        for (var x = 0; x < a.Width; x++)
        {
            var p = a.GetPixel(x, y);
            var q = b.GetPixel(x, y);
            if (Math.Abs(p.Red - q.Red) > 2 || Math.Abs(p.Green - q.Green) > 2 || Math.Abs(p.Blue - q.Blue) > 2)
                different++;
        }
        Assert.Equal(0, different);
    }

    /// <summary>
    /// The shipped bug as it happens: the mouse rests on a button that glows while hovered, inside a scrolled composite
    /// list. One hover change, no flicker, and the picture equals a full render of the hovered state.
    /// </summary>
    [Fact]
    public void GlowOnHover_PointerResting_HoverStaysAndNoShift()
    {
        using var host = new HeadlessCanvasHost(300, 300, scale: 1f, background: Colors.Black);
        var (scroll, list, button) = BuildScrolledWithButton(composite: true);
        var changes = new List<bool>();
        var glow = new OuterGlowEffect { Color = Colors.White, Blur = 10 };
        button.OnHovered((me, on) =>
        {
            changes.Add(on);
            if (on)
                me.VisualEffects.Add(glow);
            else
                me.VisualEffects.Remove(glow);
            me.Update();
        });
        host.Canvas.Content = scroll;
        host.AdvanceFrames(3);
        scroll.ScrollToBottom(0);
        host.AdvanceFrames(5);

        // every record of the list: its surface must be the size of the area it says it holds (the effects margin
        // of the glow included); a surface kept from another margin draws everything shifted
        var mismatches = new List<string>();
        list.CreatedCache += (_, ro) =>
        {
            var clip = ro.Surface?.Canvas.DeviceClipBounds;
            if (clip != null && (Math.Abs(clip.Value.Width - ro.Bounds.Width) > 1 || Math.Abs(clip.Value.Height - ro.Bounds.Height) > 1))
                mismatches.Add($"surface {clip.Value.Width}x{clip.Value.Height} for bounds {ro.Bounds.Width}x{ro.Bounds.Height}");
        };

        var r = button.GetAccessibilityPixelRect();
        for (var i = 0; i < 30; i++)
        {
            host.Canvas.HandleDesktopPointerMove(r.MidX + (i % 5), r.MidY, false, 300, 300);
            host.RenderFrame();
        }

        Assert.True(changes.Count > 0, $"no hover: button at {r}, scroll {scroll.ViewportOffsetY}, drawing {button.DrawingRect}");
        Assert.Equal(new[] { true }, changes);

        // the mouse leaves: the glow goes, the margin shrinks back
        host.Canvas.HandleDesktopPointerMove(5, 5, false, 300, 300);
        host.AdvanceFrames(3);
        Assert.Equal(new[] { true, false }, changes);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void ManyChanges_OneFullRecord()
    {
        using var host = new HeadlessCanvasHost(300, 400, scale: 1f, background: Colors.Black);
        var (list, _, cards, _) = Build(count: SkiaControl.MaxCompositionAreas + 4);
        host.Canvas.Content = list;
        host.AdvanceFrames(3);

        foreach (var card in cards)
            card.BackgroundColor = Colors.Red;
        host.AdvanceFrames(2);

        Assert.False(list.LastCompositeRecord.Partial);
        AssertSameAsFullRender(host, c =>
        {
            foreach (var card in c)
                card.BackgroundColor = Colors.Red;
        }, count: SkiaControl.MaxCompositionAreas + 4);
    }
}
