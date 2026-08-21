using System;
using UnityEngine;

namespace InputAssist
{
    /// <summary>Applies radial dead-zone mapping, a response curve, rate limiting, and direction classification.</summary>
    [Serializable]
    public sealed class InputVectorFilter
    {
        private const float DiagonalThreshold = 0.41421356237f;

        [SerializeField, Range(0f, 0.99f)] private float _innerDeadZone = 0.15f;
        [SerializeField, Range(0.01f, 1f)] private float _outerDeadZone = 1f;
        [SerializeField] private InputResponseMode _responseMode = InputResponseMode.SmoothStep;
        [SerializeField, Min(0f)] private float _riseSpeed = 12f;
        [SerializeField, Min(0f)] private float _fallSpeed = 16f;
        [SerializeField, Range(0f, 0.99f)] private float _directionDeadZone = 0.35f;
        [SerializeField] private InputDirectionMode _directionMode = InputDirectionMode.EightWay;

        [NonSerialized] private Vector2 _current;

        /// <summary>Gets the current filtered vector.</summary>
        public Vector2 Current => _current;

        /// <summary>Gets the configured inner radial dead-zone boundary.</summary>
        public float InnerDeadZone => _innerDeadZone;

        /// <summary>Gets the configured outer radial dead-zone boundary.</summary>
        public float OuterDeadZone => _outerDeadZone;

        /// <summary>Gets the configured magnitude response curve.</summary>
        public InputResponseMode ResponseMode => _responseMode;

        /// <summary>Gets the maximum magnitude change per second while input increases. Zero disables rate limiting.</summary>
        public float RiseSpeed => _riseSpeed;

        /// <summary>Gets the maximum magnitude change per second while input decreases. Zero disables rate limiting.</summary>
        public float FallSpeed => _fallSpeed;

        /// <summary>Gets the magnitude boundary used for neutral direction classification.</summary>
        public float DirectionDeadZone => _directionDeadZone;

        /// <summary>Gets the configured four-way or eight-way classification mode.</summary>
        public InputDirectionMode DirectionMode => _directionMode;

        /// <summary>Replaces all settings only when every supplied value is valid.</summary>
        /// <param name="innerDeadZone">Magnitude at or below which input becomes zero.</param>
        /// <param name="outerDeadZone">Magnitude at which filtered input reaches one.</param>
        /// <param name="responseMode">Curve applied to normalized magnitude.</param>
        /// <param name="riseSpeed">Maximum magnitude increase per second, or zero to disable limiting.</param>
        /// <param name="fallSpeed">Maximum magnitude decrease per second, or zero to disable limiting.</param>
        /// <param name="directionDeadZone">Filtered magnitude treated as the neutral direction.</param>
        /// <param name="directionMode">Four-way or eight-way direction classification.</param>
        /// <param name="error">Receives the validation error when configuration is rejected.</param>
        /// <returns><see langword="true"/> when all settings were accepted; otherwise <see langword="false"/>.</returns>
        public bool TryConfigure(float innerDeadZone, float outerDeadZone, InputResponseMode responseMode, float riseSpeed, float fallSpeed, float directionDeadZone, InputDirectionMode directionMode, out InputAssistError error)
        {
            if (!IsValidConfiguration(innerDeadZone, outerDeadZone, responseMode, riseSpeed, fallSpeed, directionDeadZone, directionMode))
            {
                error = InputAssistError.InvalidConfiguration;
                return false;
            }

            _innerDeadZone = innerDeadZone;
            _outerDeadZone = outerDeadZone;
            _responseMode = responseMode;
            _riseSpeed = riseSpeed;
            _fallSpeed = fallSpeed;
            _directionDeadZone = directionDeadZone;
            _directionMode = directionMode;
            error = InputAssistError.None;
            return true;
        }

        /// <summary>Processes one raw vector using explicit elapsed time and keeps state only on success.</summary>
        /// <param name="rawInput">Raw two-dimensional input supplied by the caller.</param>
        /// <param name="deltaTime">Elapsed seconds since the previous sample.</param>
        /// <returns>The raw value, filtered value, classified direction, and validation result.</returns>
        public InputVectorFilterResult Process(Vector2 rawInput, float deltaTime)
        {
            if (!IsValidConfiguration(_innerDeadZone, _outerDeadZone, _responseMode, _riseSpeed, _fallSpeed, _directionDeadZone, _directionMode))
                return Failure(rawInput, InputAssistError.InvalidConfiguration);
            if (!IsFinite(rawInput.x) || !IsFinite(rawInput.y) || !IsFinite(deltaTime))
                return Failure(rawInput, InputAssistError.NonFiniteInput);
            if (deltaTime < 0f) return Failure(rawInput, InputAssistError.NegativeDeltaTime);

            var target = ApplyDeadZoneAndCurve(rawInput);
            var speed = target.sqrMagnitude >= _current.sqrMagnitude ? _riseSpeed : _fallSpeed;
            _current = speed == 0f ? target : Vector2.MoveTowards(_current, target, speed * deltaTime);
            var direction = ClassifyDirection(_current, _directionDeadZone, _directionMode);
            return new InputVectorFilterResult(rawInput, _current, direction, InputAssistError.None);
        }

