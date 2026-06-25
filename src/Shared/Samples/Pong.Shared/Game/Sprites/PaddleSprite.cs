using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace Pong.Game;

public class PaddleSprite : SkiaShape, IWithHitBox
{
    public PaddleSprite(Color color)
    {
        UseCache = SkiaCacheType.GPU;
        HeightRequest = PongGame.PADDLE_HEIGHT;
        WidthRequest = PongGame.PADDLE_WIDTH;
        CornerRadius = PongGame.PADDLE_HEIGHT / 2.0;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        Type = ShapeType.Rectangle;
        BackgroundColor = color;
        StrokeColor = Color.Parse("#CCCCFF");
        StrokeWidth = 2;
        BevelType = BevelType.Bevel;
        Bevel = new SkiaBevel()
        {
            Depth = 4,
            LightColor = Colors.White,
            ShadowColor = Color.Parse("#333333"),
            Opacity = 0.33,
        };
    }

    public void UpdateState(long time, bool force = false)
    {
        if (force || _stateUpdated != time)
        {
            HitBox = this.GetHitBox();
            _stateUpdated = time;
        }
    }

    private long _stateUpdated;

    public SKRect HitBox { get; set; }
}