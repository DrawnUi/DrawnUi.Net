using System.Reflection;

namespace Add3dGallery;

/// <summary>The app's palette, type and the models it ships.</summary>
public static class Theme
{
    public static readonly Color Page = Color.FromRgb(14, 16, 24);
    public static readonly Color Header = Color.FromRgb(19, 21, 32);
    public static readonly Color Card = Color.FromRgb(24, 27, 40);
    public static readonly Color Line = Color.FromRgb(40, 44, 62);
    /// <summary>The panel a 3D piece stands on inside a card.</summary>
    public static readonly Color Stage = Color.FromRgb(42, 47, 70);
    public static readonly Color Ink = Color.FromRgb(242, 241, 248);
    public static readonly Color Muted = Color.FromRgb(154, 152, 176);
    public static readonly Color Accent = Color.FromRgb(242, 179, 61);
    public static readonly Color Quiet = Color.FromRgb(70, 76, 106);
    /// <summary>Prices.</summary>
    public static readonly Color Gold = Color.FromRgb(255, 196, 61);

    public const string Title = "FontTextTitle";

    static byte[] Model(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"no embedded asset {name}");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    // Models: CC0, see Assets/MODELS-LICENSE.txt. One array each, so every control showing one shares a single read.
    public static readonly byte[] Dragon = Model("dragon.glb");
    public static readonly byte[] Avatar = Model("avatar.glb");
    public static readonly byte[] Chest = Model("gift.glb");
    public static readonly byte[] Mob = Model("mob.glb");
    /// <summary>The shop's other pieces (Poly Haven, CC0).</summary>
    public static readonly byte[] LoungeChair = Model("lounge_chair.glb");
    public static readonly byte[] Armchair = Model("armchair.glb");
    public static readonly byte[] Ottoman = Model("ottoman.glb");
    public static readonly byte[] CoffeeTable = Model("coffee_table.glb");
    public static readonly byte[] DrawerCabinet = Model("drawer_cabinet.glb");

    /// <summary>The product viewer's sofa (Poly Haven Sofa_01, CC0) and its upholstery images (Poly Haven, CC0).</summary>
    public static readonly byte[] Sofa = Model("sofa.glb");
    public static readonly byte[] Linen = Model("linen.jpg");
    public static readonly byte[] Wool = Model("wool.jpg");
    public static readonly byte[] Velvet = Model("velvet.jpg");
    public static readonly byte[] Leather = Model("leather.jpg");

    /// <summary>NASA's Blue Marble (public domain), an equirectangular map for the globe.</summary>
    public static readonly byte[] Earth = Model("earth.jpg");

    public static readonly Color Bad = Color.FromRgb(255, 99, 132);

    /// <summary>A color moved toward white by <paramref name="amount"/> (0..1): the hover lift.</summary>
    public static Color Lighter(Color c, float amount) =>
        Color.FromRgba(c.Red + (1 - c.Red) * amount, c.Green + (1 - c.Green) * amount, c.Blue + (1 - c.Blue) * amount, c.Alpha);

    /// <summary>The edge light of cards (raised) and of the panels sunk into them.</summary>
    public static SkiaBevel CardBevel() => new() { Depth = 1.5, LightColor = Colors.White, ShadowColor = Colors.Black, Opacity = 0.22 };

    public static Color Darker(Color c, float amount) =>
        Color.FromRgba(c.Red * (1 - amount), c.Green * (1 - amount), c.Blue * (1 - amount), c.Alpha);

    // Where the gallery links to (this is the .NET build; the Rust and React galleries are the same demo).
    public const string ArticleUrl = "https://drawnui.net/articles/add3d.html";
    public const string DocsUrl = "https://drawnui.net/";
    public const string RepoUrl = "https://github.com/DrawnUi/DrawnUi.Net";
    public const string RustDemoUrl = "https://add3d.drawnui.net/";
    public const string ReactDemoUrl = "https://add3dreact.drawnui.net/";

