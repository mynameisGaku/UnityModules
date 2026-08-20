namespace InputAxisConflict
{
    /// <summary>negativeとpositiveが同時押下された時の解決方法。</summary>
    public enum InputAxisConflictPolicy
    {
        /// <summary>競合中はneutralの0を返す。</summary>
        Neutral = 0,

        /// <summary>競合中はnegativeの-1を返す。</summary>
        NegativeWins = 1,

        /// <summary>競合中はpositiveの1を返す。</summary>
        PositiveWins = 2,

        /// <summary>より新しい押下edge側を返し、同一tickのedgeはneutralにする。</summary>
        LastPressedWins = 3
    }
}
