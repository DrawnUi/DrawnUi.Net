using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Wpf;
using DrawnUi.Controls;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>Recycled chip cell for the templated layouts: visuals once, <see cref="SetContent"/> per bind.</summary>
public class ChipCell : SkiaDynamicDrawnCell
{
    private SkiaShape _shape;
    private SkiaLabel _label;

    /// <summary>Builds the chip.</summary>
    public ChipCell()
    {
        HorizontalOptions = LayoutOptions.Fill;
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                CornerRadius = 10,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel
                    {
                        FontSize = 13,
                        TextColor = Color.Parse("#DEE2E6"),
                        Padding = new Thickness(12, 8),
                        HorizontalOptions = LayoutOptions.Center,
                    }.Assign(out _label),
                },
            }.Assign(out _shape),
        };
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);
        if (ctx is LayoutsPage.Chip item)
        {
            _label.Text = item.Text;
            _shape.BackgroundColor = Color.Parse(item.Color);
        }
    }
}

/// <summary>Text cell for the decorated grid: title + value, padded, no background — the grid lines separate the cells.</summary>
public class TextCell : SkiaDynamicDrawnCell
{
    private SkiaLabel _title;
    private SkiaLabel _value;

    /// <summary>Builds the cell.</summary>
    public TextCell()
    {
        Type = LayoutType.Column;
        Padding = new Thickness(12, 10);
        HorizontalOptions = LayoutOptions.Fill;
        Children = new List<SkiaControl>
        {
            new SkiaLabel { FontSize = 11, TextColor = Color.Parse("#8B95A1"), TextTransform = TextTransform.Uppercase }.Assign(out _title),
            new SkiaLabel { FontSize = 15, TextColor = Color.Parse("#DEE2E6"), FontFamily = "FontTextBold" }.Assign(out _value),
        };
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);
        if (ctx is LayoutsPage.Fact item)
        {
            _title.Text = item.Title;
            _value.Text = item.Value;
        }
    }
}

/// <summary>
/// Every SkiaLayout type: Absolute (SkiaLayer), Column (SkiaStack), Row (SkiaRow), Wrap (SkiaWrap),
/// Grid (SkiaGrid), ImageComposite caching, ItemsSource on Wrap / Row / Grid with Split.
/// Ported from the React demo's LayoutsPage.tsx.
/// </summary>
public class LayoutsPage : SkiaLayer
{
    /// <summary>Item of the templated chip layouts.</summary>
    public record Chip(string Text, string Color);

    /// <summary>Item of the decorated grid.</summary>
    public record Fact(string Title, string Value);

    private static readonly string[] Palette = { "#0F3460", "#533483", "#1B4332", "#7B2D26", "#495057", "#0D6EFD", "#D63384", "#2D6A4F" };

    private static readonly Fact[] Facts =
    {
        new("Engine", "Skia"), new("Language", "C#"), new("Layouts", "5 types"), new("Cache", "4 kinds"),
        new("Gestures", "Unified"), new("Shaders", "SkSL"), new("Fonts", "Any TTF"), new("Animations", "60 fps"),
        new("Navigation", "SkiaShell"), new("Accessibility", "UIA"), new("License", "MIT"), new("Docs", "drawnui.net"),
    };

    private static readonly Color Body = Color.Parse("#DEE2E6");
    private static readonly Color Muted = Color.Parse("#ADB5BD");
    private static readonly Color Well = Color.Parse("#1F2937");

    private int _count = 10;
    private int _split = 3;
    private bool _dynamic;
    private SkiaWrap _chips;
    private SkiaLabel _chipsTitle;
    private readonly List<SkiaButton> _splitButtons = new();
    private SkiaButton _dynamicButton;

