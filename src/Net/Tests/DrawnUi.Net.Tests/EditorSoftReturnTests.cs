using System.Reflection;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The return key of a phone's on-screen keyboard in a single-line editor ends editing on the web heads, as a native
/// single-line field does on phones (drawnui-cross 6ck): the editor submits and loses its focus, so the keyboard closes.
/// A physical keyboard's Enter submits and keeps the focus; a multi-line editor gets its new line either way.
/// </summary>
public class EditorSoftReturnTests
{
    static readonly FieldInfo SoftKeyboard =
        typeof(KeyboardManager).GetField("SoftKeyboardInput", BindingFlags.NonPublic | BindingFlags.Static)!;

    static (HeadlessCanvasHost host, SkiaEditor editor) Scene(int maxLines)
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 2f, background: Colors.Black);
        var editor = new SkiaEditor { WidthRequest = 300, MaxLines = maxLines, FontSize = 18, Text = "Ann" };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { editor }
        };
        host.AdvanceFrames(6);
        editor.IsFocused = true;
        for (var i = 0; i < 20; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(15);
        }
        return (host, editor);
    }

    [Theory]
    [InlineData(true, false)]  // on-screen keyboard: editing ends
    [InlineData(false, true)]  // physical keyboard: the field keeps its focus
    public void SingleLine_Return(bool soft, bool staysFocused)
    {
        var (host, editor) = Scene(1);
        using var _ = host;
        var submitted = 0;
        editor.TextSubmitted += (_, _) => submitted++;
        SoftKeyboard.SetValue(null, soft);
        try
        {
            editor.StubPressEnter();
        }
        finally
        {
            SoftKeyboard.SetValue(null, false);
        }

        Assert.Equal(1, submitted);
        Assert.Equal(staysFocused, editor.IsFocused);
        Assert.Equal("Ann", editor.Text);
    }

    [Fact]
    public void MultiLine_SoftReturn_AddsALine_AndKeepsTheFocus()
    {
        var (host, editor) = Scene(3);
        using var _ = host;
        editor.CursorPosition = 3;
        SoftKeyboard.SetValue(null, true);
        try
        {
            editor.StubPressEnter();
        }
        finally
        {
            SoftKeyboard.SetValue(null, false);
        }

        Assert.True(editor.IsFocused);
        Assert.Contains("\n", editor.Text);
    }
}
