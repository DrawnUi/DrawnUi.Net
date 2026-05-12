using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public sealed partial class ParallaxGame
    {
        /// <summary>
        /// Sprite-set wrapper that exposes semantic player animation states and left/right mirroring.
        /// </summary>
        public class PlayerSprite : SkiaSpriteSet
        {
            /// <summary>
            /// Concrete animation variants, split by action and facing direction.
            /// </summary>
            public enum PlayerAnimState
            {
                IdleRight,
                IdleLeft,
                RunRight,
                RunLeft,
                JumpRight,
                JumpLeft,
                AttackRight,
                AttackLeft,
            }

            private PlayerAnimState _state;

            /// <summary>
            /// Gets or sets the active semantic animation variant and maps it onto the underlying sprite-set state index.
            /// </summary>
            public new PlayerAnimState State
            {
                get => _state;
                set
                {
                    if (_state == value)
                    {
                        return;
                    }

                    _state = value;
                    base.State = value switch
                    {
                        PlayerAnimState.IdleLeft or PlayerAnimState.IdleRight => 0,
                        PlayerAnimState.RunLeft or PlayerAnimState.RunRight => 1,
                        PlayerAnimState.JumpLeft or PlayerAnimState.JumpRight => 2,
                        _ => 3,
                    };
                    ApplyMirror();
                }
            }

            /// <summary>
            /// Registers the player's sprite sheets and default starting animation.
            /// </summary>
            public PlayerSprite()
            {
                var bottomCenter = 0.5f;
                const float playerUnitsPerPixel = 3f;

                Define(0, "media/gothicvania/heroine-idle.png", columns: 4, rows: 1, fps: 8,
                    placement: new SpritePlacementConfig
                    {
                        UnitsPerPixel = playerUnitsPerPixel,
                        AnchorX = bottomCenter,
                        AnchorY = 1f,
                    });
                Define(1, "media/gothicvania/heroine-run.png", columns: 7, rows: 1, fps: 12,
                    placement: new SpritePlacementConfig
                    {
                        UnitsPerPixel = playerUnitsPerPixel,
                        AnchorX = bottomCenter,
                        AnchorY = 1f,
                    });
                Define(2, "media/gothicvania/heroine-jump.png", columns: 4, rows: 1, fps: 10,
                    placement: new SpritePlacementConfig
                    {
                        UnitsPerPixel = playerUnitsPerPixel,
                        AnchorX = bottomCenter,
                        AnchorY = 1f,
                    });
                Define(3, "media/gothicvania/heroine-attack.png", columns: 5, rows: 1, fps: 18,
                    placement: new SpritePlacementConfig
                    {
                        UnitsPerPixel = playerUnitsPerPixel,
                        AnchorX = bottomCenter,
                        AnchorY = 1f,
                        OffsetXUnits = 18f,
                    });
                State = PlayerAnimState.IdleRight;
            }

            /// <summary>
            /// Reapplies horizontal mirroring whenever the underlying sprite-set changes animation.
            /// </summary>
            protected override void OnChangeState(int oldState, int newState)
            {
                base.OnChangeState(oldState, newState);
                ApplyMirror();
            }

            /// <summary>
            /// Mirrors the active sprite horizontally for left-facing animation variants.
            /// </summary>
            private void ApplyMirror()
            {
                if (CurrentSprite == null)
                {
                    return;
                }

                var mirror = State is PlayerAnimState.IdleLeft
                    or PlayerAnimState.RunLeft
                    or PlayerAnimState.JumpLeft
                    or PlayerAnimState.AttackLeft;

                CurrentSprite.ScaleX = mirror ? -1 : 1;
            }
        }
    }
}
