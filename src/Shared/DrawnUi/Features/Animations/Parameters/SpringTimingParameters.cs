using System;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{

    public class SpringTimingParameters : IDampingTimingParameters
    {
        public Spring Spring { get; }
        public float Displacement { get; }
        public float InitialVelocity { get; }
        public float Threshold { get; }
        private IDampingTimingParameters Impl { get; }

        public SpringTimingParameters(Spring spring, float displacement, float initialVelocity, float threshold)
        {
            if (spring.DampingRatio <= 0 || spring.DampingRatio > 1)
            {
                throw new ArgumentException("dampingRatio should be greater than 0 and less than or equal to 1");
            }

            Spring = spring;
            Displacement = displacement;
            InitialVelocity = initialVelocity;
            Threshold = threshold;

            if (spring.DampingRatio >= 1)
            {
                Impl = new CriticallyDampedSpringTimingParameters(spring, displacement, initialVelocity, threshold);
            }
            else
            {
                Impl = new UnderdampedSpringTimingParameters(spring, displacement, initialVelocity, threshold);
            }
        }

        public float DurationSecs => Impl.DurationSecs;

        public float ValueAt(float offsetSecs)
        {
            return Impl.ValueAt(offsetSecs);
        }

        public float AmplitudeAt(float offsetSecs)
        {
            return Impl.AmplitudeAt(offsetSecs);
        }
    }


    public class CriticallyDampedSpringTimingParameters : IDampingTimingParameters
    {
        private readonly Spring _spring;
        private readonly float _displacement;
        private readonly float _initialVelocity;
        private readonly float _threshold;

        public CriticallyDampedSpringTimingParameters(Spring spring, float displacement, float initialVelocity, float threshold)
        {
            _spring = spring;
            _displacement = displacement;
            _initialVelocity = initialVelocity;
            _threshold = threshold;
        }

        public float DurationSecs
        {
            get
            {
                if (_displacement == 0 && _initialVelocity == 0)
                {
                    return 0;
                }

                float b = Beta();
                float e = MathF.Exp(1);

                float t1 = 1 / b * MathF.Log(2 * MathF.Abs(_c1) / _threshold);
                float t2 = 2 / b * MathF.Log(4 * MathF.Abs(_c2) / (e * b * _threshold));

                return MathF.Max(t1, t2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ValueAt(float offsetSecs)
        {
            float t = offsetSecs;
            return MathF.Exp(-Beta() * t) * (_c1 + _c2 * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AmplitudeAt(float offsetSecs)
        {
            var value = ValueAt(offsetSecs);
            return MathF.Abs(value);
        }

        private float _c1 => _displacement;
        private float _c2 => _initialVelocity + Beta() * _displacement;

        private float Beta()
        {
            return _spring.DampingRatio * NaturalFrequency() * 2;
        }

        private float NaturalFrequency()
        {
            return MathF.Sqrt(_spring.Stiffness / _spring.Mass);
        }
    }

    public class UnderdampedSpringTimingParameters : IDampingTimingParameters
    {
        private readonly Spring _spring;
        private readonly float _displacement;
        private readonly float _initialVelocity;
        private readonly float _threshold;

        public UnderdampedSpringTimingParameters(Spring spring, float displacement, float initialVelocity, float threshold)
        {
            _spring = spring;
            _displacement = displacement;
            _initialVelocity = initialVelocity;
            _threshold = threshold;
        }

        public float DurationSecs
        {
            get
            {
                if (_displacement == 0 && _initialVelocity == 0)
                {
                    return 0;
                }

                return MathF.Log((MathF.Abs(_c1) + MathF.Abs(_c2)) / _threshold) / Beta();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ValueAt(float offsetSecs)
        {
            float t = offsetSecs;
            float wd = DampedNaturalFrequency();
            return MathF.Exp(-Beta() * t) * (_c1 * MathF.Cos(wd * t) + _c2 * MathF.Sin(wd * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AmplitudeAt(float offsetSecs)
        {
            var value = ValueAt(offsetSecs);
            return MathF.Abs(value);
        }

        private float _c1 => _displacement;
        private float _c2 => (_initialVelocity + Beta() * _displacement) / DampedNaturalFrequency();

        private float Beta()
        {
            return _spring.DampingRatio * NaturalFrequency() * 2;
        }

        private float NaturalFrequency()
        {
            return MathF.Sqrt(_spring.Stiffness / _spring.Mass);
        }

        private float DampedNaturalFrequency()
        {
            return NaturalFrequency() * MathF.Sqrt(1 - _spring.DampingRatio * _spring.DampingRatio);
        }
    }




}