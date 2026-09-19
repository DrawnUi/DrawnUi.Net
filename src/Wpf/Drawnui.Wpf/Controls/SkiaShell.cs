using DrawnUi.Controls;
using DrawnUi.Views;
using SkiaSharp;
using Color = DrawnUi.Color;

namespace DrawnUi.Draw;

/// <summary>Arguments carried by a route: its query string plus whatever <see cref="SkiaShell.GoToAsync(string, bool, IDictionary{string, object})"/> was given.</summary>
public class ShellArguments : Dictionary<string, object>
{
    /// <summary>Reads an argument as text, or null when it was not supplied.</summary>
    public string Get(string key) => TryGetValue(key, out var value) ? value?.ToString() : null;
}

/// <summary>Where a navigation came from.</summary>
public enum NavigationSource
{
    /// <summary>A page, popup or modal was pushed.</summary>
    Push,

    /// <summary>A page, popup or modal was popped.</summary>
    Pop,
}

/// <summary>A shell tab: its root route and the label shown in the tab bar.</summary>
public record ShellTab(string Route, string Title);

/// <summary>Raised before a navigation happens; set <see cref="Cancel"/> to stop it.</summary>
public class ShellNavigatingArgs : EventArgs
{
    /// <summary>The route being navigated to, or away from on a pop.</summary>
    public string Route { get; init; }

    /// <summary>Push or pop.</summary>
    public NavigationSource Source { get; init; }

    /// <summary>The view involved, when it already exists (pops, popups, modals).</summary>
    public SkiaControl View { get; init; }

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

    /// <summary>The view that was pushed, or the popup / modal content.</summary>
    public SkiaControl View { get; init; }
}

/// <summary>
/// Drawn navigation host for the WPF head, the same verbs as the MAUI and React SkiaShell: pages
/// pushed with a slide from the right over a root (or over per-tab roots with a tab bar), popups
/// centred over a dimmed backdrop, modals as full-screen drawers from the bottom, toasts rising from
/// the bottom edge. Everything is a plain drawn tree inside one <see cref="SkiaLayer"/>, so it drops
/// into any canvas.
/// </summary>
public class SkiaShell : SkiaLayer
{
    /// <summary>Dim colour behind popups and modals.</summary>
    public static Color PopupBackgroundColor = Color.Parse("#66000000");

    /// <summary>Blur applied to the page behind a popup or modal.</summary>
    public static double PopupsBackgroundBlur = 6;

    /// <summary>Popup open/close animation length in milliseconds.</summary>
    public static int PopupsAnimationSpeed = 250;

    /// <summary>Toast background.</summary>
    public static Color ToastBackgroundColor = Color.Parse("#CC000000");

    /// <summary>Toast text colour.</summary>
    public static Color ToastTextColor = Colors.White;

    /// <summary>Toast text size.</summary>
    public static double ToastTextSize = 16;

    /// <summary>Toast text margins.</summary>
    public static double ToastTextMargins = 24;

    /// <summary>Tab bar label colour.</summary>
    public static Color TabColor = Color.Parse("#ADB5BD");

    /// <summary>Selected tab colour.</summary>
    public static Color TabSelectedColor = Color.Parse("#6EA8FE");

    /// <summary>ZIndex of modals; popups and toasts stack above.</summary>
    public static int ZIndexModals = 1000;

    /// <summary>ZIndex of popups.</summary>
    public static int ZIndexPopups = 2000;

    /// <summary>ZIndex of toasts.</summary>
    public static int ZIndexToasts = 3000;

    private sealed class PageEntry
    {
        public string Route;
        public ShellArguments Arguments;
        public SkiaControl Host;
        public SkiaControl View;
    }

    private sealed class Overlay
    {
        public SkiaControl Layer;
        public SkiaControl Content;
        public SkiaDrawer Drawer;
        public bool Closing;
        public bool DisappearingSent;
    }

    private readonly List<SkiaLayer> _tabContainers = new();
    private readonly List<SkiaControl> _tabRoots = new();
    private readonly List<List<PageEntry>> _stacks = new();
    private readonly List<Overlay> _popups = new();
    private readonly List<Overlay> _modals = new();
    private readonly List<Overlay> _toasts = new();
    private readonly List<SkiaLabel> _tabLabels = new();
    private readonly List<SkiaShape> _tabMarkers = new();
    private SkiaControl _rootContent;
    private SkiaLayer _tabBar;
    private int _selectedTab;
    private int _nextId;
    private bool _navigating;
    private string _lastRoute = "";

