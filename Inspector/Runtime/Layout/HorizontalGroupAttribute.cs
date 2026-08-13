using System;

namespace Inspector
{
    /// <summary>
    /// 横並びのまとまりに入れる。関係の強い少数のフィールドを 1 行に詰めたいときに使う。
    /// <code>
    /// [HorizontalGroup("範囲")] [SerializeField] private float _min;
    /// [HorizontalGroup("範囲")] [SerializeField] private float _max;
    /// </code>
    /// <para>
    /// 幅は等分される。1 行に詰め込むほどラベルが痩せるので、
    /// <see cref="LabelWidthAttribute"/> や <see cref="HideLabelAttribute"/> と併せて使うことが多い。
    /// 入れ子のグループは横並びの中には置けない（横方向の幅計算が破綻するため、
    /// 子グループは通常の縦積みとして描かれる）。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HorizontalGroupAttribute : GroupAttribute
    {
        /// <summary>指定したパスのメンバーを横一列に並べる。</summary>
        /// <param name="path"><c>/</c> 区切りで入れ子にできるグループパス。</param>
        public HorizontalGroupAttribute(string path) : base(path) { }

        /// <summary>見出しを出すか。既定は出さない（1 行に詰めるのが目的のため）。</summary>
        public bool ShowLabel { get; set; }

        /// <inheritdoc/>
        public override GroupKind Kind => GroupKind.Horizontal;
    }
}
