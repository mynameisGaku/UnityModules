using System;

namespace Inspector
{
    /// <summary>
    /// フィールドの近くに注意書きを出す。
    /// <code>
    /// [InfoBox("0 にすると当たり判定が無効になる", InfoBoxKind.Warning)]
    /// [SerializeField] private float _radius;
    ///
    /// [InfoBox("この設定は上書きが有効なときだけ効く", VisibleIf = nameof(_useOverride))]
    /// [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// 設定の意味をコード側のコメントではなく Inspector 上に出しておくと、
    /// 触るのがプログラマ以外でも事故が減る。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InfoBoxAttribute : DecoratorAttribute
    {
        public InfoBoxAttribute(string text, InfoBoxKind kind = InfoBoxKind.Info)
        {
            Text = text;
            Kind = kind;
        }

        public string Text { get; }

        public InfoBoxKind Kind { get; }

        /// <summary>
        /// この注意書きを出す条件となるメンバー名。先頭に <c>!</c> を付けると反転する。
        /// 未指定なら常に出る。
        /// </summary>
        public string VisibleIf { get; set; }

        /// <summary>フィールドの前に出すか後に出すか。既定は前。</summary>
        public DecoratorPosition Placement { get; set; } = DecoratorPosition.Before;

        /// <inheritdoc/>
        public override DecoratorPosition Position => Placement;
    }

    /// <summary>注意書きの重み。<c>UnityEditor.MessageType</c> に対応する。</summary>
    public enum InfoBoxKind
    {
        None,
        Info,
        Warning,
        Error,
    }
}
