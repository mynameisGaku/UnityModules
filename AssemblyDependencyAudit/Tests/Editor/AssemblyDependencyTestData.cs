using System;
using UnityEngine;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// 壊れた asmdef asset を project へ置かず、memory 上だけで監査入力を作ります。
    /// </summary>
    internal static class AssemblyDependencyTestData
    {
        /// <summary>
        /// 指定した宣言を JSON 化した source を作ります。
        /// </summary>
        internal static Editor.AssemblyDefinitionSource CreateSource(
            string assetPath,
            string name,
            string guid,
            string[] references = null,
            string[] includePlatforms = null,
            string[] excludePlatforms = null)
        {
            var definition = new Editor.AssemblyDefinitionJson
            {
                name = name,
                references = references ?? Array.Empty<string>(),
                includePlatforms = includePlatforms ?? Array.Empty<string>(),
                excludePlatforms = excludePlatforms ?? Array.Empty<string>()
            };

            return new Editor.AssemblyDefinitionSource(assetPath, guid, JsonUtility.ToJson(definition));
        }
    }
}
