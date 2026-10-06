using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Controls;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Port of the Fiddle "Looks" preset via the React demo's LooksPage.tsx: one card per prebuilt
/// style, the same controls in each, only ControlStyle changes.
/// </summary>
public class LooksPage : SkiaLayer
{
    private static readonly PrebuiltControlStyle[] Styles =
    {
        PrebuiltControlStyle.Unset, PrebuiltControlStyle.Windows, PrebuiltControlStyle.Cupertino,
        PrebuiltControlStyle.Material, PrebuiltControlStyle.Material3,
    };

    private SkiaLabel _last;
    private SkiaButton _liveButton;
    private SkiaLabel _liveTitle;
    private readonly List<SkiaControl> _liveControls = new();
    private int _live;

    /// <summary>Builds the page.</summary>
    public LooksPage()
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
                        new SkiaLabel("Common Controls") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                        new SkiaLabel("SkiaSwitch, SkiaCheckbox, SkiaRadioButton, SkiaButton, SkiaProgress, SkiaSlider — the same tree per card, only ControlStyle changes (the Fiddle 'Looks' snippet).")
                        {
                            FontSize = 13, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },
                        new SkiaLabel("Last: interact with any control") { FontSize = 13, TextColor = Color.Parse("#6EA8FE"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center }.Assign(out _last),
                        // a live card: ControlStyle changes on an already built tree rebuild the default content
                        // centered inside a Fill row: a Center child would make this auto-width stack as
                        // narrow as the button and squeeze every Fill card below it
                        new SkiaLayer
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            Children = new List<SkiaControl>
                            {
                                new SkiaButton(LiveButtonText()) { HorizontalOptions = LayoutOptions.Center }
                                    .Assign(out _liveButton)
                                    .OnTapped(me => SwitchLiveStyle()),
                            },
                        },
                        Card($"Live — {Styles[0]}", Styles[0], _liveControls, title => _liveTitle = title),
                        Card("Default", PrebuiltControlStyle.Unset),
                        Card("Windows — Fluent", PrebuiltControlStyle.Windows),
                        Card("Cupertino — iOS", PrebuiltControlStyle.Cupertino),
                        Card("Material — Android", PrebuiltControlStyle.Material),
                        Card("Material3 — Android", PrebuiltControlStyle.Material3),
                    },
                },
            }.Fill(),
        };
    }

    private void Log(string message) => _last.Text = $"Last: {message}";

    private static string Bool(bool value) => value ? "true" : "false";

    private string LiveButtonText() => $"Live card: {Styles[_live]} — tap to switch style";

    private void SwitchLiveStyle()
    {
        _live = (_live + 1) % Styles.Length;
        _liveButton.Text = LiveButtonText();
        _liveTitle.Text = $"Live — {Styles[_live]}";
        foreach (var control in _liveControls)
            control.ControlStyle = Styles[_live];
    }

    /// <summary>
    /// One card of the same controls in a given style. <paramref name="styled"/> collects the styled
    /// controls and <paramref name="titleOut"/> receives the title label, for the live card.
    /// </summary>
    private SkiaControl Card(string title, PrebuiltControlStyle style, List<SkiaControl> styled = null, Action<SkiaLabel> titleOut = null)
    {
        string Title() => styled != null && _liveTitle != null ? _liveTitle.Text : title;

        return BuildCard(title, style, styled, titleOut, Title);
    }

    private SkiaControl BuildCard(string title, PrebuiltControlStyle style, List<SkiaControl> styled, Action<SkiaLabel> titleOut, Func<string> Title) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 16,
        BackgroundColor = Color.Parse("#F5F5F5"),
        Padding = new Thickness(18, 14),
        HorizontalOptions = LayoutOptions.Fill,
        UseCache = SkiaCacheType.Image,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 14,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel(title) { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.Parse("#111827") }
                        .Adapt(me => titleOut?.Invoke(me)),

                    new SkiaRow
                    {
                        Spacing = 16,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>
                        {
                            new SkiaSwitch { ControlStyle = style, AccessibilityLabel = "Wi-Fi", IsToggled = true, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => { styled?.Add(me); me.Toggled += (_, v) => Log($"{Title()} switch: {Bool(v)}"); }),
                            new SkiaCheckbox { ControlStyle = style, AccessibilityLabel = "Remember me", IsToggled = true, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => { styled?.Add(me); me.Toggled += (_, v) => Log($"{Title()} checkbox: {Bool(v)}"); }),
                            new SkiaRadioButton { ControlStyle = style, Text = "One", IsToggled = true, GroupName = title, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => { styled?.Add(me); me.Toggled += (_, v) => { if (v) Log($"{Title()} radio: One"); }; }),
                            new SkiaRadioButton { ControlStyle = style, Text = "Two", GroupName = title, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => { styled?.Add(me); me.Toggled += (_, v) => { if (v) Log($"{Title()} radio: Two"); }; }),
                        },
                    },

                    new SkiaButton("Button") { ControlStyle = style, HorizontalOptions = LayoutOptions.Start }
                        .Adapt(me => styled?.Add(me))
                        .OnTapped(me => Log($"{Title()} button tapped")),

                    new SkiaProgress { ControlStyle = style, AccessibilityLabel = "Download", Value = 65, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(me => styled?.Add(me)),

                    new SkiaSlider { ControlStyle = style, AccessibilityLabel = "Volume", End = 65, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(me => { styled?.Add(me); me.EndChanged += (_, v) => Log($"{Title()} slider: {v:0}"); }),

                    // range mode: two thumbs
                    new SkiaSlider { ControlStyle = style, AccessibilityLabel = "Price range", EnableRange = true, Start = 20, End = 80, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(me =>
                        {
                            styled?.Add(me);
                            me.StartChanged += (_, v) => Log($"{Title()} range start: {v:0}");
                            me.EndChanged += (_, v) => Log($"{Title()} range end: {v:0}");
                        }),
                },
            },
        },
    };
}
