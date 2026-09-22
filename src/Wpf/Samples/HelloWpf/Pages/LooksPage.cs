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
    private SkiaLabel _last;

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

    private SkiaControl Card(string title, PrebuiltControlStyle style) => new SkiaShape
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
                    new SkiaLabel(title) { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.Parse("#111827") },

                    new SkiaRow
                    {
                        Spacing = 16,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>
                        {
                            new SkiaSwitch { ControlStyle = style, IsToggled = true, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => me.Toggled += (_, v) => Log($"{title} switch: {v}")),
                            new SkiaCheckbox { ControlStyle = style, IsToggled = true, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => me.Toggled += (_, v) => Log($"{title} checkbox: {v}")),
                            new SkiaRadioButton { ControlStyle = style, Text = "One", IsToggled = true, GroupName = title, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => me.Toggled += (_, v) => { if (v) Log($"{title} radio: One"); }),
                            new SkiaRadioButton { ControlStyle = style, Text = "Two", GroupName = title, VerticalOptions = LayoutOptions.Center }
                                .Adapt(me => me.Toggled += (_, v) => { if (v) Log($"{title} radio: Two"); }),
                        },
                    },

                    new SkiaButton("Button") { ControlStyle = style, HorizontalOptions = LayoutOptions.Start }
                        .OnTapped(me => Log($"{title} button tapped")),

                    new SkiaProgress { ControlStyle = style, Value = 65, HorizontalOptions = LayoutOptions.Fill },

                    new SkiaSlider { ControlStyle = style, End = 65, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(me => me.EndChanged += (_, v) => Log($"{title} slider: {v:0}")),

                    // range mode: two thumbs
                    new SkiaSlider { ControlStyle = style, EnableRange = true, Start = 20, End = 80, HorizontalOptions = LayoutOptions.Fill }
                        .Adapt(me =>
                        {
                            me.StartChanged += (_, v) => Log($"{title} range start: {v:0}");
                            me.EndChanged += (_, v) => Log($"{title} range end: {v:0}");
                        }),
                },
            },
        },
    };
}
