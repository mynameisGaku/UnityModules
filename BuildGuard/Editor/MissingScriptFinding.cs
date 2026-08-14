// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Scene階層内でMissing MonoBehaviourを持つGameObjectと件数を表します。
    /// </summary>
    internal readonly struct MissingScriptFinding
    {
        /// <summary>
        /// 階層pathと件数を指定して検出結果を作成します。
        /// </summary>
        /// <param name="hierarchyPath">兄弟indexを含む決定論的な階層path。</param>
        /// <param name="missingScriptCount">GameObject上のMissing MonoBehaviour件数。</param>
        internal MissingScriptFinding(string hierarchyPath, int missingScriptCount)
        {
            HierarchyPath = hierarchyPath;
            MissingScriptCount = missingScriptCount;
        }

        /// <summary>
        /// 兄弟indexを含む決定論的な階層pathを取得します。
        /// </summary>
        internal string HierarchyPath { get; }

        /// <summary>
        /// GameObject上のMissing MonoBehaviour件数を取得します。
        /// </summary>
        internal int MissingScriptCount { get; }
    }
}
