namespace GameplayRules
{
    /// <summary>Numeric Requirement Evaluatorが入力を受理できなかった理由を表します。</summary>
    public enum NumericRequirementError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,
        /// <summary>条件配列がnullです。</summary>
        NullRequirements = 1,
        /// <summary>条件件数が1以上32以下ではありません。</summary>
        InvalidRequirementCount = 2,
        /// <summary>条件識別子が正の整数ではありません。</summary>
        InvalidIdentifier = 3,
        /// <summary>実値または基準値がNaNかInfinityです。</summary>
        NonFiniteValue = 4,
        /// <summary>比較方法が未定義値です。</summary>
        InvalidComparison = 5,
        /// <summary>許容差が非有限・負、または大小比較に0以外が指定されています。</summary>
        InvalidTolerance = 6,
        /// <summary>同じ条件識別子が複数あります。</summary>
        DuplicateIdentifier = 7,
        /// <summary>有限入力から有限な差を表現できません。</summary>
        ResultOutOfRange = 8
    }
}
