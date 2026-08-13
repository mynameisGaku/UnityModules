using System;

namespace Inspector
{
    /// <summary>
    /// 条件が成立している間だけ Inspector に出す。
    /// <code>
    /// [SerializeField] private bool _useOverride;
    ///
    /// [ShowIf(nameof(_useOverride))]
    /// [SerializeField] private float _speed;
    /// </code>
    /// <para>
    /// 複数付けた場合は全部成立したときだけ出る。
    /// 「出す代わりに灰色にする」なら <see cref="EnableIfAttribute"/> を使う。
    /// 条件で消えたフィールドの値は保持されたままで、初期化などは行わない。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ShowIfAttribute : ConditionAttribute
    {
        /// <inheritdoc cref="ConditionAttribute(string, object[])"/>
        public ShowIfAttribute(string member, params object[] values) : base(member, values) { }

        /// <inheritdoc cref="ConditionAttribute(ConditionOperator, string[])"/>
        public ShowIfAttribute(ConditionOperator conditionOperator, params string[] members)
            : base(conditionOperator, members) { }

        /// <inheritdoc/>
        public override ConditionEffect Effect => ConditionEffect.Show;
    }

    /// <summary>
    /// 条件が成立している間だけ Inspector から隠す。<see cref="ShowIfAttribute"/> の裏返し。
    /// <code>
    /// [HideIf(nameof(_mode), Mode.Simple)]
    /// [SerializeField] private AnimationCurve _falloff;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HideIfAttribute : ConditionAttribute
    {
        /// <inheritdoc cref="ConditionAttribute(string, object[])"/>
        public HideIfAttribute(string member, params object[] values) : base(member, values) { }

        /// <inheritdoc cref="ConditionAttribute(ConditionOperator, string[])"/>
        public HideIfAttribute(ConditionOperator conditionOperator, params string[] members)
            : base(conditionOperator, members) { }

        /// <inheritdoc/>
        public override ConditionEffect Effect => ConditionEffect.Hide;
    }
}
