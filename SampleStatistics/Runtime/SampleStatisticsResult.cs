using System;

namespace GameplayAnalysis
{
    /// <summary>有限sample列の境界値・平均・母分散・母標準偏差を再構築可能に保持します。</summary>
    public readonly struct SampleStatisticsResult : IEquatable<SampleStatisticsResult>
    {
        internal SampleStatisticsResult(int sampleCount, double minimum, double maximum, double mean, double range, double populationVariance, double populationStandardDeviation)
        {
            SampleCount = sampleCount;
            Minimum = minimum;
            Maximum = maximum;
            Mean = mean;
            Range = range;
            PopulationVariance = populationVariance;
            PopulationStandardDeviation = populationStandardDeviation;
        }

        /// <summary>評価したsample件数を取得します。</summary>
        public int SampleCount { get; }
        /// <summary>指定範囲の最小sampleを取得します。</summary>
        public double Minimum { get; }
        /// <summary>指定範囲の最大sampleを取得します。</summary>
        public double Maximum { get; }
        /// <summary>指定範囲の算術平均を取得します。</summary>
        public double Mean { get; }
        /// <summary>最大値から最小値を引いたrangeを取得します。</summary>
        public double Range { get; }
        /// <summary>sample数を分母にした母分散を取得します。</summary>
        public double PopulationVariance { get; }
        /// <summary>母分散の平方根である母標準偏差を取得します。</summary>
        public double PopulationStandardDeviation { get; }

        /// <summary>2つの要約統計が全fieldで等しいかを返します。</summary>
        /// <param name="other">比較する要約統計です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(SampleStatisticsResult other)
        {
            return SampleCount == other.SampleCount
                && Minimum.Equals(other.Minimum)
                && Maximum.Equals(other.Maximum)
                && Mean.Equals(other.Mean)
                && Range.Equals(other.Range)
                && PopulationVariance.Equals(other.PopulationVariance)
                && PopulationStandardDeviation.Equals(other.PopulationStandardDeviation);
        }

        /// <summary>指定したobjectが同じ要約統計かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ要約統計である場合はtrueです。</returns>
        public override bool Equals(object obj)
        {
            return obj is SampleStatisticsResult other && Equals(other);
        }

        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SampleCount;
                hash = (hash * 397) ^ Minimum.GetHashCode();
                hash = (hash * 397) ^ Maximum.GetHashCode();
                hash = (hash * 397) ^ Mean.GetHashCode();
                hash = (hash * 397) ^ Range.GetHashCode();
                hash = (hash * 397) ^ PopulationVariance.GetHashCode();
                return (hash * 397) ^ PopulationStandardDeviation.GetHashCode();
            }
        }

        /// <summary>2つの要約統計が等しいかを返します。</summary>
        /// <param name="left">左側の要約統計です。</param>
        /// <param name="right">右側の要約統計です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(SampleStatisticsResult left, SampleStatisticsResult right)
        {
            return left.Equals(right);
        }

        /// <summary>2つの要約統計が異なるかを返します。</summary>
        /// <param name="left">左側の要約統計です。</param>
        /// <param name="right">右側の要約統計です。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(SampleStatisticsResult left, SampleStatisticsResult right)
        {
            return !left.Equals(right);
        }
    }
}
