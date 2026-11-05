using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using HelloOpenTk.Pages;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The menu bar of the Hello apps' Tooltips &amp; Menus page (HelloOpenTk's copy, the same in HelloWpf, HelloMaui and
/// HelloSlintNet) from the keyboard alone: Alt alone puts the
/// keyboard on the bar, Left and Right go round the titles, Down opens a menu on its first line, the arrows go round its
/// lines, Escape closes one level, leaving gives the keyboard back. Keys reach the canvas navigation first, then
/// KeyboardManager, as on every head, with the menus in windows of their own and drawn inside the canvas.
/// </summary>
[Collection("Tooltips")] // KeyboardManager and Super.TooltipsFromAccessibility are static
public class HelloMenuBarTests
{
    const int W = 1000, H = 900;

    static readonly Color Selected = Color.Parse("#0D6EFD");

    sealed class OwnWindows : IPopupWindowHost
    {
        public SKRect ScreenBounds() => new(0, 0, 3000, 2000);
        public void ShowPopup(PopupId id, SKImage image, SKRect canvasRect) => image.Dispose();
        public void HidePopup(PopupId id) { }
    }

    sealed class Bar : IDisposable
    {
        public readonly HeadlessCanvasHost Host = new(W, H);
        public List<SkiaControl> Titles = null!;

        public Bar(bool ownWindows = true)
        {
            Host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
            if (ownWindows)
                Host.Canvas.PopupWindowHost = new OwnWindows();
            Host.Canvas.Content = new TooltipsMenusPage();
            Host.AdvanceFrames(5);
            Titles = Find(Host.Canvas.Content as SkiaControl, c => c.AccessibilityRole == Aria.RoleMenuBar)!.Views.ToList();
        }

        public void Settle()
        {
            for (var i = 0; i < 30 && Host.NeedsFrame; i++)
                Host.RenderFrame(16);
            Host.AdvanceFrames(2);
        }

        public void Press(InputKey key)
        {
            Host.Canvas.HandleKeyboardNavigation(key, false); // as every head: the canvas first, then KeyDown
            KeyboardManager.KeyboardPressed(key);
            KeyboardManager.KeyboardReleased(key);
            Settle();
        }

        public void Alt(InputKey with = InputKey.Unknown)
        {
            KeyboardManager.KeyboardPressed(InputKey.AltLeft);
            if (with != InputKey.Unknown)
            {
                KeyboardManager.KeyboardPressed(with);
                KeyboardManager.KeyboardReleased(with);
            }
            KeyboardManager.KeyboardReleased(InputKey.AltLeft);
            Settle();
        }

        /// <summary>Titles in the selected look: the keyboard is on them, or their menu is open.</summary>
        public string LitTitles => string.Join(",", Titles.Where(t => Equals(t.BackgroundColor, Selected)).Select(t => t.AccessibilityLabel));

        public SkiaControl? Menu => Host.Canvas.HasPopupWindows ? Find(Host.Canvas.TopPopupWindow, c => c.AccessibilityRole == Aria.RoleMenu) : null;

        public string? OpenMenu => Menu?.AccessibilityLabel;

        /// <summary>The line in the selected look (lit by the keyboard).</summary>
        public string? LitLine => Menu?.Views.FirstOrDefault(v => Equals(v.BackgroundColor, Selected))?.AccessibilityLabel;

        public string? Focus => (Host.Canvas.AccessibilityManager.FocusedNode as SkiaControl)?.AccessibilityLabel;

        public string Command => Find(Host.Canvas.Content as SkiaControl, c => c is SkiaLabel l && l.Text?.StartsWith("Last command") == true) is SkiaLabel l ? l.Text : "";

        /// <summary>Where the control is on the canvas now (a popup's content is laid out at its own origin).</summary>
        public (float X, float Y) Center(SkiaControl c) => (c.GetAccessibilityPixelRect().MidX, c.GetAccessibilityPixelRect().MidY);

        public void MoveMouse(float x, float y)
        {
            Host.Canvas.HandleDesktopPointerMove(x, y, false, W, H);
            Settle();
        }

