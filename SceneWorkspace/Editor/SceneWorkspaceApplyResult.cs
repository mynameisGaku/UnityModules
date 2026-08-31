namespace SceneWorkspace.Editor
{
    /// <summary>元の切り替え失敗を復元結果で隠さないよう、切り替えと復元を別々に報告します。</summary>
    public sealed class SceneWorkspaceApplyResult
    {
        /// <summary>切り替えと復元の実行有無、成否、理由、案内から結果を作成します。</summary>
        internal SceneWorkspaceApplyResult(bool applyAttempted, bool applySucceeded, SceneWorkspaceError applyError, string applyMessage, bool rollbackAttempted, bool rollbackSucceeded, SceneWorkspaceError rollbackError, string rollbackMessage)
        {
            ApplyAttempted = applyAttempted;
            ApplySucceeded = applySucceeded;
            ApplyError = applyError;
            ApplyMessage = applyMessage ?? string.Empty;
            RollbackAttempted = rollbackAttempted;
            RollbackSucceeded = rollbackSucceeded;
            RollbackError = rollbackError;
            RollbackMessage = rollbackMessage ?? string.Empty;
        }

        /// <summary>シーン構成を実際に変更する処理まで進んだかを返します。</summary>
        public bool ApplyAttempted { get; }

        /// <summary>確認済みの構成と切り替え後の構成が一致したかを返します。</summary>
        public bool ApplySucceeded { get; }

        /// <summary>切り替えに失敗した理由を返します。</summary>
        public SceneWorkspaceError ApplyError { get; }

        /// <summary>切り替え結果の日本語案内を返します。</summary>
        public string ApplyMessage { get; }

        /// <summary>元のシーン構成への復元を試みたかを返します。</summary>
        public bool RollbackAttempted { get; }

        /// <summary>元のシーン構成と復元後の構成が一致したかを返します。</summary>
        public bool RollbackSucceeded { get; }

        /// <summary>復元に失敗した理由を返します。</summary>
        public SceneWorkspaceError RollbackError { get; }

        /// <summary>復元結果の日本語案内を返します。</summary>
        public string RollbackMessage { get; }

        /// <summary>切り替えが成功し、復元失敗もない場合に有効です。</summary>
        public bool Succeeded => ApplySucceeded && RollbackError == SceneWorkspaceError.None;
    }
}