    /// <summary>
    /// A text link: the accent color, brighter under the mouse, a link for the keyboard and screen readers; opens the
    /// address in the browser.
    /// </summary>
    public static SkiaLabel Link(string text, string url) => new Copyable(text)
        {
            Url = url,
            FontSize = 14,
            FontFamily = Title,
            TextColor = LinkColor,
            AccessibilityRole = Aria.RoleLink,
            AccessibilityLabel = text,
            AccessibilityHint = url,
        }
        .OnHovered((me, on) => me.TextColor = on ? Ink : LinkColor)
        // the main button opens it; a right click is for its menu (Copy Link)
        .OnTapped((me, e) =>
        {
            if (e.Parameters?.Event?.Pointer is not { Button: not AppoMobi.Gestures.MouseButton.Left })
                Super.OpenLink(url);
        });

    public static readonly Color LinkColor = Color.FromRgb(255, 208, 110);

    /// <summary>A section title with the line under it.</summary>
    public static SkiaControl Section(string title, string caption) => new SkiaStack
    {
        Spacing = 4,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 12, 0, 0),
        Children = new List<SkiaControl>
        {
            new Copyable(title)
            {
                FontSize = 22, FontFamily = Title, TextColor = Ink, HorizontalOptions = LayoutOptions.Center,
                AccessibilityRole = Aria.RoleHeading,
            },
            new Copyable(caption) { FontSize = 14, TextColor = Muted, HorizontalOptions = LayoutOptions.Center },
        }
    };
}

/// <summary>
/// The app's button, raised in 3D: a face lit from above (a light gradient and a bevel on its edges) standing on its
/// darker side, with a soft shadow under it. Pressed, the face sinks onto its side; under the mouse it gets lighter.
/// A button for the keyboard and screen readers. <see cref="Fill"/> is its state color (selected, active).
/// </summary>
public class Button3D : SkiaButton
{
    /// <summary>How far the face stands above the page, points: the side seen under it.</summary>
    const float Depth = 4;
    const float FaceHeight = 40;

    readonly double _width;
    Color _fill;
    bool _hovered;
    SkiaShape _face;
    SkiaShadow _side;
    SkiaLabel _shade, _edge;

    SkiaLabel Caption(Color color, float offsetY) => new(Text)
    {
        FontFamily = Theme.Title,
        FontSize = 14,
        TextColor = color,
        TranslationY = offsetY,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        // the button is the accessible node, read once: its captions are not
        AccessibilityRole = Aria.RolePresentation,
    };

    /// <summary>A light face (the gold accent) takes dark ink, a dark face light ink.</summary>
    bool LightFace => 0.299 * _fill.Red + 0.587 * _fill.Green + 0.114 * _fill.Blue > 0.55;

    Color Ink => LightFace ? Color.FromRgb(40, 28, 6) : Theme.Ink.WithAlpha(0.92f);

    public override void ApplyProperties()
    {
        base.ApplyProperties();
        if (MainLabel != null)
            MainLabel.TextColor = Ink;
        if (_shade != null)
            _shade.Text = _edge.Text = MainLabel?.Text ?? Text;
    }

    /// <param name="width">Points; -1 = as wide as its caption plus padding (a chip).</param>
    public Button3D(string text, Color fill, double width = 150)
    {
        _fill = fill;
        _width = width;
        Text = text;
        TextColor = Theme.Ink;
        FontFamily = Theme.Title;
        FontSize = 14;
        WidthRequest = width;
        HeightRequest = FaceHeight + Depth;
        // Still between its state changes: one bitmap blit inside cards that redraw every frame around a moving 3D
        // view, not its shapes and caption replayed each frame.
        UseCache = SkiaCacheType.Image;
        AccessibilityRole = Aria.RoleButton;
        this.OnHovered((me, on) =>
        {
            _hovered = on;
            Apply();
        });
    }

    /// <summary>The state color; hover lightens it.</summary>
    public Color Fill
    {
        get => _fill;
        set
        {
            _fill = value;
            Apply();
        }
    }

