using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace DrawnUi.Draw;

/// <summary>Arguments carried by a route, parsed from its query string.</summary>
public class ShellArguments : Dictionary<string, string>
{
    /// <summary>Reads an argument, or null when it was not supplied.</summary>
    public string Get(string key) => TryGetValue(key, out var value) ? value : null;
}

/// <summary>Where a navigation came from.</summary>
public enum NavigationSource
{
    /// <summary>A page was pushed.</summary>
    Push,

    /// <summary>A page was popped.</summary>
    Pop,
}

/// <summary>Raised before a navigation happens; set <see cref="Cancel"/> to stop it.</summary>
public class ShellNavigatingArgs : EventArgs
{
    /// <summary>The route being navigated to, or away from on a pop.</summary>
    public string Route { get; init; }

    /// <summary>Push or pop.</summary>
    public NavigationSource Source { get; init; }

    /// <summary>Set to true to cancel the navigation.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Raised after a navigation completed.</summary>
public class ShellNavigatedArgs : EventArgs
{
    /// <summary>The route that is now on top, empty for the root.</summary>
    public string Route { get; init; }

    /// <summary>Push or pop.</summary>
    public NavigationSource Source { get; init; }
}

/// <summary>
/// Drawn navigation host for the WPF head, mirroring the React head's SkiaShell: the root content
/// plus a stack of pushed pages inside one <see cref="SkiaLayer"/>, each pushed page carrying a nav
/// bar with Back and Home, sliding in from the right and covering what is below.
/// <para>
/// The MAUI head's SkiaShell is a ContentPage and cannot be reused off MAUI, which is why every head
/// brings its own — this one is a plain control, so it drops into any drawn tree.
/// </para>
/// </summary>
public class SkiaShell : SkiaLayer
{
    private readonly List<string> _stack = new();
    private readonly Dictionary<string, SkiaControl> _hosts = new();
    private SkiaControl _rootHost;
    private bool _navigating;

    /// <summary>Page factories by route name. The factory receives the route's parsed arguments.</summary>
    public Dictionary<string, Func<ShellArguments, SkiaControl>> Routes { get; set; } = new();

    /// <summary>Nav bar titles by route name. The route name itself is used when absent.</summary>
    public Dictionary<string, string> Titles { get; set; } = new();

    /// <summary>Height of the nav bar on pushed pages, in points.</summary>
    public double NavBarHeight { get; set; } = 56;

    /// <summary>Nav bar background.</summary>
    public Color NavBarColor { get; set; } = Color.Parse("#212529");

    /// <summary>Background painted behind every pushed page, so it covers what is below.</summary>
    public Color PageBackgroundColor { get; set; } = Color.Parse("#212529");

    /// <summary>Push/pop slide duration in milliseconds.</summary>
    public int PagesAnimationSpeed { get; set; } = 200;

    /// <summary>True when there is something to go back to.</summary>
    public bool CanGoBack => _stack.Count > 0;

    /// <summary>The routes currently pushed, root excluded.</summary>
    public IReadOnlyList<string> NavigationStack => _stack;

    /// <summary>Raised before navigating; cancellable.</summary>
    public event EventHandler<ShellNavigatingArgs> Navigating;

    /// <summary>Raised after navigating.</summary>
    public event EventHandler<ShellNavigatedArgs> Navigated;

    /// <summary>Creates the shell.</summary>
    public SkiaShell()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }

    /// <summary>
    /// The root content, shown when nothing is pushed. Assign it instead of adding children directly,
    /// so the shell can keep it beneath the pushed pages.
    /// </summary>
    public SkiaControl RootContent
    {
        get => _rootHost;
        set
        {
            if (_rootHost != null)
                RemoveSubView(_rootHost);

            _rootHost = value;

            if (_rootHost != null)
            {
                _rootHost.ZIndex = 0;
                AddSubView(_rootHost);
            }
        }
    }

