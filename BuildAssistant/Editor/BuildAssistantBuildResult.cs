namespace BuildAssistant.Editor
{
    /// <summary>履歴を永続保存できたかどうかとは別に、ビルドの成否を表します。</summary>
    public sealed class BuildAssistantBuildResult
    {
        internal BuildAssistantBuildResult(bool buildSucceeded, bool historyPersisted, BuildAssistantError error, string message, BuildAssistantHistoryEntry entry)
        {
            BuildSucceeded = buildSucceeded;
            HistoryPersisted = historyPersisted;
            Error = error;
            Message = message ?? string.Empty;
            Entry = entry;
        }

        /// <summary>Unityがプレイヤーのビルド成功を報告したかどうかを取得します。</summary>
        public bool BuildSucceeded { get; }

        /// <summary>終了結果が件数制限付きの履歴へ保存されたかどうかを取得します。</summary>
        public bool HistoryPersisted { get; }

        /// <summary>主となる定義済みエラーを取得します。プレイヤーのビルドが成功していても、解析や履歴保存のエラーを示す場合があります。</summary>
        public BuildAssistantError Error { get; }

        /// <summary>Unityオブジェクトに依存せず、エディター画面へ表示できる診断文を取得します。</summary>
        public string Message { get; }

        /// <summary>ビルド呼び出しを開始した場合、その終了記録をUnityオブジェクトに依存しない形で取得します。</summary>
        public BuildAssistantHistoryEntry Entry { get; }
    }
}
