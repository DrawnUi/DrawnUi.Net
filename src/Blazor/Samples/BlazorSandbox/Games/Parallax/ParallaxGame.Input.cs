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
    /// Captures keyboard input and converts it into movement, jump, and attack requests.
    /// </summary>
    public override void OnKeyDown(InputKey key)
    {
        Console.WriteLine($"[ParallaxGame] KeyDown {key}");

        switch (key)
        {
            case InputKey.ArrowLeft:
            case InputKey.KeyA:
                _moveLeft = true;
                _facingLeft = true;
                break;
            case InputKey.ArrowRight:
            case InputKey.KeyD:
                _moveRight = true;
                _facingLeft = false;
                break;
            case InputKey.ArrowUp:
            case InputKey.KeyW:
                QueueJumpIfGrounded();
                break;
            case InputKey.Space:
                _attackTimeRemaining = AttackDuration;
                break;
        }
    }

    /// <summary>
    /// Clears held movement input when the corresponding key is released.
    /// </summary>
    public override void OnKeyUp(InputKey key)
    {
        Console.WriteLine($"[ParallaxGame] KeyUp {key}");

        switch (key)
        {
            case InputKey.ArrowLeft:
            case InputKey.KeyA:
                _moveLeft = false;
                break;
            case InputKey.ArrowRight:
            case InputKey.KeyD:
                _moveRight = false;
                break;
        }
    }

    /// <summary>
    /// Captures pointer presses so mouse-down to the left or right of the heroine moves her,
    /// while taps still trigger attacks.
    /// </summary>
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        switch (args.Type)
        {
            case TouchActionResult.Down:
                TryHandlePointerJump(args.Event.Location.Y);
                SetPointerMovement(args.Event.Location.X);
                return this;

            case TouchActionResult.Panning:
                TryHandlePointerJump(args.Event.Location.Y);
                SetPointerMovement(args.Event.Location.X);
                return this;

            case TouchActionResult.Tapped:
                _attackTimeRemaining = AttackDuration;
                return this;

            case TouchActionResult.Up:
                ClearPointerMovement();
                return this;
        }

        return base.ProcessGestures(args, apply);
    }

    /// <summary>
    /// Clears pointer-driven movement without affecting held keyboard input.
    /// </summary>
    private void ClearPointerMovement()
    {
        _pointerMoveLeft = false;
        _pointerMoveRight = false;
    }

    /// <summary>
    /// Converts a pointer X position into the same left/right movement latches used by the keyboard.
    /// </summary>
    private void SetPointerMovement(float pointerX)
    {
        var playerCenterX = _player.HitBoxAuto.MidX;

        if (pointerX < playerCenterX)
        {
            _pointerMoveLeft = true;
            _pointerMoveRight = false;
            _facingLeft = true;
        }
        else
        {
            _pointerMoveLeft = false;
            _pointerMoveRight = true;
            _facingLeft = false;
        }
    }

    /// <summary>
    /// Triggers jump when the pointer goes down above the heroine's current hit box.
    /// </summary>
    private bool TryHandlePointerJump(float pointerY)
    {
        if (pointerY < _player.HitBoxAuto.Top)
        {
            ClearPointerMovement();
            QueueJumpIfGrounded();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Queues a jump when the heroine is grounded.
    /// </summary>
    private void QueueJumpIfGrounded()
    {
        if (_heroineJumpOffset == 0)
        {
            _jumpRequested = true;
        }
    }




}
