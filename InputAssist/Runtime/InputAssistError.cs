namespace InputAssist
{
    /// <summary>Describes why an input processing request was rejected.</summary>
    public enum InputAssistError
    {
        /// <summary>The request completed successfully.</summary>
        None = 0,

        /// <summary>The serialized or supplied configuration is invalid.</summary>
        InvalidConfiguration = 1,

        /// <summary>An input component or elapsed time is NaN or infinite.</summary>
        NonFiniteInput = 2,

        /// <summary>The supplied elapsed time is negative.</summary>
        NegativeDeltaTime = 3,

        /// <summary>The requested reset state lies outside the unit circle.</summary>
        ResetValueOutOfRange = 4
    }
}
