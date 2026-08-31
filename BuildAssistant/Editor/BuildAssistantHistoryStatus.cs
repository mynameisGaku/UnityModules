namespace BuildAssistant.Editor
{
    /// <summary>ビルド実行アシスタントの1回の実行について、記録された終了状態を表します。</summary>
    public enum BuildAssistantHistoryStatus
    {
        /// <summary>Unityのビルドが正常に完了しました。</summary>
        Succeeded = 0,
        /// <summary>Unityのビルドが失敗または中止されたか、読み取れる報告を取得できませんでした。</summary>
        Failed = 1,
        /// <summary>再読込または処理中断の後に、永続化された実行中記録が残っていました。</summary>
        Interrupted = 2
    }
}
