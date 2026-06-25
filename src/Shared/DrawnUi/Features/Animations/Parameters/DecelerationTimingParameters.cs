using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public class DecelerationTimingParameters : ITimingParameters
    {
        public float InitialValue { get; set; }
        public float InitialVelocity { get; set; }
        public float DecelerationRate { get; protected set; }
        public float DecelerationK { get; protected set; }
        public float Threshold { get; set; }

        public DecelerationTimingParameters(float initialValue, float initialVelocity, float decelerationRate, float threshold)
        {
            if (decelerationRate <= 0 || decelerationRate >= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(decelerationRate), "Deceleration rate must be greater than 0 and less than 1.");
            }

            InitialValue = initialValue;
            InitialVelocity = initialVelocity;
            Threshold = threshold;
            DecelerationRate = decelerationRate;
            DecelerationK = 1000 * (float)Math.Log(DecelerationRate);
        }

        /// <summary>
/// Creates timing parameters specifically for scrolling to a target position in a specified time
/// </summary>
/// <param name="currentValue">The current position/value</param>
/// <param name="targetValue">The target position/value to scroll to</param>
/// <param name="durationSecs">The desired duration in seconds</param>
/// <param name="decelerationRate">The deceleration rate (between 0 and 1)</param>
/// <param name="threshold">The threshold for considering motion stopped</param>
public DecelerationTimingParameters(float currentValue, float targetValue, float durationSecs, float decelerationRate, float threshold = 0.1f)
{
    if (decelerationRate <= 0 || decelerationRate >= 1)
    {
        throw new ArgumentOutOfRangeException(nameof(decelerationRate), "Deceleration rate must be greater than 0 and less than 1.");
    }
    
    if (durationSecs <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(durationSecs), "Duration must be greater than 0.");
    }
    
    // Store basic parameters
    InitialValue = currentValue;
    Threshold = threshold;
    DecelerationRate = decelerationRate;
    DecelerationK = 1000 * (float)Math.Log(DecelerationRate);
    
    // Calculate the distance to travel
    float distance = targetValue - currentValue;
    
    // If we're already at the target, no velocity needed
    if (Math.Abs(distance) < 1e-5f)
    {
        InitialVelocity = 0;
        return;
    }
    
    // Calculate the required initial velocity to reach the target in the specified time
    // This is derived from inverting the position formula:
    // x(t) = x0 + v0 * (e^(k*t) - 1) / k
    // Solving for v0 gives:
    // v0 = (x - x0) * k / (e^(k*t) - 1)
    float numerator = distance * DecelerationK;
    float denominator = (float)(Math.Pow(DecelerationRate, 1000 * durationSecs) - 1);
    
    // Handle extreme cases for numerical stability
    if (Math.Abs(denominator) < 1e-5f)
    {
        // If denominator is too small, use a simplified calculation
        InitialVelocity = distance / durationSecs;
    }
    else
    {
        InitialVelocity = numerator / denominator;
    }
}

        public float Destination
        {
            get
            {
                return InitialVelocity == 0 ? InitialValue : InitialValue - InitialVelocity / DecelerationK;
            }
        }

        public float DurationSecs
        {
            get
            {
                if (InitialVelocity == 0)
                    return 0;

                float divisor = -DecelerationK * Threshold / Math.Abs(InitialVelocity);
                return divisor <= 0 ? 0 : (float)Math.Log(divisor) / DecelerationK;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ValueAt(float offsetSecs)
        {
            if (DecelerationK == 0) return InitialValue;

            float time = offsetSecs;
            float factor = (float)((Math.Pow(DecelerationRate, 1000 * time) - 1) / DecelerationK);
            return InitialValue + InitialVelocity * factor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float VelocityAt(double time)
        {
            return InitialVelocity * (float)Math.Pow(DecelerationRate, 1000 * time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DurationToValue(float value)
        {
            if (DecelerationK == 0 || InitialVelocity == 0) return 0;

            float distance = Math.Abs(value - InitialValue);
            return distance == 0 ? 0 : Math.Log(1 + DecelerationK * distance / Math.Abs(InitialVelocity)) / DecelerationK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float VelocityTo(float startingPoint, float targetPoint, double time)
        {
            float factor = (float)(Math.Pow(DecelerationRate, 1000 * time) - 1);
            return factor == 0 ? 0 : (targetPoint - startingPoint) * DecelerationK / factor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float VelocityToZero(float startingPoint, float targetPoint, float maxTimeSecs = 0, float epsilon = 1e-6f)
        {
            float distance = targetPoint - startingPoint;
            float distanceMagnitude = Math.Abs(distance);

            if (distanceMagnitude == 0 || DecelerationK == 0) return 0;

            float optimalTime = (float)(Math.Log(epsilon) - Math.Log(distanceMagnitude)) / DecelerationK;

            if (maxTimeSecs > 0 && optimalTime > maxTimeSecs)
                optimalTime = maxTimeSecs;

            return VelocityTo(startingPoint, targetPoint, optimalTime);
        }
    }
}
