using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaControl.AccessibilityModal: a dialog drawn over the screen keeps screen readers and Tab inside it while it is
/// shown, takes the keyboard when it appears and gives it back when it goes; the topmost one wins.
/// </summary>
[Collection("Tooltips")] // popup windows and keys go through the static KeyboardManager
public class AccessibilityModalTests
{
    static SkiaShape Button(string name) => new()
    {
        WidthRequest = 120, HeightRequest = 30, AccessibilityRole = Aria.RoleButton, AccessibilityCanInteract = true, AccessibilityLabel = name,
    };

    static SkiaLayer Dialog(string a, string b) => new()
    {
        AccessibilityModal = true,
        BackgroundColor = Colors.Black,
        Children = new List<SkiaControl>
        {
            new SkiaStack { Spacing = 4, Margin = new Thickness(40), Children = { Button(a), Button(b) } },
        },
    };

    static HeadlessCanvasHost Scene(out SkiaLayer page)
    {
        var host = new HeadlessCanvasHost(400, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = { new SkiaStack { Spacing = 4, Children = { Button("Play"), Button("Settings"), Button("About") } } },
        }.Assign(out page);
        host.AdvanceFrames(4);
        return host;
    }

    static string[] Labels(HeadlessCanvasHost host) =>
        host.Canvas.AccessibilityManager.Snapshot.Where(n => n.CanInteract).Select(n => n.Label).ToArray();

    static string Focused(HeadlessCanvasHost host) => (host.Canvas.KeyboardFocusNode as SkiaControl)?.AccessibilityLabel;

    static void Tab(HeadlessCanvasHost host)
    {
        host.Canvas.HandleKeyboardNavigation(InputKey.Tab, false);
        host.AdvanceFrames(3);
    }

    [Fact]
    public void Modal_LimitsTheSnapshotAndTab_TakesTheKeyboard_GivesItBack()
    {
        using var host = Scene(out var page);
        Assert.Equal(new[] { "Play", "Settings", "About" }, Labels(host));

        Tab(host);
        Tab(host);
        Assert.Equal("Settings", Focused(host));

        var dialog = Dialog("Sound", "Close");
        page.AddSubView(dialog);
        host.AdvanceFrames(4);

        Assert.Equal(new[] { "Sound", "Close" }, Labels(host));
        Assert.Equal("Sound", Focused(host)); // the keyboard moved in
        Assert.Contains(host.Canvas.AccessibilityManager.Snapshot, n => n.Role == Aria.RoleDialog);

        Tab(host);
        Assert.Equal("Close", Focused(host));
        Tab(host); // past the end: round to the dialog's first control, never to the menu under it
        Assert.Equal("Sound", Focused(host));

        page.RemoveSubView(dialog);
        dialog.Dispose();
        host.AdvanceFrames(4);

        Assert.Equal(new[] { "Play", "Settings", "About" }, Labels(host));
        Assert.Equal("Settings", Focused(host)); // back where it was
    }

    [Fact]
    public void MouseOpenedDialog_ArrowStartsInside_NotAtTheControlBehind()
    {
        using var host = Scene(out var page);
        host.Canvas.ArrowsStartKeyboardNavigation = true;
        var settings = (SkiaControl)((SkiaStack)page.Views[0]).Views[1];
        host.Canvas.AccessibilityManager.NotifyFocused(settings); // tapped with the mouse, which opened the dialog
        page.AddSubView(Dialog("Sound", "Close"));
        host.AdvanceFrames(4);
        Assert.True(host.Canvas.HasNoKeyboardFocus);

        Assert.True(host.Canvas.HandleKeyboardNavigation(InputKey.ArrowDown, false));
        host.AdvanceFrames(3);
        Assert.Equal("Sound", Focused(host));
    }

    [Fact]
    public void FocusedControlLeaves_NoScreenReader_FocusGoesToNothing()
    {
        {
            foreach (var reader in new[] { false, true })
            {
                using var host = Scene(out var page);
                var manager = host.Canvas.AccessibilityManager;
                manager.IsScreenReaderRunning = () => reader;
                // as the WPF and MAUI Windows heads do: the reader moves to the node, which takes the focus
                manager.ReaderRefocusRequested += next => manager.NotifyFocused(page.Views.First(v => v.AccessibilityLabel == next.Label));
                Tab(host);
                Assert.Equal("Play", Focused(host));
                var play = (SkiaControl)host.Canvas.KeyboardFocusNode;
                manager.NotifyReaderFocused(play); // as a head reports the reader's cursor

                // the game screen replaces the menu: one gear button, the menu is disposed
                page.ClearChildren();
                play.Dispose();
                page.AddSubView(Button("Gear"));
                host.AdvanceFrames(6);

                Assert.True(play.GetAccessibilityPixelRect().IsEmpty); // a disposed control is on no screen: no ring around it
                if (reader)
                {
                    Assert.Equal("Gear", (manager.ReaderNode as SkiaControl)?.AccessibilityLabel);
                    Assert.Equal("Gear", Focused(host)); // the keyboard focus and its ring follow the reader
                }
                else
                {
                    Assert.Null(host.Canvas.KeyboardFocusNode); // the keys belong to the game again
                    Assert.True(host.Canvas.HasNoKeyboardFocus);
                }
            }
        }
    }

