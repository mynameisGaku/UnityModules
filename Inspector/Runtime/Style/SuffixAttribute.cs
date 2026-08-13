using System;

namespace Inspector
{
    /// <summary>
    /// 値欄の右端に単位などの短い文字を添える。
    /// <code>
    /// [Suffix("m/s")]  [SerializeField] private float _speed;
    /// [Suffix("秒")]   [SerializeField] private float _cooldown;
    /// </code>
    /// <para>
    /// ラベル側に単位を書く（<see cref="LabelTextAttribute"/>）方法もあるが、
    /// 単位は値に付くものなので値側に置いたほうが読み違えにくい。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class SuffixAttribute : StyleAttribute
    {
        public SuffixAttribute(string text) => Text = text;

        public string Text { get; }

        /// <summary>単位表示に使う幅。長い単位を入れるときに広げる。</summary>
        public float Width { get; set; } = 34f;
    }
}
