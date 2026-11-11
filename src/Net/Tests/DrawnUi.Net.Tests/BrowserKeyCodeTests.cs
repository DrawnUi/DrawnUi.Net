using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>The browser heads (pure WebAssembly, Blazor) turn a DOM KeyboardEvent.code into the same keys as the desktop heads.</summary>
public class BrowserKeyCodeTests
{
    [Theory]
    [InlineData("Enter", InputKey.Enter)]
    [InlineData("NumpadEnter", InputKey.Enter)] // the numpad's Enter is Enter, as on WPF, OpenTK and MAUI Windows
    [InlineData("Space", InputKey.Space)]
    [InlineData("KeyP", InputKey.KeyP)]
    [InlineData("F1", InputKey.F1)]
    [InlineData("Numpad5", InputKey.Numpad5)]
    [InlineData("NoSuchKey", InputKey.Unknown)]
    [InlineData("", InputKey.Unknown)]
    [InlineData(null, InputKey.Unknown)]
    public void CodesMapToKeys(string code, InputKey key)
    {
        Assert.Equal(key, KeyboardManager.MapBrowserCode(code));
    }
}
