using System;

namespace Inspector
{
    /// <summary>
    /// 数値をバーとして描く。割合が一目で分かる値に使う。
    /// <code>
    /// [ProgressBar("体力", 100f, Color = InspectorColor.Green)]
    /// [SerializeField] private float _hp = 100f;
    ///
    /// [ProgressBar("体力", MaxMember = nameof(_maxHp))]
    /// [SerializeField] private float _hp2;
    /// </code>
    /// <para>
    /// 上限は定数（<see cref="Max"/>）でも、他のメンバーの値（<see cref="MaxMember"/>）でも指定できる。
    /// 実行中の値を眺めるのが主目的なので、バーの上をドラッグしての編集はできない。
    /// 編集したいなら <c>[Range]</c> を使う。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ProgressBarAttribute : FieldDrawerAttribute
    {
        /// <summary>表示名と上限値を指定して進捗バーを作る。</summary>
        /// <param name="label">バーの左に出すラベル。省略するとフィールド名から作る。</param>
        /// <param name="max">バーが満たされた状態とみなす上限値。</param>
        public ProgressBarAttribute(string label = null, float max = 1f)
        {
            Label = label;
            Max = max;
        }

        /// <summary>バーの左に出すラベル。省略するとフィールド名から作る。</summary>
        public string Label { get; }

        /// <summary>上限値。<see cref="MaxMember"/> を指定した場合はそちらが優先される。</summary>
        public float Max { get; }

        /// <summary>上限値を返すメンバーの名前。可変の最大値（最大体力など）に使う。</summary>
        public string MaxMember { get; set; }

        /// <summary>バーを塗る色。</summary>
        public InspectorColor Color { get; set; } = InspectorColor.Cyan;

        /// <summary>バーの中に「現在値 / 上限」を書くか。</summary>
        public bool ShowValue { get; set; } = true;

        /// <summary>バーの高さ。</summary>
        public float Height { get; set; } = 18f;
    }
}
