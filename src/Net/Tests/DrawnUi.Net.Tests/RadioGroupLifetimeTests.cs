using System.Runtime.CompilerServices;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Radio groups hold their buttons weakly: a page with radio buttons that is dropped (without Dispose) is collected,
/// while the buttons of a live group still exclude each other.
/// </summary>
public class RadioGroupLifetimeTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference page, WeakReference radio) CreateAndDrop(string groupName)
    {
        var radio = new SkiaRadioButton { GroupName = groupName };
        var page = new SkiaLayout { Children = new List<SkiaControl> { radio, new SkiaRadioButton { GroupName = groupName } } };
        return (new WeakReference(page), new WeakReference(radio));
    }

    private static void Collect(WeakReference weak)
    {
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("leak-check")]
    public void A_dropped_page_with_radio_buttons_is_collected(string groupName)
    {
        var (page, radio) = CreateAndDrop(groupName);
        Collect(page);
        Collect(radio);
        Assert.False(page.IsAlive);
        Assert.False(radio.IsAlive);
    }

    [Fact]
    public void Buttons_of_a_live_group_still_exclude_each_other()
    {
        var a = new SkiaRadioButton();
        var b = new SkiaRadioButton();
        var parent = new SkiaLayout { Children = new List<SkiaControl> { a, b } };
        GC.Collect();

        b.IsToggled = true;

        Assert.False(a.IsToggled);
        Assert.Equal(1, RadioButtons.All.GetSelectedIndex(parent));
        Assert.Same(b, RadioButtons.All.GetSelected(parent));
    }
}
