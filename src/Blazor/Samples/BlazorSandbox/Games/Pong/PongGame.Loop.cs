namespace Pong.Game;

public partial class PongGame
{
    private const float BALL_RADIUS = 7f;
    private bool _aiServes;
    private float _autoServeTimer;
    private float _aiWanderTimer;
    private float _aiWanderDir;
    private bool _playerHasMoved;
    private readonly Random _random = new();

    public override void GameLoop(float deltaSeconds)
    {
        if (_phase == GamePhase.WaitingToStart)
        {
            if (_aiServes)
            {
                _autoServeTimer -= deltaSeconds;

                _aiWanderTimer -= deltaSeconds;
                if (_aiWanderTimer <= 0)
                {
                    _aiWanderDir = _random.NextDouble() < 0.25 ? 0f
                        : _random.NextDouble() < 0.5f ? -1f : 1f;
                    _aiWanderTimer = 0.25f + _random.NextSingle() * 0.35f;
                }

                if ((float)AiPaddle.Left <= 0 && _aiWanderDir < 0)
                    _aiWanderDir = 1;
                if ((float)AiPaddle.Left + PADDLE_WIDTH >= WIDTH && _aiWanderDir > 0)
                    _aiWanderDir = -1;

                MovePaddle(AiPaddle, _aiWanderDir, deltaSeconds);
                MovePaddle(PlayerPaddle, _playerMovement, deltaSeconds);
                Ball.Left = AiPaddle.Left + (PADDLE_WIDTH - 14) / 2.0;

                if (_autoServeTimer <= 0)
                    Serve(triggeredByAi: true);
            }
            else
            {
                if (_playerMovement != 0)
                    _playerHasMoved = true;

                MovePaddle(PlayerPaddle, _playerMovement, deltaSeconds);
                Ball.Left = PlayerPaddle.Left + (PADDLE_WIDTH - 14) / 2.0;

                if (_playerHasMoved)
                {
                    _aiWanderTimer -= deltaSeconds;
                    if (_aiWanderTimer <= 0)
                    {
                        _aiWanderDir = _random.NextDouble() < 0.3 ? 0f
                            : _random.NextDouble() < 0.5f ? -1f : 1f;
                        _aiWanderTimer = 0.5f + _random.NextSingle() * 0.7f;
                    }

                    if ((float)AiPaddle.Left <= 0 && _aiWanderDir < 0)
                        _aiWanderDir = 1;
                    if ((float)AiPaddle.Left + PADDLE_WIDTH >= WIDTH && _aiWanderDir > 0)
                        _aiWanderDir = -1;

                    MovePaddle(AiPaddle, _aiWanderDir * 0.3f, deltaSeconds);
                }
            }

            return;
        }

        if (_phase == GamePhase.Scored)
        {
            _phaseTimer -= deltaSeconds;
            if (_phaseTimer <= 0)
            {
                if (_playerScore >= WIN_SCORE || _aiScore >= WIN_SCORE)
                {
                    _phase = GamePhase.GameOver;
                    _messageLabel.Text = _playerScore >= WIN_SCORE ? "YOU WIN!\nTAP TO RESTART" : "AI WINS!\nTAP TO RESTART";
                }
                else
                {
                    bool playerServes = _lastScorer == Scorer.Player;
                    _aiServes = !playerServes;
                    _autoServeTimer = 1.5f;
                    _playerHasMoved = false;
                    _aiWanderDir = 0;
                    ResetPaddles();
                    ResetBall(playerServes);
                    _phase = GamePhase.WaitingToStart;
                    _messageLabel.Text = _aiServes ? string.Empty : "TAP TO SERVE";
                }
            }

            return;
        }

        if (_phase == GamePhase.GameOver)
            return;

        var time = (long)(deltaSeconds * 1000);
        Ball.UpdateState(time, true);
        PlayerPaddle.UpdateState(time, true);
        AiPaddle.UpdateState(time, true);

        Ball.UpdatePosition(deltaSeconds);

        float ballLeft = (float)Ball.Left;

        if (ballLeft < 0)
        {
            Ball.Left = 0;
            Ball.Angle = MathF.PI - Ball.Angle;
        }
        else if (ballLeft + 14 > WIDTH)
        {
            Ball.Left = WIDTH - 14;
            Ball.Angle = MathF.PI - Ball.Angle;
        }

        Ball.UpdateState(time, true);
        var ballHit = Ball.HitBox;

        const float MAX_DEV = MathF.PI * 0.27f;
        const float MAX_SPEED = BALL_SPEED * 2.0f;

        var playerHit = PlayerPaddle.HitBox;
        if (ballHit.IntersectsWith(playerHit) && Ball.Angle > 0)
        {
            var hitPos = (ballHit.MidX - playerHit.Left) / playerHit.Width;
            Ball.Angle = BallSprite.ClampAngleFromHorizontal(-MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
            if (MathF.Sin(Ball.Angle) > 0)
                Ball.Angle = -Ball.Angle;
            Ball.Top = playerHit.Top - 14;
            Ball.Speed = MathF.Min(Ball.Speed + 20f, MAX_SPEED);
        }

        var aiHit = AiPaddle.HitBox;
        if (ballHit.IntersectsWith(aiHit) && Ball.Angle < 0)
        {
            var hitPos = (ballHit.MidX - aiHit.Left) / aiHit.Width;
            Ball.Angle = BallSprite.ClampAngleFromHorizontal(MathF.PI / 2f + (hitPos - 0.5f) * MAX_DEV * 2f);
            if (MathF.Sin(Ball.Angle) < 0)
                Ball.Angle = -Ball.Angle;
            Ball.Top = aiHit.Bottom;
            Ball.Speed = MathF.Min(Ball.Speed + 20f, MAX_SPEED);
        }

        if (Ball.Top + 14 < 0)
        {
            _playerScore++;
            UpdateScoreLabel();
            Score(Scorer.Player);
            return;
        }

        if (Ball.Top > HEIGHT)
        {
            _aiScore++;
            UpdateScoreLabel();
            Score(Scorer.Ai);
            return;
        }

        MovePaddle(PlayerPaddle, _playerMovement, deltaSeconds);
        MovePaddle(AiPaddle, _aiMovement, deltaSeconds);

        _ai.Update(deltaSeconds);
    }

    private enum Scorer { Player, Ai }

    private Scorer _lastScorer;

    private void Score(Scorer scorer)
    {
        _lastScorer = scorer;
        Ball.IsMoving = false;
        _phase = GamePhase.Scored;
        _phaseTimer = 1.5f;

        _messageLabel.Text = scorer == Scorer.Player ? "POINT!" : "AI SCORES!";
    }

    private static void MovePaddle(PaddleSprite paddle, float dir, float delta)
    {
        if (dir == 0)
            return;

        float newLeft = (float)paddle.Left + dir * PADDLE_SPEED * delta;
        newLeft = MathF.Max(0, MathF.Min(newLeft, WIDTH - PADDLE_WIDTH));
        paddle.Left = newLeft;
    }
}