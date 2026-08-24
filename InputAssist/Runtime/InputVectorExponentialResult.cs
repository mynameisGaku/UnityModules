using System;

namespace InputFiltering
{
    /// <summary>更新後の2D成分、適用差分、残差を表すimmutableな指数平滑結果。</summary>
    public readonly struct InputVectorExponentialResult : IEquatable<InputVectorExponentialResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の更新後horizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の更新後vertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時にこのstepで実際に適用したvector差のmagnitude。失敗時は0。</summary>
        public double AppliedDeltaMagnitude { get; }

        /// <summary>成功時にtargetまで残ったvector差のmagnitude。失敗時は0。</summary>
        public double RemainingDeltaMagnitude { get; }

        /// <summary>成功時にtargetへexactに到達したか。</summary>
        public bool ReachedTarget { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputVectorExponentialSmootherError Error { get; }

        /// <summary>有効な更新結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputVectorExponentialSmootherError.None;

        private InputVectorExponentialResult(double horizontal, double vertical, double appliedDeltaMagnitude, double remainingDeltaMagnitude, bool reachedTarget, InputVectorExponentialSmootherError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            AppliedDeltaMagnitude = appliedDeltaMagnitude;
            RemainingDeltaMagnitude = remainingDeltaMagnitude;
            ReachedTarget = reachedTarget;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputVectorExponentialResult Success(double horizontal, double vertical, double appliedDeltaMagnitude, double remainingDeltaMagnitude, bool reachedTarget) => new InputVectorExponentialResult(horizontal, vertical, appliedDeltaMagnitude, remainingDeltaMagnitude, reachedTarget, InputVectorExponentialSmootherError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputVectorExponentialResult Failure(InputVectorExponentialSmootherError error) => new InputVectorExponentialResult(0d, 0d, 0d, 0d, false, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(InputVectorExponentialResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && AppliedDeltaMagnitude.Equals(other.AppliedDeltaMagnitude) && RemainingDeltaMagnitude.Equals(other.RemainingDeltaMagnitude) && ReachedTarget == other.ReachedTarget && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ結果の場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorExponentialResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        /// <returns>全出力と成功状態から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ AppliedDeltaMagnitude.GetHashCode();
                hash = (hash * 397) ^ RemainingDeltaMagnitude.GetHashCode();
                hash = (hash * 397) ^ (ReachedTarget ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public static bool operator ==(InputVectorExponentialResult left, InputVectorExponentialResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる結果の場合true。</returns>
        public static bool operator !=(InputVectorExponentialResult left, InputVectorExponentialResult right) => !left.Equals(right);
    }
}