        public void Dispose()
        {
            Host.Dispose();
            Super.TooltipsFromAccessibility = false;
        }
    }

    static SkiaControl? Find(SkiaControl? root, Func<SkiaControl, bool> match)
    {
        if (root == null)
            return null;
        if (match(root))
            return root;
        foreach (var child in root.Views)
        {
            if (Find(child, match) is { } found)
                return found;
        }
        return null;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_menu_bar_works_from_the_keyboard_alone(bool ownWindows)
    {
        using var bar = new Bar(ownWindows);

        // Alt alone: the keyboard on the bar, File lit, no menu open; the screen reader is on File
        bar.Alt();
        Assert.Equal("File", bar.LitTitles);
        Assert.Null(bar.OpenMenu);
        Assert.Equal("File", bar.Focus);

        // Left and Right go round all the titles
        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            bar.Press(InputKey.ArrowRight);
            seen.Add(bar.LitTitles);
        }
        Assert.Equal(new[] { "Edit", "View", "Help", "File" }, seen);
        bar.Press(InputKey.ArrowLeft);
        Assert.Equal("Help", bar.LitTitles);
        Assert.Equal("Help", bar.Focus);
        bar.Press(InputKey.ArrowRight);

        // Down opens File on its first line, the reader on it
        bar.Press(InputKey.ArrowDown);
        Assert.Equal("File", bar.OpenMenu);
        Assert.Equal("File", bar.LitTitles);
        Assert.Equal("New", bar.LitLine);
        Assert.Equal("New", bar.Focus);

        // Down walks it past the separator and comes round; Up goes back round; Home, End
        seen.Clear();
        for (var i = 0; i < 4; i++)
        {
            bar.Press(InputKey.ArrowDown);
            seen.Add(bar.LitLine!);
        }
        Assert.Equal(new[] { "Open…", "Save", "Close", "New" }, seen);
        bar.Press(InputKey.ArrowUp);
        Assert.Equal("Close", bar.LitLine);
        Assert.Equal("Close", bar.Focus);
        bar.Press(InputKey.Home);
        Assert.Equal("New", bar.LitLine);
        bar.Press(InputKey.End);
        Assert.Equal("Close", bar.LitLine);

        // Right and Left open the menu after and before, round at the ends, each on its first line
        bar.Press(InputKey.ArrowRight);
        Assert.Equal(("Edit", "Undo"), (bar.OpenMenu, bar.LitLine));
        bar.Press(InputKey.ArrowLeft);
        bar.Press(InputKey.ArrowLeft);
        Assert.Equal(("Help", "Documentation", "Help"), (bar.OpenMenu, bar.LitLine, bar.LitTitles));
        Assert.Equal("Documentation", bar.Focus);

        // Escape closes the menu, the keyboard stays on its title; Escape again gives it back (here: nowhere)
        bar.Press(InputKey.Escape);
        Assert.Null(bar.OpenMenu);
        Assert.Equal("Help", bar.LitTitles);
        Assert.Equal("Help", bar.Focus);
        bar.Press(InputKey.Escape);
        Assert.Equal("", bar.LitTitles);
        Assert.Null(bar.Focus);

        // Alt+F opens File on its first line; Down, Enter runs Open…, the bar lets go and the keyboard goes back
        bar.Alt(InputKey.KeyF);
        Assert.Equal(("File", "New"), (bar.OpenMenu, bar.LitLine));
        bar.Press(InputKey.ArrowDown);
        bar.Press(InputKey.Enter);
        Assert.Equal("Last command: File › Open… (Ctrl+O)", bar.Command);
        Assert.Null(bar.OpenMenu);
        Assert.Equal("", bar.LitTitles);
        Assert.Null(bar.Focus);

        // Enter and Space on the bar open the lit menu once, Space runs a line once
        bar.Alt();
        bar.Press(InputKey.ArrowRight);
        bar.Press(InputKey.Enter);
        Assert.Equal(("Edit", "Undo"), (bar.OpenMenu, bar.LitLine));
        bar.Press(InputKey.Space);
        Assert.Equal("Last command: Edit › Undo (Ctrl+Z)", bar.Command);
        Assert.Null(bar.OpenMenu);

        // Alt leaves from an open menu too
        bar.Alt(InputKey.KeyV);
        Assert.Equal("View", bar.OpenMenu);
        bar.Alt();
        Assert.Null(bar.OpenMenu);
        Assert.Equal("", bar.LitTitles);
        Assert.Null(bar.Focus);

        // a shortcut works with the menus closed
        KeyboardManager.KeyboardPressed(InputKey.ControlLeft);
        bar.Press(InputKey.KeyS);
        KeyboardManager.KeyboardReleased(InputKey.ControlLeft);
        Assert.Equal("Last command: File › Save (Ctrl+S)", bar.Command);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_resting_mouse_never_takes_the_highlight_a_real_move_does(bool ownWindows)
    {
        using var bar = new Bar(ownWindows);

        // the mouse rests on Edit; the keyboard goes round all four titles past it
        var edit = bar.Center(bar.Titles[1]);
        bar.MoveMouse(edit.X, edit.Y);
        bar.Alt();
        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            bar.Press(InputKey.ArrowRight);
            seen.Add(bar.LitTitles);
        }
        Assert.Equal(new[] { "Edit", "View", "Help", "File" }, seen);

        // with a menu open too: the menus open and close around the resting mouse, nothing follows it
        bar.Press(InputKey.ArrowDown);
        seen.Clear();
        for (var i = 0; i < 4; i++)
        {
            bar.Press(InputKey.ArrowRight);
            seen.Add(bar.OpenMenu!);
        }
        Assert.Equal(new[] { "Edit", "View", "Help", "File" }, seen);
        Assert.Equal("New", bar.LitLine);

        // the mouse rests where the second line of File is: its line stays the keyboard's
        var (ox, oy) = bar.Center(bar.Menu!.Views.First(v => v.AccessibilityLabel == "Open…"));
        bar.Press(InputKey.Escape);
        bar.MoveMouse(ox, oy); // a real move, over nothing yet
        bar.Press(InputKey.ArrowDown); // File opens under the resting mouse
        Assert.Equal(("File", "New"), (bar.OpenMenu, bar.LitLine));

        // a real move over a line lights it in the hover look, the keyboard look goes
        bar.MoveMouse(ox, oy + 1);
        Assert.Null(bar.LitLine);
        Assert.Equal("Open…", bar.Focus);

        // the keyboard again: Right opens Edit with a line under the resting mouse, the keyboard's first line stays lit
        bar.Press(InputKey.ArrowRight);
        bar.MoveMouse(ox, oy + 1); // Windows sends a window that opens under the cursor a move at the same place
        Assert.Equal(("Edit", "Undo"), (bar.OpenMenu, bar.LitLine));
        Assert.Equal("Undo", bar.Focus);

        // a real move over another title opens its menu
        var view = bar.Center(bar.Titles[2]);
        bar.MoveMouse(view.X, view.Y);
        Assert.Equal("View", bar.OpenMenu);
    }

    [Fact]
    public void Screen_readers_read_the_bar_its_menu_and_their_states()
    {
        using var bar = new Bar();
        bar.Alt(InputKey.KeyE);
        bar.Host.AdvanceFrames(3);

        var nodes = bar.Host.Canvas.AccessibilityManager.Snapshot;
        Assert.Contains(nodes, n => n.Role == Aria.RoleMenuBar && n.Label == "Menu bar");
        Assert.Contains(nodes, n => n.Role == Aria.RoleMenu && n.Label == "Edit");
        Assert.Contains(nodes, n => n.Role == Aria.RoleMenuItem && n.Label == "Edit" && n.IsExpanded == true); // its menu is open
        Assert.Contains(nodes, n => n.Role == Aria.RoleMenuItem && n.Label == "File" && n.IsExpanded == false);
        Assert.Contains(nodes, n => n.Role == Aria.RoleMenuItem && n.Label == "Paste");
        Assert.Equal("Undo", bar.Focus);
    }
}
