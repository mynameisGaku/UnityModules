namespace InputAssist
{
    /// <summary>Defines how normalized stick magnitude is reshaped.</summary>
    public enum InputResponseMode
    {
        /// <summary>Preserves the normalized magnitude.</summary>
        Linear = 0,

        /// <summary>Reduces low input by squaring the magnitude.</summary>
        Squared = 1,

        /// <summary>Reduces low input more strongly by cubing the magnitude.</summary>
        Cubic = 2,

        /// <summary>Uses a smooth step curve with gentle endpoints.</summary>
        SmoothStep = 3
    }
}
