using System;

namespace InputSmoothing
{
    /// <summary>更新後の2D成分と適用差分を表すimmutableなslew制限結果。</summary>
    public readonly struct InputVectorSlewResult : IEquatable<InputVectorSlewResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の更新後horizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の更新後vertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時にこのstepで適用したvector差のmagnitude。失敗時は0。</summary>
        public double AppliedDeltaMagnitude { get; }

        /// <summary>成功時にtargetへ到達したか。</summary>
        public bool ReachedTarget { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputVectorSlewLimiterError Error { get; }

        /// <summary>有効な更新結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputVectorSlewLimiterError.None;

        private InputVectorSlewResult(double horizontal, double vertical, double appliedDeltaMagnitude, bool reachedTarget, InputVectorSlewLimiterError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            AppliedDeltaMagnitude = appliedDeltaMagnitude;
            ReachedTarget = reachedTarget;
            Error = error;
            _hasValue = hasValue;
        }

        internal static InputVectorSlewResult Success(double horizontal, double vertical, double appliedDeltaMagnitude, bool reachedTarget) => new InputVectorSlewResult(horizontal, vertical, appliedDeltaMagnitude, reachedTarget, InputVectorSlewLimiterError.None, true);

        internal static InputVectorSlewResult Failure(InputVectorSlewLimiterError error) => new InputVectorSlewResult(0d, 0d, 0d, false, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        public bool Equals(InputVectorSlewResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && AppliedDeltaMagnitude.Equals(other.AppliedDeltaMagnitude) && ReachedTarget == other.ReachedTarget && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputVectorSlewResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ AppliedDeltaMagnitude.GetHashCode();
                hash = (hash * 397) ^ (ReachedTarget ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputVectorSlewResult left, InputVectorSlewResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputVectorSlewResult left, InputVectorSlewResult right) => !left.Equals(right);
    }
}
