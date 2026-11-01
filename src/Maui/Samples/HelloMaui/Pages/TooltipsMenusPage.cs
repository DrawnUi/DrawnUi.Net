using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;


namespace HelloMaui.Pages;

/// <summary>
/// Tooltips and menus: tooltips on any control (TooltipsFromAccessibility for icon buttons), a context menu and a
/// dropdown opened with DrawnView.OpenPopupWindow, the popup window API of DrawnUI for Rust. Same screen on every head.
/// </summary>
public class TooltipsMenusPage : SkiaLayer
{
    private const string IconNew = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zm4 18H6V4h7v5h5zM11 11h2v3h3v2h-3v3h-2v-3H8v-2h3z'/></svg>";
    private const string IconOpen = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M20 6h-8l-2-2H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2m0 12H4V8h16z'/></svg>";
    private const string IconSave = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7zm2 16H5V5h11.17L19 7.83zM12 12a3 3 0 1 0 0 6 3 3 0 0 0 0-6M6 6h9v4H6z'/></svg>";
    private const string IconSettings = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M19.4 13a7.5 7.5 0 0 0 0-2l2.1-1.6-2-3.5-2.5 1a7.6 7.6 0 0 0-1.7-1L15 3h-4l-.4 2.9a7.6 7.6 0 0 0-1.7 1l-2.5-1-2 3.5L6.6 11a7.5 7.5 0 0 0 0 2l-2.1 1.6 2 3.5 2.5-1a7.6 7.6 0 0 0 1.7 1L11 21h4l.4-2.9a7.6 7.6 0 0 0 1.7-1l2.5 1 2-3.5zM13 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 0 1 0 7'/></svg>";

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
                        new SkiaLabel("Rest the mouse on a control for its tooltip. Right-click the menu card for a context menu, or open the dropdown. A click outside or Escape closes a menu.")
                        {
                            FontSize = 14, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Card("Tooltips — Tooltip and TooltipsFromAccessibility",
                            new SkiaLabel("The icons have no Tooltip: they show their AccessibilityLabel. A tooltip opens above its control and stays there; the next one shows at once; a click, the wheel or a key closes it. Never on touch.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill },
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
                                },
                            },
                            new SkiaRow
                            {
                                Spacing = 10,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaSwitch { IsToggled = true, AccessibilityLabel = "Tooltips from accessibility text", VerticalOptions = LayoutOptions.Center, ColorFrameOn = Color.Parse("#0D6EFD"), ColorFrameOff = Color.Parse("#495057"), ColorThumbOn = Colors.White, ColorThumbOff = Colors.White }
                                        .ObserveSelf((me, prop) =>
                                        {
                                            if (prop == nameof(SkiaSwitch.IsToggled))
                                                Super.TooltipsFromAccessibility = me.IsToggled;
                                        }),
                                    new SkiaLabel("Tooltips from accessibility text") { FontSize = 13, TextColor = Color.Parse("#DEE2E6"), VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation },
                                },
                            },
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Publish") { BackgroundColor = Color.Parse("#0D6EFD") }.WithTooltip("Uploads the current version"),
                                    new SkiaButton("Preview") { BackgroundColor = Color.Parse("#0D6EFD") }.WithTooltip("Opens a live preview"),
                                    new SkiaButton("Long text") { BackgroundColor = Color.Parse("#495057") }.WithTooltip("A longer tooltip wraps at the maximum width of 320 points, so a few lines of help stay easy to read."),
                                },
                            }),

                        Card("Context menu and dropdown — OpenPopupWindow",
                            new SkiaLabel("Right-click anywhere in this card: the menu opens where you clicked, flipped where the window ends. The button opens a list below it.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaButton("Choose a fruit") { BackgroundColor = Color.Parse("#495057") }
                                .OnTapped(me => _menu = me.Superview.OpenPopupWindow(Menu("Fruits", "Apple", "Banana", "Cherry", "Grape", "Mango", "Orange", "Peach", "Pear"), me)),
                            new SkiaLabel("Nothing picked yet") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleStatus, AccessibilityLive = Aria.LivePolite }
                                .Assign(out _picked))
                            .OnContextMenu((me, e) =>
                            {
                                _menu = me.Superview.OpenPopupWindow(Menu("Edit", "Cut", "Copy", "Paste", "Select all", "Settings…"), e.Location);
                                return true;
                            }),

                        Card("Scrolling closes a tooltip",
                            new SkiaLabel("Rest on a row, then scroll the list with the wheel.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaShape
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 200,
                                CornerRadius = 6,
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
                                                HeightRequest = 34,
                                                HorizontalOptions = LayoutOptions.Fill,
                                                BackgroundColor = Color.Parse("#2B3035"),
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLabel($"Row {i}") { FontSize = 14, TextColor = Colors.White, Margin = new Thickness(12, 0), VerticalOptions = LayoutOptions.Center },
                                                },
                                            }.WithTooltip($"Details of row {i}")).ToList(),
                                        },
                                    },
                                },
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

    /// <summary>An icon button without text: its tooltip is its accessibility label.</summary>
    private static SkiaControl IconButton(string svg, string label) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 6,
        BackgroundColor = Color.Parse("#373B3E"),
        WidthRequest = 40,
        HeightRequest = 40,
        AnimationTapped = SkiaTouchAnimation.Ripple,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityCanInteract = true,
        AccessibilityLabel = label,
        Children = new List<SkiaControl>
        {
            new SkiaSvg { SvgString = svg, WidthRequest = 20, HeightRequest = 20, TintColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        },
    }
    .OnHovered((me, on) => me.BackgroundColor = Color.Parse(on ? "#495057" : "#373B3E"))
    .OnTapped(me => { });

    private static SkiaControl Card(string title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        AccessibilityRole = Aria.RoleGroup,
        AccessibilityLabel = title,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new SkiaControl[]
                {
                    new SkiaLabel(title) { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase, HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleHeading },
                }.Concat(content).ToList(),
            },
        },
    };
}
