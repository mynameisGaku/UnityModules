namespace GameplayEffects
{
    /// <summary>再適用時に残りtick数を組み合わせる方法です。</summary>
    public enum TimedStackDurationMode
    {
        /// <summary>追加値へ更新して上限に収めます。</summary>
        RefreshClamped = 0,

        /// <summary>現在値と追加値を加算して上限に収めます。</summary>
        AddClamped = 1,

        /// <summary>現在値と追加値の大きい方を選び上限に収めます。</summary>
        MaximumClamped = 2
    }
}
