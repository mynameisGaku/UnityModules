using System;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// <c>[SerializeReference]</c> のフィールドやリストに「＋ ▸ 派生型」の選択ドロップダウンを付ける。
    /// <para>
    /// これが Unity でデータ駆動設計を現実的にする鍵になる。無ければ、条件や効果の派生ごとに
    /// ScriptableObject アセットを量産することになる。あれば、designer が 1 つのアセットの中で
    /// 型付きの要素を組み合わせて振る舞いを作れる：
    /// <code>
    /// [SerializeReference, SubclassSelector]
    /// private List&lt;ISpawnCondition&gt; _conditions = new();
    /// </code>
    /// </para>
    /// <para>
    /// 候補になるのは、フィールドの要素型に代入できる非抽象クラス。
    /// Unity が保存できるよう <c>[Serializable]</c> かつ引数なしのコンストラクタが必要。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
        /// <summary>ドロップダウンに「(なし)」を出す。既定は true。</summary>
        public bool AllowNull { get; set; } = true;

        /// <summary>
        /// ドロップダウンを名前空間で階層化する。候補が 1 画面に収まらなくなったら有効にするとよい。
        /// </summary>
        public bool GroupByNamespace { get; set; }
    }
}
