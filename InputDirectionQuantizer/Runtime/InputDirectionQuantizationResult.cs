using System;

namespace InputDirectionQuantization
{
    /// <summary>成功方向と失敗理由を同時に表すimmutableな2D量子化結果。</summary>
    public readonly struct InputDirectionQuantizationResult : IEquatable<InputDirectionQuantizationResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時のhorizontal方向。-1、0、1のいずれか。失敗時は0。</summary>
        public sbyte Horizontal { get; }

        /// <summary>成功時のvertical方向。-1、0、1のいずれか。失敗時は0。</summary>
        public sbyte Vertical { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputDirectionQuantizationError Error { get; }

        /// <summary>有効な成功方向を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputDirectionQuantizationError.None;

        /// <summary>成功方向がneutralか。</summary>
        public bool IsNeutral => Succeeded && Horizontal == 0 && Vertical == 0;

        /// <summary>成功方向がdiagonalか。</summary>
        public bool IsDiagonal => Succeeded && Horizontal != 0 && Vertical != 0;

        private InputDirectionQuantizationResult(sbyte horizontal, sbyte vertical, InputDirectionQuantizationError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputDirectionQuantizationResult Success(int horizontal, int vertical) => new InputDirectionQuantizationResult((sbyte)horizontal, (sbyte)vertical, InputDirectionQuantizationError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputDirectionQuantizationResult Failure(InputDirectionQuantizationError error) => new InputDirectionQuantizationResult(0, 0, error, false);

        /// <summary>方向、error、成功状態が同じかを返す。</summary>
        public bool Equals(InputDirectionQuantizationResult other) => Horizontal == other.Horizontal && Vertical == other.Vertical && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputDirectionQuantizationResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Horizontal;
                hash = (hash * 397) ^ Vertical;
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputDirectionQuantizationResult left, InputDirectionQuantizationResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputDirectionQuantizationResult left, InputDirectionQuantizationResult right) => !left.Equals(right);
    }
}
