using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaImage: one source, every TransformAspect side by side, the built-in effects, custom Skia
/// filters, tiling and alignment. Ported from the React demo's ImagesPage.tsx.
/// </summary>
public class ImagesPage : SkiaLayer
{
    private const string Photo = "images/baboon.jpg";

    private static readonly TransformAspect[] Aspects =
    {
        TransformAspect.AspectCover, TransformAspect.AspectFit, TransformAspect.AspectFill,
        TransformAspect.AspectFitFill, TransformAspect.Fill, TransformAspect.Fit,
        TransformAspect.FitFill, TransformAspect.Cover, TransformAspect.None,
    };

    /// <summary>
    /// Preload queue demo: 8 distinct urls of the same photo (query string = own cache entry). MaxParallelLoads
    /// limits network loads only; these are local files, read at once, so running / queued stay 0 on this head.
    /// </summary>
    private static readonly List<string> PreloadSources =
        Enumerable.Range(0, 8).Select(i => $"images/glass2.jpg?queue={i}").ToList();

    private SkiaLabel _queueStatus;

    /// <summary>Builds the page.</summary>
    public ImagesPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    Children = new List<SkiaControl>
                    {
                        Heading("SkiaImage · Aspect", 24, top: 0),
                        new SkiaLabel("Same photo in a 220×120 box. Overflow is clipped to the box.")
                        {
                            FontSize = 13,
                            TextColor = Colors.LightGray,
                            HorizontalOptions = LayoutOptions.Center,
                        },
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            MaximumWidthRequest = 720,
                            Children = Aspects.Select(AspectTile).ToList(),
                        },

