using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Gate for a horizontal templated strip whose roster SHRINKS while it is scrolled along (FiltersCamera
/// looks strip, device 2026-09-10: picking a preset swapped 38 looks for 14 and the row vanished).
/// Three SkiaScroll defects combined there:
/// - the content-size clamp never ran after a re-measure, so the offset stayed past the new end;
/// - a horizontal ScrollToIndex(0) was rejected as "not valid" and never resolved or cleared;
/// - every scroll was born with a phantom pending ScrollToIndex(0).
/// Asserts each one on its own, then the device sequence visible and hidden under a modal.
/// </summary>
public static class ScrollShrinkRepro
{
    class Item
    {
        public int Id { get; init; }
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============= SCROLL SHRINK (strip roster shrinks while scrolled) =============");
        int bad = 0;
        try
        {
            bad += RunCore("fresh scroll has no pending order", park: false, hide: false, order: false, freshOnly: true);
            bad += RunCore("shrink alone clamps into range", park: true, hide: false, order: false);
            bad += RunCore("shrink + scroll to 0, visible", park: true, hide: false, order: true);
            bad += RunCore("shrink + scroll to 0, hidden under a modal", park: true, hide: true, order: true);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  CRASH: {e}");
            bad++;
        }

        Console.WriteLine(bad == 0
            ? "=> PASS (shrinking strip stays in range, lands on the ordered index, no phantom order)"
            : $"=> FAIL ({bad} checks failed)");
        Console.WriteLine("===============================================================================");
    }

    static int RunCore(string phase, bool park, bool hide, bool order, bool freshOnly = false)
    {
        using var host = new HeadlessCanvasHost(440, 900, scale: 1f, background: Colors.Black);
        SkiaScroll strip = null;
        SkiaLayout row = null;
        SkiaLayer root = null;

        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaScroll
                        {
                            Orientation = ScrollOrientation.Horizontal,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.End,
                            Margin = new Thickness(0, 0, 0, 100),
                            HeightRequest = 92,
                            Padding = new Thickness(0, 8),
                            SkipRenderingOutOfBounds = true,
                            Content = new SkiaLayout
                            {
                                Type = LayoutType.Row,
                                VerticalOptions = LayoutOptions.Center,
                                Spacing = 8,
                                RecyclingTemplate = RecyclingTemplate.Disabled,
                                VirtualisationInflated = 50,
                                ItemTemplate = new DataTemplate(() => new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    WidthRequest = 62,
                                    HeightRequest = 62,
                                    BackgroundColor = Colors.White,
                                    UseCache = SkiaCacheType.Image,
                                }),
                                ItemsSource = new ObservableRangeCollection<Item>(
                                    Enumerable.Range(1, 40).Select(i => new Item { Id = i })),
                            }.Assign(out row)
                        }.Assign(out strip)
                    }
                }.Assign(out root)
            }
        };

        for (int i = 0; i < 200 && row.LastVisibleIndex < 0; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(2);
        }

        host.AdvanceFrames(20, 16);

        int bad = 0;
        Console.WriteLine($"  --- {phase} ---");

        if (freshOnly)
        {
            if (strip.HasPendingScrollOrder)
            {
                bad++;
                Console.WriteLine("   FAIL a scroll nobody ordered to move reports a pending scroll order");
            }
            else
            {
                Console.WriteLine("  no pending order");
            }

            return bad;
        }

        if (park)
        {
            // scrolled along the strip, the way the app parks it on the selected look at startup
            strip.ScrollTo(-900, 0, 0, true);
            host.AdvanceFrames(10, 16);
        }

        if (hide)
        {
            // a freezing modal (the preset picker) hides the root layout above the strip
            root.IsVisible = false;
            host.AdvanceFrames(10, 16);
        }

        // the preset pick: a smaller roster, then the ordered scroll back to the first look
        row.ItemsSource = new ObservableRangeCollection<Item>(Enumerable.Range(100, 14).Select(i => new Item { Id = i }));
        if (order)
        {
            strip.OrderedScroll = -1;
            strip.OrderedScroll = 0;
        }

        host.AdvanceFrames(10, 16);

        if (hide)
        {
            root.IsVisible = true;
        }

        host.AdvanceFrames(60, 16);

        var travel = Math.Max(0, strip.ContentSize.Pixels.Width - strip.MeasuredSize.Pixels.Width);
        var offset = strip.ViewportOffsetX;
        var cells = row.RenderTree?.Count() ?? 0;
        Console.WriteLine($"  offX={offset:0.0} valid=0..{-travel:0} cells={cells} pendingOrder={strip.HasPendingScrollOrder}");

        if (offset > 0.5 || offset < -travel - 0.5)
        {
            bad++;
            Console.WriteLine($"   FAIL offset {offset:0.0} is outside the scrollable range after the content shrank");
        }

        if (cells == 0)
        {
            bad++;
            Console.WriteLine("   FAIL no cell on screen: the strip looks gone");
        }

        if (order)
        {
            if (Math.Abs(offset) > 0.5)
            {
                bad++;
                Console.WriteLine($"   FAIL ScrollToIndex(0) did not land: offset {offset:0.0}");
            }

            if (strip.HasPendingScrollOrder)
            {
                bad++;
                Console.WriteLine("   FAIL the scroll order is still pending after it should have resolved");
            }
        }

        return bad;
    }
}
