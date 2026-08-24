namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 現在の Unity project を 1 回の完全な asmdef 監査として実行します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditService
    {
        /// <summary>asmdef source と compiler 解決を提供します。</summary>
        private readonly IAssemblyDependencySourceAdapter _sourceAdapter;

        /// <summary>
        /// AssetDatabase と CompilationPipeline を使う service を作ります。
        /// </summary>
        internal AssemblyDependencyAuditService()
            : this(new UnityAssemblyDependencySourceAdapter())
        {
        }

        /// <summary>
        /// test または別環境から source adapter を注入します。
        /// </summary>
        internal AssemblyDependencyAuditService(IAssemblyDependencySourceAdapter sourceAdapter)
        {
            _sourceAdapter = sourceAdapter;
        }

        /// <summary>
        /// 全 asmdef を監査します。取得または上限確認に失敗した場合は部分結果を返しません。
        /// </summary>
        internal bool TryAudit(
            out AssemblyDependencyAuditResult result,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            result = null;
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;

            if (_sourceAdapter == null)
            {
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "asmdef source を取得できませんでした。";
                return false;
            }

            if (!_sourceAdapter.TryReadAll(out var sources, out var sourceError))
            {
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = string.IsNullOrEmpty(sourceError) ? "asmdef source を取得できませんでした。" : sourceError;
                return false;
            }

            return AssemblyDependencyAnalyzer.TryAnalyze(sources, _sourceAdapter, out result, out error, out errorMessage);
        }
    }
}
