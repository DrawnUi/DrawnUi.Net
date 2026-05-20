using DrawnUi.Draw;
using SkiaSharp;

namespace Pong.Game;

public static class GameExtensions
{
    public static SKRect GetHitBox(this SkiaControl sprite)
    {
        SKPoint position;
        if (sprite.VisualLayer == null)
            position = sprite.GetFuturePositionOnCanvasInPoints();
        else
            position = sprite.VisualLayer.HitBoxWithTransforms.Units.Location;

        return new SKRect(
            position.X, position.Y,
            (float)(position.X + sprite.Width),
            (float)(position.Y + sprite.Height));
    }
}