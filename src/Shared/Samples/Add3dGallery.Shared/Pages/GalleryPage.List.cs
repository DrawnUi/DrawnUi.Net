namespace Add3dGallery;

/// <summary>
/// 3D in a scrolling list: thirty product rows, each with a small 3D thumbnail; the row under the mouse turns its
/// model. A still thumbnail is shown again from its last render (no 3D pass while the list scrolls) and a row the list
/// does not draw (off screen) costs nothing. A sideways drag on a thumbnail turns it, a vertical one scrolls the list.
/// </summary>
public partial class GalleryPage
{
    const int ListRows = 30;
    const float ListHeight = 440, RowHeight = 84, ThumbSize = 68;

    /// <summary>The shop's pieces (Poly Haven, CC0): file, name, detail and base price; the same as DrawnUI for Rust's gallery3d.</summary>
    static readonly (byte[] Model, string Name, string Detail, int Price)[] Pieces =
    {
        (Theme.Sofa, "Walnut sofa", "Three-seater, carved walnut frame", 1190),
        (Theme.LoungeChair, "Lounge chair", "Leather on a bent plywood shell", 890),
        (Theme.Armchair, "Armchair", "Carved frame, linen seat", 640),
        (Theme.Ottoman, "Ottoman", "Soft leather on wooden feet", 290),
        (Theme.CoffeeTable, "Coffee table", "Stone top on an oak frame", 420),
        (Theme.DrawerCabinet, "Drawer cabinet", "Six drawers with brass pulls", 760),
    };

    /// <summary>Finishes: name, the tint over the piece's own colors (null = as it is), what it adds to the price.</summary>
    static readonly (string Name, Color Tint, int Extra)[] Finishes =
    {
        ("Natural", null, 0),
        ("Honey", Color.FromRgb(255, 232, 196), 20),
        ("Smoke", Color.FromRgb(190, 196, 210), 40),
        ("Sage", Color.FromRgb(204, 232, 210), 60),
        ("Espresso", Color.FromRgb(150, 134, 122), 80),
    };

    SkiaScroll _listScroll;
    SkiaLabel _listCount;
    readonly SkiaLayout[] _rows = new SkiaLayout[ListRows];
    int _thumbsDrawn = -1;

