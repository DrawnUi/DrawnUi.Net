
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

                        Card("Buttons — AccessibilityRole opts a SkiaButton in: label from Text, hint, disabled",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Tapped 0×") { AccessibilityRole = Aria.RoleButton, BackgroundColor = Color.Parse("#0D6EFD"), AccessibilityHint = "Increments the counter" }
                                        .Assign(out _counter)
                                        .OnTapped(me => { _count++; _counter.Text = $"Tapped {_count}×"; Activated("counter"); }),
                                    new SkiaButton("★") { AccessibilityRole = Aria.RoleButton, FontSize = 18, FontFamily = "FontSymbols2", BackgroundColor = Color.Parse("#6610F2"), WidthRequest = 48, AccessibilityLabel = "Favorite", AccessibilityHint = "Icon-only button: AccessibilityLabel replaces the glyph" }
                                        .OnTapped(me => Activated("favorite")),
                                    new SkiaButton("Disabled") { AccessibilityRole = Aria.RoleButton, BackgroundColor = Color.Parse("#495057"), IsDisabled = true, AccessibilityHint = "IsDisabled: not activatable" },
                                },
                            }),

                        Card("Toggles — AccessibilityIsPressed",
                            new SkiaRow
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Sound: on") { AccessibilityRole = Aria.RoleButton, BackgroundColor = Color.Parse("#20C997"), AccessibilityLabel = "Sound", AccessibilityIsPressed = true }
                                        .Assign(out _sound)
                                        .OnTapped(me =>
                                        {
                                            _soundOn = !_soundOn;
                                            _sound.Text = _soundOn ? "Sound: on" : "Sound: off";
                                            _sound.BackgroundColor = Color.Parse(_soundOn ? "#20C997" : "#495057");
                                            _sound.AccessibilityIsPressed = _soundOn;
                                            Activated("sound");
                                        }),
                                    new SkiaButton("Dark: off") { AccessibilityRole = Aria.RoleButton, BackgroundColor = Color.Parse("#495057"), AccessibilityLabel = "Dark mode", AccessibilityIsPressed = false }
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

                        Card("Labels — opt in per control",
                            new SkiaLabel("This label is a node: AccessibilityRole=Aria.RoleText.") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleText },
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

        // live view of the engine's accessibility snapshot; focus is not part of the snapshot, hence the poll
        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(300);
        _timer.Tick += (_, _) => RefreshSnapshot();
        _timer.Start();
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        _timer?.Stop();
        _timer = null;
        base.OnDisposing();
    }

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
