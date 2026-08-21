namespace InputMixing
{
    /// <summary>2D weighted mixを拒否した具体的な理由。</summary>
    public enum InputVectorWeightedMixerError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,
        /// <summary>contribution配列がnullだった。</summary>
        NullInput = 1,
        /// <summary>contribution数が公開上限を超えた。</summary>
        TooManyContributions = 2,
        /// <summary>2D成分にNaNまたはInfinityが含まれた。</summary>
        NonFiniteInput = 3,
        /// <summary>2D成分が-1以上1以下の範囲外だった。</summary>
        InputOutOfRange = 4,
        /// <summary>weightがNaNまたはInfinityだった。</summary>
        NonFiniteWeight = 5,
        /// <summary>weightが0以上1以下の範囲外だった。</summary>
        WeightOutOfRange = 6
    }
}
