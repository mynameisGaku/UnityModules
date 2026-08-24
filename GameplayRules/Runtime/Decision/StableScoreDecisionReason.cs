namespace GameplayDecision
{
    /// <summary>最終候補を選択または維持した理由です。</summary>
    public enum StableScoreDecisionReason
    {
        /// <summary>有効な決定理由はありません。</summary>
        None = 0,

        /// <summary>現在選択が指定されていないため最高score候補を選びました。</summary>
        SelectedWithoutCurrent = 1,

        /// <summary>指定された現在候補が入力に無いため最高score候補へ置き換えました。</summary>
        ReplacedMissingCurrent = 2,

        /// <summary>現在候補以外に比較対象が無いため維持しました。</summary>
        KeptOnlyCurrent = 3,

        /// <summary>最高challengerが現在候補以下のscoreだったため維持しました。</summary>
        KeptCurrentTieOrLower = 4,

        /// <summary>challengerの優位差が最小優位差未満だったため現在候補を維持しました。</summary>
        KeptCurrentBelowMinimumAdvantage = 5,

        /// <summary>challengerが最小優位差以上の高いscoreを持つため切り替えました。</summary>
        SwitchedByMinimumAdvantage = 6
    }
}
