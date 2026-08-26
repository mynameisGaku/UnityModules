namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 監査で検出した 1 件の問題を保持します。
    /// </summary>
    internal sealed class AssemblyDependencyIssue
    {
        /// <summary>
        /// 問題種別と発生元、関連先、参照表記、説明を保持します。
        /// </summary>
        internal AssemblyDependencyIssue(
            AssemblyDependencyIssueKind kind,
            string assetPath,
            string relatedAssetPath,
            string reference,
            string message)
        {
            Kind = kind;
            AssetPath = assetPath ?? string.Empty;
            RelatedAssetPath = relatedAssetPath ?? string.Empty;
            Reference = reference ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>検出した問題の種類です。</summary>
        internal AssemblyDependencyIssueKind Kind { get; }

        /// <summary>問題がある asmdef または asmref の asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>参照先または競合ownerなど関連するassembly asset pathです。</summary>
        internal string RelatedAssetPath { get; }

        /// <summary>問題に関係する元の参照表記です。</summary>
        internal string Reference { get; }

        /// <summary>UI に表示できる日本語の説明です。</summary>
        internal string Message { get; }
    }
}