    /// <summary>Page factories by route name. The factory receives the route's arguments.</summary>
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

    /// <summary>Tabs; when set, each tab's route is its root and keeps its own page stack. Set before the shell is shown.</summary>
    public List<ShellTab> Tabs { get; set; }

    /// <summary>Tab bar height in points.</summary>
    public double TabBarHeight { get; set; } = 56;

    /// <summary>Tab bar background.</summary>
    public Color TabBarColor { get; set; } = Color.Parse("#212529");

    /// <summary>Slide + fade between tabs.</summary>
    public bool AnimateTabs { get; set; }

    /// <summary>Tab switch duration in milliseconds.</summary>
    public int TabsAnimationSpeed { get; set; } = 150;

    /// <summary>Index of the selected tab.</summary>
    public int SelectedTab => _selectedTab;

    /// <summary>True when there is something to go back from: a popup, a modal or a pushed page.</summary>
    public bool CanGoBack => _popups.Count > 0 || _modals.Count > 0 || Stack.Count > 0;

    /// <summary>The routes pushed in the selected tab, root excluded.</summary>
    public IReadOnlyList<string> NavigationStack => Stack.Select(p => p.Route).ToList();

    /// <summary>The route on top of the selected tab, empty for the root.</summary>
    public string Route => Stack.Count > 0
        ? Stack[^1].Route
        : Tabs is { Count: > 0 } ? Tabs[_selectedTab].Route : "";

    /// <summary>Arguments of the route on top.</summary>
    public ShellArguments Arguments => Stack.Count > 0 ? Stack[^1].Arguments : new ShellArguments();

    /// <summary>Open popups.</summary>
    public int PopupsCount => _popups.Count;

    /// <summary>Open modals.</summary>
    public int ModalsCount => _modals.Count;

    /// <summary>Visible toasts.</summary>
    public int ToastsCount => _toasts.Count;

    /// <summary>Raised before navigating; cancellable.</summary>
    public event EventHandler<ShellNavigatingArgs> Navigating;

    /// <summary>Raised after navigating.</summary>
    public event EventHandler<ShellNavigatedArgs> Navigated;

    /// <summary>Raised when the top route changes (push, pop, tab switch).</summary>
    public event EventHandler<string> RouteChanged;

