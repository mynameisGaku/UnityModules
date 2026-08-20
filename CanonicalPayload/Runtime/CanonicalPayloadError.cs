namespace CanonicalPayload
{
    /// <summary>Canonical Payload操作の失敗理由。</summary>
    public enum CanonicalPayloadError
    {
        /// <summary>成功。</summary>
        None = 0,

        /// <summary>null入力または不正な上限。</summary>
        InvalidInput = 1,

        /// <summary>設定したbyte上限を超える。</summary>
        CapacityExceeded = 2,

        /// <summary>破棄済みwriterへの操作。</summary>
        Disposed = 3,

        /// <summary>要求した値を読むbyteが残っていない。</summary>
        EndOfPayload = 4,

        /// <summary>boolean表現が0または1ではない。</summary>
        InvalidBoolean = 5,

        /// <summary>stringが厳格なUTF-8として不正。</summary>
        InvalidUtf8 = 6,

        /// <summary>length prefixが残りbyte範囲を超える。</summary>
        InvalidLength = 7
    }
}
