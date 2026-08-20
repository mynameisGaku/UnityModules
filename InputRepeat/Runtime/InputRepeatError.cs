namespace InputRepeating
{
    /// <summary>Input Repeatの要求を受理できなかった理由。</summary>
    public enum InputRepeatError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>初回repeatまでのdelay tick数が0。</summary>
        InvalidInitialDelay = 1,

        /// <summary>repeat間隔のtick数が0。</summary>
        InvalidRepeatInterval = 2,

        /// <summary>入力tickが現在tickより前へ戻っている。</summary>
        TickMovedBackward = 3
    }
}
