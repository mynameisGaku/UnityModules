using System;

namespace Inspector
{
    /// <summary>
    /// 条件が成立している間だけ編集できるようにする。不成立なら灰色で表示だけ残る。
    /// <code>
    /// [EnableIf(nameof(_useOverride))]
    /// [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// 消す（<see cref="ShowIfAttribute"/>）のではなく灰色にするのは、
    /// 「今は効いていないが、そこに設定がある」ことを見せたいときに向く。
    /// フィールドが行方不明になったと勘違いされにくい。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class EnableIfAttribute : ConditionAttribute
    {
        /// <inheritdoc cref="ConditionAttribute(string, object[])"/>
        public EnableIfAttribute(string member, params object[] values) : base(member, values) { }

        /// <inheritdoc cref="ConditionAttribute(ConditionOperator, string[])"/>
        public EnableIfAttribute(ConditionOperator conditionOperator, params string[] members)
            : base(conditionOperator, members) { }

        /// <inheritdoc/>
        public override ConditionEffect Effect => ConditionEffect.Enable;
    }

    /// <summary>
    /// 条件が成立している間だけ灰色にする。<see cref="EnableIfAttribute"/> の裏返し。
    /// <code>
    /// [DisableIf(nameof(IsRunning))]
    /// [SerializeField] private int _threadCount;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class DisableIfAttribute : ConditionAttribute
    {
        /// <inheritdoc cref="ConditionAttribute(string, object[])"/>
        public DisableIfAttribute(string member, params object[] values) : base(member, values) { }

        /// <inheritdoc cref="ConditionAttribute(ConditionOperator, string[])"/>
        public DisableIfAttribute(ConditionOperator conditionOperator, params string[] members)
            : base(conditionOperator, members) { }

        /// <inheritdoc/>
        public override ConditionEffect Effect => ConditionEffect.Disable;
    }
}
