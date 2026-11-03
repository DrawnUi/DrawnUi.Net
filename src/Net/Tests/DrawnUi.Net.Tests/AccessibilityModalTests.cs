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
        Tab(host); // past the end: never back to the menu under the dialog
        Assert.NotEqual("Play", Focused(host));
        Assert.NotEqual("About", Focused(host));

        page.RemoveSubView(dialog);
        dialog.Dispose();
        host.AdvanceFrames(4);

        Assert.Equal(new[] { "Play", "Settings", "About" }, Labels(host));
        Assert.Equal("Settings", Focused(host)); // back where it was
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