                        Heading("Effects · AddEffect, Blur, Zoom, offsets", 20),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            MaximumWidthRequest = 720,
                            Children = BuildEffects(),
                        },

                        Heading("Custom filters · AddEffect = Custom + PaintColorFilter / PaintImageFilter", 20),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            MaximumWidthRequest = 720,
                            Children = new List<SkiaControl>
                            {
                                Tile("PaintColorFilter = CreateColorMatrix (R↔B)", new FilterImage
                                {
                                    Source = Photo,
                                    WidthRequest = 160,
                                    HeightRequest = 100,
                                    // Required: SkiaImage rewrites PaintColorFilter from AddEffect, so
                                    // anything but Custom throws the supplied filter away.
                                    AddEffect = SkiaImageEffect.Custom,
                                    ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                                    {
                                        0, 0, 1, 0, 0,
                                        0, 1, 0, 0, 0,
                                        1, 0, 0, 0, 0,
                                        0, 0, 0, 1, 0,
                                    }),
                                }),
                                Tile("PaintImageFilter = CreateDilate(4)", new FilterImage
                                {
                                    Source = Photo,
                                    WidthRequest = 160,
                                    HeightRequest = 100,
                                    ImageFilter = SKImageFilter.CreateDilate(4, 4),
                                }),
                            },
                        },

                        Heading("SkiaImageTiles · TileWidth/TileHeight, TileOffsetX animates", 20),
                        new SkiaImageTiles
                        {
                            Source = Photo,
                            TileWidth = 64,
                            TileHeight = 64,
                            HorizontalOptions = LayoutOptions.Center,
                            WidthRequest = 400,
                            HeightRequest = 160,
                            BackgroundColor = Colors.Black,
                        }.Animate(1, (me, animator, value, dt) =>
                        {
                            // The React page's endless drift (4 px per 50 ms, Y = X / 2) on the frame clock:
                            // accumulated per frame and wrapped at the 64 px tile, so it never resets visibly.
                            me.TileOffsetX = (me.TileOffsetX + 80 * dt) % 64;
                            me.TileOffsetY = (me.TileOffsetY + 40 * dt) % 64;
                        }, repeat: -1),

                        Heading("SkiaImageManager preload queue · idle", 20).Assign(out _queueStatus),
                        new SkiaButton("PreloadImages(8 urls, Low)")
                        {
                            BackgroundColor = Color.Parse("#0D6EFD"),
                            FontSize = 13,
                            HorizontalOptions = LayoutOptions.Center,
                        }.OnTapped(me => PreloadQueue()),

                        Heading("Alignment inside the box", 20),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            MaximumWidthRequest = 720,
                            Children = new List<SkiaControl>
                            {
                                Aligned(TransformAspect.AspectFit, DrawImageAlignment.Start, DrawImageAlignment.Start),
                                Aligned(TransformAspect.Fit, DrawImageAlignment.Center, DrawImageAlignment.Center),
                                Aligned(TransformAspect.Fit, DrawImageAlignment.End, DrawImageAlignment.End),
                            },
                        },
                    },
                },
            }.Fill(),
        };
    }

    /// <summary>
    /// Drops the 8 urls from the cache, preloads them at Low priority and shows the queue every ~10 ms
    /// (running / queued / peak), then the total time.
    /// </summary>
    private async void PreloadQueue()
    {
        var manager = SkiaImageManager.Instance;
        foreach (var source in PreloadSources)
            manager.RemoveFromCache(source);

        var peak = 0;
        var done = false; // read and written on the UI thread only: a late tick never overwrites the result
        var clock = Stopwatch.StartNew();

        using (new System.Threading.Timer(_ =>
               {
                   int running = manager.RunningCount, queued = manager.QueuedCount;
                   peak = Math.Max(peak, running);
                   MainThread.BeginInvokeOnMainThread(() =>
                   {
                       if (!done)
                           _queueStatus.Text = $"SkiaImageManager preload queue · running {running} · queued {queued} · peak {peak}";
                   });
               }, null, 0, 10))
        {
            await manager.PreloadImages(PreloadSources, LoadPriority.Low);
        }

        var elapsed = clock.ElapsedMilliseconds;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            done = true;
            _queueStatus.Text = $"SkiaImageManager preload queue · {PreloadSources.Count} loaded in {elapsed} ms · peak in flight {peak} (MaxParallelLoads={SkiaImageManager.MaxParallelLoads})";
        });
    }

    private static SkiaLabel Heading(string text, double size, double top = 12) => new(text)
    {
        FontSize = size,
        TextColor = Colors.White,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, top, 0, 0),
    };

    private static SkiaControl AspectTile(TransformAspect aspect) => new SkiaStack
    {
        Spacing = 4,
        WidthRequest = 220,
        Children = new List<SkiaControl>
        {
            new SkiaImage
            {
                Source = Photo,
                WidthRequest = 220,
                HeightRequest = 120,
                Aspect = aspect,
                BackgroundColor = Colors.Black,
            },
            new SkiaLabel($"{aspect}") { FontSize = 15, TextColor = Colors.White },
            new SkiaLabel($"Aspect=\"{aspect}\"") { FontSize = 12, TextColor = Color.Parse("#94A3B8") },
        },
    };

    /// <summary>Image with its caption below; captions may carry symbols (↔), drawn per glyph from the fallback faces.</summary>
    private static SkiaControl Tile(string caption, SkiaControl visual) => new SkiaStack
    {
        Spacing = 4,
        WidthRequest = 160,
        Children = new List<SkiaControl>
        {
            visual,
            new SkiaLabel(caption)
            {
                FontSize = 12,
                TextColor = Color.Parse("#94A3B8"),
                FontFamilyFallback = "FontSymbols,FontSymbols2",
            },
        },
    };

    private static SkiaControl Aligned(TransformAspect aspect, DrawImageAlignment horizontal, DrawImageAlignment vertical) =>
        new SkiaImage
        {
            Source = Photo,
            WidthRequest = 120,
            HeightRequest = 120,
            Aspect = aspect,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            BackgroundColor = Colors.Black,
        };

    private static List<SkiaControl> BuildEffects()
    {
        SkiaControl Effect(string title, Action<SkiaImage> configure)
        {
            var image = new SkiaImage
            {
                Source = Photo,
                WidthRequest = 160,
                HeightRequest = 100,
                BackgroundColor = Colors.Black,
            };

            configure(image);
            return Tile(title, image);
        }

        return new List<SkiaControl>
        {
            Effect("BlackAndWhite", i => i.AddEffect = SkiaImageEffect.BlackAndWhite),
            Effect("Sepia", i => i.AddEffect = SkiaImageEffect.Sepia),
            Effect("Pastel", i => i.AddEffect = SkiaImageEffect.Pastel),
            Effect("InvertColors", i => i.AddEffect = SkiaImageEffect.InvertColors),
            Effect("Tint #0D6EFD Multiply", i =>
            {
                i.AddEffect = SkiaImageEffect.Tint;
                i.ColorTint = Color.Parse("#0D6EFD");
                i.EffectBlendMode = SKBlendMode.Multiply;
            }),
            Effect("Darken=80", i => { i.AddEffect = SkiaImageEffect.Darken; i.Darken = 80; }),
            Effect("Lighten=80", i => { i.AddEffect = SkiaImageEffect.Lighten; i.Lighten = 80; }),
            Effect("Contrast=1.5", i => { i.AddEffect = SkiaImageEffect.Contrast; i.Contrast = 1.5; }),
            Effect("Saturation=2", i => { i.AddEffect = SkiaImageEffect.Saturation; i.Saturation = 2; }),
            Effect("Blur=3", i => i.Blur = 3),
            Effect("ZoomX/Y=1.8", i => { i.ZoomX = 1.8; i.ZoomY = 1.8; }),
            Effect("HorizontalOffset=-40", i =>
            {
                i.HorizontalOffset = -40;
                i.Aspect = TransformAspect.AspectFit;
            }),
            Effect("HSL Gamma=0.6 (hue) Sat=1 Bright=0.5", i =>
            {
                i.AddEffect = SkiaImageEffect.HSL;
                i.BackgroundColor = Colors.White;
                i.Gamma = 0.6;
                i.Saturation = 1;
                i.Brightness = 0.5;
            }),
        };
    }
}

/// <summary>
/// Lets app code supply its own Skia filters to a <see cref="SkiaImage"/>.
/// <para>
/// Both filter fields are protected and, more importantly, <c>Paint</c> clears them at the top of
/// every draw while the matching NeedInvalidate flag is set — so simply assigning one from outside
/// is wiped before it is ever used, on every C# head. Re-asserting them here, with the flag cleared,
/// is what makes a custom filter survive into the paint.
/// </para>
/// </summary>
public class FilterImage : SkiaImage
{
    private SKColorFilter _colorFilter;
    private SKImageFilter _imageFilter;

    /// <summary>Color filter applied to the image paint.</summary>
    public SKColorFilter ColorFilter
    {
        get => _colorFilter;
        set
        {
            _colorFilter = value;
            InvalidateColorFilter();
            Update();
        }
    }

    /// <summary>Image filter applied to the image paint.</summary>
    public SKImageFilter ImageFilter
    {
        get => _imageFilter;
        set
        {
            _imageFilter = value;
            Update();
        }
    }

    /// <inheritdoc/>
    protected override void Paint(DrawingContext ctx)
    {
        if (_colorFilter != null)
        {
            // Clear the flag first: with it set, base.Paint nulls the filter before using it, and the
            // AddEffect switch below that only runs when the filter is null.
            NeedInvalidateColorFilter = false;
            PaintColorFilter = _colorFilter;
        }

        if (_imageFilter != null)
        {
            NeedInvalidateImageFilter = false;
            PaintImageFilter = _imageFilter;
        }

        base.Paint(ctx);
    }
}
