using AppoMobi.Gestures;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using DrawnUi.Views;
using Pong.Game.Ai;
using SkiaSharp;

namespace Pong.Game;

public partial class PongGame : DrawnGame
{
    public const float WIDTH = 360f;
    public const float HEIGHT = 640f;
    public const float BALL_SPEED = 300f;
    public const float PADDLE_SPEED = 420f;
    public const float PADDLE_WIDTH = 80f;
    public const float PADDLE_HEIGHT = 16f;
    public const float PADDLE_MARGIN = 40f;
    public const int WIN_SCORE = 7;

    public BallSprite Ball;
    public PaddleSprite PlayerPaddle;
    public PaddleSprite AiPaddle;

    private int _playerScore;
    private int _aiScore;
    private PongAI _ai;

    private float _playerMovement;
    private float _aiMovement;

    private SkiaLabel _scoreLabel;
    private SkiaLabel _messageLabel;

    private enum GamePhase
    {
        WaitingToStart,
        Playing,
        Scored,
        GameOver
    }

    private GamePhase _phase = GamePhase.WaitingToStart;
    private float _phaseTimer;

    private static readonly Color PlayerColor = Color.Parse("#4CC9F0");
    private static readonly Color AiColor = Color.Parse("#FF2222");
    private static readonly Color NetColor = Color.Parse("#334455");

    public PongGame()
    {
        WidthRequest = WIDTH;
        HeightRequest = HEIGHT;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        BackgroundColor = Colors.DarkGreen;
        Type = LayoutType.Absolute;

        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.Transparent,
                StrokeColor = Color.Parse("#FEFEFE"),
                StrokeWidth = 2,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            },

            new PaddleSprite(AiColor)
            {
                Left = (WIDTH - PADDLE_WIDTH) / 2.0,
                Top = PADDLE_MARGIN,
            }.Assign(out AiPaddle),

            new PaddleSprite(PlayerColor)
            {
                Left = (WIDTH - PADDLE_WIDTH) / 2.0,
                Top = HEIGHT - PADDLE_MARGIN - PADDLE_HEIGHT,
            }.Assign(out PlayerPaddle),

            new BallSprite
            {
                Left = (WIDTH - 14) / 2.0,
                Top = HEIGHT - PADDLE_MARGIN - PADDLE_HEIGHT - 14,
            }.Assign(out Ball),

            new SkiaLabel
            {
                Text = "0 : 0",
                FontFamily = "FontGame",
                FontSize = 28,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, HEIGHT / 2.0 - 24, 0, 0),
                HorizontalTextAlignment = DrawTextAlignment.Center,
            }.Assign(out _scoreLabel),

            new SkiaLabel
            {
                Text = "TAP TO SERVE",
                FontFamily = "FontGame",
                FontSize = 14,
                TextColor = Colors.White.WithAlpha(0.8f),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, HEIGHT / 2.0 + 12, 0, 0),
                HorizontalTextAlignment = DrawTextAlignment.Center,
            }.Assign(out _messageLabel),
        };

        ResetBall(true);
        _ai = new PongAI(this, AIDifficulty.Medium);

        StartLoop();
        IgnoreChildrenInvalidations = true;
    }

    private void ResetBall(bool playerServes)
    {
        Ball.Left = (WIDTH - 14) / 2.0;
        Ball.Top = playerServes
            ? HEIGHT - PADDLE_MARGIN - PADDLE_HEIGHT - 14
            : PADDLE_MARGIN + PADDLE_HEIGHT - 1;
        Ball.IsMoving = false;
        Ball.UpdateState(0, true);

        var angle = playerServes
            ? (float)(MathF.PI / 2.0 + (new Random().NextDouble() - 0.5) * 0.6)
            : (float)(-MathF.PI / 2.0 + (new Random().NextDouble() - 0.5) * 0.6);

        Ball.Angle = BallSprite.ClampAngleFromHorizontal(angle);
    }

    private void ResetPaddles()
    {
        PlayerPaddle.Left = (WIDTH - PADDLE_WIDTH) / 2.0;
        AiPaddle.Left = (WIDTH - PADDLE_WIDTH) / 2.0;
        PlayerPaddle.UpdateState(0, true);
        AiPaddle.UpdateState(0, true);
    }

    private void UpdateScoreLabel()
    {
        _scoreLabel.Text = $"{_aiScore} : {_playerScore}";
    }

    public void SetAiMovement(float dir) => _aiMovement = dir;

    public void SetPlayerMovement(float dir) => _playerMovement = dir;

    public void Serve(bool triggeredByAi = false)
    {
        if (_phase == GamePhase.WaitingToStart && (!_aiServes || triggeredByAi))
        {
            Ball.IsMoving = true;
            Ball.Speed = BALL_SPEED;
            _phase = GamePhase.Playing;
            _messageLabel.Text = string.Empty;
            _ai.ResetTimers();
        }
        else if (_phase == GamePhase.GameOver)
        {
            _playerScore = 0;
            _aiScore = 0;
            _aiServes = false;
            _playerHasMoved = false;
            _aiWanderDir = 0;
            UpdateScoreLabel();
            ResetBall(true);
            ResetPaddles();
            _phase = GamePhase.WaitingToStart;
            _messageLabel.Text = "TAP TO SERVE";
        }
    }

    public override void OnKeyDown(InputKey key)
    {
        switch (key)
        {
            case InputKey.ArrowLeft:
                SetPlayerMovement(-1);
                break;
            case InputKey.ArrowRight:
                SetPlayerMovement(1);
                break;
            case InputKey.Space:
            case InputKey.ArrowUp:
            case InputKey.ArrowDown:
            case InputKey.Enter:
                Serve();
                break;
        }
    }

    public override void OnKeyUp(InputKey key)
    {
        switch (key)
        {
            case InputKey.ArrowLeft when _playerMovement < 0:
                SetPlayerMovement(0);
                break;
            case InputKey.ArrowRight when _playerMovement > 0:
                SetPlayerMovement(0);
                break;
        }
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo info)
    {
        if (args.Type == TouchActionResult.Panning)
        {
            var velocityX = (float)(args.Event.Distance.Velocity.X / RenderingScale);
            if (MathF.Abs(velocityX) > 5)
                SetPlayerMovement(velocityX > 0 ? 1 : -1);
            else
                SetPlayerMovement(0);
        }
        else if (args.Type == TouchActionResult.Tapped)
        {
            Serve();
        }
        else if (args.Type == TouchActionResult.Up)
        {
            SetPlayerMovement(0);
        }

        return base.ProcessGestures(args, info);
    }
}
