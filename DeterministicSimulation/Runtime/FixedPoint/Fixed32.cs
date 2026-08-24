using System;
using System.Globalization;

namespace FixedPoint
{
    /// <summary>16bit整数部と16bit小数部を持つ符号付きQ16.16値。</summary>
    public readonly struct Fixed32 : IEquatable<Fixed32>, IComparable<Fixed32>
    {
        /// <summary>小数部に使うbit数。</summary>
        public const int FractionalBitCount = 16;

        /// <summary>1を表すraw値。</summary>
        public const int Scale = 1 << FractionalBitCount;

        /// <summary>0を表す値。</summary>
        public static readonly Fixed32 Zero = new Fixed32(0);

        /// <summary>1を表す値。</summary>
        public static readonly Fixed32 One = new Fixed32(Scale);

        /// <summary>表現できる最小値。</summary>
        public static readonly Fixed32 MinValue = new Fixed32(int.MinValue);

        /// <summary>表現できる最大値。</summary>
        public static readonly Fixed32 MaxValue = new Fixed32(int.MaxValue);

        private readonly int _rawValue;

        private Fixed32(int rawValue)
        {
            _rawValue = rawValue;
        }

        /// <summary>Q16.16の符号付きraw値。</summary>
        public int RawValue => _rawValue;

        /// <summary>検証済みraw値からQ16.16値を生成する。</summary>
        /// <param name="rawValue">保持する符号付きraw値。</param>
        public static Fixed32 FromRaw(int rawValue) => new Fixed32(rawValue);

        /// <summary>整数を正確なQ16.16値へ変換する。</summary>
        /// <param name="value">変換する整数。</param>
        /// <returns>範囲外ならOverflowを返す。</returns>
        public static Fixed32Result FromInt32(int value)
        {
            var raw = (long)value * Scale;
            return FromLongRaw(raw);
        }

        /// <summary>整数の比を0方向へ丸めたQ16.16値へ変換する。</summary>
        /// <param name="numerator">分子。</param>
        /// <param name="denominator">0ではない分母。</param>
        /// <returns>0除算または範囲外を明示した結果。</returns>
        public static Fixed32Result FromRatio(int numerator, int denominator)
        {
            if (denominator == 0) return Fixed32Result.Failure(Fixed32Error.DivisionByZero);
            var raw = (long)numerator * Scale / denominator;
            return FromLongRaw(raw);
        }

        /// <summary>2値を加算する。</summary>
        public static Fixed32Result Add(Fixed32 left, Fixed32 right) => FromLongRaw((long)left._rawValue + right._rawValue);

        /// <summary>右値を左値から減算する。</summary>
        public static Fixed32Result Subtract(Fixed32 left, Fixed32 right) => FromLongRaw((long)left._rawValue - right._rawValue);

        /// <summary>2値を乗算し、余った小数bitを0方向へ丸める。</summary>
        public static Fixed32Result Multiply(Fixed32 left, Fixed32 right)
        {
            var raw = (long)left._rawValue * right._rawValue / Scale;
            return FromLongRaw(raw);
        }

        /// <summary>左値を右値で除算し、余りを0方向へ丸める。</summary>
        public static Fixed32Result Divide(Fixed32 left, Fixed32 right)
        {
            if (right._rawValue == 0) return Fixed32Result.Failure(Fixed32Error.DivisionByZero);
            var raw = (long)left._rawValue * Scale / right._rawValue;
            return FromLongRaw(raw);
        }

        /// <summary>符号を反転する。</summary>
        public static Fixed32Result Negate(Fixed32 value)
        {
            if (value._rawValue == int.MinValue) return Fixed32Result.Failure(Fixed32Error.Overflow);
            return Fixed32Result.Success(new Fixed32(-value._rawValue));
        }

        /// <summary>絶対値を返す。</summary>
        public static Fixed32Result Abs(Fixed32 value) => value._rawValue < 0 ? Negate(value) : Fixed32Result.Success(value);

        /// <summary>小数部を0方向へ切り捨てた整数を返す。</summary>
        public int TruncateToInt32() => _rawValue / Scale;

        /// <summary>負の無限大方向へ丸めた整数を返す。</summary>
        public int FloorToInt32()
        {
            var value = _rawValue / Scale;
            return _rawValue < 0 && _rawValue % Scale != 0 ? value - 1 : value;
        }

        /// <summary>正の無限大方向へ丸めた整数を返す。</summary>
        public int CeilingToInt32()
        {
            var value = _rawValue / Scale;
            return _rawValue > 0 && _rawValue % Scale != 0 ? value + 1 : value;
        }

        /// <summary>表示やengine adapter向けのdouble値を返す。</summary>
        public double ToDouble() => (double)_rawValue / Scale;

        /// <summary>raw値の大小を比較する。</summary>
        public int CompareTo(Fixed32 other) => _rawValue.CompareTo(other._rawValue);

        /// <summary>raw値が等しいかを判定する。</summary>
        public bool Equals(Fixed32 other) => _rawValue == other._rawValue;

        /// <summary>raw値が等しいかを判定する。</summary>
        public override bool Equals(object obj) => obj is Fixed32 other && Equals(other);

        /// <summary>raw値からhash codeを返す。</summary>
        public override int GetHashCode() => _rawValue;

        /// <summary>invariant cultureの小数表記を返す。</summary>
        public override string ToString() => ToDouble().ToString("0.################", CultureInfo.InvariantCulture);

        /// <summary>左値が右値より小さいかを判定する。</summary>
        public static bool operator <(Fixed32 left, Fixed32 right) => left._rawValue < right._rawValue;

        /// <summary>左値が右値以下かを判定する。</summary>
        public static bool operator <=(Fixed32 left, Fixed32 right) => left._rawValue <= right._rawValue;

        /// <summary>左値が右値より大きいかを判定する。</summary>
        public static bool operator >(Fixed32 left, Fixed32 right) => left._rawValue > right._rawValue;

        /// <summary>左値が右値以上かを判定する。</summary>
        public static bool operator >=(Fixed32 left, Fixed32 right) => left._rawValue >= right._rawValue;

        /// <summary>2値が等しいかを判定する。</summary>
        public static bool operator ==(Fixed32 left, Fixed32 right) => left.Equals(right);

        /// <summary>2値が異なるかを判定する。</summary>
        public static bool operator !=(Fixed32 left, Fixed32 right) => !left.Equals(right);

        private static Fixed32Result FromLongRaw(long raw)
        {
            return raw < int.MinValue || raw > int.MaxValue
                ? Fixed32Result.Failure(Fixed32Error.Overflow)
                : Fixed32Result.Success(new Fixed32((int)raw));
        }
    }
}