    SkiaControl ListCard()
    {
        return WideCard("3D in a scrolling list", "Thirty pieces of furniture, each with a live 3D thumbnail: point at a row to turn it, or drag a thumbnail sideways. Rows off screen draw nothing.", new SkiaStack
        {
            Spacing = 10,
            Children =
            {
                new SkiaRow
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    // Neither the title nor the count (a fixed box) ever changes the row's size: a new count
                    // redraws the row and does not lay out the card and the list under it again.
                    IgnoreChildrenInvalidations = true,
                    Children =
                    {
                        new Copyable("Shop") { FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                        // Changes while the list scrolls: a fixed box, so a new count never lays the page out again.
                        new SkiaLabel
                            {
                                FontSize = 13, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
                                WidthRequest = 320, HeightRequest = 20, HorizontalTextAlignment = DrawTextAlignment.End, IsParentIndependent = true,
                            }
                            .Assign(out _listCount),
                    }
                }
                // Narrow, the count goes under the title.
                .Adapt(header => _adapt.Add(width => header.Type = CardInner(width) >= RowBesideMin ? LayoutType.Row : LayoutType.Wrap)),
                new SkiaShape
                {
                    CornerRadius = 12,
                    UseCache = SkiaCacheType.None,
                    BackgroundColor = Theme.Page,
                    BevelType = BevelType.Emboss,
                    Bevel = Theme.CardBevel(),
                    HeightRequest = ListHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaScroll
                        {
                            AutoCache = false,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            // Where the list is and how much is left, shown at rest too (the list scrolls); the
                            // thumb can be dragged.
                            ScrollBar = new SkiaScrollBar { IsDraggable = true, Thickness = 6 },
                            KeepScrollBarsVisible = true,
                            ScrollBarThumbColor = Theme.Muted,
                            ScrollBarTrackColor = Color.FromRgba(255, 255, 255, 18),
                            Content = new SkiaStack
                            {
                                Spacing = 2,
                                // room for the scroll bar at the right edge
                                Padding = new Thickness(0, 0, 10, 0),
                                AccessibilityRole = Aria.RoleList,
                                AccessibilityLabel = "Products",
                                Children = Enumerable.Range(0, ListRows).Select(ProductRow).ToList(),
                            }
                        }.Assign(out _listScroll),
                    }
                },
            }
        });
    }

    /// <summary>One row: the 3D thumbnail, name and detail, price, the cart button.</summary>
    SkiaControl ProductRow(int index)
    {
        var piece = Pieces[index % Pieces.Length];
        var finish = Finishes[index / Pieces.Length % Finishes.Length];
        var large = index >= 15;
        var name = $"{piece.Name}, {finish.Name}";
        var price = piece.Price + finish.Extra + (large ? 120 : 0);
        var model = new SkiaModel3D(piece.Model)
        {
            Yaw = 32,
            Pitch = 14,
            LockPitch = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        // Each piece is one material (the sofa: its upholstery first): the finish tints it.
        if (finish.Tint != null && model.MaterialNames.Count > 0)
            model.SetMaterialColor(model.MaterialNames[0], finish.Tint);
        var added = false;
        return new SkiaRow
            {
                MinimumHeightRequest = RowHeight,
                HorizontalOptions = LayoutOptions.Fill,
                Spacing = 16,
                Padding = new Thickness(12, 8),
                // A still row (its thumbnail shows its last render) is one GPU blit, also while the list scrolls; the
                // row under the mouse is drawn again while its model turns.
                UseCache = SkiaCacheType.GPU,
                // named by its texts (name, detail, price), read in order
                AccessibilityRole = Aria.RoleListItem,
                Children =
                {
                    new SkiaShape
                    {
                        CornerRadius = 10,
                        BackgroundColor = Theme.Stage,
                        WidthRequest = ThumbSize,
                        HeightRequest = ThumbSize,
                        VerticalOptions = LayoutOptions.Center,
                        Children = { model },
                    },
                    new SkiaStack
                    {
                        Spacing = 2,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Copyable(name) { FontSize = 15, TextColor = Theme.Ink },
                            new Copyable(large ? $"{piece.Detail} · large" : piece.Detail) { FontSize = 12, TextColor = Theme.Muted },
                            new Copyable(Dollars(price)) { FontSize = 14, TextColor = Theme.Gold },
                        }
                    },
                    new Button3D("+ Add", Theme.Quiet, 120)
                        {
                            AccessibilityLabel = $"Add {name} to the cart", HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
                        }
                        .OnTapped(me =>
                        {
                            added = !added;
                            me.Text = added ? "In the cart" : "+ Add";
                            me.AccessibilityLabel = added ? $"Remove {name} from the cart" : $"Add {name} to the cart";
                            me.Fill = added ? Theme.Accent : Theme.Quiet;
                        }),
                }
            }
            .Assign(out _rows[index])
            // The row under the mouse turns its piece; the others stay still and are only blitted.
            .OnHovered((_, on) => model.AutoRotate = on ? 40 : 0)
            // Narrow, the button goes under the text.
            .Adapt(row => _adapt.Add(width => row.Type = CardInner(width) >= RowBesideMin ? LayoutType.Row : LayoutType.Wrap));
    }

    /// <summary>Each frame: how many thumbnails are on screen now, shown above the list (the rest cost nothing).</summary>
    void ListFrame()
    {
        if (!_listScroll.WasInLastFrame())
            return;
        // Rows the list showed this frame (a still row is blitted from its cache, its thumbnail not drawn again but on
        // screen all the same), inside the list's viewport and the page's window (a row past either edge is not seen).
        var window = _scroll.GetAccessibilityPixelRect();
        var list = _listScroll.GetAccessibilityPixelRect();
        var drawn = 0;
        foreach (var row in _rows)
        {
            if (!row.WasInLastFrame())
                continue;
            var r = row.GetAccessibilityPixelRect();
            if (r.IntersectsWith(window) && r.IntersectsWith(list))
                drawn++;
        }
        if (drawn == _thumbsDrawn)
            return;
        _thumbsDrawn = drawn;
        _listCount.Text = $"3D thumbnails drawn now: {drawn} of {ListRows}";
    }
}
