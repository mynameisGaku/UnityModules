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
        public OnValueChangedAttribute(string method) => Method = method;

        public string Method { get; }
    }
}
