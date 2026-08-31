namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 一度だけ使える計画を消費した後の反映結果と復元結果を、互いに独立して報告します。
    /// 反映前に失敗した場合は<see cref="ApplyAttempted"/>が<c>false</c>となります。
    /// </summary>
    public sealed class PlayModeTuningApplyResult
    {
        internal PlayModeTuningApplyResult(bool applyAttempted, bool applySucceeded, PlayModeTuningError applyError, string applyMessage, bool rollbackAttempted, bool rollbackSucceeded, PlayModeTuningError rollbackError, string rollbackMessage, PlayModeTuningSession session)
        {
            ApplyAttempted = applyAttempted;
            ApplySucceeded = applySucceeded;
            ApplyError = applyError;
            ApplyMessage = applyMessage ?? string.Empty;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            RollbackError = rollbackError;
            RollbackMessage = rollbackMessage ?? string.Empty;
            Session = session;
        }

        /// <summary>対象値の書き換えを開始したかどうかを取得します。</summary>
        public bool ApplyAttempted { get; }

        /// <summary>書き換え、反映後確認、シーンの変更済み設定をすべて完了したかどうかを取得します。</summary>
        public bool ApplySucceeded { get; }

        /// <summary>反映処理の失敗理由を取得します。成功時は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError ApplyError { get; }

        /// <summary>反映処理の結果を補足する説明を取得します。</summary>
        public string ApplyMessage { get; }

        /// <summary>反映失敗後に、反映直前の状態への復元を試みたかどうかを取得します。</summary>
        public bool RollbackAttempted { get; }

        /// <summary>復元を試み、選択項目と未選択項目の両方が反映直前の状態へ戻ったかどうかを取得します。</summary>
        public bool RollbackSucceeded { get; }

        /// <summary>復元処理の失敗理由を取得します。復元成功時または復元不要時は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError RollbackError { get; }

        /// <summary>復元処理の結果を補足する説明を取得します。</summary>
        public string RollbackMessage { get; }

        /// <summary>反映または復元を終えた時点の調整作業状態を取得します。</summary>
        public PlayModeTuningSession Session { get; }
    }
}
