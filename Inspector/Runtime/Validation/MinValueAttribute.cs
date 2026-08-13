using System;

namespace Inspector
{
    /// <summary>
    /// 下限を下回った値を書き戻して丸める。
    /// <code>
    /// [MinValue(0f)]
    /// [SerializeField] private float _radius;
    /// </code>
    /// <para>
    /// <c>[Range]</c> と違いスライダーにはならない。上限が決まらない値
    /// （体力、所持金、秒数）に「負にはならない」とだけ言いたいときに使う。
    /// <c>int</c> / <c>float</c> のほか <c>Vector2</c>・<c>Vector3</c>・<c>Vector4</c> と
    /// その整数版にも効き、成分ごとに丸める。
    /// </para>
    /// <para>
    /// 警告を出すのではなく<b>実際に値を書き換える</b>のは、
    /// 不正な値がそのまま保存されてしまう状態を残さないため。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinValueAttribute : ValidatorAttribute
    {
        /// <summary>対象値の浮動小数点数としての下限を指定する。</summary>
        /// <param name="value">対象値へ適用する下限。</param>
        public MinValueAttribute(float value) => Value = value;

        /// <summary>対象値の整数としての下限を指定する。</summary>
        /// <param name="value">対象値へ適用する下限。</param>
        public MinValueAttribute(int value) => Value = value;

        /// <summary>対象値へ適用する下限。</summary>
        public float Value { get; }
    }

    /// <summary>上限を上回った値を書き戻して丸める。<see cref="MinValueAttribute"/> の裏返し。</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MaxValueAttribute : ValidatorAttribute
    {
        /// <summary>対象値の浮動小数点数としての上限を指定する。</summary>
        /// <param name="value">対象値へ適用する上限。</param>
        public MaxValueAttribute(float value) => Value = value;

        /// <summary>対象値の整数としての上限を指定する。</summary>
        /// <param name="value">対象値へ適用する上限。</param>
        public MaxValueAttribute(int value) => Value = value;

        /// <summary>対象値へ適用する上限。</summary>
        public float Value { get; }
    }
}
