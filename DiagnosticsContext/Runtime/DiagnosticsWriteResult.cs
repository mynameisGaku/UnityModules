namespace DiagnosticsContext
{
    /// <summary>診断reportの書出し成否、失敗理由、保存先、UTF-8 byte数を返す値。</summary>
    public readonly struct DiagnosticsWriteResult
    {
        /// <summary>書出し結果を構築する。</summary>
        /// <param name="succeeded">最終reportを保存できた場合はtrue。</param>
        /// <param name="error">失敗理由。成功時はNone。</param>
        /// <param name="reportPath">成功した最終reportの絶対path。失敗時は空。</param>
        /// <param name="reportByteCount">保存したUTF-8 byte数。失敗時は0。</param>
        internal DiagnosticsWriteResult(bool succeeded, DiagnosticsError error, string reportPath, int reportByteCount)
        {
            Succeeded = succeeded;
            Error = error;
            ReportPath = reportPath;
            ReportByteCount = reportByteCount;
        }

        /// <summary>最終reportを保存できた場合はtrue。</summary>
        public bool Succeeded { get; }

        /// <summary>書出しを完了できなかった理由。成功時はNone。</summary>
        public DiagnosticsError Error { get; }

        /// <summary>成功した最終reportの絶対path。失敗時は空文字列。</summary>
        public string ReportPath { get; }

        /// <summary>保存したUTF-8 JSONのbyte数。失敗時は0。</summary>
        public int ReportByteCount { get; }

        /// <summary>指定理由による失敗結果を構築する。</summary>
        /// <param name="error">None以外の失敗理由。</param>
        /// <returns>pathとbyte数を持たない失敗結果。</returns>
        internal static DiagnosticsWriteResult Failure(DiagnosticsError error)
        {
            return new DiagnosticsWriteResult(false, error, string.Empty, 0);
        }

        /// <summary>保存済みreportの成功結果を構築する。</summary>
        /// <param name="reportPath">保存した最終reportの絶対path。</param>
        /// <param name="reportByteCount">保存したUTF-8 byte数。</param>
        /// <returns>失敗理由を持たない成功結果。</returns>
        internal static DiagnosticsWriteResult Success(string reportPath, int reportByteCount)
        {
            return new DiagnosticsWriteResult(true, DiagnosticsError.None, reportPath, reportByteCount);
        }
    }
}
