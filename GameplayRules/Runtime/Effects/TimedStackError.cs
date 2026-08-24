namespace GameplayEffects
{
    /// <summary>時限stackの解決要求を受理できなかった理由です。</summary>
    public enum TimedStackError
    {
        /// <summary>失敗していません。</summary>
        None = 0,

        /// <summary>最大stack数が許容範囲外です。</summary>
        InvalidMaximumStackCount = 1,

        /// <summary>最大残りtick数が許容範囲外です。</summary>
        InvalidMaximumDurationTicks = 2,

        /// <summary>stack数の再適用方法が未定義です。</summary>
        InvalidStackMode = 3,

        /// <summary>残りtick数の再適用方法が未定義です。</summary>
        InvalidDurationMode = 4,

        /// <summary>現在状態が非active表現または方針の範囲に収まりません。</summary>
        InvalidCurrentState = 5,

        /// <summary>追加状態が正の許容範囲に収まりません。</summary>
        InvalidIncomingState = 6
    }
}
