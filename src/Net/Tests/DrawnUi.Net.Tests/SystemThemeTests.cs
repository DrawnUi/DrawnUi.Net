using System.Runtime.InteropServices;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The system's light or dark appearance: raised once when the head learns it, then once per change, a repeated value
/// dropped (as DrawnUI for Rust and React); the Windows heads read it from AppsUseLightTheme on WM_SETTINGCHANGE.
/// </summary>
public class SystemThemeTests
{
    [Fact]
    public void Learned_once_then_each_change_and_never_a_repeat()
    {
        using var host = new HeadlessCanvasHost(100, 100);
        var got = new List<SystemTheme>();
        Assert.Null(host.Canvas.SystemTheme);
        host.Canvas.SystemThemeChanged += (_, theme) => got.Add(theme);

        host.Canvas.SetSystemTheme(SystemTheme.Dark);
        host.Canvas.SetSystemTheme(SystemTheme.Dark);
        host.Canvas.SetSystemTheme(SystemTheme.Light);

        Assert.Equal(new[] { SystemTheme.Dark, SystemTheme.Light }, got);
        Assert.Equal(SystemTheme.Light, host.Canvas.SystemTheme);
    }

    [Fact]
    public void Only_the_color_set_message_is_a_theme_change()
    {
        var colors = Marshal.StringToHGlobalUni("ImmersiveColorSet");
        var other = Marshal.StringToHGlobalUni("Environment");
        try
        {
            Assert.True(DrawnView.IsWindowsThemeMessage(0x001A, colors));
            Assert.False(DrawnView.IsWindowsThemeMessage(0x001A, other));
            Assert.False(DrawnView.IsWindowsThemeMessage(0x001A, IntPtr.Zero));
            Assert.False(DrawnView.IsWindowsThemeMessage(0x0010, colors));
        }
        finally
        {
            Marshal.FreeHGlobal(colors);
            Marshal.FreeHGlobal(other);
        }
    }

    [Fact]
    public void Windows_reads_the_apps_theme()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var expected = key?.GetValue("AppsUseLightTheme") is int light ? light == 0 ? SystemTheme.Dark : SystemTheme.Light : (SystemTheme?)null;
        Assert.Equal(expected, DrawnView.ReadWindowsSystemTheme());
    }
}
