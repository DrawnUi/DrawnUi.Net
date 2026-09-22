using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Infrastructure;
using DrawnUi;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// SkiaShell: pages with slide transitions, a nested tabbed shell, popups, modals, toasts and the
/// Sandbox frosted-glass backdrop. Ported from the React demo's ShellPage.tsx. Needs the app shell
/// it lives in, to push pages and overlays on it.
/// </summary>
public class ShellPage : SkiaLayer
{
    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private readonly SkiaShell _shell;
    private SkiaShell _tabs;
    private SkiaLabel _state;
    private SkiaLabel _events;
    private SkiaButton _cancelButton;
    private readonly List<string> _log = new();
    private string _extra = "";
    private bool _cancelNext;

    /// <summary>Builds the page over the shell that hosts it.</summary>
    public ShellPage(SkiaShell shell)
    {
        _shell = shell;
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
                        new SkiaLabel("SkiaShell") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                        new SkiaLabel { FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill }.Assign(out _state),
                        new SkiaLabel("GoBack closes the top popup, then the top modal, then pops the page — the MAUI GoBack order. The nav bar's Back button does the same.")
                        {
                            FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                        },

                        Card("Pages — GoToAsync slides the page in from the right (PagesAnimationSpeed 200 ms), GoBackAsync slides it out",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("GoToAsync('shapes')") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = _shell.GoToAsync("shapes")),
                                    new SkiaButton("GoToAsync('shell') again") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = _shell.GoToAsync("shell")),
                                    new SkiaButton("GoBackAsync()") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _ = _shell.GoBackAsync()),
                                    new SkiaButton("PopToRootAsync()") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _ = _shell.PopToRootAsync()),
                                },
                            }),

                        Card("Tabs — a nested SkiaShell with Tabs: per-tab navigation stacks, AnimateTabs (slide + fade), PopTabToRootAsync",
                            new SkiaLayer
                            {
                                HeightRequest = 300,
                                HorizontalOptions = LayoutOptions.Fill,
                                IsClippedToBounds = true,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShell
                                    {
                                        NavBarHeight = 44,
                                        AnimateTabs = true,
                                        Tabs = new List<ShellTab> { new("home", "Home"), new("search", "Search"), new("profile", "Profile") },
                                        Titles = new Dictionary<string, string> { ["detail"] = "Detail" },
                                    }
                                    .Assign(out _tabs)
                                    .Adapt(me =>
                                    {
                                        me.Routes = new Dictionary<string, Func<ShellArguments, SkiaControl>>
                                        {
                                            ["home"] = _ => TabPage("Home", "#0F3460"),
                                            ["search"] = _ => TabPage("Search", "#533483"),
                                            ["profile"] = _ => TabPage("Profile", "#1B4332"),
                                            ["detail"] = args => TabPage(args.ContainsKey("id") ? $"Detail id={args.Get("id")}" : "Detail page (pushed inside this tab)", "#2B3035"),
                                        };
                                        me.Navigating += (_, e) =>
                                        {
                                            if (_cancelNext)
                                            {
                                                e.Cancel = true;
                                                _cancelNext = false;
                                                PaintCancel();
                                            }
                                            Log($"Navigating {e.Source} '{e.Route}'{(e.Cancel ? " CANCELLED" : "")}");
                                        };
                                        me.Navigated += (_, e) => Log($"Navigated {e.Source} '{e.Route}' view={e.View?.GetType().Name ?? "-"}");
                                        me.RouteChanged += (_, r) => Log($"RouteChanged '{r}'");
                                    }),
                                },
                            },
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Cancel next navigation") { FontSize = 12 }.Assign(out _cancelButton).OnTapped(me => { _cancelNext = !_cancelNext; PaintCancel(); }),
                                },
                            },
                            new SkiaLabel("Navigating / Navigated / RouteChanged events of the nested shell appear here") { FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill }.Assign(out _events)),

                        Card("SkiaBackdrop — the Sandbox MainPageBackdrop frosted glass, same tree",
                            // Sandbox: scroll content UseCache=Image (the backdrop snapshots that offscreen surface), baboon, then a 200x200 composition
                            new SkiaLayer
                            {
                                HeightRequest = 260, HorizontalOptions = LayoutOptions.Fill, IsClippedToBounds = true, UseCache = SkiaCacheType.Image,
                                BackgroundColor = Color.Parse("#F5F5F5"), Padding = new Thickness(24),
                                Children = new List<SkiaControl>
                                {
                                    new SkiaImage { Source = "images/baboon.jpg", Aspect = TransformAspect.AspectCover, BackgroundColor = Colors.Green, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                                    new SkiaLayer
                                    {
                                        WidthRequest = 200, HeightRequest = 200, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                                        Children = new List<SkiaControl>
                                        {
                                            // static shadow + texture, cached
                                            new SkiaLayer
                                            {
                                                Padding = new Thickness(16), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, UseCache = SkiaCacheType.Image, ZIndex = -1,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaShape
                                                    {
                                                        Type = ShapeType.Rectangle, BackgroundColor = Color.Parse("#22DDDDDD"), CornerRadius = 16, StrokeColor = Colors.Red, StrokeWidth = 2,
                                                        HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                                        StrokeGradient = new SkiaGradient
                                                        {
                                                            Type = GradientType.Linear, StartXRatio = 0, StartYRatio = 0, EndXRatio = 1, EndYRatio = 1,
                                                            Colors = new List<Color> { Color.Parse("#66FFFFFF"), Color.Parse("#66999999") },
                                                        },
                                                        Children = new List<SkiaControl>
                                                        {
                                                            new SkiaImage { Source = "images/glass2.jpg", Aspect = TransformAspect.AspectCover, Opacity = 0.15, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                                                        },
                                                    },
                                                },
                                            },
                                            // BACKDROP
                                            new SkiaShape
                                            {
                                                Type = ShapeType.Rectangle, Margin = new Thickness(16), BackgroundColor = Color.Parse("#66FFFFFF"), ClipBackgroundColor = true, CornerRadius = 19,
                                                HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                                Shadows = { new SkiaShadow { X = 4, Y = 4, Blur = 3, Opacity = 1, Color = Color.Parse("#44000000") } },
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLayer
                                                    {
                                                        Children = new List<SkiaControl>
                                                        {
                                                            new SkiaBackdrop { Blur = 3, UseContext = true, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, ZIndex = -1 },
                                                            new SkiaLayer
                                                            {
                                                                Padding = new Thickness(8), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                                                Children = new List<SkiaControl>
                                                                {
                                                                    new SkiaLabel("Wonnabe Frosted Glass") { FontSize = 20, TextColor = Color.Parse("#EFEFEF"), HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = DrawTextAlignment.Center, VerticalOptions = LayoutOptions.Center },
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            }),

                        Card("Popups — OpenPopupAsync(content, animated, closeWhenBackgroundTapped, showOverlay, backgroundColor)",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Open popup") { BackgroundColor = Color.Parse("#6610F2") }.OnTapped(me => _ = OpenPopup("Hello popup").ContinueWith(_ => SetExtra("· popup opened"))),
                                    new SkiaButton("Not closable outside") { BackgroundColor = Color.Parse("#6610F2") }.OnTapped(me => _ = _shell.OpenPopupAsync(Popup("closeWhenBackgroundTapped=false"), closeWhenBackgroundTapped: false)),
                                    new SkiaButton("No overlay, not animated") { BackgroundColor = Color.Parse("#6610F2") }.OnTapped(me => _ = _shell.OpenPopupAsync(Popup("showOverlay=false"), animated: false, showOverlay: false)),
                                    new SkiaButton("Red overlay") { BackgroundColor = Color.Parse("#6610F2") }.OnTapped(me => _ = _shell.OpenPopupAsync(Popup("backgroundColor"), backgroundColor: Color.Parse("#66FF0000"))),
                                    new SkiaButton("CloseAllPopups()") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _ = _shell.CloseAllPopups()),
                                },
                            }),

                        Card("Modals — PushModalAsync(content, useGestures, animated)",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Push modal") { BackgroundColor = Color.Parse("#20C997"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _ = _shell.PushModalAsync(Modal()).ContinueWith(_ => SetExtra("· modal opened"))),
                                    new SkiaButton("Draggable (useGestures)") { BackgroundColor = Color.Parse("#20C997"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _ = _shell.PushModalAsync(Modal(), useGestures: true)),
                                    new SkiaButton("Not animated") { BackgroundColor = Color.Parse("#20C997"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _ = _shell.PushModalAsync(Modal(), animated: false)),
                                },
                            }),

                        Card("Toasts — ShowToast(text | content, msShowTime)",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("ShowToast('Saved!')") { BackgroundColor = Color.Parse("#FD7E14"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _shell.ShowToast("**Saved!** The toast slides up, stays 4 s, slides down.")),
                                    new SkiaButton("Short (1.5 s)") { BackgroundColor = Color.Parse("#FD7E14"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _shell.ShowToast("Gone in 1.5 seconds", 1500)),
                                    new SkiaButton("Custom content") { BackgroundColor = Color.Parse("#FD7E14"), TextColor = Color.Parse("#1A1A2E") }.OnTapped(me => _shell.ShowToast(new SkiaStack
                                    {
                                        Spacing = 4,
                                        Padding = new Thickness(24, 16),
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaLabel("Custom toast") { FontSize = 16, FontFamily = "FontTextBold", TextColor = Colors.White },
                                            new SkiaLabel("Any drawn tree works as toast content.") { FontSize = 13, TextColor = Muted },
                                        },
                                    }, 3000)),
                                    new SkiaButton("CloseAllToasts()") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _ = _shell.CloseAllToasts()),
                                },
                            }),
                    },
                },
            }.Fill(),
        };

        _shell.Navigated += OnShellChanged;
        _shell.RouteChanged += (_, _) => PaintState();
        PaintState();
        PaintCancel();
    }

    private void OnShellChanged(object sender, ShellNavigatedArgs e) => PaintState();

    private void SetExtra(string extra)
    {
        _extra = extra;
        MainThread.BeginInvokeOnMainThread(PaintState);
    }

    private void PaintState()
    {
        _state.Text = $"Route={(_shell.Route == "" ? "\"\"" : _shell.Route)} · NavigationStack=[{string.Join(", ", _shell.NavigationStack)}] · Popups={_shell.PopupsCount} · Modals={_shell.ModalsCount} · Toasts={_shell.ToastsCount} {_extra}";
    }

    private void PaintCancel()
    {
        _cancelButton.Text = _cancelNext ? "Next Navigating will be CANCELLED" : "Cancel next navigation";
        _cancelButton.BackgroundColor = Color.Parse(_cancelNext ? "#D63384" : "#495057");
    }

    private void Log(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 4)
            _log.RemoveAt(_log.Count - 1);
        _events.Text = string.Join("\n", _log);
    }

    private Task OpenPopup(string title) => _shell.OpenPopupAsync(Popup(title));

    /// <summary>Content of the demo popup: a card with its own close button.</summary>
    private SkiaControl Popup(string title) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 16,
        BackgroundColor = Color.Parse("#F5F5F5"),
        WidthRequest = 300,
        Shadows = { new SkiaShadow { X = 0, Y = 6, Blur = 12, Opacity = 0.5, Color = Colors.Black } },
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 12,
                Padding = new Thickness(20),
                Children = new List<SkiaControl>
                {
                    new SkiaLabel(title) { FontSize = 20, FontFamily = "FontTextBold", TextColor = Color.Parse("#111827") },
                    new SkiaLabel("OpenPopupAsync centers the content over a dimmed backdrop, scales it in from 0.5 and fades the layer (PopupsAnimationSpeed 250 ms). A tap outside closes it when closeWhenBackgroundTapped.")
                    {
                        FontSize = 13, TextColor = Color.Parse("#374151"), HorizontalOptions = LayoutOptions.Fill,
                    },
                    new SkiaButton("Close") { ControlStyle = PrebuiltControlStyle.Material, HorizontalOptions = LayoutOptions.End }.OnTapped(me => _ = _shell.ClosePopupAsync()),
                },
            },
        },
    };

    private SkiaControl Modal() => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        BackgroundColor = Color.Parse("#212529"),
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 16,
                Padding = new Thickness(24, 40),
                HorizontalOptions = LayoutOptions.Center,
                MaximumWidthRequest = 520,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel("Modal page") { FontSize = 28, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill },
                    new SkiaLabel("PushModalAsync wraps the content in a full-screen SkiaDrawer (Direction=FromBottom, HeaderSize=0) that slides open; with useGestures it can be dragged down to close. PopModalAsync closes it.")
                    {
                        FontSize = 14, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                    },
                    new SkiaWrap
                    {
                        Spacing = 8,
                        Children = new List<SkiaControl>
                        {
                            new SkiaButton("PopModalAsync()") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = _shell.PopModalAsync()),
                            new SkiaButton("Popup over the modal") { BackgroundColor = Color.Parse("#6610F2") }.OnTapped(me => _ = _shell.OpenPopupAsync(Popup("Popup over a modal"))),
                            new SkiaButton("Toast") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _shell.ShowToast("Toast shown above the modal (ZIndexToasts)")),
                        },
                    },
                },
            },
        },
    };

    /// <summary>A page inside the embedded tabbed shell: shows its tab stack and pushes deeper pages.</summary>
    private SkiaControl TabPage(string name, string color) => new SkiaLayer
    {
        VerticalOptions = LayoutOptions.Fill,
        BackgroundColor = Color.Parse(color),
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel(name) { FontSize = 22, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                    new SkiaLabel(TabState()) { FontSize = 12, TextColor = Color.Parse("#DEE2E6"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center }
                        .Adapt(me => _tabs.RouteChanged += (_, _) => me.Text = TabState()),
                    new SkiaWrap
                    {
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>
                        {
                            new SkiaButton("Push detail") { BackgroundColor = Color.Parse("#212529"), FontSize = 13 }.OnTapped(me => _ = _tabs.GoToAsync("detail")),
                            new SkiaButton("GoToAsync('detail', true, { id: 42, from })") { BackgroundColor = Color.Parse("#212529"), FontSize = 13 }
                                .OnTapped(me => _ = _tabs.GoToAsync("detail", true, new Dictionary<string, object> { ["id"] = 42, ["from"] = name })),
                            new SkiaButton("GoToAsync('detail?id=7')") { BackgroundColor = Color.Parse("#212529"), FontSize = 13 }.OnTapped(me => _ = _tabs.GoToAsync("detail?id=7")),
                        },
                    },
                },
            },
        },
    };

    private string TabState()
    {
        if (_tabs == null)
            return "";

        var args = _tabs.Arguments.Count > 0 ? " · Arguments {" + string.Join(", ", _tabs.Arguments.Select(a => $"{a.Key}: {a.Value}")) + "}" : "";
        return $"Tab {_tabs.SelectedTab} · stack [{string.Join(", ", _tabs.NavigationStack)}]{args}";
    }

    private static SkiaControl Card(string title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                Children = new SkiaControl[]
                {
                    new SkiaLabel(title) { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase },
                }.Concat(content).ToList(),
            },
        },
    };
}
