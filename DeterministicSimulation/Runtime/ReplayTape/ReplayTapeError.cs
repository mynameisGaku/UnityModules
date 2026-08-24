namespace ReplayTape
{
    /// <summary>Replay Tape操作が完了しなかった理由。</summary>
    public enum ReplayTapeError
    {
        /// <summary>操作が完了した。</summary>
        None = 0,

        /// <summary>command idまたは入力byte列が契約を満たさない。</summary>
        InvalidInput = 1,

        /// <summary>byte数またはentry数が設定上限を超える。</summary>
        CapacityExceeded = 2,

        /// <summary>追加tickが直前のtickより小さい。</summary>
        TickOrderViolation = 3,

        /// <summary>magicまたはheader長がReplay Tape形式ではない。</summary>
        InvalidHeader = 4,

        /// <summary>指定された形式versionをこの実装が読めない。</summary>
        UnsupportedVersion = 5,

        /// <summary>header、record数、payload長、tick順序のいずれかが壊れている。</summary>
        CorruptedData = 6,

        /// <summary>readerが末尾まで読み終えている。</summary>
        EndOfTape = 7,

        /// <summary>copy先の領域がpayloadより小さい。</summary>
        DestinationTooSmall = 8,

        /// <summary>破棄済みbuilderへ操作した。</summary>
        Disposed = 9
    }
}
