namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 再生中に明示的に記録した選択値の結果と、上限判定に使った記録量を報告します。
    /// 段階、対象識別情報、再生設定、記録量に問題がある場合は失敗結果を返します。
    /// </summary>
    public sealed class PlayModeTuningCaptureResult
    {
        internal PlayModeTuningCaptureResult(PlayModeTuningError error, string message, PlayModeTuningSession session, int capturedPropertyCount, int payloadBytes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Session = session;
            CapturedPropertyCount = capturedPropertyCount;
            PayloadBytes = payloadBytes;
        }

        /// <summary>記録処理の失敗理由を取得します。成功時は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError Error { get; }

        /// <summary>記録処理の結果を補足する説明を取得します。</summary>
        public string Message { get; }

        /// <summary>記録処理後の調整作業状態を取得します。</summary>
        public PlayModeTuningSession Session { get; }

        /// <summary>今回記録した選択項目数を取得します。失敗時は0です。</summary>
        public int CapturedPropertyCount { get; }

        /// <summary>開始値と記録値を合わせた記録量をバイト単位で取得します。失敗時は0です。</summary>
        public int PayloadBytes { get; }

        /// <summary>記録処理が失敗なく完了したかどうかを取得します。</summary>
        public bool Succeeded => Error == PlayModeTuningError.None;
    }
}
