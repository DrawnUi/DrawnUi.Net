using DrawnUi.Controls;
using DrawnUi.Views;
using HelloMaui.Pages;

namespace HelloMaui;

/// <summary>
/// The app page: DrawnUi.Maui's own <see cref="SkiaShell"/>. Every sample is a registered route that
/// pushes into the root screen's <see cref="SkiaViewSwitcher"/>, wrapped in a <see cref="PageHost{T}"/>
/// that draws the nav bar. Popups, modals and toasts come from the shell itself.
/// </summary>
public class HelloShell : SkiaShell
{
    /// <summary>Canvas and page background, same as the React and WPF demos.</summary>
    public static readonly Color PageBackground = Color.Parse("#212529");

    /// <summary>The running shell, for pages that navigate or open overlays.</summary>
    public static HelloShell Instance { get; private set; }

    /// <summary>Page type of every sample route, in catalog order.</summary>
    public static readonly Dictionary<string, Type> Routes = new()
    {
        ["cells"] = typeof(PageHost<CellsPage>),
        ["uneven"] = typeof(PageHost<UnevenCellsPage>),
        ["images"] = typeof(PageHost<ImagesPage>),
        ["svg"] = typeof(PageHost<SvgPage>),
        ["shapes"] = typeof(PageHost<ShapesPage>),
        ["text"] = typeof(PageHost<TextPage>),
        ["layouts"] = typeof(PageHost<LayoutsPage>),
        ["looks"] = typeof(PageHost<LooksPage>),
        ["snapping"] = typeof(PageHost<SnappingPage>),
        ["animations"] = typeof(PageHost<AnimationsPage>),
        ["shell"] = typeof(PageHost<ShellPage>),
        ["editor"] = typeof(PageHost<EditorPage>),
        ["keyboard"] = typeof(PageHost<KeyboardPage>),
        ["scroll"] = typeof(PageHost<ScrollPage>),
        ["shaders"] = typeof(PageHost<ShadersPage>),
        ["sprites"] = typeof(PageHost<SpritesPage>),
        ["transforms"] = typeof(PageHost<TransformsPage>),
        ["reorder"] = typeof(PageHost<ReorderPage>),
        ["a11y"] = typeof(PageHost<AccessibilityPage>),
    };

    /// <summary>Registers the routes and builds the canvas.</summary>
    public HelloShell()
    {
        Instance = this;
        BackgroundColor = PageBackground;
        SafeAreaEdges = SafeAreaEdges.All; // keep the drawn nav bar clear of status bar / notch / home indicator

        RegisterRoute("root", typeof(RootScreen));
        foreach (var route in Routes)
            RegisterRoute(route.Key, route.Value);

        Content = new Canvas
        {
            Gestures = GesturesMode.Enabled,
            RenderingMode = RenderingModeType.Accelerated,
            BackgroundColor = PageBackground,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayer
            {
                Tag = "ShellLayout",
                Children = new List<SkiaControl>
                {
                    new SkiaControl { Tag = "RootLayout" }, // replaced by the "root" route
                },
            }.Fill(),
        };

        Initialize("root");
    }

    /// <summary>Title of a sample page type, from the catalog.</summary>
    public static string TitleFor(Type pageHost)
    {
        var route = Routes.FirstOrDefault(x => x.Value == pageHost).Key;
        return Catalog.Samples.FirstOrDefault(x => x.Route == route)?.Title ?? route;
    }
}

/// <summary>
/// The shell's root: the sample menu inside the <see cref="SkiaViewSwitcher"/> tagged
/// "NavigationLayout" that the shell pushes pages into.
/// </summary>
public class RootScreen : SkiaLayer
{
    /// <summary>Builds the root.</summary>
    public RootScreen()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaViewSwitcher
            {
                Tag = "NavigationLayout",
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new RootPage
                    {
                        SampleSelected = sample => _ = HelloShell.Instance.GoToAsync(sample.Route),
                    },
                },
            },
        };
    }
}

/// <summary>
/// A pushed sample: nav bar with Back / title / Home over the page content, the same chrome the WPF
/// head's drawn shell builds around every page.
/// </summary>
public class PageHost<T> : SkiaLayer where T : SkiaControl, new()
{
    /// <summary>Nav bar height in points.</summary>
    public const double NavBarHeight = 52;

    /// <summary>Builds the host and its page.</summary>
    public PageHost()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        BackgroundColor = HelloShell.PageBackground;
        BlockGesturesBelow = true;

        Children = new List<SkiaControl>
        {
            new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, NavBarHeight, 0, 0),
                Children = new List<SkiaControl> { new T() },
            },
            new SkiaLayer
            {
                HeightRequest = NavBarHeight,
                BackgroundColor = Color.Parse("#1A1D20"),
                UseCache = SkiaCacheType.Image,
                Children = new List<SkiaControl>
                {
                    new SkiaButton("‹  Back")
                    {
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.Parse("#6EA8FE"),
                        FontSize = 16,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(8, 0, 0, 0),
                    }.OnTapped(me => HelloShell.Instance.GoBack(true)),
                    new SkiaLabel(HelloShell.TitleFor(GetType()))
                    {
                        FontSize = 18,
                        FontFamily = "FontTextBold",
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Fill,
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        MaxLines = 1,
                        Margin = new Thickness(96, 0, 96, 0),
                    },
                    new SkiaButton("Home")
                    {
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.Parse("#6EA8FE"),
                        FontSize = 16,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                    }.OnTapped(me => _ = HelloShell.Instance.PopToRootAsync()),
                    new SkiaLayer
                    {
                        HeightRequest = 1,
                        VerticalOptions = LayoutOptions.End,
                        BackgroundColor = Color.Parse("#343A40"),
                    },
                },
            },
        };
    }
}
