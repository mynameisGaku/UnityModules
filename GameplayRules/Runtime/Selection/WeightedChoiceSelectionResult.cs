using System;

namespace GameplaySelection
{
    /// <summary>normalized sampleから選ばれたentryと累積区間。</summary>
    public readonly struct WeightedChoiceSelectionResult : IEquatable<WeightedChoiceSelectionResult>
    {
        internal WeightedChoiceSelectionResult(
            bool succeeded,
            WeightedChoiceError error,
            double normalizedSample,
            double ticket,
            int selectedIdentifier,
            int selectedIndex,
            double selectedWeight,
            double intervalStart,
            double intervalEnd,
            double totalWeight)
        {
            Succeeded = succeeded;
            Error = error;
            NormalizedSample = normalizedSample;
            Ticket = ticket;
            SelectedIdentifier = selectedIdentifier;
            SelectedIndex = selectedIndex;
            SelectedWeight = selectedWeight;
            IntervalStart = intervalStart;
            IntervalEnd = intervalEnd;
            TotalWeight = totalWeight;
        }

        /// <summary>entryを選択できたか。</summary>
        public bool Succeeded { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public WeightedChoiceError Error { get; }

        /// <summary>呼出側が渡した0以上1未満のsample。無効入力時は0。</summary>
        public double NormalizedSample { get; }

        /// <summary>sampleとtotal weightから得た累積weight上の位置。</summary>
        public double Ticket { get; }

        /// <summary>選択されたentryのID。失敗時は0。</summary>
        public int SelectedIdentifier { get; }

        /// <summary>ID昇順に並べた選択entryのindex。失敗時は-1。</summary>
        public int SelectedIndex { get; }

        /// <summary>選択entryのweight。失敗時は0。</summary>
        public double SelectedWeight { get; }

        /// <summary>選択区間の累積weight下端。下端を含む。</summary>
        public double IntervalStart { get; }

        /// <summary>選択区間の累積weight上端。上端を含まない。</summary>
        public double IntervalEnd { get; }

        /// <summary>選択時点の全entry weight合計。</summary>
        public double TotalWeight { get; }

        /// <inheritdoc />
        public bool Equals(WeightedChoiceSelectionResult other) =>
            Succeeded == other.Succeeded && Error == other.Error && NormalizedSample.Equals(other.NormalizedSample) &&
            Ticket.Equals(other.Ticket) && SelectedIdentifier == other.SelectedIdentifier && SelectedIndex == other.SelectedIndex &&
            SelectedWeight.Equals(other.SelectedWeight) && IntervalStart.Equals(other.IntervalStart) &&
            IntervalEnd.Equals(other.IntervalEnd) && TotalWeight.Equals(other.TotalWeight);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is WeightedChoiceSelectionResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Succeeded ? 1 : 0;
                hash = (hash * 397) ^ (int)Error;
                hash = (hash * 397) ^ NormalizedSample.GetHashCode();
                hash = (hash * 397) ^ Ticket.GetHashCode();
                hash = (hash * 397) ^ SelectedIdentifier;
                hash = (hash * 397) ^ SelectedIndex;
                hash = (hash * 397) ^ SelectedWeight.GetHashCode();
                hash = (hash * 397) ^ IntervalStart.GetHashCode();
                hash = (hash * 397) ^ IntervalEnd.GetHashCode();
                return (hash * 397) ^ TotalWeight.GetHashCode();
            }
        }

        /// <summary>2つの選択結果が全fieldで等しいか判定する。</summary>
        public static bool operator ==(WeightedChoiceSelectionResult left, WeightedChoiceSelectionResult right) => left.Equals(right);

        /// <summary>2つの選択結果に異なるfieldがあるか判定する。</summary>
        public static bool operator !=(WeightedChoiceSelectionResult left, WeightedChoiceSelectionResult right) => !left.Equals(right);
    }
}
