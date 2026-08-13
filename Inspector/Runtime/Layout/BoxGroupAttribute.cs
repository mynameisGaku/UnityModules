using System;

namespace Inspector
{
    /// <summary>
    /// 見出し付きの枠で囲んだまとまりに入れる。折りたたまず、常に開いたまま見せたいときに使う。
    /// <code>
    /// [BoxGroup("体力")] [SerializeField] private int _maxHp;
    /// [BoxGroup("体力")] [SerializeField] private float _regenPerSecond;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class BoxGroupAttribute : GroupAttribute
    {
        /// <summary>指定したパスのメンバーを見出し付きの枠で囲む。</summary>
        /// <param name="path"><c>/</c> 区切りで入れ子にできるグループパス。</param>
        public BoxGroupAttribute(string path) : base(path) { }

        /// <inheritdoc/>
        public override GroupKind Kind => GroupKind.Box;
    }
}
