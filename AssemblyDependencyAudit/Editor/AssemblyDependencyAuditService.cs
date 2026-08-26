namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 現在の Unity project を 1 回の完全な asmdef graph・asmref target監査として実行します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditService
    {
        /// <summary>asmdef source と compiler 解決を提供します。</summary>
        private readonly IAssemblyDependencySourceAdapter _sourceAdapter;

        /// <summary>asmref source を読み取り専用で提供します。</summary>
        private readonly IAssemblyReferenceSourceAdapter _assemblyReferenceSourceAdapter;

        /// <summary>
        /// AssetDatabase と CompilationPipeline を使う service を作ります。
        /// </summary>
        internal AssemblyDependencyAuditService()
        {
            var adapter = new UnityAssemblyDependencySourceAdapter();
            _sourceAdapter = adapter;
            _assemblyReferenceSourceAdapter = adapter;
        }

        /// <summary>
        /// test または別環境から source adapter を注入します。
        /// </summary>
        internal AssemblyDependencyAuditService(IAssemblyDependencySourceAdapter sourceAdapter)
            : this(sourceAdapter, sourceAdapter as IAssemblyReferenceSourceAdapter)
        {
        }

        /// <summary>
        /// asmdef と asmref の source adapter を個別に注入します。
        /// </summary>
        internal AssemblyDependencyAuditService(
            IAssemblyDependencySourceAdapter sourceAdapter,
            IAssemblyReferenceSourceAdapter assemblyReferenceSourceAdapter)
        {
            _sourceAdapter = sourceAdapter;
            _assemblyReferenceSourceAdapter = assemblyReferenceSourceAdapter;
        }

        /// <summary>
        /// 全 asmdef と asmref を監査します。取得または上限確認に失敗した場合は部分結果を返しません。
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

            if (!_sourceAdapter.TryReadAll(out var sources, out var sourceAuditError, out var sourceError))
            {
                error = sourceAuditError == AssemblyDependencyAuditError.None
                    ? AssemblyDependencyAuditError.SourceUnavailable
                    : sourceAuditError;
                errorMessage = string.IsNullOrEmpty(sourceError) ? "asmdef source を取得できませんでした。" : sourceError;
                return false;
            }

            if (!AssemblyDependencyAnalyzer.TryAnalyze(
                    sources,
                    _sourceAdapter,
                    out var assemblyResult,
                    out error,
                    out errorMessage))
            {
                return false;
            }

            if (_assemblyReferenceSourceAdapter == null)
            {
                return AssemblyOwnershipAnalyzer.TryAnalyze(
                    assemblyResult,
                    out result,
                    out error,
                    out errorMessage);
            }

            if (!_assemblyReferenceSourceAdapter.TryReadAllAssemblyReferences(
                    out var assemblyReferenceSources,
                    out var assemblyReferenceSourceAuditError,
                    out var assemblyReferenceSourceError))
            {
                error = assemblyReferenceSourceAuditError == AssemblyDependencyAuditError.None
                    ? AssemblyDependencyAuditError.SourceUnavailable
                    : assemblyReferenceSourceAuditError;
                errorMessage = string.IsNullOrEmpty(assemblyReferenceSourceError)
                    ? "asmref source を取得できませんでした。"
                    : assemblyReferenceSourceError;
                return false;
            }

            if (!AssemblyReferenceAnalyzer.TryAnalyze(
                    assemblyReferenceSources,
                    assemblyResult,
                    out var assemblyReferenceResult,
                    out error,
                    out errorMessage))
            {
                return false;
            }

            return AssemblyOwnershipAnalyzer.TryAnalyze(
                assemblyReferenceResult,
                out result,
                out error,
                out errorMessage);
        }
    }
}
