namespace GameplayRules
{
    /// <summary>実値と基準値の満たすべき関係を表します。</summary>
    public enum NumericRequirementComparison
    {
        /// <summary>実値が基準値以上であることを要求します。</summary>
        AtLeast = 0,
        /// <summary>実値が基準値以下であることを要求します。</summary>
        AtMost = 1,
        /// <summary>実値が基準値より大きいことを要求します。</summary>
        GreaterThan = 2,
        /// <summary>実値が基準値より小さいことを要求します。</summary>
        LessThan = 3,
        /// <summary>実値と基準値の差の絶対値が許容差以下であることを要求します。</summary>
        EqualWithinTolerance = 4,
        /// <summary>実値と基準値の差の絶対値が許容差より大きいことを要求します。</summary>
        OutsideTolerance = 5
    }
}
