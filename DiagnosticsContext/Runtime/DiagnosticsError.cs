namespace DiagnosticsContext
{
    /// <summary>診断情報の保持またはreport書出しを完了できなかった理由。</summary>
    public enum DiagnosticsError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>入力が空、Unicodeとして不正、または許容上限を超えている。</summary>
        InvalidInput = 1,

        /// <summary>新しいcontext keyを追加できる固定容量へ達した。</summary>
        ContextCapacityExceeded = 2,

        /// <summary>終了済みServiceを操作した。</summary>
        Disposed = 3,

        /// <summary>Unityメインスレッドで行う必要がある操作を別threadから要求した。</summary>
        MainThreadRequired = 4,

        /// <summary>保存先pathまたは専用directoryを利用できない。</summary>
        StorageUnavailable = 5,

        /// <summary>生成したUTF-8 JSONがreport全体の上限を超えた。</summary>
        ReportTooLarge = 6,

        /// <summary>一時fileの作成、flush、または最終fileへの移動に失敗した。</summary>
        WriteFailed = 7,
    }
}