    /// <summary>Builds the page.</summary>
    public LayoutsPage()
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
                    // Every child Fill, so this auto-width stack adopts the constraint (capped) and centres.
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        Heading("SkiaLayout types", 24),
                        new SkiaLabel("One class, five Type values; the aliases preset Type (+ HorizontalOptions=Fill for SkiaStack / SkiaLayer / SkiaWrap / SkiaGrid). Children position themselves with HorizontalOptions / VerticalOptions / Margin, the container adds Spacing and Padding.")
                        {
                            FontSize = 13, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Heading("Absolute · SkiaLayer", 20),
                        Card("Children overlap in one cell; alignment + Margin place them (WPF-style, no X/Y)",
                            new SkiaLayer
                            {
                                HeightRequest = 150, BackgroundColor = Well,
                                Children = new List<SkiaControl>
                                {
                                    Box("Start/Start", "#0D6EFD"),
                                    Box("Center/Start", "#6610F2", h: LayoutOptions.Center),
                                    Box("End/Start", "#D63384", h: LayoutOptions.End),
                                    Box("Start/Center", "#FD7E14", v: LayoutOptions.Center),
                                    Box("Center/Center", "#20C997", h: LayoutOptions.Center, v: LayoutOptions.Center),
                                    Box("End/Center", "#0DCAF0", h: LayoutOptions.End, v: LayoutOptions.Center),
                                    Box("Start/End", "#6EA8FE", v: LayoutOptions.End),
                                    Box("Margin(0,0,0,12)", "#FFC107", h: LayoutOptions.Center, v: LayoutOptions.End, margin: new Thickness(0, 0, 0, 12)),
                                    Box("End/End", "#DC3545", h: LayoutOptions.End, v: LayoutOptions.End),
                                },
                            }),
                        Card("Icon + text with an Absolute layer instead of a grid (cheaper): label gets the icon's width as Margin",
                            new SkiaLayer
                            {
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape { Type = ShapeType.Circle, BackgroundColor = Color.Parse("#6EA8FE"), WidthRequest = 36, LockRatio = 1 },
                                    new SkiaLabel("Margin = new Thickness(48, 0, 0, 0), VerticalOptions = Center — no second column to measure.")
                                    {
                                        FontSize = 14, TextColor = Body, Margin = new Thickness(48, 0, 0, 0), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill,
                                    },
                                },
                            }),

