namespace Add3dGallery;

/// <summary>
/// The whole app, shared by the Blazor and the pure WebAssembly heads: a header with two tabs (the gallery and the boss
/// fight) over one drawing surface, and the frame rate in a corner.
/// </summary>
public class GalleryRoot : SkiaLayer
{
    readonly Button3D[] _tabs = new Button3D[2];
    readonly SkiaControl[] _pages = new SkiaControl[2];

    /// <summary>The gallery screen, measured by <see cref="PerfProbe"/>.</summary>
    public GalleryPage Gallery => (GalleryPage)_pages[0];

    /// <param name="subtitle">The line under the app name, naming the head.</param>
    public GalleryRoot(string subtitle)
    {
        VerticalOptions = LayoutOptions.Fill;
        BackgroundColor = Theme.Page;
        // titles and descriptions can be selected and copied: their selection in the gallery's gold
        SkiaLabel.TextSelectionColor = Theme.Accent.WithAlpha(0.4f).ToSKColor();
        // every text of the page is read by a screen reader (descriptions, hints, prices), as in DrawnUI for React
        SkiaLabel.DefaultAccessibilityRole = Aria.RoleText;
        Children = new List<SkiaControl>
        {
            new SkiaGrid
                {
                    RowSpacing = 0,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaLayer
                        {
                            BackgroundColor = Theme.Header,
                            HeightRequest = 64,
                            Spacing = 10,
                            Padding = new Thickness(24, 0),
                            // Static chrome over pages that draw every frame: blitted, not drawn again.
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                new SkiaRow
                                {
                                    Spacing = 12,
                                    VerticalOptions = LayoutOptions.Center,
                                    Children =
                                    {
                                        new SkiaShape
                                        {
                                            Type = ShapeType.Rectangle, CornerRadius = 8, WidthRequest = 32, HeightRequest = 32,
                                            VerticalOptions = LayoutOptions.Center,
                                            FillGradient = new SkiaGradient
                                            {
                                                StartXRatio = 0, StartYRatio = 0, EndXRatio = 1, EndYRatio = 1,
                                                Colors = new List<Color> { Theme.Accent, Color.FromRgb(255, 99, 132) },
                                            },
                                            Children =
                                            {
                                                new SkiaLabel("3D") { FontSize = 13, FontFamily = Theme.Title, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation }, // the logo
                                            }
                                        },
                                        new SkiaStack
                                        {
                                            Spacing = 0,
                                            HorizontalOptions = LayoutOptions.Start,
                                            VerticalOptions = LayoutOptions.Center,
                                            Children =
                                            {
                                                new Copyable("DrawnUI 3D Demo") { FontSize = 17, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                                                new Copyable(subtitle) { FontSize = 12, TextColor = Theme.Muted },
                                            }
                                        },
                                    }
                                },
                                new SkiaRow
                                {
                                    Spacing = 8,
                                    HorizontalOptions = LayoutOptions.End,
                                    VerticalOptions = LayoutOptions.Center,
                                    AccessibilityRole = Aria.RoleTabList,
                                    AccessibilityLabel = "Screens",
                                    Children =
                                    {
                                        new Button3D("Gallery", Theme.Accent, 120) { AccessibilityRole = Aria.RoleTab }
                                            .Assign(out _tabs[0])
                                            .OnTapped(_ => Show(0)),
                                        new Button3D("Boss fight", Theme.Quiet, 120) { AccessibilityRole = Aria.RoleTab }
                                            .Assign(out _tabs[1])
                                            .OnTapped(_ => Show(1)),
                                    }
                                },
                            }
                        }.Assign(out _header).SetGrid(0, 0),
                        new SkiaLayer
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new GalleryPage().Assign(out _pages[0]),
                                new BossPage { IsVisible = false }.Assign(out _pages[1]),
                            }
                        }.SetGrid(0, 1),
                    }
                }
                .WithRowDefinitions("Auto,*"),
            new SkiaLabelFps
            {
                Margin = new Thickness(0, 0, 8, 8),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End,
                FontSize = 12,
                BackgroundColor = Color.FromRgb(139, 0, 0),
                TextColor = Colors.White,
                // Its text changes every frame: recorded (Operations, the label default), not a GPU surface made again each frame.
                ZIndex = 110,
            },
        };
        Show(0);
    }

    /// <summary>Narrower canvases put the tabs under the app's name.</summary>
    const float HeaderOneLine = 620;

    SkiaLayout _header;
    bool? _oneLine;

    /// <inheritdoc />
    protected override void OnMeasured()
    {
        base.OnMeasured();
        var width = (float)MeasuredSize.Units.Width;
        if (width <= 0 || float.IsInfinity(width))
            return;
        var oneLine = width >= HeaderOneLine;
        if (_oneLine == oneLine)
            return;
        _oneLine = oneLine;
        _header.Type = oneLine ? LayoutType.Absolute : LayoutType.Column;
        _header.HeightRequest = oneLine ? 64 : -1;
        _header.Padding = oneLine ? new Thickness(24, 0) : new Thickness(16, 12);
    }

    /// <summary>For <see cref="PerfProbe"/> (tour): shows screen <paramref name="index"/> (0 = gallery, 1 = boss fight).</summary>
    internal void ProbeShow(int index) => Show(index);

    /// <summary>Shows one screen; the hidden one asks for no frames.</summary>
    void Show(int index)
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].IsVisible = i == index;
            _tabs[i].Fill = i == index ? Theme.Accent : Theme.Quiet;
            _tabs[i].AccessibilityIsPressed = i == index;
        }
    }
}
