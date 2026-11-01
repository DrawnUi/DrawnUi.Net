
using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// Accessibility snippet: the same AccessibilityRole / Label / Hint / IsPressed / Live properties as
/// the other heads feed the engine's <see cref="SkiaAccessibilityManager"/> snapshot. Ported from the
/// React demo's AccessibilityPage.tsx. On Windows DrawnUi.Maui publishes the snapshot as virtual
/// UI Automation peers of the canvas (Narrator, Accessibility Insights).
/// </summary>
public class AccessibilityPage : SkiaLayer
{
    private SkiaLabel _snapshot;
    private SkiaButton _counter;
    private SkiaButton _sound;
    private SkiaButton _dark;
    private int _count;
    private bool _soundOn = true;
    private bool _darkOn;
    private string _lastActivated = "-";
    private IDispatcherTimer _timer;

    private SkiaAccessibilityManager _manager;
    /// <summary>Builds the page.</summary>
    public AccessibilityPage()
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
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Accessibility") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center, AccessibilityRole = Aria.RoleHeading },
                        new SkiaLabel("Every drawn control can describe itself: role, label, hint, pressed state, live region. The engine keeps an accessibility snapshot of the arranged nodes (rebuilt at most once per second, so it follows scrolling); a head maps it to the platform's assistive tree.")
                        {
                            FontSize = 14, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Card("Accessibility snapshot (Canvas.AccessibilityManager)",
                            new SkiaLabel("Nodes in the snapshot: … · focused: none · last activated: -") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleStatus, AccessibilityLive = Aria.LivePolite }.Assign(out _snapshot),
                            new SkiaLabel("MAUI Windows: every node is a virtual UI Automation peer under the canvas (role, name, help text, toggle state, live regions); Narrator reads and activates them. Other MAUI platforms keep the snapshot without publishing it yet.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card("AccessibilityTextSelectable — selectable, copyable text (opt-in)",
                            new SkiaLabel("This paragraph is drawn on the canvas and its text can be selected: drag over it with the mouse (double click picks a word) or long press it with a finger and drag on, then copy with Ctrl+C or the Copy button. Off by default: a press on selectable text goes to the selection, not to the control under it, so it is never turned on for buttons, carousels or anything gesture-driven.")
                            {
                                FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill, AccessibilityTextSelectable = true,
                            },
                            new SkiaLabel("This one is a normal label: exposed to screen readers, not selectable.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card("Buttons — label from Text, hint, custom label, disabled",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Tapped 0×") { BackgroundColor = Color.Parse("#0D6EFD"), AccessibilityHint = "Increments the counter" }
                                        .Assign(out _counter)
                                        .OnTapped(me => { _count++; _counter.Text = $"Tapped {_count}×"; Activated("counter"); }),
                                    new SkiaButton("★") { FontSize = 18, FontFamily = "FontSymbols2", BackgroundColor = Color.Parse("#6610F2"), WidthRequest = 48, AccessibilityLabel = "Favorite", AccessibilityHint = "Icon-only button: AccessibilityLabel replaces the glyph" }
                                        .OnTapped(me => Activated("favorite")),
                                    new SkiaButton("Disabled") { BackgroundColor = Color.Parse("#495057"), IsDisabled = true, AccessibilityHint = "IsDisabled: no tab stop, not activatable" },
                                },
                            }),

                        Card("Toggles — AccessibilityIsPressed, in a toolbar",
                            new SkiaRow
                            {
                                Spacing = 8,
                                AccessibilityRole = Aria.RoleToolbar, // one Tab stop, Left / Right move between the toggles
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Sound: on") { BackgroundColor = Color.Parse("#20C997"), AccessibilityLabel = "Sound", AccessibilityIsPressed = true }
                                        .Assign(out _sound)
                                        .OnTapped(me =>
                                        {
                                            _soundOn = !_soundOn;
                                            _sound.Text = _soundOn ? "Sound: on" : "Sound: off";
                                            _sound.BackgroundColor = Color.Parse(_soundOn ? "#20C997" : "#495057");
                                            _sound.AccessibilityIsPressed = _soundOn;
                                            Activated("sound");
                                        }),
                                    new SkiaButton("Dark: off") { BackgroundColor = Color.Parse("#495057"), AccessibilityLabel = "Dark mode", AccessibilityIsPressed = false }
                                        .Assign(out _dark)
                                        .OnTapped(me =>
                                        {
                                            _darkOn = !_darkOn;
                                            _dark.Text = _darkOn ? "Dark: on" : "Dark: off";
                                            _dark.BackgroundColor = Color.Parse(_darkOn ? "#20C997" : "#495057");
                                            _dark.AccessibilityIsPressed = _darkOn;
                                            Activated("dark");
                                        }),
                                },
                            }),

                        Card("Any control can be a node — SkiaShape as a button, image with a description",
                            new SkiaWrap
                            {
                                Spacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        CornerRadius = 12,
                                        BackgroundColor = Color.Parse("#373B3E"),
                                        StrokeColor = Color.Parse("#6EA8FE"),
                                        StrokeWidth = 1,
                                        HorizontalOptions = LayoutOptions.Start,
                                        AnimationTapped = SkiaTouchAnimation.Ripple,
                                        AccessibilityRole = Aria.RoleButton,
                                        AccessibilityCanInteract = true,
                                        AccessibilityLabel = "Open settings",
                                        AccessibilityHint = "A SkiaShape with Tapped: role button, label and hint set explicitly",
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaStack
                                            {
                                                Spacing = 4,
                                                Padding = new Thickness(16, 12),
                                                HorizontalOptions = LayoutOptions.Start,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLabel("Settings") { FontSize = 18, FontFamily = "FontTextBold", TextColor = Colors.White, AccessibilityRole = Aria.RolePresentation },
                                                    new SkiaLabel("inner labels are RolePresentation") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), AccessibilityRole = Aria.RolePresentation },
                                                },
                                            },
                                        },
                                    }.OnTapped(me => Activated("settings card")),
                                    new SkiaSvg { Source = "images/drawnui.svg", WidthRequest = 72, LockRatio = 1, AccessibilityRole = Aria.RoleImg, AccessibilityLabel = "DrawnUI palette logo" },
                                    new SkiaShape { Type = ShapeType.Circle, BackgroundColor = Color.Parse("#FFC107"), WidthRequest = 48, LockRatio = 1, VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation },
                                },
                            },
                            new SkiaLabel("The yellow circle is decorative: AccessibilityRole=Aria.RolePresentation keeps it out of the tree.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill }),

                        Card("Keyboard groups — one Tab stop, the arrow keys inside",
                            new SkiaLabel("A container with a composite role (Aria.RoleList, RoleToolbar, RoleGrid...) is one Tab stop: the arrow keys move between its items, Home and End go to the first and the last, Enter or Space activates. Tab comes back to the item it left.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaLabel("Fruits — a list: Up and Down") { FontSize = 13, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaStack
                            {
                                Spacing = 6,
                                AccessibilityRole = Aria.RoleList,
                                AccessibilityLabel = "Fruits",
                                Children = new[] { "Apple", "Banana", "Cherry", "Date" }.Select(name => GroupItem(name, -1)).ToList(),
                            },
                            new SkiaLabel("Numbers — a grid: all four arrows") { FontSize = 13, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaWrap
                            {
                                Spacing = 6,
                                AccessibilityRole = Aria.RoleGrid,
                                AccessibilityLabel = "Numbers",
                                Children = Enumerable.Range(1, 12).Select(i => GroupItem(i.ToString(), 56)).ToList(),
                            }),

                        TooltipsCard(),

                        Card("Labels — read by default, opted out per control",
                            new SkiaLabel("This label is announced: SkiaLabel.DefaultAccessibilityRole = Aria.RoleText was set once at startup.") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaLabel("This one is visible but hidden from assistive technology (RolePresentation).") { FontSize = 14, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RolePresentation },
                            new SkiaLabel("Heading level text") { FontSize = 16, FontFamily = "FontTextBold", TextColor = Colors.White, AccessibilityRole = Aria.RoleHeading }),

                        Card("How it works",
                            new SkiaLabel("• Controls with an AccessibilityRole register with the canvas' SkiaAccessibilityManager; the snapshot is rebuilt at most once per second from the arranged rects.\n• Same property names on every head: AccessibilityRole, AccessibilityLabel, AccessibilityHint, AccessibilityCanInteract, AccessibilityIsPressed, AccessibilityLive.\n• Blazor / React mirror the snapshot into an aria overlay; WPF and MAUI Windows publish it as virtual UI Automation peers.")
                            {
                                FontSize = 13, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),
        };

        // live view of the engine's accessibility snapshot: refreshed on SkiaAccessibilityManager.Changed (see OnLayoutReady),
        // and polled because focus is not part of the snapshot
        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(300);
        _timer.Tick += (_, _) => RefreshSnapshot();
        _timer.Start();
    }

    /// <summary>Laid out inside a canvas: its accessibility manager is reachable now, subscribe once.</summary>
    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        if (_manager != null)
            return;

        _manager = Superview?.AccessibilityManager;
        if (_manager != null)
            _manager.Changed += OnSnapshotChanged;
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        if (_manager != null)
            _manager.Changed -= OnSnapshotChanged;
        _manager = null;
        _timer?.Stop();
        _timer = null;
        base.OnDisposing();
    }

    /// <summary>Raised from the frame that rebuilt the snapshot: hop to the UI thread.</summary>
    private void OnSnapshotChanged() => MainThread.BeginInvokeOnMainThread(RefreshSnapshot);

    private void Activated(string what)
    {
        _lastActivated = what;
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var manager = Superview?.AccessibilityManager;
        if (manager == null)
            return;

        _snapshot.Text = $"Nodes in the snapshot: {manager.Snapshot.Length} · focused: {manager.FocusedNode?.AccessibilityLabel ?? "none"} · last activated: {_lastActivated}";
    }

    /// <summary>An item of a keyboard group: a SkiaShape button that reports itself as activated.</summary>
    private SkiaControl GroupItem(string text, double width) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 6,
        BackgroundColor = Color.Parse("#373B3E"),
        WidthRequest = width,
        HeightRequest = 36,
        HorizontalOptions = width < 0 ? LayoutOptions.Fill : LayoutOptions.Start,
        AnimationTapped = SkiaTouchAnimation.Ripple,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityCanInteract = true,
        AccessibilityLabel = text,
        Children = new List<SkiaControl>
        {
            new SkiaLabel(text) { FontSize = 14, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation },
        },
    }.OnTapped(me => Activated(text));

    private const string IconNew = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zm4 18H6V4h7v5h5zM11 11h2v3h3v2h-3v3h-2v-3H8v-2h3z'/></svg>";
    private const string IconOpen = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M20 6h-8l-2-2H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2m0 12H4V8h16z'/></svg>";
    private const string IconSave = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M17 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V7zm2 16H5V5h11.17L19 7.83zM12 12a3 3 0 1 0 0 6 3 3 0 0 0 0-6M6 6h9v4H6z'/></svg>";
    private const string IconSettings = "<svg viewBox='0 0 24 24'><path fill='#fff' d='M19.4 13a7.5 7.5 0 0 0 0-2l2.1-1.6-2-3.5-2.5 1a7.6 7.6 0 0 0-1.7-1L15 3h-4l-.4 2.9a7.6 7.6 0 0 0-1.7 1l-2.5-1-2 3.5L6.6 11a7.5 7.5 0 0 0 0 2l-2.1 1.6 2 3.5 2.5-1a7.6 7.6 0 0 0 1.7 1L11 21h4l.4-2.9a7.6 7.6 0 0 0 1.7-1l2.5 1 2-3.5zM13 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 0 1 0 7'/></svg>";

    /// <summary>
    /// Tooltips (desktop): icon buttons that show their accessibility label, a switch for
    /// Super.TooltipsFromAccessibility, and buttons with their own Tooltip. Same card on every head.
    /// </summary>
    private SkiaControl TooltipsCard()
    {
        Super.TooltipsFromAccessibility = true;

        return Card("Tooltips — rest the mouse on a control",
            new SkiaLabel("Rest the mouse on a control: its tooltip opens above the pointer, and the next one shows at once. A click, the wheel or a key closes it. Never on touch.") { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill },
            new SkiaRow
            {
                Spacing = 6,
                AccessibilityRole = Aria.RoleToolbar,
                AccessibilityLabel = "File tools",
                Children = new List<SkiaControl>
                {
                    TooltipIcon(IconNew, "New file"),
                    TooltipIcon(IconOpen, "Open"),
                    TooltipIcon(IconSave, "Save"),
                    TooltipIcon(IconSettings, "Settings"),
                },
            },
            new SkiaRow
            {
                Spacing = 10,
                Children = new List<SkiaControl>
                {
                    new SkiaSwitch { IsToggled = true, AccessibilityLabel = "Tooltips from accessibility text", VerticalOptions = LayoutOptions.Center }
                        .ObserveSelf((me, prop) =>
                        {
                            if (prop == nameof(SkiaSwitch.IsToggled))
                                Super.TooltipsFromAccessibility = me.IsToggled;
                        }),
                    new SkiaLabel("Tooltips from accessibility text: the icons above show their AccessibilityLabel") { FontSize = 13, TextColor = Color.Parse("#DEE2E6"), VerticalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RolePresentation },
                },
            },
            new SkiaWrap
            {
                Spacing = 8,
                Children = new List<SkiaControl>
                {
                    new SkiaButton("Publish") { BackgroundColor = Color.Parse("#0D6EFD") }.WithTooltip("Uploads the current version").OnTapped(me => Activated("Publish")),
                    new SkiaButton("Preview") { BackgroundColor = Color.Parse("#0D6EFD") }.WithTooltip("Opens a live preview").OnTapped(me => Activated("Preview")),
                    new SkiaButton("Long text") { BackgroundColor = Color.Parse("#495057") }.WithTooltip("A longer tooltip wraps at the maximum width of 320 points, so a few lines of help stay easy to read.").OnTapped(me => Activated("Long text")),
                },
            });
    }

    /// <summary>An icon button without text: its tooltip is its accessibility label.</summary>
    private SkiaControl TooltipIcon(string svg, string label) => new SkiaShape
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
    .OnTapped(me => Activated(label));

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
