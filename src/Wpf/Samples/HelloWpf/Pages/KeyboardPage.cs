using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Port of the Blazor sandbox KeyboardProbe page via the React demo's KeyboardPage.tsx:
/// KeyboardManager.KeyDown / KeyUp / KeyChar with modifier state and a history. On WPF the
/// DrawnUiElement feeds the manager from its preview key events while it has keyboard focus.
/// </summary>
public class KeyboardPage : SkiaLayer
{
    private const string Waiting = "Waiting for input";

    private SkiaLabel _hero;
    private SkiaLabel _last;
    private SkiaLabel _modifiers;
    private SkiaLabel _chars;
    private readonly List<SkiaLabel> _history = new();
    private readonly List<string> _events = new();
    private string _typed = "";

    /// <summary>Builds the page and subscribes to the keyboard manager.</summary>
    public KeyboardPage()
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
                        new SkiaLabel("Keyboard Input") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                        new SkiaLabel("KeyboardManager is fed by DrawnUiElement's preview key events (the WPF equivalent of the Blazor / Wasm window listeners): shortcuts, game input, drawn editors. Click the canvas, then press letters, arrows, modifiers or function keys.")
                        {
                            FontSize = 13, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        // the Blazor probe canvas: cream card, blue banner, last key, modifiers, recent events
                        new SkiaStack
                        {
                            Spacing = 14,
                            Padding = new Thickness(20),
                            BackgroundColor = Color.Parse("#FEFDF6"),
                            HorizontalOptions = LayoutOptions.Fill,
                            Children = new List<SkiaControl>
                            {
                                new SkiaLabel("Keyboard input ready") { FontSize = 28, FontFamily = "FontTextBold", TextColor = Color.Parse("#252B37"), HorizontalOptions = LayoutOptions.Fill }.Assign(out _hero),
                                new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    CornerRadius = 18,
                                    BackgroundColor = Color.Parse("#3C639F"),
                                    HeightRequest = 92,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Padding = new Thickness(16),
                                    Children = new List<SkiaControl>
                                    {
                                        new SkiaLabel("Press letters, arrows, modifiers, or function keys") { FontSize = 18, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center },
                                    },
                                },
                                new SkiaLabel("Last key: waiting") { FontSize = 16, TextColor = Color.Parse("#41495A"), HorizontalOptions = LayoutOptions.Fill }.Assign(out _last),
                                new SkiaLabel("Modifiers: shift False, ctrl False, alt False") { FontSize = 14, TextColor = Color.Parse("#636F80"), HorizontalOptions = LayoutOptions.Fill }.Assign(out _modifiers),
                                new SkiaLabel("KeyChar (printable, no Ctrl/Alt): \"\"") { FontSize = 14, TextColor = Color.Parse("#636F80"), HorizontalOptions = LayoutOptions.Fill }.Assign(out _chars),
                                new SkiaStack
                                {
                                    Spacing = 8,
                                    Padding = new Thickness(14),
                                    BackgroundColor = Color.Parse("#F2EDE0"),
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Children = new List<SkiaControl>
                                    {
                                        new SkiaLabel("Recent events") { FontSize = 18, FontFamily = "FontTextBold", TextColor = Color.Parse("#47321C"), HorizontalOptions = LayoutOptions.Fill },
                                    }
                                    .Concat(Enumerable.Range(0, 5).Select(_ => (SkiaControl)new SkiaLabel(Waiting) { FontSize = 14, TextColor = Color.Parse("#5C4A35"), HorizontalOptions = LayoutOptions.Fill }.Adapt(l => _history.Add(l))))
                                    .ToList(),
                                },
                            },
                        },
                    },
                },
            }.Fill(),
        };

        KeyboardManager.KeyDown += OnKeyDown;
        KeyboardManager.KeyUp += OnKeyUp;
        KeyboardManager.KeyChar += OnKeyChar;
    }

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        KeyboardManager.KeyDown -= OnKeyDown;
        KeyboardManager.KeyUp -= OnKeyUp;
        KeyboardManager.KeyChar -= OnKeyChar;
        base.OnDisposing();
    }

    private void OnKeyDown(object sender, InputKey key) => Apply("down", key);

    private void OnKeyUp(object sender, InputKey key) => Apply("up", key);

    private void OnKeyChar(object sender, string ch)
    {
        _typed += ch;
        if (_typed.Length > 40)
            _typed = _typed[^40..];
        _chars.Text = $"KeyChar (printable, no Ctrl/Alt): \"{_typed}\"";
    }

    private void Apply(string phase, InputKey key)
    {
        _hero.Text = "Keyboard probe live";
        _last.Text = $"Last key: {phase} {key}";
        _modifiers.Text = $"Modifiers: shift {KeyboardManager.IsShiftPressed}, ctrl {KeyboardManager.IsControlPressed}, alt {KeyboardManager.IsAltPressed}";
        _events.Insert(0, $"{phase} {key}");
        if (_events.Count > 5)
            _events.RemoveAt(5);
        for (var i = 0; i < _history.Count; i++)
            _history[i].Text = i < _events.Count ? _events[i] : Waiting;
    }
}
