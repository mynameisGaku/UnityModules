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
        public MinValueAttribute(float value) => Value = value;

        public MinValueAttribute(int value) => Value = value;

        public float Value { get; }
    }

    /// <summary>上限を上回った値を書き戻して丸める。<see cref="MinValueAttribute"/> の裏返し。</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MaxValueAttribute : ValidatorAttribute
    {
        public MaxValueAttribute(float value) => Value = value;

        public MaxValueAttribute(int value) => Value = value;

        public float Value { get; }
    }
}
