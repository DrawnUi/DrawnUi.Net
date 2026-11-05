using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Testing;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A control flagged AccessibilityModal is held weakly by the shared modal registry: a page with a dialog that is
/// dropped without Dispose is collected.
/// </summary>
[Collection("Multithreaded")] // forces full GCs: never in parallel with timing tests
public class ModalRegistryLifetimeTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDrop()
    {
        var dialog = new SkiaLayout { AccessibilityModal = true };
        var page = new SkiaLayout { Children = new List<SkiaControl> { dialog } };
        return new WeakReference(page);
    }

    [Fact]
    public void A_dropped_page_with_a_modal_dialog_is_collected()
    {
        var weak = CreateAndDrop();
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
    }
}
