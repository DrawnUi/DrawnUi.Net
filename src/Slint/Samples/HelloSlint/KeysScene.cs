using DrawnUi.Draw;
using Color = DrawnUi.Color;

/// A ball that the arrow keys move, through DrawnUI's keyboard events, once the view has focus.
internal static class KeysScene
{
    public static SkiaControl Create()
    {
        SkiaShape ball = null!;
        return new SkiaLayer
        {
            Children = new List<SkiaControl>
            {
                new SkiaLabel("Click here, then use the arrow keys").WithFontSize(14).WithTextColor(Color.Parse("#C9D3E3")).WithMargin(12),
                new SkiaShape
                {
                    Type = ShapeType.Circle,
                    WidthRequest = 40,
                    HeightRequest = 40,
                    BackgroundColor = Color.Parse("#F3C151"),
                }.Center().Assign(out ball),
            },
        }.Fill().OnKeyDown((me, key) =>
        {
            switch (key)
            {
                case InputKey.ArrowLeft: ball.TranslationX -= 20; break;
                case InputKey.ArrowRight: ball.TranslationX += 20; break;
                case InputKey.ArrowUp: ball.TranslationY -= 20; break;
                case InputKey.ArrowDown: ball.TranslationY += 20; break;
            }
        });
    }
}
