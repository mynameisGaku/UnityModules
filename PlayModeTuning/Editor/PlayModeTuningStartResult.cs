namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 編集状態で選択内容と開始値を検証し、上限内の調整作業として固定できたかどうかを報告します。
    /// 失敗時は対象値を変更せず、理由と現在の作業状態を返します。
    /// </summary>
    public sealed class PlayModeTuningStartResult
    {
        internal PlayModeTuningStartResult(PlayModeTuningError error, string message, PlayModeTuningSession session)
        {
            Error = error;
            Message = message ?? string.Empty;
            Session = session;
        }

        /// <summary>開始処理の失敗理由を取得します。成功時は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError Error { get; }

        /// <summary>開始処理の結果を補足する説明を取得します。</summary>
        public string Message { get; }

        /// <summary>開始処理後の調整作業状態を取得します。</summary>
        public PlayModeTuningSession Session { get; }

        /// <summary>選択内容と開始値を固定し、再生開始を待てる状態になったかどうかを取得します。</summary>
        public bool Succeeded => Error == PlayModeTuningError.None;
    }
}
