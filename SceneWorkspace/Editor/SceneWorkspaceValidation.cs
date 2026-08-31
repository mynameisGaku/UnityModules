namespace SceneWorkspace.Editor
{
    /// <summary>例外やエディター変更を伴わない、一つの検証結果を保持します。</summary>
    internal readonly struct SceneWorkspaceValidation
    {
        /// <summary>失敗理由と日本語案内から検証結果を作成します。</summary>
        internal SceneWorkspaceValidation(SceneWorkspaceError error, string message)
        {
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>検証に失敗していない場合は問題なしを返します。</summary>
        internal SceneWorkspaceError Error { get; }

        /// <summary>利用者が失敗を修正するための日本語案内を返します。</summary>
        internal string Message { get; }

        /// <summary>検証に成功したかを返します。</summary>
        internal bool Succeeded => Error == SceneWorkspaceError.None;

        /// <summary>問題が見つからなかった検証結果を返します。</summary>
        internal static SceneWorkspaceValidation Success => new SceneWorkspaceValidation(SceneWorkspaceError.None, string.Empty);
    }
}
