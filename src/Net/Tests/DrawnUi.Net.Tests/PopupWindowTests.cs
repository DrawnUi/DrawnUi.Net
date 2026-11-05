using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Popup windows (OpenPopupWindow, the API of DrawnUI for Rust): a menu opens at a point or below a control, flips
/// where the canvas ends, takes the input over it, and closes with a press outside (that press goes nowhere), Escape
/// or from code.
/// </summary>
[Collection("Tooltips")] // Escape goes through the static KeyboardManager
public class PopupWindowTests
{
    const int W = 400, H = 400;

    static SkiaControl Menu(List<string> picked, params string[] items) => new SkiaStack
    {
        WidthRequest = 120,
        Spacing = 0,
        HorizontalOptions = LayoutOptions.Start,
        BackgroundColor = Colors.DarkGray,
        Children = items.Select(text => (SkiaControl)new SkiaLayer
        {
            HeightRequest = 30,
            HorizontalOptions = LayoutOptions.Fill,
            Children = { new SkiaLabel(text) },
        }.OnHovered((me, on) => me.BackgroundColor = on ? Colors.Gray : Colors.Transparent).OnTapped(me => picked.Add(text))).ToList(),
    };

    static HeadlessCanvasHost Scene(out SkiaShape button, out List<string> tapped)
    {
        var list = new List<string>();
        tapped = list;
        var host = new HeadlessCanvasHost(W, H);
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape { BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }
                    .OnTapped(me => list.Add("background")),
                new SkiaShape { WidthRequest = 100, HeightRequest = 40, Margin = new Thickness(20, 20, 0, 0), BackgroundColor = Colors.Blue }
                    .Assign(out button),
            },
        };
        host.AdvanceFrames(3);
        return host;
    }

    static void Tap(HeadlessCanvasHost host, float x, float y)
    {
        host.Canvas.HandleDesktopPointerDown(x, y, W, H);
        host.Canvas.HandleDesktopPointerUp(x, y, W, H);
        host.AdvanceFrames(2);
    }

    [Fact]
    public void OpensAtThePoint_ItemTapPicks_NothingBelowGetsIt()
    {
        using var host = Scene(out _, out var tapped);
        var picked = new List<string>();

        var id = host.Canvas.OpenPopupWindow(Menu(picked, "Cut", "Copy", "Paste"), new SKPoint(150, 150));
        host.AdvanceFrames(3);

        Assert.True(host.Canvas.IsPopupWindowOpen(id));
        var root = host.Canvas.TopPopupWindow;
        var box = root.HitBoxAuto;
        Assert.Equal(150, box.Left, 1.5);
        Assert.Equal(150, box.Top, 1.5);

        Tap(host, 160, 150 + 45); // the second item
        Assert.Equal(new[] { "Copy" }, picked);
        Assert.Empty(tapped); // the background under the menu got nothing
    }

    [Fact]
    public void FlipsWhereTheCanvasEnds()
    {
        using var host = Scene(out _, out _);
        host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B", "C"), new SKPoint(380, 380));
        host.AdvanceFrames(3);

        var box = host.Canvas.TopPopupWindow.HitBoxAuto;
        Assert.Equal(380 - 120, box.Left, 1.5); // to the left of the point
        Assert.Equal(380 - 90, box.Top, 1.5); // above it
    }

    [Fact]
    public void PressOutside_Closes_AndThatPressGoesNowhere()
    {
        using var host = Scene(out _, out var tapped);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), new SKPoint(150, 150));
        host.AdvanceFrames(3);

        Tap(host, 50, 300);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
        Assert.Empty(tapped);

        Tap(host, 50, 300); // the next press works again
        Assert.Equal(new[] { "background" }, tapped);
    }

    [Fact]
    public void EscapeCloses()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A"), new SKPoint(150, 150));
        host.AdvanceFrames(2);

        var handled = new List<(bool Handled, bool Open)>();
        void OnDown(object sender, InputKey key) => handled.Add((KeyboardManager.IsKeyHandled, host.Canvas.IsPopupWindowOpen(id)));
        KeyboardManager.KeyDown += OnDown;
        try
        {
            KeyboardManager.KeyboardPressed(InputKey.Escape); // closes it: the app's KeyDown hears a used key
            KeyboardManager.KeyboardReleased(InputKey.Escape);
            host.AdvanceFrames(2);
            KeyboardManager.KeyboardPressed(InputKey.Escape); // nothing to close: the app's key
            KeyboardManager.KeyboardReleased(InputKey.Escape);
        }
        finally
        {
            KeyboardManager.KeyDown -= OnDown;
        }

        Assert.False(host.Canvas.IsPopupWindowOpen(id));
        Assert.Equal(new[] { (true, false), (false, false) }, handled);
    }

    [Fact]
    public void Dropdown_OpensBelowItsControl()
    {
        using var host = Scene(out var button, out _);
        host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), button);
        host.AdvanceFrames(3);

        var box = host.Canvas.TopPopupWindow.HitBoxAuto;
        Assert.Equal(button.HitBoxAuto.Left, box.Left, 1.5);
        Assert.Equal(button.HitBoxAuto.Bottom, box.Top, 1.5);
    }

    [Fact]
    public void WheelOutside_Closes()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), new SKPoint(150, 150));
        host.AdvanceFrames(2);

        host.Canvas.HandleDesktopWheel(50, 350, -120, W, H);
        host.AdvanceFrames(2);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
    }

    [Fact]
    public void WheelOverAScrollThatWasScrolling_Closes()
    {
        using var host = new HeadlessCanvasHost(W, H);
        host.Canvas.Content = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children = Enumerable.Range(0, 40).Select(i => (SkiaControl)new SkiaShape { HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Gray }).ToList(),
            },
        };
        host.AdvanceFrames(3);
        host.Canvas.HandleDesktopWheel(200, 200, -120, W, H); // the scroll owns the wheel gesture now
        host.AdvanceFrames(10);

        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A", "B"), new SKPoint(150, 150));
        host.AdvanceFrames(2);
        host.Canvas.HandleDesktopWheel(50, 350, -120, W, H);
        host.AdvanceFrames(2);

        Assert.False(host.Canvas.IsPopupWindowOpen(id));
    }

    [Fact]
    public void WindowDeactivated_ClosesPopupsAndTooltip()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A"), new SKPoint(150, 150));
        host.AdvanceFrames(2);

        host.Canvas.OnWindowDeactivated();
        host.AdvanceFrames(2);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
    }

    sealed class FakeHost : IPopupWindowHost
    {
        public SKRect Screen = new(-200, -200, W + 600, H + 600);
        public readonly List<(PopupId Id, SKRect Rect)> Shown = new();
        public readonly List<PopupId> Hidden = new();
        public SKRect ScreenBounds() => Screen;
        public void ShowPopup(PopupId id, SKImage image, SKRect canvasRect) { image.Dispose(); Shown.Add((id, canvasRect)); }
        public void HidePopup(PopupId id) => Hidden.Add(id);
    }

    [Fact]
    public void OwnWindow_PastTheCanvasEdge_TakesClicks_RedrawsOnHover_HidesOnClose()
    {
        using var host = Scene(out _, out var tapped);
        var windows = new FakeHost();
        host.Canvas.PopupWindowHost = windows;
        var picked = new List<string>();
        SkiaControl first = null;
        var menu = Menu(picked, "Cut", "Copy", "Paste");
        var id = host.Canvas.OpenPopupWindow(menu, new SKPoint(350, 380));
        host.AdvanceFrames(3);

        // placed inside the screen, not the canvas: past its right and bottom edges
        Assert.NotEmpty(windows.Shown);
        var rect = windows.Shown[^1].Rect;
        Assert.Equal(350, rect.Left, 1.5);
        Assert.Equal(380, rect.Top, 1.5);
        Assert.True(rect.Right > W && rect.Bottom > H);

        // nothing changed: no new picture
        var count = windows.Shown.Count;
        host.AdvanceFrames(3);
        Assert.Equal(count, windows.Shown.Count);

        // the mouse over an item changes its look: the window gets a new picture
        host.Canvas.HandleDesktopPointerMove(360, 380 + 15, false, W, H);
        host.AdvanceFrames(3);
        Assert.True(windows.Shown.Count > count, "no new picture after a hover change");

        // a click in its window comes back at the canvas point, beyond the canvas
        host.Canvas.HandleDesktopPointerDown(360, 380 + 45, W, H);
        host.Canvas.HandleDesktopPointerUp(360, 380 + 45, W, H);
        host.AdvanceFrames(3);
        Assert.Equal(new[] { "Copy" }, picked);
        Assert.Empty(tapped);

        host.Canvas.ClosePopupWindow(id);
        host.AdvanceFrames(2);
        Assert.Contains(id, windows.Hidden);
    }

    [Fact]
    public void Keyboard_KeyDownReportsTheKeysTheCanvasUsed()
    {
        using var host = MenuScene(new List<string>(), out _, out _);
        var seen = new List<(InputKey Key, bool Handled)>();
        void OnDown(object sender, InputKey key) => seen.Add((key, KeyboardManager.IsKeyHandled));
        KeyboardManager.KeyDown += OnDown;
        try
        {
            Key(host, InputKey.KeyA); // nothing used it
            Key(host, InputKey.Tab); // moved the focus to the title
            Key(host, InputKey.Enter); // opened its menu
            Key(host, InputKey.ArrowDown); // moved inside the menu

            // used and released before its KeyDown came: the next press of that key is not reported as used
            host.Canvas.HandleKeyboardNavigation(InputKey.ArrowDown, false);
            KeyboardManager.KeyboardReleased(InputKey.ArrowDown);
            KeyboardManager.KeyboardPressed(InputKey.ArrowDown);
            KeyboardManager.KeyboardReleased(InputKey.ArrowDown);
        }
        finally
        {
            KeyboardManager.KeyDown -= OnDown;
        }

        Assert.Equal(new[]
        {
            (InputKey.KeyA, false), (InputKey.Tab, true), (InputKey.Enter, true), (InputKey.ArrowDown, true), (InputKey.ArrowDown, false),
        }, seen);
        Assert.False(KeyboardManager.IsKeyHandled);
    }

    [Fact]
    public void Keyboard_AppFocusWhenAMenuCloses_WinsOverTheReturnToItsTitle()
    {
        using var host = MenuScene(new List<string>(), out _, out var after);
        host.Canvas.PopupWindowClosed += (_, _) => host.Canvas.SetAccessibilityFocus(after);

        Key(host, InputKey.Tab);
        Key(host, InputKey.Enter);
        Assert.True(host.Canvas.HasPopupWindows);

        Key(host, InputKey.Escape);
        Assert.False(host.Canvas.HasPopupWindows);
        Assert.Equal("After", (host.Canvas.AccessibilityManager.FocusedNode as SkiaControl)?.AccessibilityLabel);
    }

    /// <summary>A menu bar title that opens a menu (a panel whose stack holds the lines, role menu), as the Hello apps.</summary>
    static HeadlessCanvasHost MenuScene(List<string> picked, out SkiaShape title, out SkiaShape after, Func<PopupId> opened = null)
    {
        var host = new HeadlessCanvasHost(W, H);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaShape Line(string text) => new SkiaShape
        {
            HeightRequest = 30,
            HorizontalOptions = LayoutOptions.Fill,
            AccessibilityRole = Aria.RoleMenuItem,
            AccessibilityCanInteract = true,
            AccessibilityLabel = text,
        }.OnTapped(me => { picked.Add(text); me.Superview?.ClosePopupWindows(); });

        SkiaShape titleView = null;
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 10,
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    WidthRequest = 80, HeightRequest = 30, AccessibilityRole = Aria.RoleMenuItem, AccessibilityCanInteract = true, AccessibilityLabel = "File",
                }.Assign(out titleView).OnTapped(me => me.Superview.OpenPopupWindow(new SkiaShape
                {
                    WidthRequest = 120,
                    Children = new List<SkiaControl>
                    {
                        new SkiaStack { Spacing = 0, AccessibilityRole = Aria.RoleMenu, Children = { Line("New"), Line("Open"), Line("Save") } },
                    },
                }, me)),
                new SkiaShape { WidthRequest = 80, HeightRequest = 30, AccessibilityRole = Aria.RoleButton, AccessibilityCanInteract = true, AccessibilityLabel = "After" }
                    .Assign(out var afterView),
            },
        };
        title = titleView;
        after = afterView;
        host.AdvanceFrames(4);
        return host;
    }

    static void Key(HeadlessCanvasHost host, InputKey key)
    {
        // as OpenTK and the other heads: the canvas navigation first, then KeyboardManager
        host.Canvas.HandleKeyboardNavigation(key, false);
        KeyboardManager.KeyboardPressed(key);
        KeyboardManager.KeyboardReleased(key);
        // as an app draws: only the frames asked for (OpenTK and WPF draw on demand)
        for (var i = 0; i < 30 && host.NeedsFrame; i++)
            host.RenderFrame(16);
    }

    static string Focused(HeadlessCanvasHost host) => (host.Canvas.KeyboardFocusNode as SkiaControl)?.AccessibilityLabel;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keyboard_MenuTakesFocus_ArrowsWalkIt_TabStaysInside_EscapeGivesItBack(bool ownWindow)
    {
        var picked = new List<string>();
        using var host = MenuScene(picked, out var title, out _);
        if (ownWindow)
            host.Canvas.PopupWindowHost = new FakeHost();

        Key(host, InputKey.Tab);
        Assert.Equal("File", Focused(host));

        Key(host, InputKey.Enter); // opens the menu: its first line takes the keyboard
        Assert.True(host.Canvas.HasPopupWindows);
        Assert.Equal("New", Focused(host));

        Key(host, InputKey.ArrowDown);
        Assert.Equal("Open", Focused(host));

        Key(host, InputKey.Tab); // the menu is one stop: Tab goes round inside it, never to "After"
        Assert.Equal("Open", Focused(host));

        Key(host, InputKey.Escape); // closes, the keyboard goes back to the title
        Assert.False(host.Canvas.HasPopupWindows);
        Assert.Equal("File", Focused(host));
        Assert.DoesNotContain(host.Canvas.AccessibilityManager.Snapshot, n => n.Label is "New" or "Open" or "Save");
        Assert.Empty(picked);
    }

    [Fact]
    public void Keyboard_EnterOnALinePicks_FocusBackToTheTitle()
    {
        var picked = new List<string>();
        using var host = MenuScene(picked, out _, out _);

        Key(host, InputKey.Tab);
        Key(host, InputKey.Enter);
        Key(host, InputKey.ArrowDown);
        Key(host, InputKey.ArrowDown);
        Key(host, InputKey.Enter);

        Assert.Equal(new[] { "Save" }, picked);
        Assert.False(host.Canvas.HasPopupWindows);
        Assert.Equal("File", Focused(host));
    }

    [Fact]
    public void Mouse_OpenedMenu_DoesNotTakeTheKeyboard()
    {
        var picked = new List<string>();
        using var host = MenuScene(picked, out var title, out _);
        var box = title.HitBoxAuto;
        Tap(host, box.MidX, box.MidY);

        Assert.True(host.Canvas.HasPopupWindows);
        Assert.Null(host.Canvas.KeyboardFocusNode);
    }

    [Fact]
    public void ClosedFromCode()
    {
        using var host = Scene(out _, out _);
        var id = host.Canvas.OpenPopupWindow(Menu(new List<string>(), "A"), new SKPoint(10, 10));
        host.AdvanceFrames(2);

        host.Canvas.ClosePopupWindow(id);
        host.AdvanceFrames(2);
        Assert.False(host.Canvas.IsPopupWindowOpen(id));
        Assert.Null(host.Canvas.TopPopupWindow);
    }
}
