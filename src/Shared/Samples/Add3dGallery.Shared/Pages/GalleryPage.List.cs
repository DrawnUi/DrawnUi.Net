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

    /// <summary>A product of the list: its name, its variants (name and color) and its base price.</summary>
    record Product(string Name, string Detail, (string Name, Color Color)[] Variants, int Price, Func<SkiaModel3D> Make, Action<SkiaModel3D, int, Color> Paint);

    SkiaScroll _listScroll;
    SkiaLabel _listCount;
    readonly SkiaModel3D[] _thumbs = new SkiaModel3D[ListRows];
    int _thumbsDrawn = -1;

    Product[] Products()
    {
        _labels ??= Labels();
        static SkiaModel3D Thumb(SkiaModel3D model, float yaw, float pitch)
        {
            model.Yaw = yaw;
            model.Pitch = pitch;
            model.LockPitch = true;
            model.HorizontalOptions = LayoutOptions.Fill;
            model.VerticalOptions = LayoutOptions.Fill;
            return model;
        }
        // One model each, shared by every row that shows it (read once).
        Model3D box = ProductBox(), medal = Medal(), heart = HeartModel();
        return new[]
        {
            new Product("Office chair", "Mesh back, five-star base",
                new[] { ("Navy", (Color)null), ("Graphite", Fabrics[1].Color), ("Coral", Fabrics[2].Color) }, 249,
                () => Thumb(new SkiaModel3D(Theme.Chair), 30, 10), (m, v, c) => m.SetMaterialColor("Chair", v == 0 ? null : Fabrics[v].Color)),
            new Product("Snack box", "Twelve bars, recyclable box",
                new[] { ("Berry", (Color)null), ("Mint", (Color)null), ("Sun", (Color)null) }, 12,
                () => Thumb(new SkiaModel3D(box), 28, 16), (m, v, c) => m.SetMaterialImage("box", _labels[v])),
            new Product("Medal", "Solid metal, engraved on the back",
                new[] { ("Gold", (Color)null), ("Silver", Color.FromRgb(200, 205, 214)), ("Bronze", Color.FromRgb(205, 127, 50)) }, 39,
                () => Thumb(new SkiaModel3D(medal), 0, 8), (m, v, c) =>
                {
                    m.SetMaterialColor("disc", v == 0 ? null : c);
                    m.SetMaterialColor("inner", v == 0 ? null : Theme.Lighter(c, 0.3f));
                }),
            new Product("Heart cushion", "Soft velvet, 40 cm",
                new[] { ("Rose", (Color)null), ("Peach", Color.FromRgb(255, 160, 122)), ("Violet", Color.FromRgb(155, 110, 245)) }, 29,
                () => Thumb(new SkiaModel3D(heart), -20, 10), (m, v, c) => m.SetMaterialColor("heart", v == 0 ? null : c)),
            new Product("Treasure chest", "Storage box with a lid",
                new[] { ("Oak", (Color)null), ("Walnut", Color.FromRgb(110, 70, 45)), ("Ebony", Color.FromRgb(60, 50, 50)) }, 59,
                () => Thumb(new SkiaModel3D(Theme.Chest) { Animation = "Chest_Close", IsPlaying = false }, 30, 18),
                (m, v, c) => m.SetMaterialColor("Wood", v == 0 ? null : c)),
        };
    }

    SkiaControl ListCard()
    {
        var products = Products();
        return WideCard(new SkiaStack
        {
            Spacing = 10,
            Children =
            {
                new SkiaRow
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaLabel("Shop") { FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                        // Changes while the list scrolls: a fixed box, so a new count never lays the page out again.
                        new SkiaLabel
                            {
                                FontSize = 13, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
                                WidthRequest = 320, HeightRequest = 20, HorizontalTextAlignment = DrawTextAlignment.End, IsParentIndependent = true,
                            }
                            .Assign(out _listCount),
                    }
                },
                new SkiaShape
                {
                    CornerRadius = 12,
                    UseCache = SkiaCacheType.None,
                    BackgroundColor = Theme.Page,
                    HeightRequest = ListHeight,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaScroll
                        {
                            AutoCache = false,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            Content = new SkiaStack
                            {
                                Spacing = 0,
                                AccessibilityRole = Aria.RoleList,
                                AccessibilityLabel = "Products",
                                Children = Enumerable.Range(0, ListRows).Select(i => ProductRow(i, products)).ToList(),
                            }
                        }.Assign(out _listScroll),
                    }
                },
            }
        });
    }

    /// <summary>One row: the 3D thumbnail, name and detail, price, the cart button.</summary>
    SkiaControl ProductRow(int index, Product[] products)
    {
        var product = products[index % products.Length];
        var variant = index / products.Length % product.Variants.Length;
        var large = index >= products.Length * product.Variants.Length;
        var name = $"{product.Name}, {product.Variants[variant].Name}";
        var price = product.Price + variant * 4 + (large ? product.Price / 2 : 0);
        var model = product.Make();
        product.Paint(model, variant, product.Variants[variant].Color);
        // Each row starts at its own turn, so the list does not spin in step.
        model.Yaw += index * 37 % 360;
        _thumbs[index] = model;
        var added = false;
        return new SkiaShape
            {
                HeightRequest = RowHeight,
                HorizontalOptions = LayoutOptions.Fill,
                // A still row (its thumbnail shows its last render) is one GPU blit, also while the list scrolls; the
                // row under the mouse is drawn again while its model turns.
                UseCache = SkiaCacheType.GPU,
                Padding = new Thickness(12, 8, 12, 0),
                AccessibilityRole = Aria.RoleListItem,
                AccessibilityLabel = $"{name}, ${price}",
                Children =
                {
                    new SkiaRow
                    {
                        Spacing = 16,
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            Stage(model).WithWidth(ThumbSize).WithHeight(ThumbSize).WithVerticalOptions(LayoutOptions.Center),
                            new SkiaRow
                            {
                                Spacing = 16,
                                VerticalOptions = LayoutOptions.Center,
                                Children =
                                {
                                    new SkiaStack
                                    {
                                        Spacing = 2,
                                        WidthRequest = 520,
                                        VerticalOptions = LayoutOptions.Center,
                                        Children =
                                        {
                                            new SkiaLabel(name) { FontSize = 16, FontFamily = Theme.Title, TextColor = Theme.Ink },
                                            new SkiaLabel(large ? $"{product.Detail} · large" : product.Detail) { FontSize = 13, TextColor = Theme.Muted },
                                        }
                                    },
                                    new SkiaLabel($"${price}")
                                    {
                                        FontSize = 17, FontFamily = Theme.Title, TextColor = Theme.Ink, VerticalOptions = LayoutOptions.Center,
                                        WidthRequest = 80, HorizontalTextAlignment = DrawTextAlignment.End,
                                    },
                                    new UiButton("Add", Theme.Accent, 120) { AccessibilityLabel = $"Add {name} to the cart", VerticalOptions = LayoutOptions.Center }
                                        .OnTapped(me =>
                                        {
                                            added = !added;
                                            me.Text = added ? "In the cart" : "Add";
                                            me.AccessibilityLabel = added ? $"Remove {name} from the cart" : $"Add {name} to the cart";
                                            me.Fill = added ? Theme.Added : Theme.Accent;
                                        }),
                                }
                            },
                        }
                    },
                    // A hairline between rows.
                    new SkiaShape { HeightRequest = 1, BackgroundColor = Theme.Line, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.End },
                }
            }
            // The row under the mouse turns its thumbnail; the others stay still and are only blitted.
            .OnHovered((me, on) =>
            {
                me.BackgroundColor = on ? Theme.Lighter(Theme.Page, 0.04f) : Colors.Transparent;
                model.AutoRotate = on ? 40 : 0;
            });
    }

    /// <summary>Each frame: how many thumbnails the list drew, shown above it (the rest cost nothing).</summary>
    void ListFrame()
    {
        if (!_listScroll.WasInLastFrame())
            return;
        var drawn = 0;
        foreach (var thumb in _thumbs)
            if (thumb.WasInLastFrame())
                drawn++;
        if (drawn == _thumbsDrawn)
            return;
        _thumbsDrawn = drawn;
        _listCount.Text = $"3D thumbnails drawn now: {drawn} of {ListRows}";
    }
}
