using System;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// 両端を含む float の範囲。ダメージの振れ幅、スポーン間隔、ばらつき角度など、
    /// 今まで「順序を保つ責任が誰にも無い 2 つのフィールド」で書かれていたもの。
    /// </summary>
    [Serializable]
    public struct FloatRange : IEquatable<FloatRange>
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;

        /// <summary>下限と上限を指定して作る。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public FloatRange(float min, float max)
        {
            _min = min;
            _max = max;
        }

        /// <summary>下限。</summary>
        public float Min => _min;

        /// <summary>上限。</summary>
        public float Max => _max;

        /// <summary>幅。</summary>
        public float Length => _max - _min;

        /// <summary>中央値。</summary>
        public float Center => (_min + _max) * 0.5f;

        /// <summary>値が範囲に入っているか。両端を含む。</summary>
        /// <param name="value">判定する値。</param>
        public bool Contains(float value) => value >= _min && value <= _max;

        /// <summary>値を範囲内に丸める。</summary>
        /// <param name="value">丸める値。</param>
        public float Clamp(float value) => value < _min ? _min : value > _max ? _max : value;

        /// <summary>0〜1 を範囲上の値に写す。</summary>
        /// <param name="t">0〜1 の位置。</param>
        public float Lerp(float t) => _min + (_max - _min) * t;

        /// <summary>範囲上の値を 0〜1 に写す。幅が 0 なら 0 を返す。</summary>
        /// <param name="value">範囲上の値。</param>
        public float InverseLerp(float value)
        {
            var length = _max - _min;
            return Mathf.Approximately(length, 0f) ? 0f : Mathf.Clamp01((value - _min) / length);
        }

        /// <summary>範囲から一様に 1 つ引く。Unity の共有乱数を使う。</summary>
        public float Random() => UnityEngine.Random.Range(_min, _max);

        /// <summary>上下が逆に入力されていた場合に入れ替えたものを返す。</summary>
        public FloatRange Normalized() => _min <= _max ? this : new FloatRange(_max, _min);

        /// <summary>両端が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(FloatRange other) => _min.Equals(other._min) && _max.Equals(other._max);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is FloatRange other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((_min.GetHashCode() * 397) ^ _max.GetHashCode());

        /// <inheritdoc/>
        public override string ToString() => $"[{_min}, {_max}]";

        /// <summary>等値比較。</summary>
        public static bool operator ==(FloatRange a, FloatRange b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(FloatRange a, FloatRange b) => !a.Equals(b);
    }

    /// <summary>両端を含む int の範囲。スタック数、ウェーブ数、ダイス。</summary>
    [Serializable]
    public struct IntRange : IEquatable<IntRange>
    {
        [SerializeField] private int _min;
        [SerializeField] private int _max;

        /// <summary>下限と上限を指定して作る。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public IntRange(int min, int max)
        {
            _min = min;
            _max = max;
        }

        /// <summary>下限。</summary>
        public int Min => _min;

        /// <summary>上限。</summary>
        public int Max => _max;

        /// <summary>範囲に含まれる整数の個数。両端を含む。</summary>
        public int Count => Mathf.Max(0, _max - _min + 1);

        /// <summary>値が範囲に入っているか。両端を含む。</summary>
        /// <param name="value">判定する値。</param>
        public bool Contains(int value) => value >= _min && value <= _max;

        /// <summary>値を範囲内に丸める。</summary>
        /// <param name="value">丸める値。</param>
        public int Clamp(int value) => value < _min ? _min : value > _max ? _max : value;

        /// <summary>
        /// 範囲から一様に 1 つ引く。<c>Random.Range(int, int)</c> と違い
        /// <see cref="Max"/> も出る。
        /// </summary>
        public int Random() => UnityEngine.Random.Range(_min, _max + 1);

        /// <summary>上下が逆に入力されていた場合に入れ替えたものを返す。</summary>
        public IntRange Normalized() => _min <= _max ? this : new IntRange(_max, _min);

        /// <summary>両端が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(IntRange other) => _min == other._min && _max == other._max;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is IntRange other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((_min * 397) ^ _max);

        /// <inheritdoc/>
        public override string ToString() => $"[{_min}, {_max}]";

        /// <summary>等値比較。</summary>
        public static bool operator ==(IntRange a, IntRange b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(IntRange a, IntRange b) => !a.Equals(b);

        /// <summary><see cref="Min"/> から <see cref="Max"/> まで、両端を含めて走査する。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>整数範囲の構造体列挙子。</summary>
        public struct Enumerator
        {
            private readonly int _max;
            private int _current;

            internal Enumerator(IntRange range)
            {
                _max = range._max;
                _current = range._min - 1;
            }

            /// <summary>今の値。</summary>
            public int Current => _current;

            /// <summary>次の値へ進む。</summary>
            public bool MoveNext() => ++_current <= _max;
        }
    }

    /// <summary>
    /// <see cref="FloatRange"/> / <see cref="IntRange"/> を、
    /// 上下限つきのミニマックススライダーとして描く。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinMaxSliderAttribute : PropertyAttribute
    {
        /// <summary>スライダーの可動範囲を指定する。</summary>
        /// <param name="lowerLimit">スライダーの左端。</param>
        /// <param name="upperLimit">スライダーの右端。</param>
        public MinMaxSliderAttribute(float lowerLimit, float upperLimit)
        {
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
        }

        /// <summary>スライダーの左端。</summary>
        public float LowerLimit { get; }

        /// <summary>スライダーの右端。</summary>
        public float UpperLimit { get; }

        /// <summary>スライダーの両脇に数値入力欄も出す。既定は true。</summary>
        public bool ShowFields { get; set; } = true;

        internal FloatRange Limit => new FloatRange(LowerLimit, UpperLimit);
    }
}
