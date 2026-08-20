namespace InputSequencing
{
    /// <summary>Input Sequence Matcherの要求を受理できなかった理由。</summary>
    public enum InputSequenceError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>pattern配列がnull。</summary>
        PatternNull = 1,

        /// <summary>pattern長が1から最大長までの範囲にない。</summary>
        PatternLengthOutOfRange = 2,

        /// <summary>pattern内に正でないcommand idがある。</summary>
        InvalidPatternCommandId = 3,

        /// <summary>入力command idが正でない。</summary>
        InvalidCommandId = 4,

        /// <summary>入力tickが現在tickより前へ戻っている。</summary>
        TickMovedBackward = 5
    }
}
