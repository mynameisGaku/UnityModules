namespace InputChording
{
    /// <summary>Input Chord Matcherの要求を受理できなかった理由。</summary>
    public enum InputChordError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>required command数が2から16の範囲外。</summary>
        InvalidRequiredCommandCount = 1,

        /// <summary>required command idに0以下が含まれる。</summary>
        InvalidRequiredCommandId = 2,

        /// <summary>required command idが重複している。</summary>
        DuplicateRequiredCommandId = 3,

        /// <summary>pressed snapshotがnull、上限超過、非正値、昇順でない、または重複している。</summary>
        InvalidPressedSnapshot = 4,

        /// <summary>入力tickが最後に受理したtickより小さい。</summary>
        TickMovedBackward = 5
    }
}
