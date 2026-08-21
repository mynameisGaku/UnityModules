namespace InputAssist
{
    /// <summary>Defines whether a processed vector is classified into four or eight directions.</summary>
    public enum InputDirectionMode
    {
        /// <summary>Returns only cardinal directions.</summary>
        FourWay = 0,

        /// <summary>Returns cardinal and diagonal directions.</summary>
        EightWay = 1
    }
}
