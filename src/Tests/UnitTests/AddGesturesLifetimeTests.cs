using System.Runtime.CompilerServices;
using System.Windows.Input;
using DrawnUi.Draw;
using Xunit;

namespace UnitTests;

/// <summary>
/// AddGestures holds the controls it gave a gesture listener weakly: a page that used the attached gesture properties
/// and was dropped without Dispose is collected, while a live control keeps its listener.
/// </summary>
public class AddGesturesLifetimeTests : DrawnTestsBase
{
    private sealed class Noop : ICommand
    {
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDrop()
    {
        var child = new SkiaControl();
        var page = new SkiaLayout { Children = new List<SkiaControl> { child } };
        AddGestures.SetCommandTapped(child, new Noop());
        Assert.True(AddGestures.AttachedListeners.TryGetValue(child, out _));
        return new WeakReference(page);
    }

    [Fact]
    public void A_dropped_page_with_attached_gestures_is_collected()
    {
        var weak = CreateAndDrop();
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void A_live_control_keeps_its_listener_until_the_command_is_cleared()
    {
        var child = new SkiaControl();
        var page = new SkiaLayout { Children = new List<SkiaControl> { child } };
        AddGestures.SetCommandTapped(child, new Noop());
        GC.Collect();
        Assert.True(AddGestures.AttachedListeners.TryGetValue(child, out _));

        AddGestures.SetCommandTapped(child, null);
        Assert.False(AddGestures.AttachedListeners.TryGetValue(child, out _));
        GC.KeepAlive(page);
    }
}
