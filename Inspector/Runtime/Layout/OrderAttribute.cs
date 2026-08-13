using System;

namespace Inspector
{
    /// <summary>
    /// Inspector 上の並び順を宣言順から変える。小さいほど上。
    /// <code>
    /// [Order(-10)] [SerializeField] private string _name;   // 一番上に出したい
    /// [Order(100)] [Button] private void Reset() { }        // 一番下に置きたい
    /// </code>
    /// <para>
    /// 既定は 0 で、<b>同じ値のメンバーは宣言順を保つ</b>。
    /// フィールドの宣言順はシリアライズの都合や継承関係で決まってしまうことがあり、
    /// 「保存形式は変えずに見た目だけ直したい」ときにここで吸収する。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OrderAttribute : InspectorAttribute
    {
        /// <summary>対象メンバーの描画順を指定する。</summary>
        /// <param name="order">描画順。小さい値ほど先に描く。</param>
        public OrderAttribute(int order) => Order = order;

        /// <summary>対象メンバーの描画順。</summary>
        public int Order { get; }
    }
}
