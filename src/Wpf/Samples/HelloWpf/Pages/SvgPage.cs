using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// SkiaSvg: file source, inline SvgString, TintColor, LockRatio sizing.
/// Ported from the React demo's SvgPage.tsx.
/// </summary>
public class SvgPage : SkiaLayer
{
    private const string Star =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#FFD700" d="M12 2l3.09 6.26L22 9.27l-5 4.87L18.18 21 12 17.77 5.82 21 7 14.14l-5-4.87 6.91-1.01z"/></svg>""";

    /// <summary>Builds the page.</summary>
    public SvgPage()
    {
        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("SkiaSvg")
                        {
                            FontSize = 24,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Center,
                        },
                        new SkiaSvg
                        {
                            Source = "images/drawnui.svg",
                            WidthRequest = 200,
                            LockRatio = 1,
                            HorizontalOptions = LayoutOptions.Center,
                        },
                        new SkiaLabel("Source=\"images/drawnui.svg\" WidthRequest=200 LockRatio=1")
                        {
                            FontSize = 12,
                            TextColor = Color.Parse("#94A3B8"),
                            HorizontalOptions = LayoutOptions.Center,
                        },

                        new SkiaLabel("TintColor")
                        {
                            FontSize = 20,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Center,
                            Margin = new Thickness(0, 12, 0, 0),
                        },
                        new SkiaRow
                        {
                            Spacing = 24,
                            HorizontalOptions = LayoutOptions.Center,
                            Children = new List<SkiaControl>
                            {
                                Tinted(Colors.White),
                                Tinted(Color.Parse("#FF6B6B")),
                                Tinted(Color.Parse("#4ECDC4")),
                                Tinted(Color.Parse("#FFD93D")),
                            },
                        },

                        new SkiaLabel("SvgString (inline markup) at three sizes")
                        {
                            FontSize = 20,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Center,
                            Margin = new Thickness(0, 12, 0, 0),
                        },
                        new SkiaRow
                        {
                            Spacing = 24,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Children = new List<SkiaControl>
                            {
                                StarAt(32),
                                StarAt(64),
                                StarAt(128),
                            },
                        },
                        new SkiaLabel("Rasterized at the displayed pixel size, re-rasterized only when that size changes.")
                        {
                            FontSize = 12,
                            TextColor = Color.Parse("#94A3B8"),
                            HorizontalOptions = LayoutOptions.Center,
                        },
                    },
                },
            }.Fill(),
        };
    }

    private static SkiaSvg Tinted(Color tint) => new()
    {
        Source = "images/drawnui.svg",
        WidthRequest = 72,
        LockRatio = 1,
        TintColor = tint,
    };

    private static SkiaSvg StarAt(double size) => new()
    {
        SvgString = Star,
        WidthRequest = size,
        LockRatio = 1,
        VerticalOptions = LayoutOptions.Center,
    };
}
