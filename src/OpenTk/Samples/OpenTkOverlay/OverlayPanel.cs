using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

/// <summary>
/// A semi-transparent DrawnUI panel centered over the 3D scene.
/// Demonstrates gesture routing, text input, and live state updates from the GL render loop.
/// </summary>
internal sealed class OverlayPanel : SkiaShape
{
    private readonly SkiaLabel _angleLabel;
    private readonly SkiaLabel _statusLabel;

    public OverlayPanel(Action onReset)
    {
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        WidthRequest = 390;
        CornerRadius = 16;
        Children = new List<SkiaControl>
        {

            new SkiaBackdrop()
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Blur = 0, //backdrop has default blur
                VisualEffects = new List<SkiaEffect>
                {
                    new GlassBackdropEffect()
                    {
                        EdgeOpacity = 0.58f,
                        EdgeGlow = 0.85f,
                        Depth = 1.66f,
                        Emboss = 18.5f,
                        BlurStrength = 1.0f,
                        Opacity = 0.9f,
                        Tint = Colors.White.WithAlpha(0.05f),
                        CornerRadius = 16,
                    }
                }
            },

            new SkiaStack()
            {
                Padding = new Thickness(32),
                UseCache = SkiaCacheType.Operations,
                Children =
                {
                    new SkiaLabel
                    {
                        UseCache = SkiaCacheType.Operations,
                        Text = "DrawnUI.OpenTK",
                        FontFamily = "FontGame",
                        FontSize = 26,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Fill,
                        Margin = new Thickness(0, 0, 0, 6),
                    },
                    new SkiaRichLabel
                    {
                        UseCache = SkiaCacheType.Operations,
                        Text = "DrawnUI UI `Canvas` drawn over existing OpenTK 3D content 😆\nUses `CanvasHost` on top of an existing OpenGL 3D scene.\nThe text input below is totally drawn including cursor! 👍",
                        FontSize = 15,
                        TextColor = Colors.Black,
                        CodeTextColor = Colors.Red,
                        CodeBackgroundColor = Colors.Transparent,
                        HorizontalOptions = LayoutOptions.Fill,
                        Margin = new Thickness(0, 0, 0, 22),
                    },

                    new SkiaLabel
                    {
                        UseCache = SkiaCacheType.Operations,
                        IsParentIndependent = true,
                        Text = "Y Rotation: 0.0°",
                        FontSize = 18,
                        TextColor = Color.FromArgb("#7EC8F0"),
                        HorizontalOptions = LayoutOptions.Fill,
                        Margin = new Thickness(0, 0, 0, 14),
                    }.Assign(out _angleLabel),

                    new SkiaEditor
                    {
                        UseCache = SkiaCacheType.None,
                        HorizontalOptions = LayoutOptions.Fill,
                        HeightRequest = 50,
                        BackgroundColor = Color.FromArgb("#111E33"),
                        TextColor = Colors.White,
                        CursorColor = Color.FromArgb("#2563EB"),
                        SelectionColor = Color.FromArgb("#552563EB"),
                        PlaceholderText = "Enter your name…",
                        PlaceholderColor = Color.FromArgb("#3D5470"),
                        FontSize = 17,
                        Padding = new Thickness(12, 10),
                        Margin = new Thickness(0, 0, 0, 14),
                    }.OnTextChanged(text =>
                    {
                        _statusLabel.Text = string.IsNullOrEmpty(text)
                            ? "Type something or tap Reset."
                            : $"Typed: \"{text}\"";
                    }),

                    new SkiaLabel
                    {
                        UseCache = SkiaCacheType.Operations,
                        IsParentIndependent = true,
                        Text = "Type something or tap Reset.",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#6B8FAD"),
                        HorizontalOptions = LayoutOptions.Fill,
                        Margin = new Thickness(0, 0, 0, 14),
                    }.Assign(out _statusLabel),

                    new SkiaButton("Reset Rotation")
                    {
                        UseCache = SkiaCacheType.Image,
                        IsParentIndependent = true,
                        HorizontalOptions = LayoutOptions.Fill,
                        BackgroundColor = Color.FromArgb("#2563EB"),
                        TextColor = Colors.White,
                        FontFamily = "FontGame",
                        FontSize = 14,
                        Padding = new Thickness(0, 13),
                        CornerRadius = 8,
                    }.OnTapped(me =>
                    {
                        onReset();
                        _statusLabel.Text = "Rotation reset to 0°.";
                    })
                }
            }
        };

    }

    public void UpdateAngle(float degrees) =>
        _angleLabel.Text = $"Y Rotation: {degrees:F1}°";
}
