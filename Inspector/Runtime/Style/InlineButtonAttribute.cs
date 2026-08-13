using System;

namespace Inspector
{
    /// <summary>
    /// フィールドの右隣に小さなボタンを置く。そのフィールドを埋めるための操作に使う。
    /// <code>
    /// [InlineButton(nameof(GenerateId), "生成")]
    /// [SerializeField] private string _id;
    ///
    /// private void GenerateId() =&gt; _id = Guid.NewGuid().ToString("N");
    /// </code>
    /// <para>
    /// 独立した <see cref="ButtonAttribute"/> と違い、対象のフィールドの真横に出る。
    /// 「この値のための操作」であることが一目で分かる。
    /// 複数付ければ左から順に並ぶ。
    /// </para>
    /// <para>
    /// 呼ぶメソッドは引数なしであること。押した後に値が書き換わっていても拾えるよう、
    /// 呼び出しの前後で対象を <c>Undo</c> に記録し、変更を保存する。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class InlineButtonAttribute : StyleAttribute
    {
        /// <param name="method">押したときに呼ぶ引数なしメソッドの名前。</param>
        /// <param name="label">ボタンの文言。省略するとメソッド名から作る。</param>
        public InlineButtonAttribute(string method, string label = null)
        {
            Method = method;
            Label = label;
        }

        public string Method { get; }

        public string Label { get; }

        /// <summary>ボタンの幅。</summary>
        public float Width { get; set; } = 60f;
    }
}
