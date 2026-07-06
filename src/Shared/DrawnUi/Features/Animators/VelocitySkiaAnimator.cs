using DrawnUi.Draw;
using System.Diagnostics;

namespace DrawnUi.Animate.Animators;

/*
#region IOS STYLE TODO

float Project(float initialVelocity, float decelerationRate)
{
	return (initialVelocity / 1000.0f) * decelerationRate / (1.0f - decelerationRate);
}

public class ScrollTimingParameters
{
	private Point initialValue { get; set; }
	Point initialVelocity { get; set; }
	float decelerationRate { get; set; }
	float threshold { get; set; }
}

#endregion
*/

/// <summary>
/// Basically a modified port of Android FlingAnimation
/// </summary>
public class VelocitySkiaAnimator : SkiaValueAnimator
{
    /// <summary>
    /// Must be over 0
    /// </summary>
    public float mMinOverscrollValue { get; set; }

    /// <summary>
    /// Must be over 0
    /// </summary>
    public float mMaxOverscrollValue { get; set; }

    public float MinLimit
    {
        get
        {
            return (float)(mMinValue - mMinOverscrollValue);
        }
    }

    public float MaxLimit
    {
        get
        {
            return (float)(mMaxValue + mMaxOverscrollValue);
        }
    }


    public float SnapBouncingIfVelocityLessThan { get; set; } = 750f;
    public bool InvertOnLimits { get; set; }
    public float mVelocity { get; set; }

    public float mMaxVelocity { get; set; } = 5000.0f;

    public float mMinVelocity { get; set; } = -5000.0f;

    public float mVerticalVelocityChange { get; set; } //= -100;


    object lockUpdate = new object();
    private bool lockCheck;

    public override void Stop()
    {
        base.Stop();

        mVelocity = 0;
    }

    protected override void ClampOnStart()
    {
        if (mValue < MinLimit)
        {
            mValue = MinLimit;
        }
        else
        if (mValue > MaxLimit)
        {
            mValue = MaxLimit;
        }
    }

    public float InverseK { get; set; } = -0.75f;

    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        if (lockCheck)
        {
            RemainingVelocity = mVelocity;
            return true;
        }
        lockCheck = true;

