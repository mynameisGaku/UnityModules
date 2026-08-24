using System;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// Unity の asmdef JSON から監査対象項目だけを受け取ります。
    /// </summary>
    [Serializable]
    internal sealed class AssemblyDefinitionJson
    {
        /// <summary>assembly 名です。</summary>
        public string name = string.Empty;

        /// <summary>参照する assembly 名または GUID です。</summary>
        public string[] references = Array.Empty<string>();

        /// <summary>含める platform 名です。</summary>
        public string[] includePlatforms = Array.Empty<string>();

        /// <summary>除外する platform 名です。</summary>
        public string[] excludePlatforms = Array.Empty<string>();
    }
}
