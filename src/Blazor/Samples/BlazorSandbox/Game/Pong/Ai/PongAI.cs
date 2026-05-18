namespace Pong.Game.Ai;

public enum AIDifficulty { Easy, Medium, Hard, Perfect }

public class PongAI
{
    private readonly PongGame _game;
    private readonly Random _random = new();
    private double _targetX;
    private float _reactionTimer;
    private float _mistakeTimer;
    private float _decisionChangeTimer;
    private float _movementSmoothingTimer;
    private bool _makingMistake;
    private float _mistakeDirection;
    private bool _isMoving;
    private float _lastMovementDir;

    private readonly float _reactionTimeMin;
    private readonly float _reactionTimeMax;
    private readonly float _accuracy;
    private readonly float _mistakeProbability;
    private readonly float _mistakeDurationMin;
    private readonly float _mistakeDurationMax;
    private float _decisionChangeInterval;
    private readonly float _movementSmoothingTime;

    public PongAI(PongGame game, AIDifficulty difficulty = AIDifficulty.Medium)
    {
        _game = game;

        switch (difficulty)
        {
        case AIDifficulty.Easy:
            _reactionTimeMin = 0.5f; _reactionTimeMax = 1.2f;
            _accuracy = 0.55f; _mistakeProbability = 0.45f;
            _mistakeDurationMin = 0.8f; _mistakeDurationMax = 1.6f;
            _decisionChangeInterval = 1.0f; _movementSmoothingTime = 0.3f;
            break;
        case AIDifficulty.Hard:
            _reactionTimeMin = 0.05f; _reactionTimeMax = 0.15f;
            _accuracy = 0.95f; _mistakeProbability = 0.03f;
            _mistakeDurationMin = 0.1f; _mistakeDurationMax = 0.25f;
            _decisionChangeInterval = 3.0f; _movementSmoothingTime = 0.05f;
            break;
        case AIDifficulty.Perfect:
            _reactionTimeMin = 0.01f; _reactionTimeMax = 0.02f;
            _accuracy = 1.0f; _mistakeProbability = 0.0f;
            _mistakeDurationMin = 0f; _mistakeDurationMax = 0f;
            _decisionChangeInterval = 5.0f; _movementSmoothingTime = 0.01f;
            break;
        case AIDifficulty.Medium:
        default:
            _reactionTimeMin = 0.06f; _reactionTimeMax = 0.22f;
            _accuracy = 0.84f; _mistakeProbability = 0.10f;
            _mistakeDurationMin = 0.25f; _mistakeDurationMax = 0.5f;
            _decisionChangeInterval = 1.8f; _movementSmoothingTime = 0.10f;
            break;
        }

        ResetTimers();
    }

    public void ResetTimers()
    {
        _reactionTimer = GetReactionTime();
        _mistakeTimer = 0;
        _decisionChangeTimer = _decisionChangeInterval;
        _movementSmoothingTimer = 0;
        _makingMistake = false;
        _mistakeDirection = 0;
        _isMoving = false;
        _lastMovementDir = 0;
        _game.SetAiMovement(0);
    }

    public void Update(float delta)
    {
        var ball = _game.Ball;
        if (!ball.IsMoving)
        {
            IdleWander(delta);
            return;
        }

        _reactionTimer -= delta;
        _movementSmoothingTimer -= delta;

        if (_makingMistake)
        {
            _mistakeTimer -= delta;
            if (_mistakeTimer <= 0)
            {
                _makingMistake = false;
                _reactionTimer = GetReactionTime();
                SetMovement(0);
            }
            else
            {
                ApplyMistakeMovement();
            }

            return;
        }

        _decisionChangeTimer -= delta;
        if (_decisionChangeTimer <= 0)
        {
            if (_random.NextDouble() < _mistakeProbability)
            {
                _makingMistake = true;
                _mistakeTimer = _random.NextSingle() * (_mistakeDurationMax - _mistakeDurationMin) + _mistakeDurationMin;
                _mistakeDirection = _random.NextDouble() < 0.7f ? (_random.NextDouble() < 0.5f ? -1f : 1f) : 0f;
            }

            _decisionChangeInterval = 0.5f + _random.NextSingle();
            _decisionChangeTimer = _decisionChangeInterval;
        }

        if (_makingMistake)
        {
            ApplyMistakeMovement();
            return;
        }

        bool ballComingUp = MathF.Sin(ball.Angle) < 0;

        if (ballComingUp && _reactionTimer <= 0)
        {
            var ballVelX = MathF.Cos(ball.Angle) * ball.Speed;
            var ballVelY = MathF.Sin(ball.Angle) * ball.Speed;

            var aiPaddleCenterY = _game.AiPaddle.Top + PongGame.PADDLE_HEIGHT / 2f;
            var ballCenterY = ball.HitBox.MidY;

            var timeToIntersect = (aiPaddleCenterY - ballCenterY) / ballVelY;

            if (timeToIntersect > 0)
            {
                var predicted = ball.HitBox.MidX + ballVelX * timeToIntersect;

                while (predicted < 0 || predicted > PongGame.WIDTH)
                {
                    if (predicted < 0)
                        predicted = -predicted;
                    else if (predicted > PongGame.WIDTH)
                        predicted = 2 * PongGame.WIDTH - predicted;
                }

                var maxError = (1f - _accuracy) * PongGame.PADDLE_WIDTH;
                var error = (_random.NextSingle() * 2 - 1) * maxError;
                _targetX = predicted + error - PongGame.PADDLE_WIDTH / 2f;
                _reactionTimer = GetReactionTime();

                MoveTowardTarget();
            }
        }
        else if (!ballComingUp)
        {
            if (_movementSmoothingTimer <= 0)
            {
                var centerX = (_game.Width - PongGame.PADDLE_WIDTH) / 2f;
                if (MathF.Abs((float)(_game.AiPaddle.Left - centerX)) > PongGame.PADDLE_WIDTH)
                {
                    _targetX = centerX;
                    MoveTowardTarget();
                }
                else if (_isMoving && _random.NextDouble() < 0.3)
                {
                    SetMovement(0);
                }

                _movementSmoothingTimer = _movementSmoothingTime;
            }
        }
    }

    private void ApplyMistakeMovement()
    {
        if (_mistakeDirection < 0)
            SetMovement(-1);
        else if (_mistakeDirection > 0)
            SetMovement(1);
        else
            SetMovement(0);
    }

    private void MoveTowardTarget()
    {
        if (_movementSmoothingTimer > 0)
            return;

        _movementSmoothingTimer = _movementSmoothingTime / 2f;

        var dist = _targetX - _game.AiPaddle.Left;
        var deadzone = PongGame.PADDLE_WIDTH * 0.15f;

        if (MathF.Abs((float)dist) < deadzone)
        {
            SetMovement(0);
            return;
        }

        SetMovement(dist < 0 ? -1 : 1);
    }

    private void IdleWander(float delta)
    {
        SetMovement(0);
    }

    private void SetMovement(float dir)
    {
        if (dir == _lastMovementDir)
            return;

        _lastMovementDir = dir;
        _isMoving = dir != 0;
        _game.SetAiMovement(dir);
    }

    private float GetReactionTime() =>
        _random.NextSingle() * (_reactionTimeMax - _reactionTimeMin) + _reactionTimeMin;
}