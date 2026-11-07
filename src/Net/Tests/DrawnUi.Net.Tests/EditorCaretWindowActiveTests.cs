using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A focused editor keeps its focus while the app's window is inactive (another app has the keyboard) but hides its
/// caret, so it neither shows nor blinks; when the window is active again the caret is back in the same field, as in
/// native text fields.
/// </summary>
public class EditorCaretWindowActiveTests
{
    [Fact]
    public void Caret_hides_while_the_window_is_inactive_and_comes_back()
    {
        using var host = new HeadlessCanvasHost(400, 200, scale: 2f, background: Colors.Black);
        var editor = new SkiaEditor { WidthRequest = 300, HeightRequest = 44, MaxLines = 1, FontSize = 20, Text = "hello" };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { editor }
        };
        host.AdvanceFrames(4);
        editor.IsFocused = true;
        host.AdvanceFrames(4);
        Assert.True(editor.Cursor.IsVisible);

        host.Canvas.OnWindowDeactivated();
        host.AdvanceFrames(2);
        Assert.True(editor.IsFocused);
        Assert.False(editor.Cursor.IsVisible);

        host.Canvas.OnWindowActivated();
        host.AdvanceFrames(2);
        Assert.True(editor.IsFocused);
        Assert.True(editor.Cursor.IsVisible);

        // an unfocused editor does not listen: a window change does not bring its caret
        editor.IsFocused = false;
        host.AdvanceFrames(2);
        host.Canvas.OnWindowDeactivated();
        host.Canvas.OnWindowActivated();
        host.AdvanceFrames(2);
        Assert.False(editor.Cursor.IsVisible);
    }
}
