using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Models;
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

        Children = new List<SkiaControl>
        {
            new SkiaLabel
            {
                Text = "DrawnUi.Net on a GPU-backed SKSurface",
                FontSize = 34,
                TextColor = Colors.White,
                Margin = new Thickness(24, 24, 24, 12)
            }.WithAccessibilityText(),

            new SkiaLabel
            {
                Text = "Host: OpenTK GameWindow -> OpenGL framebuffer -> GRContext -> SKSurface -> DrawnUI",
                FontSize = 18,
                TextColor = Color.FromArgb("#9FB3C8"),
                Margin = new Thickness(24, 0, 24, 18)
            }.WithAccessibilityText(),

            new SkiaLabel
            {
                Text = "Click the editor, type, or tap the button.",
                FontSize = 18,
                TextColor = Color.FromArgb("#D6E4F0"),
                Margin = new Thickness(24, 0, 24, 12)
            }.Assign(out _status)
             .WithAccessibilityText(),

            new SkiaLabel
            {
                Text = "This sample forwards OpenTK mouse and keyboard events into DrawnUI. Use it to validate focus, taps, typing, and drag selection.",
                FontSize = 16,
                TextColor = Color.FromArgb("#C4CED8"),
                Margin = new Thickness(24, 0, 24, 24)
            },

            new SkiaEditor
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
            }
            .WithAccessibility(Aria.RoleTextBox, "Demo editor", "Type here to test input", canInteract: true)
            .OnFocusChanged((me, focused) =>
                _status.Text = focused ? "Editor focused. Type into the window." : "Editor focus cleared.")
            .OnTextChanged(text =>
                _status.Text = $"Editor text length: {text?.Length ?? 0}"),

            new SkiaButton("Tap Me")
            {
                HorizontalOptions = LayoutOptions.Start,
                HeightRequest = 46,
                Margin = new Thickness(24, 0, 24, 0),
                BackgroundColor = Color.FromArgb("#2E86DE"),
                TextColor = Colors.White,
                Padding = new Thickness(18, 10)
            }
            .WithAccessibilityButton("Tap Me", "Triggers a demo action")
            .OnTapped(me =>
                _status.Text = "Button tapped via DrawnUI gesture routing.")
        };
    }
}
