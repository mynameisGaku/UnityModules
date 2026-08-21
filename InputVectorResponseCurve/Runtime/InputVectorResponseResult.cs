using System;

namespace InputResponse
{
    /// <summary>curve適用済み2D成分と失敗理由を同時に表すimmutableな結果。</summary>
    public readonly struct InputVectorResponseResult : IEquatable<InputVectorResponseResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の処理済みhorizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の処理済みvertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時の処理済みmagnitude。0以上1以下。失敗時は0。</summary>
        public double Magnitude { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputVectorResponseCurveError Error { get; }

        /// <summary>有効な処理結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputVectorResponseCurveError.None;

        /// <summary>成功結果のmagnitudeが0か。</summary>
        public bool IsZero => Succeeded && Magnitude == 0d;

        private InputVectorResponseResult(double horizontal, double vertical, double magnitude, InputVectorResponseCurveError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Magnitude = magnitude;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputVectorResponseResult Success(double horizontal, double vertical, double magnitude) => new InputVectorResponseResult(horizontal, vertical, magnitude, InputVectorResponseCurveError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputVectorResponseResult Failure(InputVectorResponseCurveError error) => new InputVectorResponseResult(0d, 0d, 0d, error, false);

        /// <summary>成分、magnitude、error、成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(InputVectorResponseResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && Magnitude.Equals(other.Magnitude) && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ結果の場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorResponseResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        /// <returns>成分、magnitude、error、成功状態から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ Magnitude.GetHashCode();
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public static bool operator ==(InputVectorResponseResult left, InputVectorResponseResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる結果の場合true。</returns>
        public static bool operator !=(InputVectorResponseResult left, InputVectorResponseResult right) => !left.Equals(right);
    }
}
