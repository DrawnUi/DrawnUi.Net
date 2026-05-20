using System.Diagnostics;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public class PlayerSprite : MirroredSpriteSet<PlayerSprite.PlayerAnimState>
    {
        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            ScaledSize measured = base.OnMeasuring(widthConstraint, heightConstraint, scale);

            Console.WriteLine($"[ParallaxGame] PlayerSprite measured: {measured}, constraint: ({widthConstraint}, {heightConstraint}), scale: {scale}");

            return measured;
        }

        /// <summary>
        /// Width of the melee attack hit box projected in front of the heroine.
        /// </summary>
        private const float AttackHitReach = 92f;

        /// <summary>
        /// Width of the small overlap anchor kept inside the heroine when projecting the melee hit box.
        /// </summary>
        private const float AttackHitAnchorWidth = 18f;

        /// <summary>
        /// Fractional top inset used to keep the melee hit box off the heroine's hair and empty head space.
        /// </summary>
        private const float AttackHitTopInsetFactor = 0.24f;

        /// <summary>
        /// Fractional bottom inset used to keep the melee hit box above the heroine's feet.
        /// </summary>
        private const float AttackHitBottomInsetFactor = 0.20f;

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

        /// <summary>
        /// Registers the player's sprite sheets and default starting animation.
        /// </summary>
        public PlayerSprite()
        {
            var bottomCenter = 0.5f;

            Define(0, "media/gothicvania/heroine-idle.png", columns: 4, rows: 1, fps: 8,
                placement: new SpritePlacementConfig
                {
                    AnchorX = bottomCenter,
                    AnchorY = 1f,
                });
            Define(1, "media/gothicvania/heroine-run.png", columns: 7, rows: 1, fps: 12,
                placement: new SpritePlacementConfig
                {
                    AnchorX = bottomCenter,
                    AnchorY = 1f,
                });
            Define(2, "media/gothicvania/heroine-jump.png", columns: 4, rows: 1, fps: 10,
                placement: new SpritePlacementConfig
                {
                    AnchorX = bottomCenter,
                    AnchorY = 1f,
                });
            Define(3, "media/gothicvania/heroine-attack.png", columns: 5, rows: 1, fps: 18,
                placement: new SpritePlacementConfig
                {
                    AnchorX = bottomCenter,
                    AnchorY = 1f,
                    OffsetXUnits = 18f,
                });
            AnimationState = PlayerAnimState.IdleRight;
        }
        protected override int MapAnimationState(PlayerAnimState animationState)
        {
            return animationState switch
            {
                PlayerAnimState.IdleLeft or PlayerAnimState.IdleRight => 0,
                PlayerAnimState.RunLeft or PlayerAnimState.RunRight => 1,
                PlayerAnimState.JumpLeft or PlayerAnimState.JumpRight => 2,
                _ => 3,
            };
        }

        protected override float GetSpriteScaleX(PlayerAnimState animationState)
        {
            return animationState is PlayerAnimState.IdleLeft
                or PlayerAnimState.RunLeft
                or PlayerAnimState.JumpLeft
                or PlayerAnimState.AttackLeft
                ? -1
                : 1;
        }

        /// <summary>
        /// Current facing direction used to select left or right animation variants.
        /// </summary>
        private bool _facingLeft;

        /// <summary>
        /// Last resolved high-level player gameplay state.
        /// </summary>
        public new PlayerState State;

        /// <summary>
        /// Maps a gameplay state into a concrete left/right animation variant and applies it to the sprite set.
        /// </summary>
        public void SetState(PlayerState state, bool facingLeft)
        {
            _facingLeft = facingLeft;
            PlayerAnimState nextAnimation = state switch
            {
                PlayerState.Idle => _facingLeft ? PlayerAnimState.IdleLeft : PlayerAnimState.IdleRight,
                PlayerState.Run => _facingLeft ? PlayerAnimState.RunLeft : PlayerAnimState.RunRight,
                PlayerState.Jump => _facingLeft ? PlayerAnimState.JumpLeft : PlayerAnimState.JumpRight,
                _ => _facingLeft ? PlayerAnimState.AttackLeft : PlayerAnimState.AttackRight,
            };

            if (State == state && this.AnimationState == nextAnimation)
            {
                return;
            }

            Debug.WriteLine($"[ParallaxGame] State {State} -> {state}, anim {this.AnimationState} -> {nextAnimation}");

            State = state;
            this.AnimationState = nextAnimation;
        }

        /// <summary>
        /// Builds a facing-aware melee hit box from the heroine's current automatic hit box.
        /// </summary>
        public SKRect GetAttackHitBox()
        {
            SKRect playerHitBox = this.HitBoxAuto;
            return SpriteHitBoxHelpers.CreateForwardReachHitBox(
                playerHitBox,
                _facingLeft,
                AttackHitReach,
                AttackHitAnchorWidth,
                AttackHitTopInsetFactor,
                AttackHitBottomInsetFactor);
        }

    }
}
