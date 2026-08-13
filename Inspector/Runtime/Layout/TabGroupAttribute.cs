using System;

namespace Inspector
{
    /// <summary>
    /// タブで切り替わるまとまりに入れる。同時に 1 枚しか見えないので、排他的な設定に向く。
    /// <code>
    /// [TabGroup("設定", "見た目")] [SerializeField] private Color _tint;
    /// [TabGroup("設定", "挙動")]   [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// タブの並びは、そのタブに属する最初のメンバーが現れた順になる。
    /// 選択中のタブは型ごとに記憶される。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TabGroupAttribute : GroupAttribute
    {
        /// <summary>指定したタブ列の 1 枚へ対象メンバーを入れる。</summary>
        /// <param name="group">タブ列そのものの名前。<c>/</c> 区切りで入れ子にできる。</param>
        /// <param name="tab">このメンバーが乗るタブの名前。</param>
        public TabGroupAttribute(string group, string tab) : base(Combine(group, tab))
        {
            Group = group;
            Tab = tab;
        }

        /// <summary>タブ列の名前。</summary>
        public string Group { get; }

        /// <summary>タブの名前。</summary>
        public string Tab { get; }

        /// <inheritdoc/>
        public override GroupKind Kind => GroupKind.TabPage;

        private static string Combine(string group, string tab)
        {
            if (string.IsNullOrEmpty(group)) return tab;
            if (string.IsNullOrEmpty(tab)) return group;
            return group + "/" + tab;
        }
    }
}
