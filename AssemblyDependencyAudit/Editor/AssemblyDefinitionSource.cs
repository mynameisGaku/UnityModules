namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 1 件の asmdef を解析するための不変な入力を保持します。
    /// </summary>
    internal sealed class AssemblyDefinitionSource
    {
        /// <summary>
        /// パス、GUID、JSON 本文を保持します。
        /// </summary>
        internal AssemblyDefinitionSource(string assetPath, string guid, string json)
        {
            AssetPath = string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
            Guid = guid ?? string.Empty;
            Json = json ?? string.Empty;
        }

        /// <summary>Unity project から見た asmdef のパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>asmdef asset の GUID です。</summary>
        internal string Guid { get; }

        /// <summary>asmdef の JSON 本文です。</summary>
        internal string Json { get; }
    }
}
