// SPDX-License-Identifier: MIT

using System;

namespace PerfMeter
{
    /// <summary>1つの取得タイミングで揃えたframe time統計。全fieldの等価比較を提供する。</summary>
    public readonly struct FrameTimeSnapshot : IEquatable<FrameTimeSnapshot>
    {
        /// <summary>全fieldを明示してsnapshotを作る。値の妥当性は生成元のsampler契約に従う。</summary>
        /// <param name="last">直後に追加されたsample秒。</param>
        /// <param name="average">平均frame time秒。</param>
        /// <param name="minimum">最小frame time秒。</param>
        /// <param name="maximum">最大frame time秒。</param>
        /// <param name="median">中央値frame time秒。</param>
        /// <param name="standardDeviation">母標準偏差秒。</param>
        /// <param name="sampleCount">window内のsample件数。</param>
        /// <param name="averageFps">平均dtの逆数fps。</param>
        public FrameTimeSnapshot(
            double last,
            double average,
            double minimum,
            double maximum,
            double median,
            double standardDeviation,
            int sampleCount,
            double averageFps)
        {
            Last = last;
            Average = average;
            Minimum = minimum;
            Maximum = maximum;
            Median = median;
            StandardDeviation = standardDeviation;
            SampleCount = sampleCount;
            AverageFps = averageFps;
        }

        /// <summary>直後に追加されたsample秒。</summary>
        public double Last { get; }

        /// <summary>平均frame time秒。</summary>
        public double Average { get; }

        /// <summary>最小frame time秒。</summary>
        public double Minimum { get; }

        /// <summary>最大frame time秒。</summary>
        public double Maximum { get; }

        /// <summary>中央値frame time秒。</summary>
        public double Median { get; }

        /// <summary>母標準偏差秒。</summary>
        public double StandardDeviation { get; }

        /// <summary>window内のsample件数。</summary>
        public int SampleCount { get; }

        /// <summary>平均dtの逆数fps。</summary>
        public double AverageFps { get; }

        /// <summary>全ての統計fieldが等しい場合はtrueを返す。</summary>
        /// <param name="other">比較するsnapshot。</param>
        /// <returns>全てのfieldが等しい場合はtrue。</returns>
        public bool Equals(FrameTimeSnapshot other)
        {
            return Last == other.Last &&
                   Average == other.Average &&
                   Minimum == other.Minimum &&
                   Maximum == other.Maximum &&
                   Median == other.Median &&
                   StandardDeviation == other.StandardDeviation &&
                   SampleCount == other.SampleCount &&
                   AverageFps == other.AverageFps;
        }

        /// <summary>指定objectが同じ統計ならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ統計ならtrue。</returns>
        public override bool Equals(object obj) => obj is FrameTimeSnapshot other && Equals(other);

        /// <summary>全ての統計fieldからhash値を返す。</summary>
        /// <returns>統計のhash値。</returns>
        public override int GetHashCode() => HashCode.Combine(Last, Average, Minimum, Maximum, Median, StandardDeviation, SampleCount, AverageFps);

        /// <summary>左右のsnapshotが等しい場合はtrueを返す。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>左右が等しい場合はtrue。</returns>
        public static bool operator ==(FrameTimeSnapshot left, FrameTimeSnapshot right) => left.Equals(right);

        /// <summary>左右のsnapshotが異なる場合はtrueを返す。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(FrameTimeSnapshot left, FrameTimeSnapshot right) => !left.Equals(right);
    }
}
