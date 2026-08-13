using System;

namespace Inspector
{
    /// <summary>
    /// 折りたたみのまとまりに入れる。
    /// <code>
    /// [Foldout("移動")] [SerializeField] private float _speed;
    /// [Foldout("移動")] [SerializeField] private float _acceleration;
    /// </code>
    /// <para>
    /// 同じ名前を付けたメンバーが 1 つのまとまりになり、
    /// <b>最初のメンバーがあった位置</b>にまとめて描かれる。開閉状態は型ごとに記憶される。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FoldoutAttribute : GroupAttribute
    {
        /// <summary>指定したパスのメンバーを折りたたみ可能なまとまりに入れる。</summary>
        /// <param name="path"><c>/</c> 区切りで入れ子にできるグループパス。</param>
        public FoldoutAttribute(string path) : base(path) { }

        /// <inheritdoc/>
        public override GroupKind Kind => GroupKind.Foldout;
    }
}