    [Fact]
    public void Dialog_KeepsItsName_WhenItsTitleSaysItToo()
    {
        using var host = Scene(out var page);
        var dialog = Dialog("Sound", "Close");
        dialog.AccessibilityLabel = "Settings";
        ((SkiaStack)dialog.Views[0]).Children.Insert(0, new SkiaLabel("Settings") { AccessibilityRole = Aria.RoleHeading });
        page.AddSubView(dialog);
        host.AdvanceFrames(4);

        var snapshot = host.Canvas.AccessibilityManager.Snapshot;
        Assert.Contains(snapshot, n => n.Role == Aria.RoleDialog && n.Label == "Settings" && !n.NamedByChild);
        Assert.Contains(snapshot, n => n.Role == Aria.RoleHeading && n.Label == "Settings");
    }

    /// <summary>
    /// A popup host without a name (SkiaShell's layer) around a named dialog: readers meet one dialog, the named one
    /// (drawnui-cross 6bs). The heads' overlay becomes ActiveModalNode.
    /// </summary>
    [Fact]
    public void UnnamedModalAroundNamedDialog_IsOneNamedDialog()
    {
        using var host = Scene(out var page);
        var help = Dialog("Back", "OK");
        help.AccessibilityModal = false;
        help.AccessibilityRole = Aria.RoleDialog;
        help.AccessibilityLabel = "HOW TO PLAY";
        var wrapper = new SkiaLayer { AccessibilityModal = true, Children = { help } };
        page.AddSubView(wrapper);
        host.AdvanceFrames(4);

        var manager = host.Canvas.AccessibilityManager;
        var dialogs = manager.Snapshot.Where(n => n.Role == Aria.RoleDialog).ToArray();
        Assert.Single(dialogs);
        Assert.Equal("HOW TO PLAY", dialogs[0].Label);
        Assert.Same(wrapper, manager.ActiveModal);
        Assert.Same(help, manager.ActiveModalNode);
        Assert.Equal(new[] { "Back", "OK" }, Labels(host));
    }

    /// <summary>
    /// The keyboard goes back to the control that opened the dialog also when the app shows that control again only after
    /// the close (the Dungeon hides its HUD under a dialog): the return waits for it.
    /// </summary>
    [Fact]
    public void Close_ReturnsToTheOpener_ShownAgainAfterTheClose()
    {
        using var host = Scene(out var page);
        Tab(host);
        Tab(host);
        Assert.Equal("Settings", Focused(host));
        var menu = (SkiaStack)page.Views[0];

        var dialog = Dialog("Sound", "Close");
        page.AddSubView(dialog);
        host.AdvanceFrames(2);
        menu.IsVisible = false; // the app hides what is under its dialog
        host.AdvanceFrames(4);
        Assert.Equal("Sound", Focused(host));

        page.RemoveSubView(dialog);
        dialog.Dispose();
        host.AdvanceFrames(10); // the close fades: the menu comes back a little later
        menu.IsVisible = true;
        host.AdvanceFrames(6);

        Assert.Equal("Settings", Focused(host));
    }

