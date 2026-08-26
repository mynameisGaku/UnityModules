namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 1 件の asmref を解析するための不変な入力を保持します。
    /// </summary>
    internal sealed class AssemblyReferenceSource
    {
        /// <summary>
        /// Unity asset path、asset GUID、JSON 本文を保持します。
        /// </summary>
        internal AssemblyReferenceSource(string assetPath, string guid, string json)
        {
            AssetPath = string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
            Guid = guid ?? string.Empty;
            Json = json ?? string.Empty;
        }

        /// <summary>Unity project から見た asmref の path です。</summary>
        internal string AssetPath { get; }

        /// <summary>asmref asset 自身の GUID です。</summary>
        internal string Guid { get; }

        /// <summary>asmref の JSON 本文です。</summary>
        internal string Json { get; }
    }
}
