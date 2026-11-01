using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// Tooltips and menus: tooltips on any control (TooltipsFromAccessibility for icon buttons), a context menu and a
/// dropdown opened with DrawnView.OpenPopupWindow, the popup window API of DrawnUI for Rust.
/// </summary>
public class TooltipsMenusPage : SkiaLayer
{
    private static readonly Color CardColor = Color.Parse("#2B3035");
    private static readonly Color ButtonColor = Color.Parse("#373B3E");
    private static readonly Color ButtonHover = Color.Parse("#495057");
    private static readonly Color Muted = Color.Parse("#ADB5BD");
    private static readonly Color Accent = Color.Parse("#6EA8FE");

    private const string IconNew = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zm4 18H6V4h7v5h5zM11 11h2v3h3v2h-3v3h-2v-3H8v-2h3z'/></svg>";
    private const string IconOpen = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M20 6h-8l-2-2H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2m0 12H4V8h16z'/></svg>";
    private const string IconSave = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7zm2 16H5V5h11.17L19 7.83zM12 12a3 3 0 1 0 0 6 3 3 0 0 0 0-6M6 6h9v4H6z'/></svg>";
    private const string IconSettings = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M19.4 13a7.5 7.5 0 0 0 0-2l2.1-1.6-2-3.5-2.5 1a7.6 7.6 0 0 0-1.7-1L15 3h-4l-.4 2.9a7.6 7.6 0 0 0-1.7 1l-2.5-1-2 3.5L6.6 11a7.5 7.5 0 0 0 0 2l-2.1 1.6 2 3.5 2.5-1a7.6 7.6 0 0 0 1.7 1L11 21h4l.4-2.9a7.6 7.6 0 0 0 1.7-1l2.5 1 2-3.5zM13 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 0 1 0 7'/></svg>";
    private const string IconDelete = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M6 19a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V7H6zm2-10h8v10H8zm7.5-5-1-1h-5l-1 1H5v2h14V4z'/></svg>";
    private const string IconInfo = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M11 7h2v2h-2zm0 4h2v6h-2zm1-9a10 10 0 1 0 0 20 10 10 0 0 0 0-20m0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16'/></svg>";

    private SkiaLabel _picked;
    private PopupId _menu;

    /// <summary>Builds the page.</summary>
    public TooltipsMenusPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Super.TooltipsFromAccessibility = true;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Tooltips & Menus") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center, AccessibilityRole = Aria.RoleHeading },
                        new SkiaLabel("Rest the mouse on a control. Move along a row: the next tooltip shows at once. Click, scroll, use the wheel or press a key: it closes. Right-click the last card for a context menu.")
                        {
                            FontSize = 14, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Card("Icon toolbar", "No Tooltip set: the text is each button's AccessibilityLabel (TooltipsFromAccessibility, switch below).",
                            new SkiaRow
                            {
                                Spacing = 6,
                                AccessibilityRole = Aria.RoleToolbar,
                                AccessibilityLabel = "File tools",
                                Children = new List<SkiaControl>
                                {
                                    IconButton(IconNew, "New file"),
                                    IconButton(IconOpen, "Open"),
                                    IconButton(IconSave, "Save"),
                                    IconButton(IconSettings, "Settings"),
                                    IconButton(IconDelete, "Delete"),
                                },
                            },
                            new SkiaRow
                            {
                                Spacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaSwitch
                                    {
                                        IsToggled = true,
                                        AccessibilityLabel = "Tooltips from accessibility text",
                                        VerticalOptions = LayoutOptions.Center,
                                        ColorFrameOn = Color.Parse("#0D6EFD"),
                                        ColorFrameOff = ButtonHover,
                                        ColorThumbOn = Colors.White,
                                        ColorThumbOff = Colors.White,
                                    }
                                    .ObserveSelf((me, prop) =>
                                    {
                                        if (prop == nameof(SkiaSwitch.IsToggled))
                                            Super.TooltipsFromAccessibility = me.IsToggled;
                                    }),
                                    new SkiaLabel("Tooltips from accessibility text") { FontSize = 14, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation },
                                },
                            }),

                        Card("Explicit tooltips", "Set with .WithTooltip(...). The last one is long: it wraps at the maximum width.",
                            new SkiaWrap
                            {
                                Spacing = 10,
                                Children = new List<SkiaControl>
                                {
                                    TextButton("Publish").WithTooltip("Uploads the current version"),
                                    TextButton("Preview").WithTooltip("Opens a live preview"),
                                    TextButton("Share").WithTooltip("Copies a link to this project"),
                                    TextButton("Long text").WithTooltip("This tooltip is long on purpose: it wraps at the maximum width of 320 points, so you can see how a tooltip with several lines looks."),
                                },
                            }),

                        Card("Own text wins", "This button has an AccessibilityLabel but shows its own text, so it gets no tooltip from it.",
                            TextButton("Export").Adapt(me => me.AccessibilityLabel = "Export the file")),

