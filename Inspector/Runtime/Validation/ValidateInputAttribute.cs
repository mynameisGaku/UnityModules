using System;

namespace Inspector
{
    /// <summary>
    /// 自前の検査メソッドで値を確かめ、通らなければ Inspector 上に出す。
    /// <code>
    /// [ValidateInput(nameof(IsPowerOfTwo), "2 のべき乗にすること")]
    /// [SerializeField] private int _textureSize = 256;
    ///
    /// private bool IsPowerOfTwo(int value) =&gt; value &gt; 0 &amp;&amp; (value &amp; (value - 1)) == 0;
    /// </code>
    /// <para>
    /// 検査メソッドの形は次のいずれか。
    /// </para>
    /// <list type="bullet">
    /// <item><c>bool Method(T value)</c> — フィールドの値を受け取る形。</item>
    /// <item><c>bool Method(T value, out string message)</c> — 文言を状況で変えたい形。</item>
    /// <item><c>bool Method()</c> — 複数フィールドの整合を見るなど、値を使わない形。</item>
    /// </list>
    /// <para>
    /// <see cref="RequiredAttribute"/> で足りない条件（範囲・組み合わせ・命名規則）をここで書く。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class ValidateInputAttribute : ValidatorAttribute
    {
        /// <summary>指定したメソッドで対象メンバーの値を検査する。</summary>
        /// <param name="method">検査メソッドの名前。</param>
        /// <param name="message">通らなかったときの文言。メソッドが <c>out string</c> を返す場合はそちらが優先される。</param>
        public ValidateInputAttribute(string method, string message = null)
        {
            Method = method;
            Message = message;
        }

        /// <summary>値を検査するメソッドの名前。</summary>
        public string Method { get; }

        /// <summary>検査に通らなかったときに表示する文言。</summary>
        public string Message { get; }

        /// <summary>通らなかったときの重み。既定は警告ではなくエラー。</summary>
        public InfoBoxKind Kind { get; set; } = InfoBoxKind.Error;
    }
}
