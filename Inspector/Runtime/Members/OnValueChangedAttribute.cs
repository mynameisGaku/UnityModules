using System;

namespace Inspector
{
    /// <summary>
    /// Inspector で値が変わった直後にメソッドを呼ぶ。
    /// <code>
    /// [OnValueChanged(nameof(ApplyRadius))]
    /// [SerializeField] private float _radius;
    ///
    /// private void ApplyRadius() =&gt; _collider.radius = _radius;
    /// </code>
    /// <para>
    /// 呼ぶメソッドは引数なし。<b>値が書き戻された後</b>に呼ばれるので、
    /// メソッドの中でフィールドを読めば新しい値が入っている。
    /// </para>
    /// <para>
    /// <c>OnValidate</c> との違いは、変わったフィールドが分かること。
    /// <c>OnValidate</c> は「どこかが変わった」しか分からず、
    /// 重い処理を全部やり直すか、自前で前回値を持つ羽目になる。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class OnValueChangedAttribute : InspectorAttribute
    {
        /// <summary>対象フィールドの値が変わった後に指定したメソッドを呼ぶ。</summary>
        /// <param name="method">変更後に呼ぶ引数なしメソッドの名前。</param>
        public OnValueChangedAttribute(string method) => Method = method;

        /// <summary>変更後に呼ぶ引数なしメソッドの名前。</summary>
        public string Method { get; }
    }
}
