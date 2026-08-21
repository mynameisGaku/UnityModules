namespace InputAssist
{
    /// <summary>Contains button state, completed gesture flags, counts, and a failure reason.</summary>
    public readonly struct InputButtonResult
    {
        /// <summary>Gets whether processing succeeded.</summary>
        public bool Succeeded => Error == InputAssistError.None;

        /// <summary>Gets the current physical pressed state.</summary>
        public bool IsPressed { get; }

        /// <summary>Gets whether the current press reached the hold boundary.</summary>
        public bool IsHeld { get; }

        /// <summary>Gets the events produced by this update.</summary>
        public InputButtonEvent Events { get; }

        /// <summary>Gets the number of repeat intervals completed in this update.</summary>
        public int RepeatCount { get; }

        /// <summary>Gets the completed tap count when TapCompleted is present.</summary>
        public int TapCount { get; }

        /// <summary>Gets the current press duration in seconds.</summary>
        public float PressDuration { get; }

        /// <summary>Gets the explicit failure reason.</summary>
        public InputAssistError Error { get; }

        internal InputButtonResult(bool isPressed, bool isHeld, InputButtonEvent events, int repeatCount, int tapCount, float pressDuration, InputAssistError error)
        {
            IsPressed = isPressed;
            IsHeld = isHeld;
            Events = events;
            RepeatCount = repeatCount;
            TapCount = tapCount;
            PressDuration = pressDuration;
            Error = error;
        }
    }
}
