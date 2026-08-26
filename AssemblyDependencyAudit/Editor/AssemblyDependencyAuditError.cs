namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 監査結果を返せなかった理由を表します。
    /// </summary>
    internal enum AssemblyDependencyAuditError
    {
        /// <summary>失敗はありません。</summary>
        None,

        /// <summary>assembly asset一覧または内容を取得できませんでした。</summary>
        SourceUnavailable,

        /// <summary>assembly asset pathが承認済みroot配下のreparse pointを通ります。</summary>
        UnsafeAssemblyAssetPath,

        /// <summary>asmdef 数が安全上限を超えました。</summary>
        TooManyAssemblyDefinitions,

        /// <summary>asmref 数が安全上限を超えました。</summary>
        TooManyAssemblyReferences,

        /// <summary>assembly asset探索中のdirectoryまたはfile entry数が安全上限を超えました。</summary>
        AssemblyAssetTraversalLimitExceeded,

        /// <summary>assembly assetとmetaの読取byte総数が安全上限を超えました。</summary>
        AssemblyAssetTotalBytesExceeded,

        /// <summary>1 件の asmdef、asmref、またはmetaが安全なsize上限を超えました。</summary>
        SourceTooLarge,

        /// <summary>1 件の参照数が安全上限を超えました。</summary>
        TooManyReferencesPerAssembly,

        /// <summary>全 asmdef の参照総数が安全上限を超えました。</summary>
        TooManyReferences,

        /// <summary>検出問題数が安全上限を超えました。</summary>
        TooManyIssues
    }
}
