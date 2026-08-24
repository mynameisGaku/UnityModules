using System;

namespace GameplayMath
{
    /// <summary>point変更前後のcurve状態と失敗理由。</summary>
    public readonly struct CurveChangeResult : IEquatable<CurveChangeResult>
    {
        internal CurveChangeResult(bool succeeded, bool changed, CurveError error, double affectedX, double previousY, double currentY, int previousPointCount, int currentPointCount)
        {
            Succeeded = succeeded;
            Changed = changed;
            Error = error;
            AffectedX = affectedX;
            PreviousY = previousY;
            CurrentY = currentY;
            PreviousPointCount = previousPointCount;
            CurrentPointCount = currentPointCount;
        }

        /// <summary>要求が受理されたか。</summary>
        public bool Succeeded { get; }

        /// <summary>curve状態が実際に変化したか。</summary>
        public bool Changed { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public CurveError Error { get; }

        /// <summary>対象pointのX。Clearまたは無効Xでは0。</summary>
        public double AffectedX { get; }

        /// <summary>変更前の対象Y。追加またはClearでは0。</summary>
        public double PreviousY { get; }

        /// <summary>変更後の対象Y。削除またはClearでは0。</summary>
        public double CurrentY { get; }

        /// <summary>変更前のpoint件数。</summary>
        public int PreviousPointCount { get; }

        /// <summary>変更後のpoint件数。</summary>
        public int CurrentPointCount { get; }

        /// <inheritdoc />
        public bool Equals(CurveChangeResult other) => Succeeded == other.Succeeded && Changed == other.Changed && Error == other.Error && AffectedX.Equals(other.AffectedX) && PreviousY.Equals(other.PreviousY) && CurrentY.Equals(other.CurrentY) && PreviousPointCount == other.PreviousPointCount && CurrentPointCount == other.CurrentPointCount;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CurveChangeResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Succeeded ? 1 : 0;
                hash = (hash * 397) ^ (Changed ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                hash = (hash * 397) ^ AffectedX.GetHashCode();
                hash = (hash * 397) ^ PreviousY.GetHashCode();
                hash = (hash * 397) ^ CurrentY.GetHashCode();
                hash = (hash * 397) ^ PreviousPointCount;
                return (hash * 397) ^ CurrentPointCount;
            }
        }

        /// <summary>2つの変更結果が全fieldで等しいか判定する。</summary>
        public static bool operator ==(CurveChangeResult left, CurveChangeResult right) => left.Equals(right);

        /// <summary>2つの変更結果に異なるfieldがあるか判定する。</summary>
        public static bool operator !=(CurveChangeResult left, CurveChangeResult right) => !left.Equals(right);
    }
}
