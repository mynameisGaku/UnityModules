namespace FixedPoint
{
    /// <summary>Q16.16値の生成または演算が失敗した理由。</summary>
    public enum Fixed32Error
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>結果を符号付きQ16.16の範囲へ収められない。</summary>
        Overflow = 1,

        /// <summary>分母または除数が0である。</summary>
        DivisionByZero = 2
    }
}
