using System;

namespace GameplaySelection
{
    /// <summary>entry変更前後のtable状態と失敗理由。</summary>
    public readonly struct WeightedChoiceChangeResult : IEquatable<WeightedChoiceChangeResult>
    {
        internal WeightedChoiceChangeResult(
            bool succeeded,
            bool changed,
            WeightedChoiceError error,
            int affectedIdentifier,
            double previousWeight,
            double currentWeight,
            double previousTotalWeight,
            double currentTotalWeight,
            int previousEntryCount,
            int currentEntryCount)
        {
            Succeeded = succeeded;
            Changed = changed;
            Error = error;
            AffectedIdentifier = affectedIdentifier;
            PreviousWeight = previousWeight;
            CurrentWeight = currentWeight;
            PreviousTotalWeight = previousTotalWeight;
            CurrentTotalWeight = currentTotalWeight;
            PreviousEntryCount = previousEntryCount;
            CurrentEntryCount = currentEntryCount;
        }

        /// <summary>要求が受理されたか。</summary>
        public bool Succeeded { get; }

        /// <summary>table状態が実際に変化したか。</summary>
        public bool Changed { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public WeightedChoiceError Error { get; }

        /// <summary>対象entryのID。Clearでは0。</summary>
        public int AffectedIdentifier { get; }

        /// <summary>変更前の対象weight。追加またはClearでは0。</summary>
        public double PreviousWeight { get; }

        /// <summary>変更後の対象weight。削除またはClearでは0。</summary>
        public double CurrentWeight { get; }

        /// <summary>変更前のweight合計。</summary>
        public double PreviousTotalWeight { get; }

        /// <summary>変更後のweight合計。</summary>
        public double CurrentTotalWeight { get; }

        /// <summary>変更前のentry件数。</summary>
        public int PreviousEntryCount { get; }

        /// <summary>変更後のentry件数。</summary>
        public int CurrentEntryCount { get; }

        /// <inheritdoc />
        public bool Equals(WeightedChoiceChangeResult other) =>
            Succeeded == other.Succeeded && Changed == other.Changed && Error == other.Error &&
            AffectedIdentifier == other.AffectedIdentifier && PreviousWeight.Equals(other.PreviousWeight) &&
            CurrentWeight.Equals(other.CurrentWeight) && PreviousTotalWeight.Equals(other.PreviousTotalWeight) &&
            CurrentTotalWeight.Equals(other.CurrentTotalWeight) && PreviousEntryCount == other.PreviousEntryCount &&
            CurrentEntryCount == other.CurrentEntryCount;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is WeightedChoiceChangeResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Succeeded ? 1 : 0;
                hash = (hash * 397) ^ (Changed ? 1 : 0);
                hash = (hash * 397) ^ (int)Error;
                hash = (hash * 397) ^ AffectedIdentifier;
                hash = (hash * 397) ^ PreviousWeight.GetHashCode();
                hash = (hash * 397) ^ CurrentWeight.GetHashCode();
                hash = (hash * 397) ^ PreviousTotalWeight.GetHashCode();
                hash = (hash * 397) ^ CurrentTotalWeight.GetHashCode();
                hash = (hash * 397) ^ PreviousEntryCount;
                return (hash * 397) ^ CurrentEntryCount;
            }
        }

        /// <summary>2つの変更結果が全fieldで等しいか判定する。</summary>
        public static bool operator ==(WeightedChoiceChangeResult left, WeightedChoiceChangeResult right) => left.Equals(right);

        /// <summary>2つの変更結果に異なるfieldがあるか判定する。</summary>
        public static bool operator !=(WeightedChoiceChangeResult left, WeightedChoiceChangeResult right) => !left.Equals(right);
    }
}
