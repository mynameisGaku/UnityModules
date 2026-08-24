using System;

namespace GameplayMetrics
{
    /// <summary>ある時点のFIFO窓と統計を再構築可能に表す。</summary>
    public readonly struct SampleWindowSnapshot : IEquatable<SampleWindowSnapshot>
    {
        internal SampleWindowSnapshot(int capacity, int count, bool hasSamples, double minimum, double maximum, double mean, double oldest, double newest)
        {
            Capacity = capacity;
            Count = count;
            HasSamples = hasSamples;
            Minimum = minimum;
            Maximum = maximum;
            Mean = mean;
            Oldest = oldest;
            Newest = newest;
        }

        /// <summary>窓が保持できるsample上限。</summary>
        public int Capacity { get; }

        /// <summary>現在保持するsample件数。</summary>
        public int Count { get; }

        /// <summary>sampleが1件以上あるならtrue。</summary>
        public bool HasSamples { get; }

        /// <summary>保持sampleの最小値。空なら0。</summary>
        public double Minimum { get; }

        /// <summary>保持sampleの最大値。空なら0。</summary>
        public double Maximum { get; }

        /// <summary>保持sampleをoldest-first順に集約した算術平均。空なら0。</summary>
        public double Mean { get; }

        /// <summary>最も古いsample。空なら0。</summary>
        public double Oldest { get; }

        /// <summary>最も新しいsample。空なら0。</summary>
        public double Newest { get; }

        /// <summary>全fieldが等しいか判定する。</summary>
        /// <param name="other">比較するsnapshot。</param>
        /// <returns>全fieldが等しいならtrue。</returns>
        public bool Equals(SampleWindowSnapshot other) => Capacity == other.Capacity && Count == other.Count && HasSamples == other.HasSamples && Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum) && Mean.Equals(other.Mean) && Oldest.Equals(other.Oldest) && Newest.Equals(other.Newest);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SampleWindowSnapshot other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Capacity;
                hash = (hash * 397) ^ Count;
                hash = (hash * 397) ^ HasSamples.GetHashCode();
                hash = (hash * 397) ^ Minimum.GetHashCode();
                hash = (hash * 397) ^ Maximum.GetHashCode();
                hash = (hash * 397) ^ Mean.GetHashCode();
                hash = (hash * 397) ^ Oldest.GetHashCode();
                return (hash * 397) ^ Newest.GetHashCode();
            }
        }

        /// <summary>2つのsnapshotが等しいか判定する。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>等しいならtrue。</returns>
        public static bool operator ==(SampleWindowSnapshot left, SampleWindowSnapshot right) => left.Equals(right);

        /// <summary>2つのsnapshotが異なるか判定する。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>異なるならtrue。</returns>
        public static bool operator !=(SampleWindowSnapshot left, SampleWindowSnapshot right) => !left.Equals(right);
    }
}
