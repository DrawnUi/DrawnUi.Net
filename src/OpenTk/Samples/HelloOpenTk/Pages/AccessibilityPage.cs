using DrawnUi.Models;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloOpenTk.Pages;

/// <summary>
/// Accessibility snippet: the same AccessibilityRole / Label / Hint / IsPressed / Live properties as
/// the other heads feed the engine's <see cref="SkiaAccessibilityManager"/> snapshot. Ported from the
/// React demo's AccessibilityPage.tsx. The OpenTK window publishes the snapshot to screen readers: UI Automation on Windows
/// (WindowsUiaProvider), AT-SPI on Linux (LinuxAtSpiProvider).
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
    private UiTimer _timer;
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
                            new SkiaLabel("OpenTK head: every node is published to UI Automation on Windows and to AT-SPI on Linux (role, name, help text, value, live regions), so Narrator and Orca read it. Tab, the arrow keys in groups, Enter and Space work as on the other desktop heads.")
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

                        Card("Labels — read by default, opted out per control",
                            new SkiaLabel("This label is announced: SkiaLabel.DefaultAccessibilityRole = Aria.RoleText was set once at startup.") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill },
                            new SkiaLabel("This one is visible but hidden from assistive technology (RolePresentation).") { FontSize = 14, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RolePresentation },
                            new SkiaLabel("Heading level text") { FontSize = 16, FontFamily = "FontTextBold", TextColor = Colors.White, AccessibilityRole = Aria.RoleHeading }),

                        Card("How it works",
                            new SkiaLabel("• Controls with an AccessibilityRole register with the canvas' SkiaAccessibilityManager; the snapshot is rebuilt at most once per second from the arranged rects.\n• Same property names on every head: AccessibilityRole, AccessibilityLabel, AccessibilityHint, AccessibilityCanInteract, AccessibilityIsPressed, AccessibilityLive.\n• Blazor / React mirror the snapshot into an aria overlay; WPF and OpenTK publish it to UI Automation, OpenTK on Linux to AT-SPI.")
                            {
                                FontSize = 13, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),
        };

        // live view of the engine's accessibility snapshot: refreshed on SkiaAccessibilityManager.Changed (see OnLayoutReady),
        // and polled because focus is not part of the snapshot
        _timer = new UiTimer { Interval = TimeSpan.FromMilliseconds(300) };
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

    /// <summary>Raised from the frame that rebuilt the snapshot: hop to the window thread.</summary>
    private void OnSnapshotChanged() => DrawnUi.MainThread.BeginInvokeOnMainThread(RefreshSnapshot);

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