    /// <summary>
    /// The Dungeon's hand-over (drawnui-cross 6bs): Help opens "HOW TO PLAY" with the keyboard, Escape closes it and opens
    /// "PAUSED" in the same step, Escape again closes that one, and the keyboard is back on Help, which the app hides
    /// while a dialog is shown.
    /// </summary>
    [Theory]
    [InlineData(true)]  // the pause opens in the same step as the help closes
    [InlineData(false)] // the help fades out first, the pause opens after
    public async Task DialogHandsOverToAnother_ClosingTheLast_ReturnsToTheOpener(bool sameStep)
    {
        using var host = new HeadlessCanvasHost(400, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaStack hud;
        var shell = new SkiaShell
        {
            RootContent = new SkiaStack { Spacing = 4, Children = { Button("Play"), Button("Help") } }.Assign(out hud),
        };
        host.Canvas.Content = shell;
        host.AdvanceFrames(4);

        async Task Run(Task task, string step)
        {
            // frames until the step is done: an unfinished task is reported, never awaited (that hangs the run)
            var frames = 0;
            for (var wait = Stopwatch.StartNew(); !task.IsCompleted && wait.ElapsedMilliseconds < 10000; frames++)
            {
                host.AdvanceFrames(1);
                if (frames > 120)
                    Thread.Sleep(1);
            }
            Assert.True(task.IsCompleted, $"{step} did not finish after {frames} frames");
            await task;
            host.AdvanceFrames(4);
        }

        static SkiaLayer Named(string title, string a, string b) => new()
        {
            AccessibilityRole = Aria.RoleDialog,
            AccessibilityLabel = title,
            WidthRequest = 220,
            Children = { new SkiaStack { Spacing = 4, Children = { Button(a), Button(b) } } },
        };

        Tab(host);
        Tab(host);
        Assert.Equal("Help", Focused(host));

        // Enter on Help: the dialog opens, the app hides its HUD under it
        var opening = shell.OpenPopupAsync(Named("HOW TO PLAY", "Link", "OK"), showOverlay: false);
        hud.IsVisible = false;
        await Run(opening, "help open");
        Assert.Equal("Link", Focused(host));

        // Escape: the help closes and the pause opens
        if (sameStep)
        {
            var closing = shell.ClosePopupAsync(animated: false);
            var pausing = shell.OpenPopupAsync(Named("PAUSED", "Resume", "Home"), showOverlay: false);
            await Run(Task.WhenAll(closing, pausing), "help close + pause open");
        }
        else
        {
            await Run(shell.ClosePopupAsync(), "help close");
            await Run(shell.OpenPopupAsync(Named("PAUSED", "Resume", "Home"), showOverlay: false), "pause open");
        }
        Assert.Equal("Resume", Focused(host));

        // Escape again: the pause closes, the app shows its HUD after the close
        await Run(shell.ClosePopupAsync(), "pause close");
        hud.IsVisible = true;
        host.AdvanceFrames(6);
        Assert.Equal("Help", Focused(host));
    }

    [Fact]
    public async Task ShellPopup_KeepsReadersAndTabInside()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        var shell = new SkiaShell
        {
            RootContent = new SkiaStack { Spacing = 4, Children = { Button("Play"), Button("Settings") } },
        };
        host.Canvas.Content = shell;
        host.AdvanceFrames(4);
        Assert.Equal(new[] { "Play", "Settings" }, Labels(host));

        var opening = shell.OpenPopupAsync(new SkiaStack { WidthRequest = 200, Children = { Button("Sound"), Button("Close") } }, animated: false);
        for (var i = 0; i < 60 && !opening.IsCompleted; i++)
            host.AdvanceFrames(1);
        await opening;
        host.AdvanceFrames(4);

        Assert.Equal(new[] { "Sound", "Close" }, Labels(host));
    }

    [Fact]
    public void TwoModals_TheTopWins_ClosingItHandsOver()
    {
        using var host = Scene(out var page);
        var first = Dialog("A1", "A2");
        var second = Dialog("B1", "B2");
        page.AddSubView(first);
        page.AddSubView(second);
        host.AdvanceFrames(4);
        Assert.Equal(new[] { "B1", "B2" }, Labels(host));

        page.RemoveSubView(second);
        second.Dispose();
        host.AdvanceFrames(4);
        Assert.Equal(new[] { "A1", "A2" }, Labels(host));
    }

    [Fact]
    public void Unflagged_Overlay_ChangesNothing()
    {
        using var host = Scene(out var page);
        var overlay = Dialog("Sound", "Close");
        overlay.AccessibilityModal = false;
        page.AddSubView(overlay);
        host.AdvanceFrames(4);
        Assert.Equal(new[] { "Play", "Settings", "About", "Sound", "Close" }, Labels(host).OrderBy(l => l == "Sound" || l == "Close").ToArray());
        Assert.Equal(5, Labels(host).Length);
    }

    [Fact]
    public void PopupWindow_OpenedOverAModal_StaysReadable()
    {
        using var host = Scene(out var page);
        page.AddSubView(Dialog("Sound", "Close"));
        host.AdvanceFrames(4);

        host.Canvas.OpenPopupWindow(new SkiaStack { WidthRequest = 100, Children = { Button("Low"), Button("High") } }, new SKPoint(200, 200));
        host.AdvanceFrames(4);

        var labels = Labels(host);
        Assert.Contains("Low", labels);
        Assert.Contains("Sound", labels);
        Assert.DoesNotContain("Play", labels);
        host.Canvas.ClosePopupWindows();
        host.AdvanceFrames(2);
    }
}
