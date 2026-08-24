using System;

namespace GameplayMetrics
{
    /// <summary>1つのsample追加で起きた退避と前後状態を表す。</summary>
    public readonly struct SampleWindowAddResult : IEquatable<SampleWindowAddResult>
    {
        internal SampleWindowAddResult(bool succeeded, SampleWindowError error, double addedSample, bool hadEviction, double evictedSample, SampleWindowSnapshot previousSnapshot, SampleWindowSnapshot currentSnapshot)
        {
            Succeeded = succeeded;
            Error = error;
            AddedSample = addedSample;
            HadEviction = hadEviction;
            EvictedSample = evictedSample;
            PreviousSnapshot = previousSnapshot;
            CurrentSnapshot = currentSnapshot;
        }

        /// <summary>追加が成功したならtrue。</summary>
        public bool Succeeded { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public SampleWindowError Error { get; }

        /// <summary>追加を要求したsample。</summary>
        public double AddedSample { get; }

        /// <summary>容量超過によりoldest sampleを退避したならtrue。</summary>
        public bool HadEviction { get; }

        /// <summary>退避したsample。退避がなければ0。</summary>
        public double EvictedSample { get; }

        /// <summary>追加前のsnapshot。</summary>
        public SampleWindowSnapshot PreviousSnapshot { get; }

        /// <summary>追加後のsnapshot。失敗時は追加前と同一。</summary>
        public SampleWindowSnapshot CurrentSnapshot { get; }

        /// <summary>全fieldが等しいか判定する。</summary>
        /// <param name="other">比較する追加結果。</param>
        /// <returns>全fieldが等しいならtrue。</returns>
        public bool Equals(SampleWindowAddResult other) => Succeeded == other.Succeeded && Error == other.Error && AddedSample.Equals(other.AddedSample) && HadEviction == other.HadEviction && EvictedSample.Equals(other.EvictedSample) && PreviousSnapshot.Equals(other.PreviousSnapshot) && CurrentSnapshot.Equals(other.CurrentSnapshot);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SampleWindowAddResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Succeeded.GetHashCode();
                hash = (hash * 397) ^ (int)Error;
                hash = (hash * 397) ^ AddedSample.GetHashCode();
                hash = (hash * 397) ^ HadEviction.GetHashCode();
                hash = (hash * 397) ^ EvictedSample.GetHashCode();
                hash = (hash * 397) ^ PreviousSnapshot.GetHashCode();
                return (hash * 397) ^ CurrentSnapshot.GetHashCode();
            }
        }

        /// <summary>2つの追加結果が等しいか判定する。</summary>
        /// <param name="left">左側の追加結果。</param>
        /// <param name="right">右側の追加結果。</param>
        /// <returns>等しいならtrue。</returns>
        public static bool operator ==(SampleWindowAddResult left, SampleWindowAddResult right) => left.Equals(right);

        /// <summary>2つの追加結果が異なるか判定する。</summary>
        /// <param name="left">左側の追加結果。</param>
        /// <param name="right">右側の追加結果。</param>
        /// <returns>異なるならtrue。</returns>
        public static bool operator !=(SampleWindowAddResult left, SampleWindowAddResult right) => !left.Equals(right);

        internal static SampleWindowAddResult Success(double addedSample, bool hadEviction, double evictedSample, SampleWindowSnapshot previousSnapshot, SampleWindowSnapshot currentSnapshot) => new SampleWindowAddResult(true, SampleWindowError.None, addedSample, hadEviction, evictedSample, previousSnapshot, currentSnapshot);

        internal static SampleWindowAddResult Failure(SampleWindowError error, double addedSample, SampleWindowSnapshot currentSnapshot) => new SampleWindowAddResult(false, error, addedSample, false, 0d, currentSnapshot, currentSnapshot);
    }
}
