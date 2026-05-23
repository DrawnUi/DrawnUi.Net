using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Views;
using Color = DrawnUi.Color;

internal sealed class DemoScene : SkiaLayout
{
    private readonly SkiaLabel _status;

    public DemoScene()
    {
        Type = LayoutType.Column;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        BackgroundColor = Color.FromArgb("#11161C");

        var title = new SkiaLabel
        {
            Text = "DrawnUi.Net on a GPU-backed SKSurface",
            FontSize = 34,
            TextColor = Colors.White,
            Margin = new Thickness(24, 24, 24, 12)
        };

        var subtitle = new SkiaLabel
        {
            Text = "Host: OpenTK GameWindow -> OpenGL framebuffer -> GRContext -> SKSurface -> DrawnUI",
            FontSize = 18,
            TextColor = Color.FromArgb("#9FB3C8"),
            Margin = new Thickness(24, 0, 24, 18)
        };

        _status = new SkiaLabel
        {
            Text = "Click the editor, type, or tap the button.",
            FontSize = 18,
            TextColor = Color.FromArgb("#D6E4F0"),
            Margin = new Thickness(24, 0, 24, 12)
        };

        var notes = new SkiaLabel
        {
            Text = "This sample forwards OpenTK mouse and keyboard events into DrawnUI. Use it to validate focus, taps, typing, and drag selection.",
            FontSize = 16,
            TextColor = Color.FromArgb("#C4CED8"),
            Margin = new Thickness(24, 0, 24, 24)
        };

        var editor = new SkiaEditor
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 180,
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            TextColor = Colors.Black,
            CursorColor = Color.FromArgb("#2E86DE"),
            SelectionColor = Color.FromArgb("#55358CFF"),
            PlaceholderText = "Click here and type. Drag to select. Backspace/Delete/Enter/Home/End/Arrows work.",
            PlaceholderColor = Color.FromArgb("#728197"),
            FontSize = 22,
            MaxLines = 6,
            Margin = new Thickness(24, 0, 24, 18),
            Padding = new Thickness(16, 12)
        };

        var button = new SkiaButton("Tap Me")
        {
            HorizontalOptions = LayoutOptions.Start,
            HeightRequest = 46,
            Margin = new Thickness(24, 0, 24, 0),
            BackgroundColor = Color.FromArgb("#2E86DE"),
            TextColor = Colors.White,
            Padding = new Thickness(18, 10)
        };

        button.Tapped += (_, _) => _status.Text = "Button tapped via DrawnUI gesture routing.";
        editor.FocusChanged += (_, focused) =>
            _status.Text = focused ? "Editor focused. Type into the window." : "Editor focus cleared.";
        editor.TextChanged += (_, text) =>
            _status.Text = $"Editor text length: {text?.Length ?? 0}";

        Children = new List<SkiaControl>
        {
            title,
            subtitle,
            _status,
            notes,
            editor,
            button
        };
    }
}
