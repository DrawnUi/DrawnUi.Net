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
    public static readonly Color Accent = Color.FromRgb(124, 108, 242);
    public static readonly Color Quiet = Color.FromRgb(52, 56, 78);

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
    public static readonly byte[] Chair = Model("chair.glb");

    /// <summary>The product viewer's chair at its address on poly.pizza (CC0), loaded over the network.</summary>
    public const string ChairUrl = "https://static.poly.pizza/ac2301f3-af86-435a-885a-d8f1a32f9f76.glb";

    /// <summary>NASA's Blue Marble (public domain), an equirectangular map for the globe.</summary>
    public static readonly byte[] Earth = Model("earth.jpg");

    public static readonly Color Good = Color.FromRgb(76, 201, 140);
    public static readonly Color Bad = Color.FromRgb(255, 107, 107);
    /// <summary>A button's color once its action is done (in the cart).</summary>
    public static readonly Color Added = Color.FromRgb(38, 122, 88);
    public static readonly Color Field = Color.FromRgb(31, 35, 52);

    /// <summary>A color moved toward white by <paramref name="amount"/> (0..1): the hover lift.</summary>
    public static Color Lighter(Color c, float amount) =>
        Color.FromRgba(c.Red + (1 - c.Red) * amount, c.Green + (1 - c.Green) * amount, c.Blue + (1 - c.Blue) * amount, c.Alpha);

    /// <summary>A section title with the line under it.</summary>
    public static SkiaControl Section(string title, string caption) => new SkiaStack
    {
        Spacing = 4,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 12, 0, 0),
        Children = new List<SkiaControl>
        {
            new SkiaLabel(title)
            {
                FontSize = 22, FontFamily = Title, TextColor = Ink, HorizontalOptions = LayoutOptions.Center,
                AccessibilityRole = Aria.RoleHeading,
            },
            new SkiaLabel(caption) { FontSize = 14, TextColor = Muted, HorizontalOptions = LayoutOptions.Center },
        }
    };
}

/// <summary>
/// The app's button: a rounded pill with a caption, lighter while the mouse is over it, a button for the keyboard
/// and screen readers. <see cref="Fill"/> is its state color (selected, active); the hover lift goes on top of it.
/// </summary>
public class UiButton : SkiaShape
{
    readonly SkiaLabel _caption;
    Color _fill;
    bool _hovered;

    public UiButton(string text, Color fill, double width = 150)
    {
        _fill = fill;
        CornerRadius = 10;
        WidthRequest = width;
        HeightRequest = 40;
        BackgroundColor = fill;
        AccessibilityRole = Aria.RoleButton;
        AccessibilityLabel = text;
        Children = new List<SkiaControl>
        {
            new SkiaLabel(text)
                {
                    FontSize = 14, FontFamily = Theme.Title, TextColor = Theme.Ink,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                }
                .Assign(out _caption),
        };
        this.OnHovered((me, on) =>
        {
            _hovered = on;
            Apply();
        });
    }

    /// <summary>The caption, also its name for screen readers.</summary>
    public string Text
    {
        get => _caption.Text;
        set
        {
            _caption.Text = value;
            AccessibilityLabel = value;
        }
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

    void Apply() => BackgroundColor = _hovered ? Theme.Lighter(_fill, 0.14f) : _fill;
}
