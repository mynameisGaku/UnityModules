namespace InputAssist
{
    /// <summary>Represents a neutral, cardinal, or diagonal input direction.</summary>
    public enum InputDirection
    {
        /// <summary>No direction is active.</summary>
        Neutral = 0,

        /// <summary>The vector points upward.</summary>
        Up = 1,

        /// <summary>The vector points down.</summary>
        Down = 2,

        /// <summary>The vector points left.</summary>
        Left = 3,

        /// <summary>The vector points right.</summary>
        Right = 4,

        /// <summary>The vector points up and left.</summary>
        UpLeft = 5,

        /// <summary>The vector points up and right.</summary>
        UpRight = 6,

        /// <summary>The vector points down and left.</summary>
        DownLeft = 7,

        /// <summary>The vector points down and right.</summary>
        DownRight = 8
    }
}
