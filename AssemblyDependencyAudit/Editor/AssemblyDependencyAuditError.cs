namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 監査結果を返せなかった理由を表します。
    /// </summary>
    internal enum AssemblyDependencyAuditError
    {
        /// <summary>失敗はありません。</summary>
        None,

        /// <summary>asmdef 一覧または内容を取得できませんでした。</summary>
        SourceUnavailable,

        /// <summary>asmdef 数が安全上限を超えました。</summary>
        TooManyAssemblyDefinitions,

        /// <summary>1 件の asmdef が文字数上限を超えました。</summary>
        SourceTooLarge,

        /// <summary>1 件の参照数が安全上限を超えました。</summary>
        TooManyReferencesPerAssembly,

        /// <summary>全 asmdef の参照総数が安全上限を超えました。</summary>
        TooManyReferences,

        /// <summary>検出問題数が安全上限を超えました。</summary>
        TooManyIssues
    }
}