        /// <summary>Clears the state to zero without changing the serialized settings.</summary>
        public void Reset()
        {
            _current = Vector2.zero;
        }

        /// <summary>Rebuilds state from a finite vector inside the unit circle.</summary>
        /// <param name="value">Filtered state to restore inside the unit circle.</param>
        /// <param name="error">Receives the validation error when the value is rejected.</param>
        /// <returns><see langword="true"/> when the state was restored; otherwise <see langword="false"/>.</returns>
        public bool TryReset(Vector2 value, out InputAssistError error)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                error = InputAssistError.NonFiniteInput;
                return false;
            }

            if (value.sqrMagnitude > 1f)
            {
                error = InputAssistError.ResetValueOutOfRange;
                return false;
            }

            _current = value;
            error = InputAssistError.None;
            return true;
        }

        private InputVectorFilterResult Failure(Vector2 rawInput, InputAssistError error)
        {
            return new InputVectorFilterResult(rawInput, _current, ClassifyDirection(_current, _directionDeadZone, _directionMode), error);
        }

        private Vector2 ApplyDeadZoneAndCurve(Vector2 rawInput)
        {
            var magnitude = rawInput.magnitude;
            if (magnitude <= _innerDeadZone) return Vector2.zero;

            var direction = magnitude == 0f ? Vector2.zero : rawInput / magnitude;
            var clampedMagnitude = Mathf.Min(magnitude, _outerDeadZone);
            var normalized = (clampedMagnitude - _innerDeadZone) / (_outerDeadZone - _innerDeadZone);
            var curved = ApplyResponse(Mathf.Clamp01(normalized), _responseMode);
            return direction * curved;
        }

        private static float ApplyResponse(float magnitude, InputResponseMode mode)
        {
            switch (mode)
            {
                case InputResponseMode.Linear:
                    return magnitude;
                case InputResponseMode.Squared:
                    return magnitude * magnitude;
                case InputResponseMode.Cubic:
                    return magnitude * magnitude * magnitude;
                case InputResponseMode.SmoothStep:
                    return magnitude * magnitude * (3f - 2f * magnitude);
                default:
                    return magnitude;
            }
        }

        private static InputDirection ClassifyDirection(Vector2 value, float deadZone, InputDirectionMode mode)
        {
            if (value.sqrMagnitude <= deadZone * deadZone) return InputDirection.Neutral;
            var x = value.x;
            var y = value.y;
            var absX = Mathf.Abs(x);
            var absY = Mathf.Abs(y);

            if (mode == InputDirectionMode.FourWay)
            {
                if (absX > absY) return x < 0f ? InputDirection.Left : InputDirection.Right;
                return y < 0f ? InputDirection.Down : InputDirection.Up;
            }

            if (absY <= absX * DiagonalThreshold) return x < 0f ? InputDirection.Left : InputDirection.Right;
            if (absX <= absY * DiagonalThreshold) return y < 0f ? InputDirection.Down : InputDirection.Up;
            if (x < 0f) return y < 0f ? InputDirection.DownLeft : InputDirection.UpLeft;
            return y < 0f ? InputDirection.DownRight : InputDirection.UpRight;
        }

        private static bool IsValidConfiguration(float innerDeadZone, float outerDeadZone, InputResponseMode responseMode, float riseSpeed, float fallSpeed, float directionDeadZone, InputDirectionMode directionMode)
        {
            return IsFinite(innerDeadZone)
                && IsFinite(outerDeadZone)
                && innerDeadZone >= 0f
                && innerDeadZone < outerDeadZone
                && outerDeadZone <= 1f
                && responseMode >= InputResponseMode.Linear
                && responseMode <= InputResponseMode.SmoothStep
                && IsFinite(riseSpeed)
                && riseSpeed >= 0f
                && IsFinite(fallSpeed)
                && fallSpeed >= 0f
                && IsFinite(directionDeadZone)
                && directionDeadZone >= 0f
                && directionDeadZone < 1f
                && (directionMode == InputDirectionMode.FourWay || directionMode == InputDirectionMode.EightWay);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
