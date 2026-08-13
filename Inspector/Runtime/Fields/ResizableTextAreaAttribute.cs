using System;

namespace Inspector
{
    /// <summary>
    /// <c>string</c> を、中身の行数に合わせて伸びる複数行の入力欄にする。
    /// <code>
    /// [ResizableTextArea]
    /// [SerializeField] private string _description;
    /// </code>
    /// <para>
    /// Unity の <c>[TextArea]</c> は行数を先に決める必要があり、
    /// 短いテキストでも欄が空いたまま、長いテキストは中でスクロールになる。
    /// こちらは実際の行数で高さが決まる。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ResizableTextAreaAttribute : FieldDrawerAttribute
    {
        /// <summary>最小の行数。</summary>
        public int MinLines { get; set; } = 3;

        /// <summary>最大の行数。これを超えると欄の中でスクロールする。</summary>
        public int MaxLines { get; set; } = 20;
    }
}
