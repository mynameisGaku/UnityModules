using System;

namespace InputAssist
{
    /// <summary>Describes button transitions and completed gestures for one update.</summary>
    [Flags]
    public enum InputButtonEvent
    {
        /// <summary>No transition or gesture completed.</summary>
        None = 0,

        /// <summary>The button changed from released to pressed.</summary>
        Pressed = 1 << 0,

        /// <summary>The button changed from pressed to released.</summary>
        Released = 1 << 1,

        /// <summary>The hold duration was reached for the first time.</summary>
        HoldStarted = 1 << 2,

        /// <summary>One or more repeat intervals completed.</summary>
        Repeated = 1 << 3,

        /// <summary>A single or multi-tap sequence completed.</summary>
        TapCompleted = 1 << 4
    }
}
