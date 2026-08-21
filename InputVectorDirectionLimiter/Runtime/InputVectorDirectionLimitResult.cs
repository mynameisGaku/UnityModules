using System;

namespace InputSmoothing
{
    /// <summary>更新後成分と実適用・残り回転量を表すimmutableな方向制限結果。</summary>
    public readonly struct InputVectorDirectionLimitResult : IEquatable<InputVectorDirectionLimitResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の更新後horizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の更新後vertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時のtarget magnitude。失敗時は0。</summary>
        public double TargetMagnitude { get; }

        /// <summary>成功時にこのstepで実際に適用した非負radian。失敗時は0。</summary>
        public double AppliedTurnRadians { get; }

        /// <summary>成功時にtarget方向まで残った非負radian。失敗時は0。</summary>
        public double RemainingTurnRadians { get; }

        /// <summary>更新前stateに非ゼロ方向があったか。</summary>
        public bool HadPriorDirection { get; }

        /// <summary>target方向へこのstepで到達したか。</summary>
        public bool ReachedTargetDirection { get; }

        /// <summary>演算誤差によるunit circle外への逸出を補正したか。</summary>
        public bool WasNumericallyClamped { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputVectorDirectionLimiterError Error { get; }

        /// <summary>有効な更新結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputVectorDirectionLimiterError.None;

        private InputVectorDirectionLimitResult(double horizontal, double vertical, double targetMagnitude, double appliedTurnRadians, double remainingTurnRadians, bool hadPriorDirection, bool reachedTargetDirection, bool wasNumericallyClamped, InputVectorDirectionLimiterError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            TargetMagnitude = targetMagnitude;
            AppliedTurnRadians = appliedTurnRadians;
            RemainingTurnRadians = remainingTurnRadians;
            HadPriorDirection = hadPriorDirection;
            ReachedTargetDirection = reachedTargetDirection;
            WasNumericallyClamped = wasNumericallyClamped;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputVectorDirectionLimitResult Success(double horizontal, double vertical, double targetMagnitude, double appliedTurnRadians, double remainingTurnRadians, bool hadPriorDirection, bool reachedTargetDirection, bool wasNumericallyClamped) => new InputVectorDirectionLimitResult(horizontal, vertical, targetMagnitude, appliedTurnRadians, remainingTurnRadians, hadPriorDirection, reachedTargetDirection, wasNumericallyClamped, InputVectorDirectionLimiterError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputVectorDirectionLimitResult Failure(InputVectorDirectionLimiterError error) => new InputVectorDirectionLimitResult(0d, 0d, 0d, 0d, 0d, false, false, false, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(InputVectorDirectionLimitResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && TargetMagnitude.Equals(other.TargetMagnitude) && AppliedTurnRadians.Equals(other.AppliedTurnRadians) && RemainingTurnRadians.Equals(other.RemainingTurnRadians) && HadPriorDirection == other.HadPriorDirection && ReachedTargetDirection == other.ReachedTargetDirection && WasNumericallyClamped == other.WasNumericallyClamped && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ結果の場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorDirectionLimitResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        /// <returns>全出力と成功状態から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ TargetMagnitude.GetHashCode();
                hash = (hash * 397) ^ AppliedTurnRadians.GetHashCode();
                hash = (hash * 397) ^ RemainingTurnRadians.GetHashCode();
                hash = (hash * 397) ^ (HadPriorDirection ? 1 : 0);
                hash = (hash * 397) ^ (ReachedTargetDirection ? 1 : 0);
                hash = (hash * 397) ^ (WasNumericallyClamped ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public static bool operator ==(InputVectorDirectionLimitResult left, InputVectorDirectionLimitResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる結果の場合true。</returns>
        public static bool operator !=(InputVectorDirectionLimitResult left, InputVectorDirectionLimitResult right) => !left.Equals(right);
    }
}