        try
        {
            if (!IsRunning)
            {
                RemainingVelocity = mVelocity;
                return true;
            }

            if (float.IsNaN(mVelocity))
            {
                Trace.WriteLine("[VelocitySkiaAnimator] mVelocity IsNaN");
                mVelocity = 0;
            }
            else
            if (mVelocity < mMinVelocity)
            {
                RemainingVelocity = mVelocity;
                mVelocity = mMinVelocity;
            }
            else
            if (mVelocity > mMaxVelocity)
            {
                RemainingVelocity = mVelocity;
                mVelocity = mMaxVelocity;
            }

            //Trace.WriteLine($"Velocity {mVelocity:0.00}");

            MassState state =
                mFlingForce.updateValueAndVelocity(mValue, mVelocity, deltaT, deltaFromStart);

            if (float.IsNaN(state.mValue))
            {
                Trace.WriteLine("[VelocitySkiaAnimator] state.mValue IsNaN");
                RemainingVelocity = mVelocity;
                return true;
            }

            mValue = state.mValue;
            mVelocity = state.mVelocity;

            // soft snap
            if (InvertOnLimits)
            {
                if (mMaxOverscrollValue > 0 &&
                    mVelocity > 0 //we go down
                    && mValue >= mMaxValue)
                {
                    //Trace.WriteLine($"[VY] {mVelocity:0}");
                    if (Math.Abs(mVelocity) < SnapBouncingIfVelocityLessThan)
                    {
                        //have no power to bounce back, so just stop
                        mValue = mMaxValue;
                        RemainingVelocity = mVelocity;
                        mVelocity = 0;
                        return true;
                    }
                }

                if (mMaxOverscrollValue > 0 &&
                    mVelocity < 0 //we go up 
                    && mValue <= mMinValue)
                {
                    if (Math.Abs(mVelocity) < SnapBouncingIfVelocityLessThan)
                    {
                        //have no power to bounce back, so just stop
                        mValue = mMinValue;
                        RemainingVelocity = mVelocity;
                        mVelocity = 0;
                        return true;
                    }
                }
            }

            if (mValue < MinLimit)
            {
                mValue = MinLimit;
                if (InvertOnLimits)
                {
                    mVelocity *= InverseK;
                    return false;
                }
                else
                {
                    RemainingVelocity = mVelocity;
                    return true;
                }
            }

            if (mValue > MaxLimit)
            {
                mValue = MaxLimit;
                if (InvertOnLimits)
                {
                    mVelocity *= InverseK;
                    return false;
                }
                else
                {
                    RemainingVelocity = mVelocity;
                    return true;
                }
            }

            if (isAtEquilibrium(mValue, mVelocity))
            {
                RemainingVelocity = mVelocity;
                return true;
            }


            return false;
        }
        finally
        {
            lockCheck = false;
        }

    }


    bool isAtEquilibrium(double value, float velocity)
    {
        if (InvertOnLimits)
        {
            if (value < mMinValue)
            {
                return mFlingForce.isAtEquilibrium(value, velocity);
            }
            if (value > mMaxValue)
            {
                return mFlingForce.isAtEquilibrium(value, velocity);
            }
        }

        return value > MaxLimit
               || value < MinLimit
               || mFlingForce.isAtEquilibrium(value, velocity);
    }


    public VelocitySkiaAnimator SetVelocity(float value)
    {
        mVelocity = value;
        return this;
    }

    public VelocitySkiaAnimator SetFriction(float value)
    {
        Friction = value;
        return this;
    }

    private float _mMinVisibleChange;
    public float mMinVisibleChange
    {
        get
        {
            return _mMinVisibleChange;
        }
        set
        {
            if (_mMinVisibleChange != value)
            {
                if (value <= 0)
                {
                    throw new Exception("Minimum visible change must be positive.");
                }
                _mMinVisibleChange = value;
                setValueThreshold(value * (THRESHOLD_MULTIPLIER / Scale));
            }
        }
    }

    void setValueThreshold(float threshold)
    {
        mFlingForce.setValueThreshold(threshold);
        mFlingForce.setValueThreshold(getValueThreshold());
    }

    float getValueThreshold()
    {
        return mMinVisibleChange * (THRESHOLD_MULTIPLIER / Scale);
    }

    /// <summary>
    /// The bigger the sooner animation will slow down, default is 1.0
    /// </summary>
    public float Friction
    {
        get
        {
            return mFlingForce.getFrictionScalar();
        }
        set
        {
            if (value < 0)
            {
                throw new ApplicationException("Friction has to be positive");
            }
            mFlingForce.setFrictionScalar(value);
        }
    }

    /// <summary>
    /// This is set after we are done so we will know at OnStop if we have some energy left
    /// </summary>
    public float RemainingVelocity { get; set; }

    protected MassState mMassState = new();
    public class MassState
    {
        public float mValue { get; set; }
        public float mVelocity { get; set; }
    }
    protected DragForce mFlingForce = new();
    public class DragForce //: IForce
    {
        private static float DEFAULT_FRICTION = -4.2f;

        //  This multiplier is used to calculate the velocity threshold given a certain value
        //  threshold. The idea is that if it takes >= 1 frame to move the value threshold amount,
        //  then the velocity is a reasonable threshold.
        private static float VELOCITY_THRESHOLD_MULTIPLIER = 64;

        private float mFriction = DEFAULT_FRICTION;

        private float mVelocityThreshold;

        //  Internal state to hold a value/velocity pair.
        private MassState mMassState = new MassState();

        public void setFrictionScalar(float frictionScalar)
        {
            mFriction = frictionScalar * DEFAULT_FRICTION;
        }

        public float getFrictionScalar()
        {
            return mFriction / DEFAULT_FRICTION;
        }



        /*
		 DragForce dragForce = new DragForce();
		// Set friction and value threshold according to your requirements...
		float initialPosition = 0; // replace with your actual initial position
		float finalPosition = 100; // replace with your desired final position
		float duration = 1; // replace with your desired duration in seconds
		float initialVelocity = dragForce.GetInitialVelocity(initialPosition, finalPosition, duration);
		 */
        /// <summary>
        /// inverse of updateValueAndVelocity
        /// </summary>
        /// <param name="initialPosition"></param>
        /// <param name="finalPosition"></param>
        /// <param name="durationTime"></param>
        /// <returns></returns>
        public float GetInitialVelocity(float initialPosition, float finalPosition, float durationTime)
        {
            return (finalPosition - initialPosition) * mFriction / (1 - MathF.Exp(mFriction * durationTime));
        }


        public MassState updateValueAndVelocity(double value, float velocity, long deltaT, long deltaFromStart)
        {
            var time = deltaT / 1000000000f;
            var friction = mFriction;// / accel;
            mMassState.mVelocity = (float)(velocity * Math.Exp(time * friction));
            mMassState.mValue = (float)(value - velocity / friction
                                        + velocity / friction * Math.Exp(friction * time));
            if (isAtEquilibrium(mMassState.mValue, mMassState.mVelocity))
            {
                mMassState.mVelocity = 0f;
            }
            return mMassState;
        }


        public float getAcceleration(float position, float velocity)
        {
            return velocity * mFriction;
        }


        public bool isAtEquilibrium(double value, float velocity)
        {
            return Math.Abs(velocity) < mVelocityThreshold;
        }

        public void setValueThreshold(float threshold)
        {
            mVelocityThreshold = threshold * VELOCITY_THRESHOLD_MULTIPLIER;
        }
    }

    public enum PresetType
    {
        Default,
        Scale,
        Rotation,
        Alpha,
    }

    private PresetType _preset;
    public PresetType Preset
    {
        get
        {
            return _preset;
        }
        set
        {
            if (_preset != value)
            {
                _preset = value;
                InitPreset(value);
            }
        }
    }

    private float _Scale = 1;
    public float Scale
    {
        get
        {
            return _Scale;
        }
        set
        {
            if (_Scale != value)
            {
                _Scale = value;
                InitPreset(Preset);
            }
        }
    }

    void InitPreset(PresetType value)
    {
        switch (value)
        {
        case PresetType.Alpha:
        case PresetType.Scale:
        mMinVisibleChange = MIN_VISIBLE_CHANGE_ALPHA / Scale;
        break;
        case PresetType.Rotation:
        mMinVisibleChange = MIN_VISIBLE_CHANGE_ROTATION_DEGREES / Scale;
        break;
        default:
        mMinVisibleChange = MIN_VISIBLE_CHANGE_PIXELS / Scale;
        break;
        }
    }
    public VelocitySkiaAnimator(SkiaControl parent) : base(parent)
    {
        InitPreset(_preset);
        if (parent != null)
            Scale = parent.RenderingScale;
    }

    public const float MIN_VISIBLE_CHANGE_PIXELS = 1;
    public const float MIN_VISIBLE_CHANGE_ROTATION_DEGREES = 1 / 10;
    public const float MIN_VISIBLE_CHANGE_ALPHA = 1 / 256;
    public const float MIN_VISIBLE_CHANGE_SCALE = 1 / 500;
    public const float UNSET = float.MaxValue;
    public const float THRESHOLD_MULTIPLIER = 0.75f;
}