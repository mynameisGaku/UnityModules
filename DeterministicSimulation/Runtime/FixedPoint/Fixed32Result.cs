using System;

namespace FixedPoint
{
    /// <summary>Q16.16演算の値または失敗理由を保持する不変結果。</summary>
    public readonly struct Fixed32Result : IEquatable<Fixed32Result>
    {
        internal Fixed32Result(Fixed32 value, Fixed32Error error)
        {
            Value = value;
            Error = error;
        }

        /// <summary>成功時の値。失敗時は<see cref="Fixed32.Zero"/>。</summary>
        public Fixed32 Value { get; }

        /// <summary>失敗理由。成功時は<see cref="Fixed32Error.None"/>。</summary>
        public Fixed32Error Error { get; }

        /// <summary>値を生成できた場合にtrue。</summary>
        public bool Succeeded => Error == Fixed32Error.None;

        /// <summary>同じ値とerrorを持つかを判定する。</summary>
        public bool Equals(Fixed32Result other) => Value == other.Value && Error == other.Error;

        /// <summary>同じ値とerrorを持つかを判定する。</summary>
        public override bool Equals(object obj) => obj is Fixed32Result other && Equals(other);

        /// <summary>値とerrorからhash codeを返す。</summary>
        public override int GetHashCode() => HashCode.Combine(Value, (int)Error);

        /// <summary>2つの結果が等しいかを判定する。</summary>
        public static bool operator ==(Fixed32Result left, Fixed32Result right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを判定する。</summary>
        public static bool operator !=(Fixed32Result left, Fixed32Result right) => !left.Equals(right);

        internal static Fixed32Result Success(Fixed32 value) => new Fixed32Result(value, Fixed32Error.None);

        internal static Fixed32Result Failure(Fixed32Error error) => new Fixed32Result(Fixed32.Zero, error);
    }
}
