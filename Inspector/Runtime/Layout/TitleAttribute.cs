using System;

namespace Inspector
{
    /// <summary>
    /// フィールドの上に見出しを描く。Unity の <c>[Header]</c> より情報量が多い版。
    /// <code>
    /// [Title("移動", "接地しているときだけ効く")]
    /// [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// <c>[Header]</c> でも足りる場面ではそちらを使えばよい（このモジュールは
    /// <c>[Header]</c> や <c>[Space]</c> を素通しするので併用できる）。
    /// こちらは副題と下線が要るとき、あるいは <see cref="ShowNonSerializedAttribute"/> のように
    /// <c>[Header]</c> が効かないメンバーに見出しを付けたいときに使う。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TitleAttribute : DecoratorAttribute
    {
        /// <summary>対象メンバーの上に見出しと任意の副題を表示する。</summary>
        /// <param name="title">表示する見出し。</param>
        /// <param name="subtitle">見出しの下に添える説明。</param>
        public TitleAttribute(string title, string subtitle = null)
        {
            Title = title;
            Subtitle = subtitle;
        }

        /// <summary>表示する見出し。</summary>
        public string Title { get; }

        /// <summary>見出しの下に小さく添える説明。</summary>
        public string Subtitle { get; }

        /// <summary>見出しの下に線を引くか。</summary>
        public bool Line { get; set; } = true;

        /// <summary>太字にするか。</summary>
        public bool Bold { get; set; } = true;

        /// <inheritdoc/>
        public override DecoratorPosition Position => DecoratorPosition.Before;
    }
}
