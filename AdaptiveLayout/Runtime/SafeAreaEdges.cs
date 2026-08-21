using System;

namespace AdaptiveLayout
{
    /// <summary>
    /// Selects the screen edges that must remain inside the safe area.
    /// </summary>
    [Flags]
    public enum SafeAreaEdges
    {
        /// <summary>Does not constrain any edge.</summary>
        None = 0,

        /// <summary>Constrains the left edge.</summary>
        Left = 1 << 0,

        /// <summary>Constrains the top edge.</summary>
        Top = 1 << 1,

        /// <summary>Constrains the right edge.</summary>
        Right = 1 << 2,

        /// <summary>Constrains the bottom edge.</summary>
        Bottom = 1 << 3,

        /// <summary>Constrains every edge.</summary>
        All = Left | Top | Right | Bottom
    }
}
