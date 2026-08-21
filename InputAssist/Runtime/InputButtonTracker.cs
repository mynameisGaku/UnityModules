using System;
using UnityEngine;

namespace InputAssist
{
    /// <summary>Tracks press, release, hold, repeat, and multi-tap gestures from explicit button samples.</summary>
    [Serializable]
    public sealed class InputButtonTracker
    {
        private const int MaximumRepeatCountPerUpdate = 32;

        [SerializeField, Min(0f)] private float _holdDuration = 0.35f;
        [SerializeField, Min(0f)] private float _repeatDelay = 0.45f;
        [SerializeField, Min(0.001f)] private float _repeatInterval = 0.08f;
        [SerializeField, Min(0.001f)] private float _multiTapGap = 0.25f;
        [SerializeField, Range(1, 8)] private int _maximumTapCount = 3;

        [NonSerialized] private bool _isPressed;
        [NonSerialized] private bool _isHeld;
        [NonSerialized] private float _pressDuration;
        [NonSerialized] private float _nextRepeatTime;
        [NonSerialized] private int _pendingTapCount;
        [NonSerialized] private float _tapGapElapsed;

        /// <summary>Gets whether the most recent sample is pressed.</summary>
        public bool IsPressed => _isPressed;

        /// <summary>Gets whether the current press reached the hold boundary.</summary>
        public bool IsHeld => _isHeld;

        /// <summary>Gets the current press duration in seconds.</summary>
        public float PressDuration => _pressDuration;

        /// <summary>Gets the number of short taps waiting for completion.</summary>
        public int PendingTapCount => _pendingTapCount;

        /// <summary>Replaces gesture timing only when every supplied value is valid.</summary>
        /// <param name="holdDuration">Seconds before a press produces <see cref="InputButtonEvent.HoldStarted"/>.</param>
        /// <param name="repeatDelay">Seconds before the first repeat event.</param>
        /// <param name="repeatInterval">Seconds between repeat events.</param>
        /// <param name="multiTapGap">Maximum seconds between short taps.</param>
        /// <param name="maximumTapCount">Tap count that completes immediately without waiting for the gap.</param>
        /// <param name="error">Receives the validation error when configuration is rejected.</param>
        /// <returns><see langword="true"/> when all settings were accepted; otherwise <see langword="false"/>.</returns>
        public bool TryConfigure(float holdDuration, float repeatDelay, float repeatInterval, float multiTapGap, int maximumTapCount, out InputAssistError error)
        {
            if (!IsValidConfiguration(holdDuration, repeatDelay, repeatInterval, multiTapGap, maximumTapCount))
            {
                error = InputAssistError.InvalidConfiguration;
                return false;
            }

            _holdDuration = holdDuration;
            _repeatDelay = repeatDelay;
            _repeatInterval = repeatInterval;
            _multiTapGap = multiTapGap;
            _maximumTapCount = maximumTapCount;
            error = InputAssistError.None;
            return true;
        }

        /// <summary>Processes one pressed state using explicit elapsed time.</summary>
        /// <param name="pressed">Whether the caller considers the button pressed for this sample.</param>
        /// <param name="deltaTime">Elapsed seconds since the previous sample.</param>
        /// <returns>The current button state and gesture events produced by this sample.</returns>
        public InputButtonResult Process(bool pressed, float deltaTime)
        {
            if (!IsValidConfiguration(_holdDuration, _repeatDelay, _repeatInterval, _multiTapGap, _maximumTapCount))
                return Failure(InputAssistError.InvalidConfiguration);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return Failure(InputAssistError.NonFiniteInput);
            if (deltaTime < 0f) return Failure(InputAssistError.NegativeDeltaTime);

            var events = InputButtonEvent.None;
            var repeatCount = 0;
            var completedTapCount = 0;

            if (pressed)
            {
                if (!_isPressed)
                {
                    _isPressed = true;
                    _isHeld = false;
                    _pressDuration = 0f;
                    _nextRepeatTime = _repeatDelay;
                    events |= InputButtonEvent.Pressed;
                }
                else
                {
                    _pressDuration += deltaTime;
                    if (!_isHeld && _pressDuration >= _holdDuration)
                    {
                        _isHeld = true;
                        events |= InputButtonEvent.HoldStarted;
                    }

                    if (_pressDuration >= _nextRepeatTime)
                    {
                        var roundingTolerance = _repeatInterval * 0.0001f;
                        var due = 1 + Mathf.FloorToInt((_pressDuration - _nextRepeatTime + roundingTolerance) / _repeatInterval);
                        repeatCount = Mathf.Min(due, MaximumRepeatCountPerUpdate);
                        _nextRepeatTime += repeatCount * _repeatInterval;
                        events |= InputButtonEvent.Repeated;
                    }
                }
            }
            else
            {
                if (_isPressed)
                {
                    _isPressed = false;
                    events |= InputButtonEvent.Released;
                    if (!_isHeld)
                    {
                        _pendingTapCount = Mathf.Min(_pendingTapCount + 1, _maximumTapCount);
                        _tapGapElapsed = 0f;
                    }

                    _isHeld = false;
                    _pressDuration = 0f;
                    _nextRepeatTime = _repeatDelay;
                }

                if (_pendingTapCount > 0)
                {
                    _tapGapElapsed += deltaTime;
                    if (_tapGapElapsed >= _multiTapGap || _pendingTapCount >= _maximumTapCount)
                    {
                        completedTapCount = _pendingTapCount;
                        _pendingTapCount = 0;
                        _tapGapElapsed = 0f;
                        events |= InputButtonEvent.TapCompleted;
                    }
                }
            }

            return new InputButtonResult(_isPressed, _isHeld, events, repeatCount, completedTapCount, _pressDuration, InputAssistError.None);
        }

        /// <summary>Clears all pressed, hold, repeat, and pending tap state.</summary>
        public void Reset()
        {
            _isPressed = false;
            _isHeld = false;
            _pressDuration = 0f;
            _nextRepeatTime = _repeatDelay;
            _pendingTapCount = 0;
            _tapGapElapsed = 0f;
        }

        private InputButtonResult Failure(InputAssistError error)
        {
            return new InputButtonResult(_isPressed, _isHeld, InputButtonEvent.None, 0, 0, _pressDuration, error);
        }

        private static bool IsValidConfiguration(float holdDuration, float repeatDelay, float repeatInterval, float multiTapGap, int maximumTapCount)
        {
            return IsFiniteNonNegative(holdDuration)
                && IsFiniteNonNegative(repeatDelay)
                && IsFinitePositive(repeatInterval)
                && IsFinitePositive(multiTapGap)
                && maximumTapCount >= 1
                && maximumTapCount <= 8;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
