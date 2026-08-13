using System;

namespace Inspector
{
    /// <summary>
    /// 未設定なら Inspector 上で赤く知らせる。
    /// <code>
    /// [Required("弾のプレハブを入れないと発射できない")]
    /// [SerializeField] private GameObject _projectile;
    /// </code>
    /// <para>
    /// 対象は参照型（<c>Object</c> 参照、文字列、配列・リスト）。
    /// 参照は <c>null</c> と「破棄済み」を、文字列は空白のみを、
    /// 配列・リストは要素 0 を未設定とみなす。
    /// </para>
    /// <para>
    /// 実行時に <c>NullReferenceException</c> で気付くのではなく、
    /// プレハブを見た時点で気付けるようにするための属性。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RequiredAttribute : ValidatorAttribute
    {
        /// <param name="message">出す文言。省略するとフィールド名から作る。</param>
        public RequiredAttribute(string message = null) => Message = message;

        public string Message { get; }
    }
}