    protected override void CreateDefaultStyleContent()
    {
        new SkiaShape
            {
                CornerRadius = 10,
                HeightRequest = FaceHeight,
                HorizontalOptions = _width < 0 ? LayoutOptions.Start : LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start,
                Padding = _width < 0 ? new Thickness(18, 0) : new Thickness(0),
                BevelType = BevelType.Bevel,
                Bevel = new SkiaBevel { Depth = 1.2, LightColor = Colors.White, ShadowColor = Colors.Black, Opacity = 0.5 },
                Shadows = new List<SkiaShadow>
                {
                    // the side: the face's own outline, darker, right under it
                    new SkiaShadow { Blur = 0, Y = Depth, Opacity = 1 }.Assign(out _side),
                    new SkiaShadow { Blur = 8, Y = Depth + 3, Opacity = 0.35, Color = Colors.Black },
                },
                // the caption engraved in the face: its shadow 1 pt above, its light edge 1 pt below
                Children =
                {
                    Caption(Colors.Black.WithAlpha(0.55f), -1).Assign(out _shade),
                    Caption(Colors.White.WithAlpha(0.16f), 1).Assign(out _edge),
                    Caption(Theme.Ink.WithAlpha(0.92f), 0).Assign(out var label),
                },
            }
            .Assign(out _face)
            .AssignParent(this);
        MainLabel = label;
        Apply();
    }

    protected override void OnButtonPropertyChanged(SkiaButton control, string propertyName)
    {
        base.OnButtonPropertyChanged(control, propertyName);
        if (propertyName == nameof(IsPressed) && _face != null)
        {
            // pressed, the face sits on its side
            _face.TranslationY = IsPressed ? Depth : 0;
            _side.Y = IsPressed ? 0 : Depth;
            Update();
        }
    }

    void Apply()
    {
        if (_face == null)
            return;
        var face = _hovered ? Theme.Lighter(_fill, 0.14f) : _fill;
        _face.BackgroundColor = face;
        _face.FillGradient = new SkiaGradient
        {
            StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
            Colors = new List<Color> { Theme.Lighter(face, 0.2f), face, Theme.Darker(face, 0.1f) },
            ColorPositions = new List<double> { 0, 0.55, 1 },
        };
        // the foot: the face's own color, darker; Quiet is light enough for its foot to stand off the card
        _side.Color = Theme.Darker(_fill, 0.45f);
        // engraved: a dark face takes its light edge below, a light face its shadow above
        _shade.TextColor = Colors.Black.WithAlpha(LightFace ? 0.18f : 0.55f);
        _edge.TextColor = Colors.White.WithAlpha(LightFace ? 0.45f : 0.16f);
        MainLabel.TextColor = Ink;
        Update();
    }
}

/// <summary>
/// The app's switch, in 3D like <see cref="Button3D"/>, the classic Android way: a thin track sunk into the card and a
/// bigger raised knob, lit from above, riding on it. On, the knob turns gold and the track takes a deep gold.
/// </summary>
public class Switch3D : SkiaSwitch
{
    const float Knob = 28;

    public Switch3D()
    {
        ColorFrameOn = Theme.Darker(Theme.Accent, 0.45f);
        ColorFrameOff = Theme.Darker(Theme.Quiet, 0.35f);
        AccessibilityRole = Aria.RoleSwitch;
        // under the mouse the knob and the track light up, as the buttons do
        this.OnHovered((me, on) =>
        {
            _hovered = on;
            Colorize();
        });
    }

    bool _hovered;

    Color Lift(Color c) => _hovered ? Theme.Lighter(c, 0.14f) : c;

    static SkiaGradient Lit(Color c) => new()
    {
        StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
        Colors = new List<Color> { Theme.Lighter(c, 0.35f), c, Theme.Darker(c, 0.15f) },
        ColorPositions = new List<double> { 0, 0.5, 1 },
    };

