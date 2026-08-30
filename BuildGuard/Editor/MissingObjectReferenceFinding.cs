// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// 参照先が欠落した直列化オブジェクトのプロパティを1件表します。
    /// </summary>
    internal readonly struct MissingObjectReferenceFinding
    {
        /// <summary>並び順を一定にできる欠落参照の場所を1件作成します。</summary>
        internal MissingObjectReferenceFinding(
            string hierarchyPath,
            string componentTypeName,
            int componentIndex,
            string propertyPath)
        {
            HierarchyPath = hierarchyPath;
            ComponentTypeName = componentTypeName;
            ComponentIndex = componentIndex;
            PropertyPath = propertyPath;
        }

        /// <summary>同じ親の中での順番を含む階層パスを取得します。</summary>
        internal string HierarchyPath { get; }

        /// <summary>コンポーネントの完全な型名を取得します。</summary>
        internal string ComponentTypeName { get; }

        /// <summary>ゲームオブジェクト上でのコンポーネントの順番を取得します。</summary>
        internal int ComponentIndex { get; }

        /// <summary>直列化プロパティのパスを取得します。</summary>
        internal string PropertyPath { get; }
    }
}
