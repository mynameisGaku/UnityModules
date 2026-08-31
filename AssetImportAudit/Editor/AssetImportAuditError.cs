namespace AssetImportAudit.Editor
{
    /// <summary>監査処理を完了できなかった理由を表します。</summary>
    public enum AssetImportAuditError
    {
        /// <summary>問題なく完了しました。</summary>
        None = 0,

        /// <summary>対象フォルダーが不正です。</summary>
        InvalidFolder = 1,

        /// <summary>期待する設定値が不正です。</summary>
        InvalidSettings = 2,

        /// <summary>差分確認後に取込設定が変わりました。</summary>
        StalePlan = 3,

        /// <summary>反映対象がありません。</summary>
        NoChanges = 4,

        /// <summary>取込設定を読み込めません。</summary>
        ImporterUnavailable = 5,

        /// <summary>反映開始後に処理を完了できませんでした。</summary>
        ApplyFailed = 6
    }
}
