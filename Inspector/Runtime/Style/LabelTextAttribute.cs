using System;

namespace Inspector
{
    /// <summary>
    /// ラベルの文言を差し替える。
    /// <code>
    /// [LabelText("移動速度 (m/s)")]
    /// [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// Unity は <c>_speed</c> を「Speed」と表示するが、日本語にしたい、単位を添えたい、
    /// 変数名を変えずに表示だけ直したい、という場面がある。
    /// フィールド名を変えると保存済みの値が失われる（<c>[FormerlySerializedAs]</c> が要る）ため、
    /// 表示だけの問題は表示だけで解決したほうが安全。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class LabelTextAttribute : StyleAttribute
    {
        /// <summary>対象メンバーのラベルを指定した文言に差し替える。</summary>
        /// <param name="text">ラベルに表示する文言。</param>
        public LabelTextAttribute(string text) => Text = text;

        /// <summary>ラベルに表示する文言。</summary>
        public string Text { get; }

        /// <summary>マウスを乗せたときに出す説明。</summary>
        public string Tooltip { get; set; }
    }

    /// <summary>ラベルを消して値だけを描く。<see cref="HorizontalGroupAttribute"/> の中で幅を稼ぐのに使う。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class HideLabelAttribute : StyleAttribute
    {
    }

    /// <summary>
    /// ラベル欄の幅をこのメンバーの間だけ変える。
    /// 長いラベルが省略されてしまうときや、逆に短くして値欄を広げたいときに使う。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class LabelWidthAttribute : StyleAttribute
    {
        /// <summary>対象メンバーを描く間だけラベル欄の幅を指定する。</summary>
        /// <param name="width">ラベル欄の幅。</param>
        public LabelWidthAttribute(float width) => Width = width;

        /// <summary>対象メンバーに使うラベル欄の幅。</summary>
        public float Width { get; }
    }
}
