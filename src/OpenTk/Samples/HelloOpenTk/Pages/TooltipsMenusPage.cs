using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

using Color = DrawnUi.Color;

namespace HelloOpenTk.Pages;

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

                        Card("Menu bar", "Click a menu, or press Alt with its first letter (Alt+F, Alt+E, Alt+V, Alt+H). Alt alone puts the keyboard on the bar: Left and Right go round the menus, Down, Enter or Space opens one. In a menu Up and Down go round it, Left and Right open the menu before or after, Enter picks, Escape closes one level. The shortcuts (Ctrl+N, Ctrl+S, F1...) work with the menus closed too.",
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
                                    }.WithKeyboardWrap(),
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

        // real mouse moves over the page: a mouse resting on the bar never takes the highlight from the keyboard
        this.WithGestures((me, args, apply) => NotePointer(args));

        KeyboardManager.KeyDown += OnKeyDown;
        KeyboardManager.KeyUp += OnKeyUp;
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        KeyboardManager.KeyDown -= OnKeyDown;
        KeyboardManager.KeyUp -= OnKeyUp;
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

    /// <summary>Where the keyboard is (a title, the open menu's title, the lit line): the menus' selected look, in the accent color.</summary>
    private static readonly Color Selected = Color.Parse("#0D6EFD");

    /// <summary>A line under the mouse.</summary>
    private static readonly Color LineHover = Color.Parse("#343B52");

    /// <summary>
    /// A menu bar title or a menu line. Enter / Space of keyboard navigation and a screen reader's Invoke come here, not as
    /// a tap, so the page tells them from a click.
    /// </summary>
    private sealed class MenuNode : SkiaShape
    {
        public Action Activated { get; set; }

        public override void OnAccessibilityActivated() => Activated?.Invoke();
    }

    private readonly MenuNode[] _tops = new MenuNode[Menus.Length];
    private readonly List<(MenuNode View, SkiaLabel Shortcut, MenuEntry Entry)> _items = new();
    private SkiaLabel _command;
    private PopupId _barPopup;
    private int _open = -1;
    private int _title = -1; // the keyboard is on the bar with no menu open: the title it is on
    private int _highlighted = -1;
    private int _hoveredTitle = -1;
    private bool _keys; // the keyboard drives the bar: its place reads selected, a mouse resting on the bar moves nothing
    private bool _onBar; // the keyboard came to the bar from _back (null: from nowhere), and goes back there when it leaves
    private SkiaControl _back;
    private bool _altAlone;
    private (float X, float Y)? _pointerAt;
    private int _pointerMoves, _keysMoves;
    private long _activatedFrame = -1;
    private long _giveBackUntil;

    private bool BarOpen => _open >= 0 && Superview?.IsPopupWindowOpen(_barPopup) == true;

    /// <summary>A menu title of the bar: a click opens its menu; while a menu is open, the mouse over another title opens that one.</summary>
    private MenuNode TopItem(string title, int index) => new MenuNode
    {
        CornerRadius = 6,
        Padding = new Thickness(12, 6),
        BackgroundColor = Colors.Transparent,
        AccessibilityRole = Aria.RoleMenuItem,
        AccessibilityCanInteract = true,
        AccessibilityLabel = title,
        AccessibilityIsPressed = false, // its menu is open
        Activated = () =>
        {
            if (!SamePress())
                ActivateTitle(index);
        },
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
        _hoveredTitle = on ? index : _hoveredTitle == index ? -1 : _hoveredTitle;
        if (on && BarOpen && _open != index && MouseTakesOver())
        {
            _keys = false;
            OpenMenu(index, keyboard: false);
        }
        PaintBar();
    })
    .OnTapped((me, e) =>
    {
        if (!Primary(e))
            return;
        _keys = false;
        if (BarOpen && _open == index)
            LeaveBar();
        else
            OpenMenu(index, keyboard: false);
    });

    /// <summary>The selected look where the keyboard is and on the open menu's title, the lighter hover under the mouse.</summary>
    private void PaintBar()
    {
        var open = BarOpen ? _open : -1;
        for (var i = 0; i < _tops.Length; i++)
        {
            var strong = i == open || i == _title;
            var hover = !strong && !_keys && i == _hoveredTitle;
            _tops[i].BackgroundColor = strong ? Selected : hover ? ButtonHover : Colors.Transparent;
            _tops[i].AccessibilityIsPressed = i == open;
        }
    }

    private void OpenMenu(int index, bool keyboard)
    {
        var canvas = Superview;
        if (canvas == null)
            return;

        // a menu closed from outside (a click elsewhere, the wheel, the window losing focus) lets the bar go
        canvas.PopupWindowClosed -= OnPopupWindowClosed;
        canvas.PopupWindowClosed += OnPopupWindowClosed;
        HookKeyboardFocus(canvas);

        CloseMenu();
        _items.Clear();
        _open = index;
        _title = -1;

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
                }
                .WithKeyboardWrap()
                .WithGestures((me, args, apply) => NotePointer(args)),
            },
        }, _tops[index]);

        _highlighted = -1;
        if (keyboard)
        {
            Keys();
            Light(Step(-1, InputKey.Home)); // its first line ready for the arrows
        }
        PaintItems();
        PaintBar();
    }

    private MenuNode BarItem(MenuEntry entry)
    {
        var index = _items.Count;
        SkiaLabel shortcut = null;
        var view = new MenuNode
        {
            CornerRadius = 6,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(12, 7),
            AccessibilityRole = Aria.RoleMenuItem,
            AccessibilityCanInteract = true,
            AccessibilityLabel = entry.Text,
            Activated = () =>
            {
                if (!SamePress())
                    Run(entry);
            },
            Children = new List<SkiaControl>
            {
                new SkiaLabel(entry.Text) { FontSize = 14, TextColor = Colors.White, AccessibilityRole = Aria.RolePresentation },
                new SkiaLabel(entry.Shortcut ?? "") { FontSize = 13, TextColor = Muted, HorizontalOptions = LayoutOptions.End, AccessibilityRole = Aria.RolePresentation }
                    .Assign(out shortcut),
            },
        }
        .OnHovered((me, on) =>
        {
            // a menu opening under a resting mouse leaves the keyboard's line lit; a real move takes over
            if (!on || !MouseTakesOver())
                return;
            _keys = false;
            Light(index);
        })
        .OnTapped((me, e) =>
        {
            if (Primary(e))
                Run(entry);
        });

        _items.Add((view, shortcut, entry));
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

    /// <summary>The line of the context menu or the dropdown that has the keyboard focus is highlighted, as the line under the mouse.</summary>
    private void OnKeyboardFocus(ISkiaAccessibilityNode node)
    {
        // ponytail: a menu closed with the keyboard in it sends the keyboard to its title at the frame end (the canvas'
        // rule); when the bar has just let go, that return goes on to where the keyboard was before the bar took it
        if (node is MenuNode title && Array.IndexOf(_tops, title) >= 0 && Environment.TickCount64 < _giveBackUntil)
        {
            _giveBackUntil = 0;
            GiveBack();
            return;
        }

        if (_focusLine != null && !ReferenceEquals(_focusLine, node))
            _focusLine.BackgroundColor = Colors.Transparent;
        _focusLine = node is SkiaControl { AccessibilityRole: "menuitem", Parent: SkiaControl { AccessibilityRole: "menu" } } line and not MenuNode ? line : null;
        if (_focusLine != null)
            _focusLine.BackgroundColor = LineHover;
    }

    /// <summary>Lights line <paramref name="index"/> of the open menu; the screen reader follows it.</summary>
    private void Light(int index)
    {
        _highlighted = index;
        PaintItems();
        if (index >= 0)
            Superview?.SetAccessibilityFocus(_items[index].View);
    }

    /// <summary>The line a key moves to from <paramref name="from"/>: the next or the one before, round past either end; Home and End the first and the last.</summary>
    private int Step(int from, InputKey key)
    {
        var count = _items.Count;
        if (count == 0)
            return -1;
        return key switch
        {
            InputKey.Home => 0,
            InputKey.End => count - 1,
            InputKey.ArrowDown => from < 0 ? 0 : (from + 1) % count,
            _ => from < 0 ? count - 1 : (from - 1 + count) % count,
        };
    }

    /// <summary>The line the keyboard lit: the selected look; the line under the mouse: the lighter hover.</summary>
    private void PaintItems()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var lit = i == _highlighted;
            var strong = lit && _keys;
            _items[i].View.BackgroundColor = strong ? Selected : lit ? LineHover : Colors.Transparent;
            _items[i].Shortcut.TextColor = strong ? Colors.White : Muted;
        }
    }

    /// <summary>The keyboard drives the bar, until the mouse really moves.</summary>
    private void Keys()
    {
        _keys = true;
        _keysMoves = _pointerMoves;
    }

    /// <summary>Counts real moves of the mouse: a hover check under a resting mouse (a menu opened or closed) comes at the same place.</summary>
    private ISkiaGestureListener NotePointer(SkiaGesturesParameters args)
    {
        if (args.Type == AppoMobi.Gestures.TouchActionResult.Pointer)
        {
            var at = (args.Event.Location.X, args.Event.Location.Y);
            if (_pointerAt != at)
            {
                _pointerAt = at;
                _pointerMoves++;
            }
        }
        return null;
    }

    private bool MouseTakesOver() => !_keys || _pointerMoves != _keysMoves;

    /// <summary>
    /// The canvas (keyboard navigation, a screen reader's Invoke) and KeyboardManager both report one Enter / Space, in an
    /// order that depends on the head: reports within one frame are that same press.
    /// </summary>
    private bool SamePress()
    {
        var frame = Superview?.FrameNumber ?? -1;
        if (frame == _activatedFrame)
            return true;
        _activatedFrame = frame;
        return false;
    }

    /// <summary>The keyboard comes to the bar: where the keyboard was is kept, to give it back.</summary>
    private void EnterBar()
    {
        Keys();
        if (_onBar || Superview == null)
            return;
        _onBar = true;
        _back = Superview.KeyboardFocusNode as SkiaControl;
        HookKeyboardFocus(Superview);
    }

    /// <summary>The bar lets go: menus closed, the keyboard back where it was.</summary>
    private void LeaveBar()
    {
        CloseMenu();
        _title = -1;
        _keys = false;
        if (_onBar)
        {
            _onBar = false;
            GiveBack();
            _giveBackUntil = Environment.TickCount64 + 500;
        }
        PaintBar();
    }

    private void GiveBack()
    {
        var canvas = Superview;
        if (canvas == null)
            return;
        canvas.SetAccessibilityFocus(_back); // null drops a move to a menu line still waiting for the frame end
        if (_back == null)
        {
            canvas.KeyboardFocusNode = null;
            canvas.AccessibilityManager.NotifyFocused(null);
        }
    }

    /// <summary>Enter / Space of keyboard navigation or a screen reader on a title: opens its menu, or closes it when open.</summary>
    private void ActivateTitle(int index)
    {
        EnterBar();
        if (BarOpen && _open == index)
        {
            CloseMenu();
            _title = index;
            PaintBar();
        }
        else
            OpenMenu(index, keyboard: true);
    }

    private void OnPopupWindowClosed(object sender, PopupId id)
    {
        if (id != _barPopup || _open < 0)
            return;

        _open = -1;
        _highlighted = -1;
        LeaveBar();
    }

    private void CloseMenu()
    {
        var open = _open;
        _open = -1; // first: a menu this page closes is no menu closed from outside (OnPopupWindowClosed)
        _highlighted = -1;
        if (open >= 0)
            Superview?.ClosePopupWindow(_barPopup);
        PaintBar();
    }

    private void Run(MenuEntry entry)
    {
        var menu = Menus.First(m => m.Entries.Contains(entry));
        _command.Text = entry.Shortcut != null
            ? $"Last command: {menu.Title} › {entry.Text} ({entry.Shortcut})"
            : $"Last command: {menu.Title} › {entry.Text}";
        LeaveBar();
    }

    /// <summary>
    /// The menu bar's keys, as in desktop menu bars. Alt with a title's letter opens its menu. On the bar: Left and Right
    /// go round the titles, Down, Enter or Space open one. In a menu: Up, Down, Home and End move through its lines (round
    /// at the ends), Left and Right open the menu before or after, Enter or Space runs the line, Escape closes it and
    /// leaves the keyboard on its title; Escape on the bar lets the keyboard go.
    /// </summary>
    private void OnKeyDown(object sender, InputKey key)
    {
        if (Superview == null || IsDisposed)
            return;

        // Alt alone (pressed and let go with no other key) puts the keyboard on the bar; anything with it is no Alt alone
        _altAlone = key is InputKey.AltLeft or InputKey.AltRight
                    && !KeyboardManager.IsControlPressed && !KeyboardManager.IsShiftPressed && !KeyboardManager.IsMetaPressed;

        // Alt + the first letter of a menu opens it, its first line ready for the arrows
        if (KeyboardManager.IsAltPressed)
        {
            var index = Array.FindIndex(Menus, m => m.Accelerator == key);
            if (index >= 0)
            {
                EnterBar();
                OpenMenu(index, keyboard: true);
            }
            return;
        }

        var n = Menus.Length;
        if (!BarOpen)
        {
            // keyboard navigation (Tab) brought the keyboard to a title: the keys move on the bar from there
            if (_title < 0 && Superview.KeyboardFocusNode is MenuNode node && Array.IndexOf(_tops, node) is var at and >= 0
                && key is InputKey.ArrowLeft or InputKey.ArrowRight or InputKey.ArrowDown or InputKey.Enter or InputKey.Space)
            {
                EnterBar();
                _title = at;
            }

            if (_title < 0)
            {
                // the shortcuts work with the menus closed
                var ctrl = KeyboardManager.IsControlPressed;
                var entry = Menus.SelectMany(m => m.Entries)
                    .FirstOrDefault(e => e.Text != null && e.Key != InputKey.Unknown && e.Key == key && e.Ctrl == ctrl);
                if (entry != null)
                    Run(entry);
                return;
            }

            switch (key)
            {
                case InputKey.ArrowLeft:
                    _title = (_title - 1 + n) % n;
                    break;
                case InputKey.ArrowRight:
                    _title = (_title + 1) % n;
                    break;
                case InputKey.ArrowDown:
                    OpenMenu(_title, keyboard: true);
                    return;
                case InputKey.Enter:
                case InputKey.Space:
                    if (!SamePress())
                        OpenMenu(_title, keyboard: true);
                    return;
                case InputKey.Escape:
                    LeaveBar();
                    return;
                default:
                    return;
            }

            Keys();
            Superview.SetAccessibilityFocus(_tops[_title]);
            PaintBar();
            return;
        }

        if (key is not (InputKey.ArrowDown or InputKey.ArrowUp or InputKey.Home or InputKey.End or InputKey.ArrowLeft
            or InputKey.ArrowRight or InputKey.Enter or InputKey.Space or InputKey.Escape))
            return;

        // a menu the mouse opened: the keyboard comes along
        EnterBar();
        switch (key)
        {
            case InputKey.ArrowLeft:
                OpenMenu((_open - 1 + n) % n, keyboard: true);
                break;
            case InputKey.ArrowRight:
                OpenMenu((_open + 1) % n, keyboard: true);
                break;
            case InputKey.Enter:
            case InputKey.Space:
                if (_highlighted >= 0 && !SamePress())
                    Run(_items[_highlighted].Entry);
                break;
            case InputKey.Escape:
                var open = _open;
                CloseMenu();
                _title = open;
                Superview.SetAccessibilityFocus(_tops[open]);
                PaintBar();
                break;
            default:
                Light(Step(_highlighted, key));
                break;
        }
    }

    /// <summary>Alt let go with nothing pressed with it: the keyboard goes onto the bar (its first title), or, when it is there, back where it was.</summary>
    private void OnKeyUp(object sender, InputKey key)
    {
        if (Superview == null || IsDisposed || key is not (InputKey.AltLeft or InputKey.AltRight) || !_altAlone)
            return;

        _altAlone = false;
        if (_open >= 0 || _title >= 0)
        {
            LeaveBar();
            return;
        }

        EnterBar();
        _title = 0;
        Superview.SetAccessibilityFocus(_tops[0]);
        PaintBar();
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
