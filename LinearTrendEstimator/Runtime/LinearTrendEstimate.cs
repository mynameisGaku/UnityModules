using System;

namespace GameplayAnalysis
{
    /// <summary>等間隔sampleに対する直線近似と次sample予測を再構築可能に保持します。</summary>
    public readonly struct LinearTrendEstimate : IEquatable<LinearTrendEstimate>
    {
        internal LinearTrendEstimate(int sampleCount, double firstSample, double lastSample, double mean, double slopePerSample, double interceptAtIndexZero, double predictedNextSample)
        {
            SampleCount = sampleCount;
            FirstSample = firstSample;
            LastSample = lastSample;
            Mean = mean;
            SlopePerSample = slopePerSample;
            InterceptAtIndexZero = interceptAtIndexZero;
            PredictedNextSample = predictedNextSample;
        }

        /// <summary>評価したsample件数を取得します。</summary>
        public int SampleCount { get; }
        /// <summary>指定範囲の最初のsampleを取得します。</summary>
        public double FirstSample { get; }
        /// <summary>指定範囲の最後のsampleを取得します。</summary>
        public double LastSample { get; }
        /// <summary>指定範囲の算術平均を取得します。</summary>
        public double Mean { get; }
        /// <summary>sample indexが1増えるごとの最小二乗直線の変化量を取得します。</summary>
        public double SlopePerSample { get; }
        /// <summary>sample index 0における最小二乗直線の値を取得します。</summary>
        public double InterceptAtIndexZero { get; }
        /// <summary>指定範囲の直後のindexへ直線を延長した予測値を取得します。</summary>
        public double PredictedNextSample { get; }

        /// <summary>2つの推定結果が全fieldで等しいかを返します。</summary>
        /// <param name="other">比較する推定結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(LinearTrendEstimate other)
        {
            return SampleCount == other.SampleCount
                && FirstSample.Equals(other.FirstSample)
                && LastSample.Equals(other.LastSample)
                && Mean.Equals(other.Mean)
                && SlopePerSample.Equals(other.SlopePerSample)
                && InterceptAtIndexZero.Equals(other.InterceptAtIndexZero)
                && PredictedNextSample.Equals(other.PredictedNextSample);
        }

        /// <summary>指定したobjectが同じ推定結果かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ推定結果である場合はtrueです。</returns>
        public override bool Equals(object obj)
        {
            return obj is LinearTrendEstimate other && Equals(other);
        }

        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SampleCount;
                hash = (hash * 397) ^ FirstSample.GetHashCode();
                hash = (hash * 397) ^ LastSample.GetHashCode();
                hash = (hash * 397) ^ Mean.GetHashCode();
                hash = (hash * 397) ^ SlopePerSample.GetHashCode();
                hash = (hash * 397) ^ InterceptAtIndexZero.GetHashCode();
                return (hash * 397) ^ PredictedNextSample.GetHashCode();
            }
        }

        /// <summary>2つの推定結果が等しいかを返します。</summary>
        /// <param name="left">左側の推定結果です。</param>
        /// <param name="right">右側の推定結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(LinearTrendEstimate left, LinearTrendEstimate right)
        {
            return left.Equals(right);
        }

        /// <summary>2つの推定結果が異なるかを返します。</summary>
        /// <param name="left">左側の推定結果です。</param>
        /// <param name="right">右側の推定結果です。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(LinearTrendEstimate left, LinearTrendEstimate right)
        {
            return !left.Equals(right);
        }
    }
}