                        Card("IsClippedToBounds — a child larger than its parent",
                            new SkiaRow
                            {
                                Spacing = 24,
                                Children = new List<SkiaControl>
                                {
                                    ClipDemo("overflows (default)", clip: false, clipEffects: true),
                                    ClipDemo("IsClippedToBounds", clip: true, clipEffects: true),
                                    new SkiaLayer
                                    {
                                        WidthRequest = 140, HeightRequest = 70, BackgroundColor = Well, IsClippedToBounds = true, ClipEffects = false,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaShape
                                            {
                                                Type = ShapeType.Rectangle, CornerRadius = 8, BackgroundColor = Color.Parse("#20C997"),
                                                WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                                                Shadows = { new SkiaShadow { X = 0, Y = 0, Blur = 12, Opacity = 1, Color = Color.Parse("#20C997") } },
                                            },
                                            new SkiaLabel("ClipEffects = false") { FontSize = 11, TextColor = Colors.White, Padding = new Thickness(6) },
                                        },
                                    },
                                },
                            }),

                        Heading("Column · SkiaStack", 20),
                        Card("ZIndex draws later (on top); HorizontalFillRatio/VerticalFillRatio = fraction of the box; Left/Top nudge the drawn output",
                            new SkiaLayer
                            {
                                HeightRequest = 120, BackgroundColor = Color.Parse("#212529"), HorizontalOptions = LayoutOptions.Fill,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 8, BackgroundColor = Color.Parse("#0D6EFD"),
                                        HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, HorizontalFillRatio = 0.5, VerticalFillRatio = 0.75, ZIndex = 2,
                                        Children = { Centered("ZIndex=2 · FillRatio 0.5 × 0.75", 12, Colors.White) },
                                    },
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 8, BackgroundColor = Color.Parse("#D63384"),
                                        WidthRequest = 220, HeightRequest = 70, Margin = new Thickness(120, 30, 0, 0), ZIndex = 1,
                                        Children = { Centered("ZIndex=1, declared second", 12, Colors.White) },
                                    },
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 8, BackgroundColor = Color.Parse("#20C997"),
                                        WidthRequest = 160, HeightRequest = 50, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Left = -20, Top = -10, ZIndex = 3,
                                        Children = { Centered("Left=-20 Top=-10 · ZIndex=3", 11, Color.Parse("#1A1A2E")) },
                                    },
                                },
                            }),
                        Card("Vertical stack, Spacing between children, each child aligns horizontally on its own",
                            new SkiaStack
                            {
                                Spacing = 6, BackgroundColor = Well, Padding = new Thickness(8),
                                Children = new List<SkiaControl>
                                {
                                    Box("Start (default)", "#0D6EFD"),
                                    Box("HorizontalOptions=Center", "#6610F2", h: LayoutOptions.Center),
                                    Box("HorizontalOptions=End", "#D63384", h: LayoutOptions.End),
                                    Box("HorizontalOptions=Fill", "#20C997", h: LayoutOptions.Fill),
                                    Box("WidthRequest=200", "#FD7E14", w: 200),
                                },
                            }),

                        Heading("Row · SkiaRow", 20),
                        Card("Horizontal stack; the row is as tall as its tallest child, children align vertically",
                            new SkiaRow
                            {
                                Spacing = 8, BackgroundColor = Well, Padding = new Thickness(8), HorizontalOptions = LayoutOptions.Fill,
                                Children = new List<SkiaControl>
                                {
                                    Box("Start", "#0D6EFD", height: 70),
                                    Box("Center", "#6610F2", v: LayoutOptions.Center),
                                    Box("End", "#D63384", v: LayoutOptions.End),
                                    Box("Fill", "#20C997", v: LayoutOptions.Fill),
                                    Box("Margin(16,0,0,0)", "#FD7E14", v: LayoutOptions.Center, margin: new Thickness(16, 0, 0, 0)),
                                },
                            },
                            new SkiaLabel("A Row gives children an infinite width: Fill on the main axis auto-sizes (MAUI stack semantics). Use SkiaGrid with a * column when something must take the remaining width.")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Heading("Grid · SkiaGrid", 20),
                        Card("ColumnDefinitions=\"*, 2*, Auto\" RowDefinitions=\"Auto, 60\" · ColumnSpacing/RowSpacing 8",
                            new SkiaGrid
                            {
                                ColumnDefinitions = Cols("*, 2*, Auto"), RowDefinitions = Rows("Auto, 60"), ColumnSpacing = 8, RowSpacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    Cell("*", "#0D6EFD", 0, 0),
                                    Cell("2*", "#6610F2", 1, 0),
                                    Cell("Auto (this label)", "#D63384", 2, 0),
                                    Cell("Row 1 = 60pt", "#20C997", 0, 1),
                                    Cell("Column 1", "#FD7E14", 1, 1),
                                    Cell("Auto", "#DC3545", 2, 1),
                                },
                            }),
                        Card("ColumnSpan / RowSpan",
                            new SkiaGrid
                            {
                                ColumnDefinitions = Cols("*, *, *"), RowDefinitions = Rows("48, 48, 48"), ColumnSpacing = 6, RowSpacing = 6,
                                Children = new List<SkiaControl>
                                {
                                    Cell("ColumnSpan=2", "#0D6EFD", 0, 0, columnSpan: 2),
                                    Cell("RowSpan=2", "#6610F2", 2, 0, rowSpan: 2),
                                    Cell("0,1", "#20C997", 0, 1),
                                    Cell("1,1", "#FD7E14", 1, 1),
                                    Cell("ColumnSpan=3", "#D63384", 0, 2, columnSpan: 3),
                                },
                            }),
                        Card("Implicit tracks: no definitions, children reference Column/Row (DefaultColumnDefinition = Auto)",
                            new SkiaGrid
                            {
                                ColumnSpacing = 12, RowSpacing = 4, HorizontalOptions = LayoutOptions.Start,
                                Children = new List<SkiaControl>
                                {
                                    At(new SkiaLabel("Name") { FontSize = 14, TextColor = Muted }, 0, 0),
                                    At(new SkiaLabel("DrawnUI") { FontSize = 14, TextColor = Colors.White }, 1, 0),
                                    At(new SkiaLabel("Renderer") { FontSize = 14, TextColor = Muted }, 0, 1),
                                    At(new SkiaLabel("SkiaSharp (ANGLE / D3D11)") { FontSize = 14, TextColor = Colors.White }, 1, 1),
                                    At(new SkiaLabel("License") { FontSize = 14, TextColor = Muted }, 0, 2),
                                    At(new SkiaLabel("MIT") { FontSize = 14, TextColor = Colors.White }, 1, 2),
                                },
                            }),
                        Card("Icon + text pattern: Auto column for the icon, * for wrapping text",
                            new SkiaGrid
                            {
                                ColumnDefinitions = Cols("Auto, *"), ColumnSpacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    At(new SkiaShape { Type = ShapeType.Circle, BackgroundColor = Color.Parse("#6EA8FE"), WidthRequest = 40, LockRatio = 1, VerticalOptions = LayoutOptions.Start }, 0, 0),
                                    At(new SkiaLabel("The star column takes whatever the Auto column leaves, and this label wraps inside it. The row is Auto, so it grows with the text — the same layout a MAUI Grid would produce.")
                                    {
                                        FontSize = 14, TextColor = Body, HorizontalOptions = LayoutOptions.Fill,
                                    }, 1, 0),
                                },
                            }),

                        Heading("Wrap · SkiaWrap", 20),
                        Card("Type=Wrap · Spacing 8 · resize the window",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new[] { "Absolute", "Column", "Row", "Wrap", "Grid", "SkiaStack", "SkiaRow", "SkiaLayer", "SkiaWrap", "SkiaGrid", "Spacing", "Padding", "Margin" }
                                    .Select(t => (SkiaControl)new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 14, BackgroundColor = Color.Parse("#373B3E"),
                                        Children = { new SkiaLabel(t) { FontSize = 13, TextColor = Body, Padding = new Thickness(12, 6) } },
                                    }).ToList(),
                            }),

                        Heading("Caching · UseCache=ImageComposite", 20),
                        Card("SkiaLayer UseCache=\"ImageComposite\" · 24 cached shapes + 1 rotating · only the dirty child (and what it overlaps) is re-recorded each frame",
                            new SkiaLayer
                            {
                                UseCache = SkiaCacheType.ImageComposite, HeightRequest = 150, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Color.Parse("#212529"),
                                Children = Enumerable.Range(0, 24).Select(i => (SkiaControl)new SkiaShape
                                    {
                                        Type = i % 3 == 0 ? ShapeType.Circle : ShapeType.Rectangle, CornerRadius = 6, WidthRequest = 40, HeightRequest = 40,
                                        BackgroundColor = Color.Parse(Palette[i % Palette.Length]),
                                        Margin = new Thickness(12 + (i % 12) * 52, 12 + (i / 12) * 70, 0, 0), UseCache = SkiaCacheType.Operations,
                                    })
                                    .Append(new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 4, WidthRequest = 44, HeightRequest = 44, BackgroundColor = Color.Parse("#FFC107"),
                                        Margin = new Thickness(12 + 5 * 52 + 46 - 22, 12 + 40 + 15 - 22, 0, 0), UseCache = SkiaCacheType.Operations, ZIndex = 5,
                                    }.AnimateRotation(0, 360, seconds: 2.4, repeat: -1))
                                    .ToList(),
                            },
                            new SkiaLabel("The spinning child invalidates itself every frame; the composite parent re-records it plus the siblings its old and new bounds overlap, and blits the rest from its cache surface. (The React page also outlines the repainted children from LastCompositeRecord — that diagnostic is not exposed on the C# engine.)")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Heading("ItemsSource + ItemTemplate for Wrap / Row / Grid · Split", 20),
                        Card(null,
                            new SkiaLabel { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase }.Assign(out _chipsTitle),
                            new SkiaWrap
                            {
                                Spacing = 8, ItemsSource = MakeChips(_count), ItemTemplate = new DataTemplate(() => new ChipCell()), Split = _split, DynamicColumns = _dynamic,
                            }.Assign(out _chips),
                            new SkiaWrap
                            {
                                Spacing = 6,
                                Children = new[] { 0, 2, 3, 4 }
                                    .Select(v => (SkiaControl)new SkiaButton(v == 0 ? "Split 0 (flow)" : $"Split {v}") { FontSize = 12 }
                                        .Adapt(b => _splitButtons.Add(b))
                                        .OnTapped(me => SetSplit(v)))
                                    .Append(new SkiaButton("DynamicColumns off") { FontSize = 12 }.Assign(out _dynamicButton).OnTapped(me => SetDynamic(!_dynamic)))
                                    // A SkiaButton caption is markdown (SkiaRichLabel): a leading "+ " or "- " would be a
                                    // list bullet, so the markers are escaped.
                                    .Append(new SkiaButton(@"\+ item") { FontSize = 12, BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => SetCount(_count + 1)))
                                    .Append(new SkiaButton(@"\- item") { FontSize = 12, BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => SetCount(Math.Max(1, _count - 1))))
                                    .ToList(),
                            }),
                        Card("SkiaRow ItemsSource (same cells, laid out horizontally, every item realized)",
                            new SkiaRow { Spacing = 8, ItemsSource = MakeChips(5), ItemTemplate = new DataTemplate(() => new ChipCell()) }),
                        Card("SkiaDecoratedGrid ItemsSource · Split=4 · ColumnSpacing / RowSpacing 1 · gradient lines in the spacing",
                            new SkiaDecoratedGrid
                            {
                                ItemsSource = Facts, ItemTemplate = new DataTemplate(() => new TextCell()), Split = 4,
                                ColumnDefinitions = Cols("*,*,*,*"), ColumnSpacing = 1, RowSpacing = 1, BackgroundColor = Color.Parse("#212529"),
                            }),
                        Card("SkiaGrid ItemsSource · Split=3 · Invert (column-major)",
                            new SkiaGrid
                            {
                                ItemsSource = MakeChips(10), ItemTemplate = new DataTemplate(() => new ChipCell()), Split = 3, Invert = true,
                                ColumnDefinitions = Cols("*,*,*"), ColumnSpacing = 8, RowSpacing = 8,
                            }),
                    },
                },
            }.Fill(),
        };

        RefreshChipsChrome();
    }

    private static List<Chip> MakeChips(int count) =>
        Enumerable.Range(0, count).Select(i => new Chip($"Item {i + 1}", Palette[i % Palette.Length])).ToList();

    private void SetSplit(int split)
    {
        _split = split;
        _chips.Split = split;
        RefreshChipsChrome();
    }

    private void SetDynamic(bool dynamic)
    {
        _dynamic = dynamic;
        _chips.DynamicColumns = dynamic;
        RefreshChipsChrome();
    }

    private void SetCount(int count)
    {
        _count = count;
        _chips.ItemsSource = MakeChips(count);
        RefreshChipsChrome();
    }

    private void RefreshChipsChrome()
    {
        _chipsTitle.Text = $"SkiaWrap ItemsSource ({_count} recycled ChipCell) · Split={_split} · DynamicColumns={_dynamic}";
        var values = new[] { 0, 2, 3, 4 };
        for (var i = 0; i < _splitButtons.Count; i++)
            _splitButtons[i].BackgroundColor = Color.Parse(values[i] == _split ? "#533483" : "#495057");
        _dynamicButton.Text = $"DynamicColumns {(_dynamic ? "on" : "off")}";
        _dynamicButton.BackgroundColor = Color.Parse(_dynamic ? "#533483" : "#495057");
    }

    private static SkiaLabel Heading(string text, double size) => new(text)
    {
        FontSize = size,
        TextColor = Colors.White,
        HorizontalOptions = LayoutOptions.Fill,
        HorizontalTextAlignment = DrawTextAlignment.Center,
        Margin = new Thickness(0, 8, 0, 0),
    };

    /// <summary>A titled card, the React page's Card component. Null title = caller supplies its own.</summary>
    private static SkiaControl Card(string title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                Children = (title == null
                    ? Enumerable.Empty<SkiaControl>()
                    : new SkiaControl[]
                    {
                        new SkiaLabel(title) { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase },
                    })
                    .Concat(content).ToList(),
            },
        },
    };

    private static SkiaLabel Centered(string text, double size, Color color) => new(text)
    {
        FontSize = size, TextColor = color, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
    };

    /// <summary>A coloured cell with a centred caption, placed in a grid cell.</summary>
    private static SkiaControl Cell(string text, string color, int column, int row, int columnSpan = 1, int rowSpan = 1) => At(new SkiaShape
    {
        Type = ShapeType.Rectangle, CornerRadius = 6, BackgroundColor = Color.Parse(color),
        HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
        Children = { new SkiaLabel(text) { FontSize = 13, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(8, 6) } },
    }, column, row, columnSpan, rowSpan);

    /// <summary>Grid placement — the attached Grid.Column/Row storage every head shares.</summary>
    private static SkiaControl At(SkiaControl control, int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        Grid.SetColumnSpan(control, columnSpan);
        Grid.SetRowSpan(control, rowSpan);
        return control;
    }

    /// <summary>A small labelled box used by the stack/row/layer demos.</summary>
    private static SkiaControl Box(string text, string color, double w = -1, double height = -1,
        LayoutOptions? h = null, LayoutOptions? v = null, Thickness? margin = null)
    {
        var box = new SkiaShape
        {
            Type = ShapeType.Rectangle, CornerRadius = 6, BackgroundColor = Color.Parse(color), WidthRequest = w, HeightRequest = height,
            Children = { new SkiaLabel(text) { FontSize = 12, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(10, 6) } },
        };
        if (h.HasValue) box.HorizontalOptions = h.Value;
        if (v.HasValue) box.VerticalOptions = v.Value;
        if (margin.HasValue) box.Margin = margin.Value;
        return box;
    }

    private static SkiaControl ClipDemo(string caption, bool clip, bool clipEffects) => new SkiaLayer
    {
        WidthRequest = 140, HeightRequest = 70, BackgroundColor = Well, IsClippedToBounds = clip, ClipEffects = clipEffects,
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Circle, BackgroundColor = Color.Parse("#D63384"), WidthRequest = 110, LockRatio = 1,
                HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, -30, -30),
            },
            new SkiaLabel(caption) { FontSize = 11, TextColor = Colors.White, Padding = new Thickness(6) },
        },
    };

    // The WPF head's XAML converters read the same shorthand as the React props ("*, 2*, Auto").
    private static ColumnDefinitionCollection Cols(string definitions) =>
        new(DrawnGridLengthParser.ParseAll(definitions).Select(l => new ColumnDefinition(l)).ToArray());

    private static RowDefinitionCollection Rows(string definitions) =>
        new(DrawnGridLengthParser.ParseAll(definitions).Select(l => new RowDefinition(l)).ToArray());
}
