namespace InputArbitration
{
    /// <summary>command候補を仲裁できなかった理由。</summary>
    public enum InputCommandArbitrationError
    {
        /// <summary>仲裁が成功した。</summary>
        None = 0,

        /// <summary>候補listがnullだった。</summary>
        NullCandidates = 1,

        /// <summary>候補数が上限を超えた。</summary>
        TooManyCandidates = 2,

        /// <summary>正でないcommand idを含んでいた。</summary>
        InvalidCommandId = 3,

        /// <summary>同じcommand idを複数含んでいた。</summary>
        DuplicateCommandId = 4
    }
}
