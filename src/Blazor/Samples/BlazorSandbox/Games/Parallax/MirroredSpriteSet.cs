using System.Collections.Generic;
using DrawnUi.Controls;

namespace ParallaxGameLoop.Game;

/// <summary>
/// Small parallax-sample helper that keeps typed animation state, maps it to sprite-sheet indices,
/// and reapplies horizontal orientation whenever the active sprite changes.
/// </summary>
public abstract class MirroredSpriteSet<TAnimation> : SkiaSpriteSet where TAnimation : struct, Enum
{
    private TAnimation _animationState;

    public TAnimation AnimationState
    {
        get => _animationState;
        set
        {
            if (EqualityComparer<TAnimation>.Default.Equals(_animationState, value))
            {
                return;
            }

            _animationState = value;
            base.State = MapAnimationState(value);
            ApplyOrientation();
        }
    }

    protected abstract int MapAnimationState(TAnimation animationState);

    protected abstract float GetSpriteScaleX(TAnimation animationState);

    protected override void OnChangeState(int oldState, int newState)
    {
        base.OnChangeState(oldState, newState);
        ApplyOrientation();
    }

    private void ApplyOrientation()
    {
        if (CurrentSprite == null)
        {
            return;
        }

        CurrentSprite.ScaleX = GetSpriteScaleX(AnimationState);
    }
}