                        Card("Near the window edge", "Make the window narrow and rest on the icon at the right: the tooltip stays inside the window.",
                            new SkiaLayer
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 44,
                                Children = new List<SkiaControl>
                                {
                                    IconButton(IconInfo, null).EndX().WithTooltip("This tooltip opens near the right edge of the window and is long enough to reach it"),
                                },
                            }),

                        Card("Scrolling closes it", "Rest on a row, then scroll this list with the wheel.",
                            new SkiaShape
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 220,
                                CornerRadius = 8,
                                BackgroundColor = Color.Parse("#212529"),
                                IsClippedToBounds = true,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaScroll
                                    {
                                        VerticalOptions = LayoutOptions.Fill,
                                        Content = new SkiaStack
                                        {
                                            Spacing = 1,
                                            Children = Enumerable.Range(1, 30).Select(i => (SkiaControl)new SkiaLayer
                                            {
                                                HeightRequest = 36,
                                                HorizontalOptions = LayoutOptions.Fill,
                                                BackgroundColor = CardColor,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLabel($"Row {i}") { FontSize = 14, TextColor = Colors.White, Margin = new Thickness(12, 0), VerticalOptions = LayoutOptions.Center },
                                                },
                                            }.WithTooltip($"Details of row {i}")).ToList(),
                                        },
                                    },
                                },
                            }),

                        Card("Context menu and dropdown", "Right-click anywhere in this card: the menu opens where you clicked. The button opens a list below it. A click outside or Escape closes them.",
                            TextButton("Choose a fruit")
                                .OnTapped(me => _menu = me.Superview.OpenPopupWindow(Menu("Fruits", "Apple", "Banana", "Cherry", "Grape", "Mango", "Orange", "Peach", "Pear"), me)),
                            new SkiaLabel("Nothing picked yet") { FontSize = 14, TextColor = Colors.White, AccessibilityRole = Aria.RoleStatus, AccessibilityLive = Aria.LivePolite }
                                .Assign(out _picked))
                            .OnContextMenu((me, e) =>
                            {
                                _menu = me.Superview.OpenPopupWindow(Menu("Edit", "Cut", "Copy", "Paste", "Select all", "Settings…"), e.Location);
                                return true;
                            }),
                    },
                },
            }.Fill(),
        };
    }

    /// <summary>A menu as in DrawnUI for Rust's popups example: a rounded panel of items; picking one closes it.</summary>
    private SkiaControl Menu(string name, params string[] items) => new SkiaShape
    {
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#262B3B"),
        StrokeColor = Color.Parse("#3B4256"),
        StrokeWidth = 1.5,
        WidthRequest = 200,
        Padding = new Thickness(4),
        AccessibilityRole = Aria.RoleMenu,
        AccessibilityLabel = name,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 0,
                Children = items.Select(text => (SkiaControl)new SkiaShape
                {
                    CornerRadius = 6,
                    HorizontalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(12, 7),
                    AccessibilityRole = Aria.RoleMenuItem,
                    AccessibilityCanInteract = true,
                    AccessibilityLabel = text,
                    Children = new List<SkiaControl> { new SkiaLabel(text) { FontSize = 15, TextColor = Colors.White, AccessibilityRole = Aria.RolePresentation } },
                }
                .OnHovered((me, on) => me.BackgroundColor = on ? Color.Parse("#343B52") : Colors.Transparent)
                .OnTapped(me =>
                {
                    _picked.Text = $"Picked: {text}";
                    me.Superview?.ClosePopupWindow(_menu);
                })).ToList(),
            },
        },
    };

    /// <summary>A card of the page: a caption in the accent color, a note, then its content.</summary>
    private static SkiaControl Card(string title, string note, params SkiaControl[] content) => new SkiaShape
    {
        HorizontalOptions = LayoutOptions.Fill,
        CornerRadius = 12,
        BackgroundColor = CardColor,
        StrokeColor = Color.Parse("#373B3E"),
        StrokeWidth = 1,
        Padding = new Thickness(20, 16),
        UseCache = SkiaCacheType.Operations, // static card: re-recorded only when a button inside changes
        AccessibilityRole = Aria.RoleGroup,
        AccessibilityLabel = title,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 12,
                Children = new SkiaControl[]
                {
                    new SkiaLabel(title.ToUpperInvariant()) { FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Accent, AccessibilityRole = Aria.RoleHeading },
                    new SkiaLabel(note) { FontSize = 13, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill },
                }.Concat(content).ToList(),
            },
        },
    };

    /// <summary>An icon button without text: its tooltip is its accessibility label.</summary>
    private static SkiaShape IconButton(string svg, string label) => new SkiaShape
    {
        WidthRequest = 40,
        HeightRequest = 40,
        CornerRadius = 8,
        BackgroundColor = ButtonColor,
        AnimationTapped = SkiaTouchAnimation.Ripple,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityCanInteract = true,
        AccessibilityLabel = label,
        Children = new List<SkiaControl>
        {
            new SkiaSvg { SvgString = svg, WidthRequest = 20, HeightRequest = 20, TintColor = Colors.White }.Center(),
        },
    }
    .OnHovered((me, on) => me.BackgroundColor = on ? ButtonHover : ButtonColor)
    .OnTapped(me => { });

    /// <summary>A text button in the page's colors.</summary>
    private static SkiaShape TextButton(string text) => new SkiaShape
    {
        CornerRadius = 8,
        BackgroundColor = ButtonColor,
        Padding = new Thickness(16, 9),
        AnimationTapped = SkiaTouchAnimation.Ripple,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityCanInteract = true,
        Children = new List<SkiaControl>
        {
            new SkiaLabel(text) { FontSize = 14, TextColor = Colors.White },
        },
    }
    .OnHovered((me, on) => me.BackgroundColor = on ? ButtonHover : ButtonColor)
    .OnTapped(me => { });
}
