using System.Numerics;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace Pong.Game;

public class BallSprite : SkiaShape, IWithHitBox
{
    public BallSprite()
    {
        UseCache = SkiaCacheType.GPU;
        HeightRequest = 14;
        LockRatio = 1;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        Type = ShapeType.Circle;
        StrokeColor = Colors.White;
        StrokeWidth = 2;
        BackgroundColor = Colors.Yellow;
        BevelType = BevelType.Bevel;
        Bevel = new SkiaBevel()
        {
            Depth = 3,
            LightColor = Colors.White,
            ShadowColor = Color.Parse("#333333"),
            Opacity = 0.33
        };
    }

    public float Angle
    {
        get => _angle;
        set
        {
            _angle = ClampAngleFromHorizontal(value);
            TrackAngleForOscillation(_angle);
        }
    }

    public bool IsMoving { get; set; }

    public float Speed { get; set; } = PongGame.BALL_SPEED;

    public void UpdateState(long time, bool force = false)
    {
        if (force || _stateUpdated != time)
        {
            HitBox = this.GetHitBox();
            _stateUpdated = time;
        }
    }

    public void UpdatePosition(float deltaSeconds)
    {
        if (deltaSeconds <= 0 || !IsMoving)
            return;

        Left += Speed * MathF.Cos(Angle) * deltaSeconds;
        Top += Speed * MathF.Sin(Angle) * deltaSeconds;
    }

    public Vector2 Direction => new(MathF.Cos(Angle), MathF.Sin(Angle));

    public Vector2 Position => new(HitBox.MidX, HitBox.MidY);

    public void SetDirection(Vector2 direction)
    {
        if (direction == Vector2.Zero)
            return;

        var normalizedDirection = Vector2.Normalize(direction);
        Angle = MathF.Atan2(normalizedDirection.Y, normalizedDirection.X);
    }

    public static float ClampAngleFromHorizontal(float angle, float min = MathF.PI / 10.0f)
    {
        float twoPi = 2.0f * MathF.PI;
        float normalizedAngle = angle % twoPi;
        if (normalizedAngle <= -MathF.PI)
            normalizedAngle += twoPi;
        else if (normalizedAngle > MathF.PI)
            normalizedAngle -= twoPi;

        bool nearZero = MathF.Abs(normalizedAngle) < min;
        bool nearPi = MathF.Abs(normalizedAngle) > (MathF.PI - min);

        if (!nearZero && !nearPi)
            return normalizedAngle;

        float sign = MathF.Sign(normalizedAngle);
        if (sign == 0)
            sign = 1;

        return nearZero ? sign * min : sign * (MathF.PI - min);
    }

    private void TrackAngleForOscillation(float newAngle)
    {
        if (!IsMoving)
        {
            ResetOscillation();
            return;
        }

        _h3 = _h2;
        _h2 = _h1;
        _h1 = newAngle;
        if (!float.IsNaN(_h3))
            CheckOscillation();
    }

    private void CheckOscillation()
    {
        const float tolerance = 0.01f;
        if (MathF.Abs(_h1 - _h3) < tolerance && MathF.Abs(_h2 - _h1) > tolerance)
        {
            if (++_oscillationCount >= 6)
                UnstickBall();
        }
        else
        {
            _oscillationCount = 0;
        }
    }

    private void UnstickBall()
    {
        float nudge = (float)(new Random().NextDouble() - 0.5) * 0.4f;
        _angle = ClampAngleFromHorizontal(_angle + nudge);
        ResetOscillation();
    }

    private void ResetOscillation()
    {
        _h1 = float.NaN;
        _h2 = float.NaN;
        _h3 = float.NaN;
        _oscillationCount = 0;
    }

    private long _stateUpdated;
    private float _angle;
    private float _h1 = float.NaN;
    private float _h2 = float.NaN;
    private float _h3 = float.NaN;
    private int _oscillationCount;

    public SKRect HitBox { get; set; }
}
