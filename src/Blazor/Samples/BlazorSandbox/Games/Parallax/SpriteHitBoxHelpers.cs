using SkiaSharp;

namespace ParallaxGameLoop.Game;

internal static class SpriteHitBoxHelpers
{
    public static SKRect Inset(SKRect hitBox, float insetXFactor, float insetYFactor)
    {
        float insetX = hitBox.Width * insetXFactor;
        float insetY = hitBox.Height * insetYFactor;

        return new SKRect(
            hitBox.Left + insetX,
            hitBox.Top + insetY,
            hitBox.Right - insetX,
            hitBox.Bottom - insetY);
    }

    public static SKRect CreateForwardReachHitBox(
        SKRect hitBox,
        bool facingLeft,
        float reach,
        float anchorWidth,
        float topInsetFactor,
        float bottomInsetFactor)
    {
        float topInset = hitBox.Height * topInsetFactor;
        float bottomInset = hitBox.Height * bottomInsetFactor;
        float top = hitBox.Top + topInset;
        float bottom = hitBox.Bottom - bottomInset;

        if (facingLeft)
        {
            return new SKRect(
                hitBox.Left - reach,
                top,
                hitBox.Left + anchorWidth,
                bottom);
        }

        return new SKRect(
            hitBox.Right - anchorWidth,
            top,
            hitBox.Right + reach,
            bottom);
    }
}