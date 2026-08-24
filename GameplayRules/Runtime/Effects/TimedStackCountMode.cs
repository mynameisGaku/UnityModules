namespace GameplayEffects
{
    /// <summary>再適用時にstack数を組み合わせる方法です。</summary>
    public enum TimedStackCountMode
    {
        /// <summary>現在値と追加値を加算して上限に収めます。</summary>
        AddClamped = 0,

        /// <summary>追加値へ置き換えて上限に収めます。</summary>
        ReplaceClamped = 1,

        /// <summary>現在値と追加値の大きい方を選び上限に収めます。</summary>
        MaximumClamped = 2
    }
}
