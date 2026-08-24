namespace InputBuffering
{
    /// <summary>Input Command Bufferの要求を受理できなかった理由。</summary>
    public enum InputCommandBufferError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>容量が1から最大容量までの範囲にない。</summary>
        InvalidCapacity = 1,

        /// <summary>command idが正の整数でない。</summary>
        InvalidCommandId = 2,

        /// <summary>指定tickが現在tickより前へ戻っている。</summary>
        TickMovedBackward = 3,

        /// <summary>期限内のcommandで固定容量が埋まっている。</summary>
        CapacityExceeded = 4,

        /// <summary>指定commandがbuffer内にない。</summary>
        NotFound = 5,

        /// <summary>command順序番号をこれ以上割り当てられない。</summary>
        SequenceExhausted = 6
    }
}
