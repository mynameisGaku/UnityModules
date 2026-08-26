namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 1 件の asmref と解決した asmdef target を保持します。
    /// </summary>
    internal sealed class AssemblyReferenceTarget
    {
        /// <summary>
        /// 元 asset、reference 表記、指定方法、解決先を保持します。
        /// </summary>
        internal AssemblyReferenceTarget(
            string assetPath,
            string rawReference,
            AssemblyReferenceTargetKind kind,
            string resolvedTargetAssetPath)
        {
            AssetPath = assetPath ?? string.Empty;
            RawReference = rawReference ?? string.Empty;
            Kind = kind;
            ResolvedTargetAssetPath = resolvedTargetAssetPath ?? string.Empty;
        }

        /// <summary>元 asmref の Unity asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>JSON に記録されていた reference の元表記です。</summary>
        internal string RawReference { get; }

        /// <summary>assembly 名または GUID の指定方法です。</summary>
        internal AssemblyReferenceTargetKind Kind { get; }

        /// <summary>一意に解決できた asmdef の asset path です。</summary>
        internal string ResolvedTargetAssetPath { get; }

        /// <summary>target を一意に解決できたかを返します。</summary>
        internal bool IsResolved => !string.IsNullOrEmpty(ResolvedTargetAssetPath);
    }
}
