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
                        new SkiaLabel("Tooltips & Menus") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center, AccessibilityRole = DrawnUi.Models.Aria.RolePresentation },
                        new SkiaLabel("A desktop menu bar, a context menu (right-click anywhere on the page) and a dropdown, then tooltips: rest the mouse on a control, move along a row and the next one shows at once; a click, the wheel or a key closes it.")
                        {
                            FontSize = 14, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Card("Menu bar", "Click a menu, or press Alt with its first letter (Alt+F, Alt+E, Alt+V, Alt+H). Left and Right open the next menu, Up and Down move through it, Enter picks, Escape closes. The shortcuts (Ctrl+N, Ctrl+S, F1...) work with the menus closed too.",
                            new SkiaShape
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                CornerRadius = 8,
                                BackgroundColor = ButtonColor, // the bar in the buttons' color
                                Padding = new Thickness(4),
                                Children = new List<SkiaControl>
                                {
                                    new SkiaRow
                                    {
                                        Spacing = 2,
                                        AccessibilityRole = Aria.RoleMenuBar,
                                        AccessibilityLabel = "Menu bar",
                                        Children = Menus.Select((menu, index) => (SkiaControl)TopItem(menu.Title, index)).ToList(),
                                    },
                                },
                            },
                            new SkiaLabel("Last command: none") { FontSize = 14, TextColor = Colors.White, AccessibilityRole = Aria.RoleStatus, AccessibilityLive = Aria.LivePolite }
                                .Assign(out _command)),

                        Card("Context menu and dropdown", "Right-click anywhere on the page: the menu opens where you clicked. The button opens a list below it. A click outside or Escape closes them.",
                            TextButton("Choose a fruit")
                                .OnTapped((me, e) =>
                                {
                                    if (!Primary(e))
                                        return;
                                    HookKeyboardFocus(me.Superview);
                                    me.Superview.ClosePopupWindow(_menu); // one of the two at a time: the context menu or the list
                                    _menu = me.Superview.OpenPopupWindow(Menu("Fruits", "Apple", "Banana", "Cherry", "Grape", "Mango", "Orange", "Peach", "Pear"), me);
                                }),
                            new SkiaLabel("Nothing picked yet") { FontSize = 14, TextColor = Colors.White, AccessibilityRole = Aria.RoleStatus, AccessibilityLive = Aria.LivePolite }
                                .Assign(out _picked)),

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

                        Card("Near the window edge", "Rest on the icon at the right. On Windows the tooltip opens in a window of its own and can go past the edge of the app window; on phones, tablets and in the browser it stays inside the app.",
                            new SkiaLayer
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 44,
                                Children = new List<SkiaControl>
                                {
                                    IconButton(IconInfo, "Information").EndX().WithTooltip("This tooltip opens near the right edge of the window and is long enough to reach it"),
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
                    },
                },
            }.Fill(),
        };

        // right-click anywhere on the page
        this.OnContextMenu((me, e) =>
        {
            HookKeyboardFocus(me.Superview);
            me.Superview.ClosePopupWindow(_menu); // one of the two at a time: the context menu or the list
            _menu = me.Superview.OpenPopupWindow(Menu("Edit", "Cut", "Copy", "Paste", "Select all", "Settings…"), e.Location);
            return true;
        });

        KeyboardManager.KeyDown += OnKeyDown;
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        KeyboardManager.KeyDown -= OnKeyDown;
        if (Superview != null)
        {
            Superview.PopupWindowClosed -= OnPopupWindowClosed;
            Superview.AccessibilityManager.FocusChanged -= OnKeyboardFocus;
        }
        base.OnDisposing();
    }

    #region Menu bar

    /// <summary>One line of a menu bar menu; Text null = a separator.</summary>
    private sealed record MenuEntry(string Text, string Shortcut = null, InputKey Key = InputKey.Unknown, bool Ctrl = true);

    private sealed record BarMenu(string Title, InputKey Accelerator, MenuEntry[] Entries);

    private static readonly BarMenu[] Menus =
    {
        new("File", InputKey.KeyF, new MenuEntry[]
        {
            new("New", "Ctrl+N", InputKey.KeyN), new("Open…", "Ctrl+O", InputKey.KeyO), new("Save", "Ctrl+S", InputKey.KeyS),
            new(null), new("Close", "Ctrl+W", InputKey.KeyW),
        }),
        new("Edit", InputKey.KeyE, new MenuEntry[]
        {
            new("Undo", "Ctrl+Z", InputKey.KeyZ), new("Redo", "Ctrl+Y", InputKey.KeyY),
            new(null), new("Cut", "Ctrl+X", InputKey.KeyX), new("Copy", "Ctrl+C", InputKey.KeyC), new("Paste", "Ctrl+V", InputKey.KeyV),
        }),
        new("View", InputKey.KeyV, new MenuEntry[]
        {
            new("Zoom in", "Ctrl+=", InputKey.Equal), new("Zoom out", "Ctrl+-", InputKey.Minus),
            new(null), new("Full screen", "F11", InputKey.F11, false),
        }),
        new("Help", InputKey.KeyH, new MenuEntry[]
        {
            new("Documentation", "F1", InputKey.F1, false), new(null), new("About"),
        }),
    };

    private readonly SkiaShape[] _tops = new SkiaShape[Menus.Length];
    private readonly List<(SkiaShape View, MenuEntry Entry)> _items = new();
    private SkiaLabel _command;
    private PopupId _barPopup;
    private int _open = -1;
    private int _highlighted = -1;

    private bool BarOpen => _open >= 0 && Superview?.IsPopupWindowOpen(_barPopup) == true;

    /// <summary>A menu title of the bar: a click opens its menu; while a menu is open, the mouse over another title opens that one.</summary>
    private SkiaShape TopItem(string title, int index) => new SkiaShape
    {
        CornerRadius = 6,
        Padding = new Thickness(12, 6),
        BackgroundColor = Colors.Transparent,
        AccessibilityRole = Aria.RoleMenuItem,
        AccessibilityCanInteract = true,
        AccessibilityLabel = title,
        Children = new List<SkiaControl>
        {
            // the accelerator letter (Alt + it opens the menu) is underlined, as in desktop menu bars
            new SkiaLabel
            {
                FontSize = 14,
                TextColor = Colors.White,
                AccessibilityRole = Aria.RolePresentation,
                Spans =
                {
                    new TextSpan { Text = title[..1], Underline = true },
                    new TextSpan { Text = title[1..] },
                },
            },
        },
    }
    .Assign(out _tops[index])
    .OnHovered((me, on) =>
    {
        if (on && BarOpen && _open != index)
            OpenMenu(index, keyboard: false);
        PaintBar(index, on);
    })
    .OnTapped((me, e) =>
    {
        if (!Primary(e))
            return;
        if (BarOpen && _open == index)
            CloseMenu();
        else
            OpenMenu(index, keyboard: false);
    });

    private void PaintBar(int hovered = -1, bool on = false)
    {
        var open = BarOpen ? _open : -1;
        for (var i = 0; i < _tops.Length; i++)
            _tops[i].BackgroundColor = i == open ? Color.Parse("#5A6270") : (i == hovered && on ? ButtonHover : Colors.Transparent);
    }

    private void OpenMenu(int index, bool keyboard)
    {
        var canvas = Superview;
        if (canvas == null)
            return;

        // a menu closed from outside (a click elsewhere, the wheel, the window losing focus) unselects its title
        canvas.PopupWindowClosed -= OnPopupWindowClosed;
        canvas.PopupWindowClosed += OnPopupWindowClosed;
        HookKeyboardFocus(canvas);

        canvas.ClosePopupWindow(_barPopup);
        _items.Clear();
        _open = index;

        var menu = Menus[index];
        _barPopup = canvas.OpenPopupWindow(new SkiaShape
        {
            CornerRadius = 8,
            BackgroundColor = Color.Parse("#262B3B"),
            StrokeColor = Color.Parse("#3B4256"),
            StrokeWidth = 1.5,
            WidthRequest = 240,
            Padding = new Thickness(4),
            Children = new List<SkiaControl>
            {
                new SkiaStack
                {
                    Spacing = 0,
                    AccessibilityRole = Aria.RoleMenu,
                    AccessibilityLabel = menu.Title,
                    Children = menu.Entries.Select(entry => entry.Text == null
                        ? new SkiaShape { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Color.Parse("#3B4256"), Margin = new Thickness(8, 4) }
                        : (SkiaControl)BarItem(entry)).ToList(),
                },
            },
        }, _tops[index]);

        _highlighted = keyboard ? 0 : -1;
        PaintItems();
        PaintBar();
    }

    private SkiaShape BarItem(MenuEntry entry)
    {
        var index = _items.Count;
        var view = new SkiaShape
        {
            CornerRadius = 6,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(12, 7),
            AccessibilityRole = Aria.RoleMenuItem,
            AccessibilityCanInteract = true,
            AccessibilityLabel = entry.Text,
            Children = new List<SkiaControl>
            {
                new SkiaLabel(entry.Text) { FontSize = 14, TextColor = Colors.White, AccessibilityRole = Aria.RolePresentation },
                new SkiaLabel(entry.Shortcut ?? "") { FontSize = 13, TextColor = Muted, HorizontalOptions = LayoutOptions.End, AccessibilityRole = Aria.RolePresentation },
            },
        }
        .OnHovered((me, on) =>
        {
            if (on)
            {
                _highlighted = index;
                PaintItems();
            }
        })
        .OnTapped((me, e) =>
        {
            if (Primary(e))
                Run(entry);
        });

        _items.Add((view, entry));
        return view;
    }

    private SkiaControl _focusLine;

    /// <summary>
    /// The left button, a touch or the keyboard: every mouse button taps, and a right click belongs to the context
    /// menu, so it must not also open the dropdown or pick a line.
    /// </summary>
    private static bool Primary(ControlTappedEventArgs e) => (e.Parameters?.Event?.Pointer?.Button ?? AppoMobi.Gestures.MouseButton.Left) == AppoMobi.Gestures.MouseButton.Left;

    private void HookKeyboardFocus(DrawnView canvas)
    {
        canvas.AccessibilityManager.FocusChanged -= OnKeyboardFocus;
        canvas.AccessibilityManager.FocusChanged += OnKeyboardFocus;
    }

    /// <summary>The line of an open menu that has the keyboard focus is highlighted, as the line under the mouse.</summary>
    private void OnKeyboardFocus(ISkiaAccessibilityNode node)
    {
        var index = _items.FindIndex(i => ReferenceEquals(i.View, node));
        if (index >= 0)
        {
            _highlighted = index;
            PaintItems();
            return;
        }

        if (_focusLine != null && !ReferenceEquals(_focusLine, node))
            _focusLine.BackgroundColor = Colors.Transparent;
        _focusLine = node is SkiaControl { AccessibilityRole: "menuitem", Parent: SkiaControl { AccessibilityRole: "menu" } } line ? line : null;
        if (_focusLine != null)
            _focusLine.BackgroundColor = Color.Parse("#343B52");
    }

    private void PaintItems()
    {
        for (var i = 0; i < _items.Count; i++)
            _items[i].View.BackgroundColor = i == _highlighted ? Color.Parse("#343B52") : Colors.Transparent;
    }

    private void OnPopupWindowClosed(object sender, PopupId id)
    {
        if (id != _barPopup || _open < 0)
            return;

        _open = -1;
        _highlighted = -1;
        PaintBar();
    }

    private void CloseMenu()
    {
        Superview?.ClosePopupWindow(_barPopup);
        _open = -1;
        _highlighted = -1;
        PaintBar();
    }

    private void Run(MenuEntry entry)
    {
        var menu = Menus.First(m => m.Entries.Contains(entry));
        _command.Text = entry.Shortcut != null
            ? $"Last command: {menu.Title} › {entry.Text} ({entry.Shortcut})"
            : $"Last command: {menu.Title} › {entry.Text}";
        CloseMenu();
    }

    private void OnKeyDown(object sender, InputKey key)
    {
        if (Superview == null || IsDisposed)
            return;

        // Alt + the first letter of a menu opens it, its first line ready for the arrows
        if (KeyboardManager.IsAltPressed)
        {
            var index = Array.FindIndex(Menus, m => m.Accelerator == key);
            if (index >= 0)
                OpenMenu(index, keyboard: true);
            return;
        }

        if (!BarOpen)
        {
            // the shortcuts work with the menus closed
            var ctrl = KeyboardManager.IsControlPressed;
            var entry = Menus.SelectMany(m => m.Entries)
                .FirstOrDefault(e => e.Text != null && e.Key != InputKey.Unknown && e.Key == key && e.Ctrl == ctrl);
            if (entry != null)
                Run(entry);
            return;
        }

        // the keyboard focus is on a line of the open menu (opened from the keyboard): Up, Down, Enter and Space move
        // and pick through it, the highlight follows it (OnKeyboardFocus)
        var focusInMenu = Superview.AccessibilityManager.FocusedNode is SkiaControl focused
                          && _items.Any(i => ReferenceEquals(i.View, focused));
        if (focusInMenu && key is InputKey.ArrowDown or InputKey.ArrowUp or InputKey.Enter or InputKey.Space)
            return;

        switch (key)
        {
            case InputKey.ArrowLeft:
                OpenMenu((_open - 1 + Menus.Length) % Menus.Length, keyboard: true);
                break;
            case InputKey.ArrowRight:
                OpenMenu((_open + 1) % Menus.Length, keyboard: true);
                break;
            case InputKey.ArrowDown:
                _highlighted = (_highlighted + 1) % _items.Count;
                PaintItems();
                break;
            case InputKey.ArrowUp:
                _highlighted = _highlighted <= 0 ? _items.Count - 1 : _highlighted - 1;
                PaintItems();
                break;
            case InputKey.Enter:
            case InputKey.Space:
                if (_highlighted >= 0)
                    Run(_items[_highlighted].Entry);
                break;
            case InputKey.Escape:
                CloseMenu();
                break;
        }
    }

    #endregion

    /// <summary>A menu as in DrawnUI for Rust's popups example: a rounded panel of items; picking one closes it.</summary>
    private SkiaControl Menu(string name, params string[] items) => new SkiaShape
    {
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#262B3B"),
        StrokeColor = Color.Parse("#3B4256"),
        StrokeWidth = 1.5,
        WidthRequest = 200,
        Padding = new Thickness(4),
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 0,
                AccessibilityRole = Aria.RoleMenu,
                AccessibilityLabel = name,
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
                .OnTapped((me, e) =>
                {
                    if (!Primary(e))
                        return;
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
        AccessibilityLabel = text,
        Children = new List<SkiaControl>
        {
            new SkiaLabel(text) { FontSize = 14, TextColor = Colors.White },
        },
    }
    .OnHovered((me, on) => me.BackgroundColor = on ? ButtonHover : ButtonColor)
    .OnTapped(me => { });
}
