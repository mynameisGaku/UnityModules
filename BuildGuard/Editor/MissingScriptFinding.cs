// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// 欠落したMonoBehaviourの枠を含むゲームオブジェクトを1件表します。
    /// </summary>
    internal readonly struct MissingScriptFinding
    {
        /// <summary>
        /// 並び順を一定にできる欠落スクリプトの検出結果を1件作成します。
        /// </summary>
        /// <param name="hierarchyPath">同じ親の中での順番を含む階層パスです。</param>
        /// <param name="missingScriptCount">欠落したMonoBehaviourの枠数です。</param>
        internal MissingScriptFinding(string hierarchyPath, int missingScriptCount)
        {
            HierarchyPath = hierarchyPath;
            MissingScriptCount = missingScriptCount;
        }

        /// <summary>
        /// 同じ親の中での順番を含む階層パスを取得します。
        /// </summary>
        internal string HierarchyPath { get; }

        /// <summary>
        /// 欠落したMonoBehaviourの枠数を取得します。
        /// </summary>
        internal int MissingScriptCount { get; }
    }
}