    /// <summary>Splits "name?a=1&amp;b=2" into its name and arguments.</summary>
    public static (string Name, ShellArguments Arguments) SplitRoute(string route)
    {
        var arguments = new ShellArguments();
        if (string.IsNullOrEmpty(route))
            return (string.Empty, arguments);

        var split = route.Split('?', 2);
        if (split.Length == 2)
        {
            foreach (var pair in split[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                arguments[Uri.UnescapeDataString(kv[0])] = kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            }
        }

        return (split[0], arguments);
    }

    /// <summary>Pushes a route onto the stack.</summary>
    public async Task GoToAsync(string route, bool animated = true)
    {
        if (_navigating || string.IsNullOrEmpty(route))
            return;

        var (name, arguments) = SplitRoute(route);
        if (!Routes.TryGetValue(name, out var factory))
        {
            Super.Log($"[SkiaShell] no route registered for '{name}'");
            return;
        }

        var navigating = new ShellNavigatingArgs { Route = route, Source = NavigationSource.Push };
        Navigating?.Invoke(this, navigating);
        if (navigating.Cancel)
            return;

        _navigating = true;
        try
        {
            var host = BuildPageHost(name, factory(arguments));
            _stack.Add(route);
            _hosts[route] = host;

            host.ZIndex = _stack.Count;
            AddSubView(host);

            if (animated)
            {
                // Off to the right, then in — the same push feel as the React head and SkiaViewSwitcher.
                host.TranslationX = CanvasWidthPoints();
                await host.TranslateToAsync(0, 0, (uint)PagesAnimationSpeed, Easing.CubicOut);
            }

            Navigated?.Invoke(this, new ShellNavigatedArgs { Route = route, Source = NavigationSource.Push });
        }
        finally
        {
            _navigating = false;
        }
    }

    /// <summary>Pops the top page.</summary>
    public async Task GoBackAsync(bool animated = true)
    {
        if (_navigating || _stack.Count == 0)
            return;

        var route = _stack[^1];
        var navigating = new ShellNavigatingArgs { Route = route, Source = NavigationSource.Pop };
        Navigating?.Invoke(this, navigating);
        if (navigating.Cancel)
            return;

        _navigating = true;
        try
        {
            if (_hosts.TryGetValue(route, out var host))
            {
                if (animated)
                    await host.TranslateToAsync(CanvasWidthPoints(), 0, (uint)PagesAnimationSpeed, Easing.CubicIn);

                RemoveSubView(host);
                host.Dispose();
                _hosts.Remove(route);
            }

            _stack.RemoveAt(_stack.Count - 1);

            Navigated?.Invoke(this, new ShellNavigatedArgs
            {
                Route = _stack.Count > 0 ? _stack[^1] : string.Empty,
                Source = NavigationSource.Pop,
            });
        }
        finally
        {
            _navigating = false;
        }
    }

    /// <summary>Pops everything back to the root content.</summary>
    public async Task PopToRootAsync()
    {
        // Only the last pop animates: the pages below are covered, so animating them is invisible work.
        while (_stack.Count > 1)
            await GoBackAsync(false);

        if (_stack.Count > 0)
            await GoBackAsync();
    }

    private double CanvasWidthPoints()
    {
        var scale = RenderingScale <= 0 ? 1 : RenderingScale;
        var width = DrawingRect.Width / scale;
        return width > 0 ? width : 400;
    }

    private SkiaControl BuildPageHost(string name, SkiaControl content)
    {
        var title = Titles.TryGetValue(name, out var known) ? known : name;

        return new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = PageBackgroundColor,
            Children = new List<SkiaControl>
            {
                // Content sits below the bar, which is drawn over it — same order as the React head.
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(0, NavBarHeight, 0, 0),
                    Children = new List<SkiaControl> { content },
                },
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = NavBarHeight,
                    VerticalOptions = LayoutOptions.Start,
                    BackgroundColor = NavBarColor,
                    Children = new List<SkiaControl>
                    {
                        new SkiaButton("‹  Back")
                        {
                            // Not BackgroundColor: SkiaControl already defaults it to Transparent, so
                            // assigning Transparent raises no change, SkiaButton's backgroundColorChanged
                            // stays false and it paints its crimson accent anyway. A non-null Background
                            // brush is the path the control actually honours (InitialBackground).
                            Background = new SolidColorBrush(Colors.Transparent),
                            TextColor = Color.Parse("#6EA8FE"),
                            FontSize = 16,
                            VerticalOptions = LayoutOptions.Center,
                            Margin = new Thickness(8, 0, 0, 0),
                        }.OnTapped(me => { _ = GoBackAsync(); }),

                        new SkiaLabel(title)
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
                            Background = new SolidColorBrush(Colors.Transparent),
                            TextColor = Color.Parse("#6EA8FE"),
                            FontSize = 16,
                            HorizontalOptions = LayoutOptions.End,
                            VerticalOptions = LayoutOptions.Center,
                            Margin = new Thickness(0, 0, 8, 0),
                        }.OnTapped(me => { _ = PopToRootAsync(); }),

                        new SkiaLayer
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 1,
                            VerticalOptions = LayoutOptions.End,
                            BackgroundColor = Color.Parse("#343A40"),
                        },
                    },
                },
            },
        };
    }
}
