using System.Reflection;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Xunit;

namespace UnitTests;

/// <summary>
/// FiltersCamera (2026-10-14, dotnet-dump of a hung app): a modal that froze the layout hides every other modal in
/// the stack (SetupModalsVisibility), and PopModalAsync refused a hidden modal by returning null while it stayed in
/// the stack. Popping the modal below from the top modal's OnDisappearing (its slide-out ended, the layout not
/// unfrozen yet) failed every time and the app's retry loop spun. The MAUI shell has no canvas here: the modals
/// are stacked by hand in the state a freezing push leaves, and the slide-out end is FinalizeTransition's close
/// branch (LeaveModalStack, then RemoveModal).
/// </summary>
public class ShellModalPopTests
{
    class Probe : SkiaLayout, IVisibilityAware
    {
        public Action Disappearing;
        public int DisappearingCount;
        public override void OnDisappearing()
        {
            DisappearingCount++;
            Disappearing?.Invoke();
        }
    }

    record Modal(SkiaShell.ModalWrapper Wrapper, SkiaShell.PageInStack InStack, Probe Content);

    static SkiaShell CreateShell()
    {
        var root = new SkiaLayout { Tag = "RootLayout" };
        return new SkiaShell
        {
            ShellLayout = new SkiaLayout { Tag = "ShellLayout", Children = { root } },
            RootLayout = root,
        };
    }

    /// <summary>A modal as PushModalAsync leaves it once open; a freezing one hides the root and every other modal.</summary>
    static async Task<Modal> Push(SkiaShell shell, bool freezes = true)
    {
        var content = new Probe();
        var wrapper = new SkiaShell.ModalWrapper(true, true, freezes, Colors.Black, shell);
        wrapper.WrapContent(content);
        var inStack = new SkiaShell.PageInStack { Page = wrapper };
        shell.NavigationStackModals.AddLast(inStack);
        wrapper.SetParent(shell.ShellLayout);
        if (freezes)
        {
            shell.FreezingModals.Add(wrapper);
            shell.RootLayout.IsVisible = false;
            await shell.SetupModalsVisibility(wrapper);
        }

        return new Modal(wrapper, inStack, content);
    }

    /// <summary>FinalizeTransition's close branch: the drawer reached its closed snap.</summary>
    static Task SlideOutEnded(SkiaShell shell, Modal modal)
    {
        typeof(SkiaShell).GetMethod("LeaveModalStack", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(shell, new object[] { modal.Wrapper });
        return (Task)typeof(SkiaShell).GetMethod("RemoveModal", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(shell, new object[] { modal.Wrapper, true })!;
    }

    [Fact]
    public async Task ModalHiddenUnderFrozenTop_PopIsHonored()
    {
        var shell = CreateShell();
        var lower = await Push(shell);
        var top = await Push(shell);
        Assert.False(lower.Wrapper.IsVisibleInViewTree());

        var popped = new List<SkiaShellNavigatedArgs>();
        shell.Navigated += (_, e) => { if (e.Source == NavigationSource.Pop) popped.Add(e); };

        var result = await shell.PopModalAsync(lower.InStack, true);

        Assert.Same(lower.Content, result);
        Assert.DoesNotContain(lower.InStack, shell.NavigationStackModals);
        Assert.Equal(1, lower.Content.DisappearingCount);
        Assert.Single(popped);
        Assert.Null(lower.Wrapper.Parent);

        // the top modal is untouched
        Assert.Contains(top.InStack, shell.NavigationStackModals);
        Assert.True(top.Wrapper.IsVisibleInViewTree());
        Assert.Contains(top.Wrapper, shell.FreezingModals);
        Assert.False(shell.RootLayout.IsVisible);
    }

    /// <summary>The hang: the modal below is popped from the top modal's OnDisappearing at the end of its slide-out.</summary>
    [Fact]
    public async Task PopFromOnDisappearingOfTop_PopsTheModalBelow()
    {
        var shell = CreateShell();
        var lower = await Push(shell);
        var top = await Push(shell);

        Task<BindableObject> popBelow = null;
        top.Content.Disappearing = () => popBelow = shell.PopModalAsync(lower.InStack, true);

        await SlideOutEnded(shell, top);

        Assert.NotNull(popBelow);
        var finished = await Task.WhenAny(popBelow, Task.Delay(2000));
        Assert.Same(popBelow, finished);
        Assert.Same(lower.Content, await popBelow);
        Assert.Empty(shell.NavigationStackModals);
        Assert.Empty(shell.FreezingModals);
        Assert.True(shell.RootLayout.IsVisible);
    }

    /// <summary>ModalRemoved comes last: the layout is unfrozen and the modal below shown again.</summary>
    [Fact]
    public async Task ModalRemoved_AfterUnfreezeAndReshow()
    {
        var shell = CreateShell();
        var lower = await Push(shell);
        var top = await Push(shell);

        var removed = new List<SkiaControl>();
        bool? lowerShown = null, topDetached = null, topInStack = null;
        shell.ModalRemoved += (_, content) =>
        {
            removed.Add(content);
            lowerShown = lower.Wrapper.IsVisibleInViewTree();
            topDetached = top.Wrapper.Parent == null;
            topInStack = shell.NavigationStackModals.Contains(top.InStack);
        };

        await SlideOutEnded(shell, top);

        Assert.Equal(new SkiaControl[] { top.Content }, removed);
        Assert.True(lowerShown);
        Assert.True(topDetached);
        Assert.False(topInStack);
    }

    [Fact]
    public async Task PopOnEmptyStack_ReturnsNull()
    {
        var shell = CreateShell();
        Assert.Null(await shell.PopModalAsync(true));
    }
}