    protected override void CreateDefaultStyleContent()
    {
        SetDefaultContentSize(54, Knob + 4);
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Tag = "Frame",
                CornerRadius = 7,
                HeightRequest = 14,
                // the knob reaches past both ends of the track
                Margin = new Thickness(6, 0),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                BevelType = BevelType.Emboss,
                Bevel = new SkiaBevel { Depth = 1.5, LightColor = Colors.White, ShadowColor = Colors.Black, Opacity = 0.45 },
            },
            new SkiaShape
            {
                Tag = "Thumb",
                Type = ShapeType.Circle,
                WidthRequest = Knob,
                HeightRequest = Knob,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                // its rim catches the light on top and falls into shade below
                StrokeWidth = 1.5,
                StrokeColor = Colors.White,
                StrokeGradient = new SkiaGradient
                {
                    StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
                    Colors = new List<Color> { Colors.White.WithAlpha(0.85f), Colors.White.WithAlpha(0.15f), Colors.Black.WithAlpha(0.35f) },
                    ColorPositions = new List<double> { 0, 0.5, 1 },
                },
                Shadows = new List<SkiaShadow> { new() { Blur = 5, Y = 3, Opacity = 0.55, Color = Colors.Black } },
            },
        };
        new SkiaHotspot()
            .Adapt(me => me.Tapped += (s, e) => IsToggled = !IsToggled)
            .AssignParent(this);
    }

    public override void ApplyOn()
    {
        base.ApplyOn();
        Colorize();
    }

    public override void ApplyOff()
    {
        base.ApplyOff();
        Colorize();
    }

    /// <summary>The knob's and the track's colors for the state and the hover; the knob stays where it is.</summary>
    void Colorize()
    {
        if (Thumb == null)
            return;
        Thumb.FillGradient = Lit(Lift(IsToggled ? Theme.Accent : Color.FromRgb(200, 202, 214)));
        Track.BackgroundColor = Lift(IsToggled ? ColorFrameOn : ColorFrameOff);
    }
}

/// <summary>
/// The gallery's text that can be selected and copied (titles, descriptions): mouse drag or double click selects,
/// Ctrl+C copies; a right click on selected text opens a small menu, Copy or Cancel.
/// </summary>
public class Copyable : SkiaLabel
{
    static PopupId _menu;

    public Copyable() : this(null)
    {
    }

    public Copyable(string text) : base(text)
    {
        AccessibilityTextSelectable = true;
        this.OnContextMenu((me, e) =>
        {
            // nothing selected and not a link: the right click is not for us
            var selected = !string.IsNullOrEmpty(me.SelectedText);
            if ((!selected && string.IsNullOrEmpty(me.Url)) || me.Superview == null)
                return false;
            me.Superview.ClosePopupWindow(_menu);
            _menu = me.Superview.OpenPopupWindow(Menu(me, selected), e.Location);
            return true;
        });
    }

    /// <summary>A link's address: its menu offers Copy Link.</summary>
    public string Url { get; set; }

    static SkiaControl Menu(Copyable label, bool selected)
    {
        var items = new List<SkiaControl>();
        if (selected)
            // a drag may have selected several texts: Copy takes all of them, joined by new lines
            items.Add(Item("Copy", () =>
            {
                if (label.Superview is { } page && !string.IsNullOrEmpty(page.SelectedText))
                    page.CopyTextSelection();
                else
                    label.CopySelection();
            }));
        if (!string.IsNullOrEmpty(label.Url))
            items.Add(Item("Copy Link", () => Super.SetClipboardText?.Invoke(label.Url)));
        items.Add(Item("Cancel", () =>
        {
            label.Superview?.ClearTextSelection();
            label.ClearSelection();
        }));
        return Panel(items);
    }

    static SkiaControl Panel(List<SkiaControl> items) => new SkiaShape
    {
        CornerRadius = 10,
        BackgroundColor = Theme.Card,
        StrokeColor = Theme.Line,
        StrokeWidth = 1,
        WidthRequest = 150,
        Padding = new Thickness(4),
        Shadows = new List<SkiaShadow> { new() { Blur = 12, Y = 4, Opacity = 0.5, Color = Colors.Black } },
        Children =
        {
            new SkiaStack
            {
                Spacing = 0,
                AccessibilityRole = Aria.RoleMenu,
                AccessibilityLabel = "Text",
                Children = items,
            }
        }
    };

    static SkiaShape Item(string text, Action act) => new SkiaShape
        {
            CornerRadius = 7,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(12, 8),
            AccessibilityRole = Aria.RoleMenuItem,
            AccessibilityCanInteract = true,
            AccessibilityLabel = text,
            Children = { new SkiaLabel(text) { FontSize = 14, TextColor = Theme.Ink, AccessibilityRole = Aria.RolePresentation } },
        }
        .OnHovered((me, on) => me.BackgroundColor = on ? Theme.Quiet : Colors.Transparent)
        .OnTapped(me =>
        {
            act();
            me.Superview?.ClosePopupWindow(_menu);
        });
}
