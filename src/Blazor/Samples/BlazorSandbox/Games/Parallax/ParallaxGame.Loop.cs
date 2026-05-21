using AppoMobi.Gestures;
using DrawnUi.Controls;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game;

 
public sealed partial class ParallaxGame : DrawnGame
{
    /// <summary>
    /// Advances one gameplay frame: reads input, updates physics, advances world travel, and refreshes visuals.
    /// </summary>
    public override void GameLoop(float deltaSeconds)
    {
        base.GameLoop(deltaSeconds);

        var moveLeft = _moveLeft || _pointerMoveLeft;
        var moveRight = _moveRight || _pointerMoveRight;

        var moveInput = 0;
        if (moveLeft)
        {
            moveInput -= 1;
        }

        if (moveRight)
        {
            moveInput += 1;
        }

        if (_jumpRequested)
        {
            _jumpRequested = false;
            _heroineVelocityY = -JumpImpulse;
        }

        if (_heroineJumpOffset != 0 || _heroineVelocityY != 0)
        {
            _heroineVelocityY += Gravity * deltaSeconds;
            _heroineJumpOffset += _heroineVelocityY * deltaSeconds;

            if (_heroineJumpOffset >= 0)
            {
                _heroineJumpOffset = 0;
                _heroineVelocityY = 0;
            }
        }

        if (_attackTimeRemaining > 0)
        {
            _attackTimeRemaining = Math.Max(0, _attackTimeRemaining - deltaSeconds);
        }

        _worldPosition += moveInput * MoveSpeed * deltaSeconds;

        _player.SetState(ResolveState(moveInput), _facingLeft);

        UpdateWorldVisuals();

        if (_attackTimeRemaining > 0)
        {
            TryAttackGhost();
        }

        UpdateGhost(deltaSeconds);
    }



}
