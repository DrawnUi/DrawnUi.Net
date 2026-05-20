using System.Runtime.InteropServices.JavaScript;
using DrawnUi.Draw;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    /// <summary>
    /// Sprite-set wrapper for the corridor ghost enemy.
    /// </summary>
    public sealed class GhostSprite : MirroredSpriteSet<GhostSprite.GhostAnimState>
    {
        public enum GhostAnimState
        {
            AppearRight,
            AppearLeft,
            ChaseRight,
            ChaseLeft,
            IdleRight,
            IdleLeft,
            ShriekRight,
            ShriekLeft,
            VanishRight,
            VanishLeft,
        }

        /// <summary>
        /// Delay before a defeated ghost respawns at its patrol origin.
        /// </summary>
        public const float GhostRespawnDelay = 10f;

        /// <summary>
        /// Half-width of the ghost patrol around its spawn point.
        /// </summary>
        public const float GhostPatrolHalfWidth = 96f;

        /// <summary>
        /// Patrol oscillation speed in radians per second.
        /// </summary>
        public const float GhostPatrolSpeed = 1.2f;

        /// <summary>
        /// Small vertical bob amplitude applied while the ghost is alive.
        /// </summary>
        public const float GhostBobAmplitude = 6f;

        /// <summary>
        /// Vertical bob speed in radians per second.
        /// </summary>
        public const float GhostBobSpeed = 2.4f;

        /// <summary>
        /// Fractional horizontal inset used to tighten the ghost's combat hit box relative to its sprite bounds.
        /// </summary>
        private const float GhostHitInsetXFactor = 0.22f;

        /// <summary>
        /// Fractional vertical inset used to tighten the ghost's combat hit box relative to its sprite bounds.
        /// </summary>
        private const float GhostHitInsetYFactor = 0.18f;

        /// <summary>
        /// Duration of the ghost appear animation in seconds.
        /// </summary>
        public const float GhostAppearDuration = 6f / 10f;

        /// <summary>
        /// Duration of the ghost vanish animation in seconds.
        /// </summary>
        private const float GhostVanishDuration = 7f / 10f;

        public GhostSprite()
        {
            UseCache = SkiaCacheType.GPU;
            Define(0, "media/gothicvania/ghost/ghost-Appear.png", columns: 6, rows: 1, fps: 10);
            Define(1, "media/gothicvania/ghost/ghost-Chase.png", columns: 4, rows: 1, fps: 10);
            Define(2, "media/gothicvania/ghost/ghost-Idle.png", columns: 7, rows: 1, fps: 10);
            Define(3, "media/gothicvania/ghost/ghost-Shriek.png", columns: 4, rows: 1, fps: 10);
            Define(4, "media/gothicvania/ghost/ghost-Vanish.png", columns: 7, rows: 1, fps: 10, repeat: 1);
            AnimationState = GhostAnimState.AppearLeft;
        }


        /// <summary>
        /// Tightens the ghost's automatic hit box so attacks land on the body rather than transparent sprite padding.
        /// </summary>
        public SKRect GetHitBox()
        {
            SKRect hitBox = HitBoxAuto;
            return SpriteHitBoxHelpers.Inset(hitBox, GhostHitInsetXFactor, GhostHitInsetYFactor);
        }


        /// <summary>
        /// Current high-level ghost behavior state.
        /// </summary>
        public new GhostState State
        {
            get;
            set
            {
                if (value == field)
                {
                    return;
                }

                field = value;
                OnPropertyChanged();
                if (value == GhostState.Appearing)
                {
                    _ghostStateTimeRemaining = GhostSprite.GhostAppearDuration;
                }
            }
        }


        /// <summary>
        /// Advances the enemy patrol state machine and resolves attack kills against the ghost.
        /// </summary>
        public void UpdateState(float deltaSeconds)
        {
            if (State == GhostState.Gone)
            {
                _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
                if (_ghostStateTimeRemaining == 0)
                {
                    _ghostPatrolTime = 0;
                    _ghostBobTime = 0;
                    SetState(GhostState.Appearing);
                }
                return;
            }

            _ghostPatrolTime += deltaSeconds;
            _ghostBobTime += deltaSeconds * GhostSprite.GhostBobSpeed;

            if (State == GhostState.Appearing)
            {
                _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
                if (_ghostStateTimeRemaining == 0)
                {
                    SetState(GhostState.Patrolling);
                }
                return;
            }

            if (State == GhostState.Vanishing)
            {
                _ghostStateTimeRemaining = Math.Max(0, _ghostStateTimeRemaining - deltaSeconds);
                if (_ghostStateTimeRemaining == 0)
                {
                    SetState(GhostState.Gone);
                }
                return;
            }

            var movingLeft = MathF.Cos(_ghostPatrolTime * GhostSprite.GhostPatrolSpeed) < 0;
            AnimationState = movingLeft ? GhostSprite.GhostAnimState.ChaseLeft : GhostSprite.GhostAnimState.ChaseRight;
        }



        /// <summary>
        /// Patrol phase accumulator used to move the ghost left and right.
        /// </summary>
        public float _ghostPatrolTime;

        /// <summary>
        /// Vertical bobbing phase accumulator for the ghost.
        /// </summary>
        public float _ghostBobTime;

        /// <summary>
        /// Remaining time for transient ghost states such as appear and vanish.
        /// </summary>
        private float _ghostStateTimeRemaining;

        /// <summary>
        /// Applies a high-level ghost state to the sprite and visibility model.
        /// </summary>
        public void SetState(GhostState state)
        {
            State = state;

            switch (state)
            {
                case GhostState.Appearing:
                    this.IsVisible = true;
                    _ghostStateTimeRemaining = GhostAppearDuration;
                    this.AnimationState = GhostSprite.GhostAnimState.AppearLeft;
                    break;
                case GhostState.Patrolling:
                    this.IsVisible = true;
                    _ghostStateTimeRemaining = 0;
                    this.AnimationState = GhostSprite.GhostAnimState.ChaseLeft;
                    break;
                case GhostState.Vanishing:
                    this.IsVisible = true;
                    _ghostStateTimeRemaining = GhostVanishDuration;
                    this.AnimationState = GhostSprite.GhostAnimState.VanishLeft;
                    break;
                default:
                    _ghostStateTimeRemaining = GhostRespawnDelay;
                    this.IsVisible = false;
                    break;
            }
        }

        protected override int MapAnimationState(GhostAnimState animationState)
        {
            return animationState switch
            {
                GhostAnimState.AppearLeft or GhostAnimState.AppearRight => 0,
                GhostAnimState.ChaseLeft or GhostAnimState.ChaseRight => 1,
                GhostAnimState.IdleLeft or GhostAnimState.IdleRight => 2,
                GhostAnimState.ShriekLeft or GhostAnimState.ShriekRight => 3,
                _ => 4,
            };
        }

        protected override float GetSpriteScaleX(GhostAnimState animationState)
        {
            return animationState is GhostAnimState.AppearLeft
                or GhostAnimState.ChaseLeft
                or GhostAnimState.IdleLeft
                or GhostAnimState.ShriekLeft
                or GhostAnimState.VanishLeft
                ? 1
                : -1;
        }
    }
}
