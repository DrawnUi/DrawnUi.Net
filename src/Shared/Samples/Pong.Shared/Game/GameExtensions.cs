using DrawnUi.Draw;
using SkiaSharp;

namespace Pong.Game;

public static class GameExtensions
{
    /// <summary>
    /// Returns the sprite's collision rect in field-space (logical game coords), built from
    /// Left/Top + Width/Height. The whole game logic (Ball.Top, WIDTH/HEIGHT, paddle.Left,
    /// scoring) lives in field-space, so collisions must stay there too. Using a canvas-space
    /// position here breaks once RescalingCanvas scales/letterboxes the field (mobile/narrow),
    /// where canvas-space != field-space.
    /// </summary>
    public static SKRect GetHitBox(this SkiaControl sprite)
    {
        return new SKRect(
            (float)sprite.Left, (float)sprite.Top,
            (float)(sprite.Left + sprite.Width),
            (float)(sprite.Top + sprite.Height));
    }
}