    /// <summary>Creates the shell.</summary>
    public SkiaShell()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }

    /// <summary>
    /// The root content, shown when nothing is pushed and no <see cref="Tabs"/> are set. Assign it
    /// instead of adding children directly, so the shell can keep it beneath the pushed pages.
    /// </summary>
    public SkiaControl RootContent
    {
        get => _rootContent;
        set
        {
            _rootContent = value;
            EnsureContainers();
            if (Tabs == null || Tabs.Count == 0)
                SetRoot(0, value);
        }
    }

    /// <summary>A tabbed shell has no RootContent to trigger the containers, so they are built on the first layout.</summary>
    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();
        EnsureContainers();
    }

    private List<PageEntry> Stack
    {
        get
        {
            EnsureContainers();
            return _stacks[_selectedTab];
        }
    }

    private double BottomInset => Tabs is { Count: > 0 } ? TabBarHeight : 0;

    /// <summary>One container per tab (one without tabs) holds that tab's root and its pushed pages.</summary>
    private void EnsureContainers()
    {
        HookHotReload();
        if (_tabContainers.Count > 0)
            return;

        if (Tabs is { Count: > 0 } && Routes.Count == 0)
            return; // routes not attached yet

        var count = Tabs is { Count: > 0 } ? Tabs.Count : 1;
        for (var i = 0; i < count; i++)
        {
            var container = new SkiaLayer
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 0, 0, BottomInset),
                IsVisible = i == 0,
                ZIndex = 0,
                // tabs cover each other while one slides over the other
                BackgroundColor = Tabs is { Count: > 0 } ? PageBackgroundColor : null,
            };
            _tabContainers.Add(container);
            _tabRoots.Add(null);
            _stacks.Add(new List<PageEntry>());
            AddSubView(container);
        }

        if (Tabs is { Count: > 0 })
        {
            for (var i = 0; i < Tabs.Count; i++)
                SetRoot(i, BuildRoute(Tabs[i].Route, new ShellArguments()));

            BuildTabBar();
        }
    }

    #region Hot reload

    /// <summary>
    /// Builds the root content. Set it instead of <see cref="RootContent"/> when the root should be
    /// rebuilt by C# Hot Reload too (an instance cannot be re-created, a builder can).
    /// </summary>
    public Func<SkiaControl> RootBuilder
    {
        get => _rootBuilder;
        set
        {
            _rootBuilder = value;
            if (value != null)
                RootContent = value();
        }
    }

    private Func<SkiaControl> _rootBuilder;
    private bool _hotReloadHooked;

    private void HookHotReload()
    {
        if (_hotReloadHooked)
            return;

        _hotReloadHooked = true;
        Super.HotReload += OnHotReload;
    }

    private void OnHotReload(Type[] types) => MainThread.BeginInvokeOnMainThread(ReloadVisiblePage);

    /// <inheritdoc/>
    public override void OnDisposing()
    {
        // Super.HotReload is static: a shell left subscribed would never be collected
        if (_hotReloadHooked)
        {
            _hotReloadHooked = false;
            Super.HotReload -= OnHotReload;
        }

        base.OnDisposing();
    }

    /// <summary>
    /// Re-creates the page the user is looking at from its route factory (or the root from
    /// <see cref="RootBuilder"/> / its tab route), keeping the navigation stack, tab and arguments.
    /// Called after C# Hot Reload; page state is lost, navigation state is not.
    /// </summary>
    public void ReloadVisiblePage()
    {
        if (IsDisposed || _navigating || _tabContainers.Count == 0)
            return;

        var stack = Stack;
        if (stack.Count > 0)
        {
            var entry = stack[^1];
            var (name, _) = SplitRoute(entry.Route);
            var view = BuildRoute(name, entry.Arguments);
            if (view == null)
                return;

            var host = BuildPageHost(name, view);
            host.ZIndex = entry.Host.ZIndex;

            var oldHost = entry.Host;
            var oldView = entry.View;
            SendDisappearing(oldView);
            SendAppearing(view);
            entry.Host = host;
            entry.View = view;
            _tabContainers[_selectedTab].AddSubView(host);
            _tabContainers[_selectedTab].RemoveSubView(oldHost);
            OnLayersChanged(oldView);
            oldHost.Dispose();
            return;
        }

        if (Tabs is { Count: > 0 })
            SetRoot(_selectedTab, BuildRoute(Tabs[_selectedTab].Route, new ShellArguments()));
        else if (_rootBuilder != null)
            RootContent = _rootBuilder();
    }

    #endregion

    #region Visibility (IVisibilityAware)

    private SkiaControl _topmost;
    private bool _holdLayers;
    private readonly object _lockLayers = new();

    /// <summary>The view the user is looking at: top popup, else top modal, else the page on top of the selected tab.</summary>
    public SkiaControl GetTopmostView()
    {
        if (_popups.Count > 0)
            return _popups[^1].Content;

        if (_modals.Count > 0)
            return _modals[^1].Content;

        if (_stacks.Count == 0)
            return null;

        var stack = _stacks[Math.Clamp(_selectedTab, 0, _stacks.Count - 1)];
        return stack.Count > 0 ? stack[^1].View : _tabRoots[Math.Clamp(_selectedTab, 0, _tabRoots.Count - 1)];
    }

    /// <summary>
    /// Same contract as the MAUI shell: a view gets <c>OnAppearing</c> before it is added and
    /// <c>OnDisappearing</c> when its closing starts; whenever the layers change, the view that stopped
    /// being topmost gets <c>OnDisappeared</c> and the new topmost one <c>OnAppeared</c>. A removed view
    /// that was not topmost still gets its <c>OnDisappeared</c>. Must run before the removed view is disposed.
    /// </summary>
    protected virtual void OnLayersChanged(SkiaControl disappeared = null)
    {
        lock (_lockLayers)
        {
            if (_holdLayers)
            {
                SendDisappeared(disappeared);
                if (ReferenceEquals(_topmost, disappeared))
                    _topmost = null; // already told; the final pass must not repeat it
                return;
            }

            var newTopmost = GetTopmostView();
            if (!ReferenceEquals(_topmost, newTopmost))
            {
                if (ReferenceEquals(_topmost, disappeared))
                    disappeared = null;

                SendDisappeared(_topmost);
                _topmost = newTopmost;
                SendAppeared(_topmost);
            }

            if (disappeared != null)
                SendDisappeared(disappeared);
        }
    }

    private static void SendAppearing(SkiaControl view)
    {
        if (view is IVisibilityAware aware && !view.IsDisposed)
            aware.OnAppearing();
    }

    private static void SendDisappearing(SkiaControl view)
    {
        if (view is IVisibilityAware aware && !view.IsDisposed)
            aware.OnDisappearing();
    }

    // A modal's content lives in a SkiaDrawer, which forwards OnAppeared to its Content once it has
    // opened: the shell must not send that one a second time. The drawer does not forward
    // OnDisappeared on the way out (harness-verified, animated or not), so that stays with the shell.
    private static bool OwnedByDrawer(SkiaControl view) => view.Parent is SkiaDrawer;

    private static void SendAppeared(SkiaControl view)
    {
        if (view is IVisibilityAware aware && !view.IsDisposed && !OwnedByDrawer(view))
            aware.OnAppeared();
    }

    private static void SendDisappeared(SkiaControl view)
    {
        if (view is IVisibilityAware aware && !view.IsDisposed)
            aware.OnDisappeared();
    }

    #endregion

    private void SetRoot(int tab, SkiaControl root)
    {
        var container = _tabContainers[tab];
        if (_tabRoots[tab] != null)
            container.RemoveSubView(_tabRoots[tab]);

        var previous = _tabRoots[tab];
        _tabRoots[tab] = root;
        if (root != null)
        {
            root.ZIndex = 0;
            if (tab == _selectedTab)
                SendAppearing(root);
            container.AddSubView(root);
        }

        OnLayersChanged(previous);
    }

    private SkiaControl BuildRoute(string name, ShellArguments arguments)
    {
        if (!Routes.TryGetValue(name, out var factory))
        {
            Super.Log($"[SkiaShell] no route registered for '{name}'");
            return null;
        }

        return factory(arguments);
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

    private bool NotifyNavigating(string route, NavigationSource source, SkiaControl view = null)
    {
        var args = new ShellNavigatingArgs { Route = route, Source = source, View = view };
        Navigating?.Invoke(this, args);
        return !args.Cancel;
    }

    private void NotifyNavigated(string route, NavigationSource source, SkiaControl view = null)
    {
        Navigated?.Invoke(this, new ShellNavigatedArgs { Route = route, Source = source, View = view });
        RaiseRouteChanged();
    }

    private void RaiseRouteChanged()
    {
        var route = Route;
        if (route == _lastRoute)
            return;

        _lastRoute = route;
        RouteChanged?.Invoke(this, route);
    }

    private double CanvasWidthPoints()
    {
        var scale = RenderingScale <= 0 ? 1 : RenderingScale;
        var width = DrawingRect.Width / scale;
        return width > 0 ? width : 400;
    }

    /// <summary>Waits until a control has been laid out once, so its size is known.</summary>
    private static async Task WaitForLayout(SkiaControl control)
    {
        for (var i = 0; i < 60 && control.DrawingRect.Height <= 0 && !control.IsDisposed; i++)
            await Task.Delay(16);
    }

    #region Pages

    /// <summary>Pushes a route onto the selected tab's stack. Extra arguments reach the page factory next to the query ones.</summary>
    public async Task GoToAsync(string route, bool animated = true, IDictionary<string, object> arguments = null)
    {
        if (_navigating || string.IsNullOrEmpty(route))
            return;

        var (name, args) = SplitRoute(route);
        if (arguments != null)
        {
            foreach (var pair in arguments)
                args[pair.Key] = pair.Value;
        }

        if (!Routes.ContainsKey(name))
        {
            Super.Log($"[SkiaShell] no route registered for '{name}'");
            return;
        }

        if (!NotifyNavigating(route, NavigationSource.Push))
            return;

        _navigating = true;
        try
        {
            var view = BuildRoute(name, args);
            var host = BuildPageHost(name, view);
            var stack = Stack;
            stack.Add(new PageEntry { Route = route, Arguments = args, Host = host, View = view });

            host.ZIndex = stack.Count;
            SendAppearing(view);
            _tabContainers[_selectedTab].AddSubView(host);

            if (animated)
            {
                // Off to the right, then in — the same push feel as the React head and SkiaViewSwitcher.
                host.TranslationX = CanvasWidthPoints();
                await host.TranslateToAsync(0, 0, (uint)PagesAnimationSpeed, Easing.CubicOut);
            }

            OnLayersChanged();
            NotifyNavigated(route, NavigationSource.Push, view);
        }
        finally
        {
            _navigating = false;
        }
    }

    private async Task<bool> PopPageAsync(bool animated)
    {
        var stack = Stack;
        if (_navigating || stack.Count == 0)
            return false;

        var entry = stack[^1];
        if (!NotifyNavigating(entry.Route, NavigationSource.Pop, entry.View))
            return false;

        _navigating = true;
        try
        {
            SendDisappearing(entry.View);
            if (animated)
                await entry.Host.TranslateToAsync(CanvasWidthPoints(), 0, (uint)PagesAnimationSpeed, Easing.CubicIn);

            _tabContainers[_selectedTab].RemoveSubView(entry.Host);
            stack.Remove(entry);
            OnLayersChanged(entry.View); // before Dispose: a disposed view is not notified
            entry.Host.Dispose();

            NotifyNavigated(Route, NavigationSource.Pop);
            return true;
        }
        finally
        {
            _navigating = false;
        }
    }

    /// <summary>Closes the top popup, else the top modal, else pops the top page — the MAUI GoBack order.</summary>
    public async Task GoBackAsync(bool animated = true)
    {
        if (_popups.Count > 0)
        {
            await ClosePopupAsync(animated);
            return;
        }

        if (_modals.Count > 0)
        {
            await PopModalAsync(animated);
            return;
        }

        await PopPageAsync(animated);
    }

    /// <summary>Pops every page in every tab back to its root.</summary>
    public async Task PopToRootAsync()
    {
        var selected = _selectedTab;
        _holdLayers = true; // the loop borrows _selectedTab: no appeared / disappeared for hidden tabs
        try
        {
            for (var tab = 0; tab < _stacks.Count; tab++)
            {
                _selectedTab = tab;
                await PopTabToRootAsync(tab == selected);
            }
        }
        finally
        {
            _selectedTab = selected;
            _holdLayers = false;
        }

        OnLayersChanged();
        RaiseRouteChanged();
    }

    /// <summary>Pops every page of the selected tab back to its root.</summary>
    public async Task PopTabToRootAsync() => await PopTabToRootAsync(true);

    private async Task PopTabToRootAsync(bool animateLast)
    {
        // Only the last pop animates: the pages below are covered, so animating them is invisible work.
        while (Stack.Count > 1)
            await PopPageAsync(false);

        if (Stack.Count > 0)
            await PopPageAsync(animateLast);
    }

    private SkiaControl BuildPageHost(string name, SkiaControl content)
    {
        var title = Titles.TryGetValue(name, out var known) ? known : name;

        return new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = PageBackgroundColor,
            BlockGesturesBelow = true,
            Children = new List<SkiaControl>
            {
                // Content sits below the bar, which is drawn over it — same order as the React head.
                new SkiaLayer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(0, NavBarHeight, 0, 0),
                    Children = content == null ? new List<SkiaControl>() : new List<SkiaControl> { content },
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

    #endregion

    #region Tabs

    private void BuildTabBar()
    {
        _tabBar = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = TabBarHeight,
            VerticalOptions = LayoutOptions.End,
            BackgroundColor = TabBarColor,
            ZIndex = ZIndexModals - 1,
            BlockGesturesBelow = true,
            Children = new List<SkiaControl>
            {
                new SkiaLayer { HorizontalOptions = LayoutOptions.Fill, HeightRequest = 1, BackgroundColor = Color.Parse("#343A40") },
                new SkiaGrid
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    ColumnDefinitions = new ColumnDefinitionCollection(Tabs.Select(_ => new ColumnDefinition(GridLength.Star)).ToArray()),
                    Children = Tabs.Select((tab, i) =>
                    {
                        var cell = new SkiaLayer
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            AnimationTapped = SkiaTouchAnimation.Ripple,
                            Children = new List<SkiaControl>
                            {
                                new SkiaLabel(tab.Title)
                                {
                                    FontSize = 13,
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center,
                                }.Adapt(l => _tabLabels.Add(l)),
                                new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    HeightRequest = 3,
                                    WidthRequest = 36,
                                    CornerRadius = 2,
                                    BackgroundColor = TabSelectedColor,
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.End,
                                }.Adapt(m => _tabMarkers.Add(m)),
                            },
                        }.OnTapped(me => { _ = SelectTabAsync(i); });
                        Grid.SetColumn(cell, i);
                        return (SkiaControl)cell;
                    }).ToList(),
                },
            },
        };

        AddSubView(_tabBar);
        PaintTabs();
    }

    private void PaintTabs()
    {
        for (var i = 0; i < _tabLabels.Count; i++)
        {
            var selected = i == _selectedTab;
            _tabLabels[i].TextColor = selected ? TabSelectedColor : TabColor;
            _tabLabels[i].FontFamily = selected ? "FontTextBold" : null;
            _tabMarkers[i].IsVisible = selected;
        }
    }

    /// <summary>Selects a tab; with <see cref="AnimateTabs"/> the new root slides in from 0.75 width with a fade.</summary>
    public async Task SelectTabAsync(int index)
    {
        if (Tabs is not { Count: > 0 })
            return;

        var to = Math.Clamp(index, 0, Tabs.Count - 1);
        var from = _selectedTab;
        if (to == from)
            return;

        if (!NotifyNavigating(Tabs[to].Route, NavigationSource.Push, _tabRoots[to]))
            return;

        SendDisappearing(GetTopmostView());
        _selectedTab = to;
        SendAppearing(GetTopmostView());
        PaintTabs();

        var next = _tabContainers[to];
        var previous = _tabContainers[from];

        if (!AnimateTabs)
        {
            previous.IsVisible = false;
            next.IsVisible = true;
            RaiseRouteChanged();
            OnLayersChanged();
            NotifyNavigated(Tabs[to].Route, NavigationSource.Push, _tabRoots[to]);
            return;
        }

        var direction = to > from ? 1 : -1;
        var width = CanvasWidthPoints();
        next.TranslationX = direction * 0.75 * width;
        next.Opacity = 0.001;
        next.ZIndex = 1;
        next.IsVisible = true;

        await Task.WhenAll(
            next.TranslateToAsync(0, 0, (uint)TabsAnimationSpeed, Easing.SpringOut),
            next.FadeToAsync(1, (uint)TabsAnimationSpeed, Easing.Linear),
            previous.TranslateToAsync(-direction * width, 0, (uint)TabsAnimationSpeed, Easing.SpringOut));

        previous.IsVisible = false;
        previous.TranslationX = 0;
        next.ZIndex = 0;
        RaiseRouteChanged();
        OnLayersChanged();
        NotifyNavigated(Tabs[to].Route, NavigationSource.Push, _tabRoots[to]);
    }

    #endregion

    #region Popups

    /// <summary>Opens content centred over a dimmed, blurred backdrop, scaling it in from 0.5.</summary>
    public async Task<SkiaControl> OpenPopupAsync(SkiaControl content, bool animated = true, bool closeWhenBackgroundTapped = true, bool showOverlay = true, Color backgroundColor = null)
    {
        if (!NotifyNavigating(Route, NavigationSource.Push, content))
            return null;

        var entry = new Overlay { Content = content };
        var wrapper = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Scale = animated ? 0.5 : 1,
            Children = new List<SkiaControl> { content },
        };

        // Children are handed over as one list: adding to Children after construction is not observed.
        var children = new List<SkiaControl>();
        if (showOverlay)
        {
            children.Add(new SkiaBackdrop
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Blur = PopupsBackgroundBlur,
                BackgroundColor = backgroundColor ?? PopupBackgroundColor,
                InputTransparent = true,
            });
        }

        children.Add(wrapper);

        var layer = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = ZIndexPopups + _nextId++,
            BlockGesturesBelow = true,
            Opacity = animated ? 0.1 : 1,
            Children = children,
        };

        // A tap outside the content closes it (MAUI PopupWrapper).
        layer.OnTapped((me, e) =>
        {
            var location = e.ProcessingInfo.MappedLocation;
            var offset = e.ProcessingInfo.ChildOffset;
            if (closeWhenBackgroundTapped && !wrapper.HitIsInside(location.X + offset.X, location.Y + offset.Y))
                _ = ClosePopupAsync(entry, animated);
        });

        entry.Layer = layer;
        SendAppearing(content);
        _popups.Add(entry);
        AddSubView(layer);

        if (animated)
        {
            await WaitForLayout(layer);
            await Task.WhenAll(
                layer.FadeToAsync(1, (uint)PopupsAnimationSpeed),
                wrapper.ScaleToAsync(1, 1, (uint)PopupsAnimationSpeed));
        }

        OnLayersChanged();
        NotifyNavigated(Route, NavigationSource.Push, content);
        return content;
    }

    /// <summary>Closes the top popup.</summary>
    public async Task ClosePopupAsync(bool animated = true)
    {
        if (_popups.Count > 0)
            await ClosePopupAsync(_popups[^1], animated);
    }

    /// <summary>Closes every popup, without animation.</summary>
    public async Task CloseAllPopups()
    {
        foreach (var popup in Enumerable.Reverse(_popups.ToArray()))
            await ClosePopupAsync(popup, false);
    }

    private async Task ClosePopupAsync(Overlay popup, bool animated)
    {
        if (popup.Closing || !_popups.Contains(popup))
            return;

        if (!NotifyNavigating(Route, NavigationSource.Pop, popup.Content))
            return;

        popup.Closing = true;
        SendDisappearing(popup.Content);
        if (animated)
        {
            var wrapper = popup.Content.Parent as SkiaControl;
            await Task.WhenAll(
                popup.Layer.FadeToAsync(0, (uint)PopupsAnimationSpeed),
                wrapper?.ScaleToAsync(0, 0, (uint)PopupsAnimationSpeed) ?? Task.CompletedTask);
        }

        _popups.Remove(popup);
        RemoveSubView(popup.Layer);
        OnLayersChanged(popup.Content);
        popup.Layer.Dispose();
        NotifyNavigated(Route, NavigationSource.Pop);
    }

    #endregion

    #region Modals

    /// <summary>Pushes content as a full-screen drawer sliding up from the bottom.</summary>
    public async Task<SkiaControl> PushModalAsync(SkiaControl content, bool useGestures = false, bool animated = true, bool freezeBackground = true)
    {
        if (!NotifyNavigating(Route, NavigationSource.Push, content))
            return null;

        var entry = new Overlay { Content = content };
        var opened = new TaskCompletionSource();

        var drawer = new SkiaDrawer
        {
            Direction = DrawerDirection.FromBottom,
            HeaderSize = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            RespondsToGestures = useGestures,
            Animated = animated,
            Bounces = false,
            BlockGesturesBelow = true,
            // SkiaDrawer hit-tests its Content, not Children: content set via Children never gets taps.
            Content = content,
        };

        drawer.StateTransitionComplete += (_, isOpen) =>
        {
            if (isOpen)
                opened.TrySetResult();
            else if (!drawer.IsOpen && !entry.Closing)
                RemoveModal(entry); // dragged down / closed by the user
        };
        drawer.IsOpenChanged += (_, isOpen) =>
        {
            if (!isOpen && !animated && !entry.Closing)
                RemoveModal(entry);
        };

        var children = new List<SkiaControl>();
        if (freezeBackground)
        {
            children.Add(new SkiaBackdrop
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Blur = PopupsBackgroundBlur,
                BackgroundColor = PopupBackgroundColor,
                InputTransparent = true,
            });
        }

        children.Add(drawer);

        var layer = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = ZIndexModals + _nextId++,
            BlockGesturesBelow = true,
            Children = children,
        };
        entry.Layer = layer;
        entry.Drawer = drawer;
        SendAppearing(content);
        _modals.Add(entry);
        AddSubView(layer);

        await WaitForLayout(drawer); // first draw: the drawer measured its travel
        drawer.IsOpen = true;
        if (animated)
            await Task.WhenAny(opened.Task, Task.Delay(2000));

        OnLayersChanged();
        NotifyNavigated(Route, NavigationSource.Push, content);
        return content;
    }

    /// <summary>Closes the top modal.</summary>
    public async Task PopModalAsync(bool animated = true)
    {
        if (_modals.Count == 0)
            return;

        var modal = _modals[^1];
        if (modal.Closing)
            return;

        if (!NotifyNavigating(Route, NavigationSource.Pop, modal.Content))
            return;

        modal.Closing = true;
        modal.DisappearingSent = true;
        SendDisappearing(modal.Content);
        if (modal.Drawer.IsOpen)
        {
            var closed = new TaskCompletionSource();
            modal.Drawer.StateTransitionComplete += (_, isOpen) => { if (!isOpen) closed.TrySetResult(); };
            modal.Drawer.Animated = animated;
            modal.Drawer.IsOpen = false;
            if (animated)
                await Task.WhenAny(closed.Task, Task.Delay(2000));
        }

        RemoveModal(entry: modal);
    }

    private void RemoveModal(Overlay entry)
    {
        if (!_modals.Remove(entry))
            return;

        entry.Closing = true;
        if (!entry.DisappearingSent)
        {
            entry.DisappearingSent = true; // closed by the user dragging it down
            SendDisappearing(entry.Content);
        }

        RemoveSubView(entry.Layer);
        OnLayersChanged(entry.Content);
        entry.Layer.Dispose();
        NotifyNavigated(Route, NavigationSource.Pop);
    }

    #endregion

    #region Toasts

    /// <summary>Shows a markdown text toast rising from the bottom edge for <paramref name="msShowTime"/> milliseconds.</summary>
    public void ShowToast(string text, int msShowTime = 4000) => ShowToast(new SkiaRichLabel(text)
    {
        TextColor = ToastTextColor,
        FontSize = ToastTextSize,
        Margin = new Thickness(ToastTextMargins),
        HorizontalOptions = LayoutOptions.Fill,
    }, msShowTime);

    /// <summary>Shows any drawn content as a toast rising from the bottom edge.</summary>
    public void ShowToast(SkiaControl content, int msShowTime = 4000)
    {
        _ = ShowToastAsync(content, msShowTime);
    }

    private async Task ShowToastAsync(SkiaControl content, int msShowTime)
    {
        await CloseAllToasts();

        var layer = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            ZIndex = ZIndexToasts + _nextId++,
            BackgroundColor = ToastBackgroundColor,
            BlockGesturesBelow = true,
            UseCache = SkiaCacheType.Operations,
            Opacity = 0,
            Children = new List<SkiaControl>
            {
                new SkiaLayer { HorizontalOptions = LayoutOptions.Fill, Children = new List<SkiaControl> { content } },
            },
        };

        var entry = new Overlay { Layer = layer, Content = content };
        _toasts.Add(entry);
        AddSubView(layer);

        await WaitForLayout(layer);
        var height = layer.DrawingRect.Height / Math.Max(1, layer.RenderingScale);
        layer.TranslationY = height;
        _ = layer.TranslateToAsync(0, 0, 300);
        _ = layer.FadeToAsync(1, 300);

        await Task.Delay(msShowTime);
        await CloseToastAsync(entry, true);
    }

    /// <summary>Removes every toast, without animation.</summary>
    public async Task CloseAllToasts()
    {
        foreach (var toast in _toasts.ToArray())
            await CloseToastAsync(toast, false);
    }

    private async Task CloseToastAsync(Overlay toast, bool animated)
    {
        if (toast.Closing || !_toasts.Contains(toast))
            return;

        toast.Closing = true;
        if (animated)
        {
            var height = toast.Layer.DrawingRect.Height / Math.Max(1, toast.Layer.RenderingScale);
            await Task.WhenAll(toast.Layer.TranslateToAsync(0, height, 250), toast.Layer.FadeToAsync(0, 250));
        }

        _toasts.Remove(toast);
        RemoveSubView(toast.Layer);
        toast.Layer.Dispose();
    }

    #endregion
}
