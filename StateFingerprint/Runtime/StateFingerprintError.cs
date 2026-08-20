namespace StateFingerprint
{
    /// <summary>fingerprint操作が完了しなかった理由。</summary>
    public enum StateFingerprintError
    {
        /// <summary>操作が完了した。</summary>
        None = 0,

        /// <summary>文字列またはbyte列の入力が契約を満たさない。</summary>
        InvalidInput = 1,

        /// <summary>canonical byte列がbuilderの上限を超える。</summary>
        CapacityExceeded = 2,

        /// <summary>破棄済みbuilderへ操作した。</summary>
        Disposed = 3
    }
}

