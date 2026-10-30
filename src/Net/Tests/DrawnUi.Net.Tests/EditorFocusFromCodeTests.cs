using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An editor focused from code (IsFocused = true when a sheet appears) shows its caret at once, at the end of its text,
/// as after a tap. FiltersCamera (2026-10-30): a multiline editor focused in a modal's OnAppeared kept CursorPosition -1
/// and drew no caret until the first key press.
/// </summary>
public class EditorFocusFromCodeTests
{
    private readonly ITestOutputHelper _out;
    public EditorFocusFromCodeTests(ITestOutputHelper o) { _out = o; }

    static (HeadlessCanvasHost host, SkiaEditor editor) Scene(string text)
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 2f, background: Colors.Black);
        var editor = new SkiaEditor
        {
            WidthRequest = 300,
            MaxLines = 3,
            FontSize = 18,
            Text = text,
            PlaceholderText = "Write a comment",
            ReturnType = ReturnType.Send,
        };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { editor }
        };
        host.AdvanceFrames(6);
        return (host, editor);
    }

    /// <summary>Frames with real time between them: the focus reaches the native field after a short delay.</summary>
    static void Settle(HeadlessCanvasHost host)
    {
        for (var i = 0; i < 30; i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(15);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("first line\nsecond")]
    public void FocusedFromCode_CaretAtTheEnd_AndDrawn(string text)
    {
        var (host, editor) = Scene(text);
        using var _ = host;
        Assert.False(editor.IsFocused);

        editor.IsFocused = true;
        Settle(host);

        _out.WriteLine($"'{text}': CursorPosition {editor.CursorPosition}, cursor visible {editor.Cursor.IsVisible} opacity {editor.Cursor.Opacity}");
        Assert.Equal(text.Length, editor.CursorPosition);
        Assert.True(editor.Cursor.IsVisible, "no caret drawn");
        Assert.Same(editor, host.Canvas.FocusedChild);
    }

    [Fact]
    public void FocusedFromCode_KeepsAPositionTheAppSet()
    {
        var (host, editor) = Scene("Hello");
        using var _ = host;

        editor.CursorPosition = 2;
        editor.IsFocused = true;
        Settle(host);

        Assert.Equal(2, editor.CursorPosition);
        Assert.True(editor.Cursor.IsVisible);
    }

    [Fact]
    public void FocusedFromCode_ThenTyped_AppendsAtTheEnd()
    {
        var (host, editor) = Scene("Hello");
        using var _ = host;

        editor.IsFocused = true;
        Settle(host);
        editor.StubTypeText("!");
        Settle(host);

        Assert.Equal("Hello!", editor.Text);
        Assert.Equal(6, editor.CursorPosition);
    }
}
