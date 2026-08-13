namespace SaveSystem
{
    /// <summary>保存または読み込みが失敗した理由。</summary>
    public enum SaveError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>スロット名に使えない文字や長さが含まれている。</summary>
        InvalidSlot = 1,

        /// <summary>保存する値またはデータ版が不正。</summary>
        InvalidData = 2,

        /// <summary>指定したスロットが存在しない。</summary>
        NotFound = 3,

        /// <summary>形式版の欠落や不正値、チェックサム不一致などにより保存データを信用できない。</summary>
        CorruptData = 4,

        /// <summary>保存データの版が呼び出し側の期待と一致しない。</summary>
        VersionMismatch = 5,

        /// <summary>値と文字列の相互変換に失敗した。</summary>
        SerializationFailed = 6,

        /// <summary>ファイルなどの保存先を読み書きできなかった。</summary>
        StorageFailed = 7,

        /// <summary>保存時の型と読み込み時に要求された型が一致しない。</summary>
        TypeMismatch = 8,

        /// <summary>保存時刻の取得に失敗した。</summary>
        TimeProviderFailed = 9,

        /// <summary>正の保存形式版をこのモジュールで読み込めない。</summary>
        FormatVersionMismatch = 10,
    }
}
