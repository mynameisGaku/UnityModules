namespace GameplayAnalysis
{
    /// <summary>2〜32個の等間隔な有限sampleを最小二乗直線へ変換する純粋関数を提供します。</summary>
    public static class LinearTrendEstimator
    {
        /// <summary>1回に評価できる最大sample数です。</summary>
        public const int MaximumSampleCount = 32;

        /// <summary>配列全体を等間隔sampleとして傾き・切片・次sample予測へ変換します。</summary>
        /// <param name="samples">2〜32個の有限sampleを持つ配列です。</param>
        /// <param name="estimate">成功時に推定結果を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>有限な推定結果を作成できた場合はtrueです。</returns>
        public static bool TryEstimate(double[] samples, out LinearTrendEstimate estimate, out LinearTrendError error)
        {
            if (samples == null)
            {
                estimate = default;
                error = LinearTrendError.NullSamples;
                return false;
            }

            return TryEstimate(samples, 0, samples.Length, out estimate, out error);
        }

        /// <summary>配列内の明示範囲を等間隔sampleとして傾き・切片・次sample予測へ変換します。</summary>
        /// <param name="samples">評価元のsample配列です。</param>
        /// <param name="startIndex">評価を始める0以上のindexです。</param>
        /// <param name="count">2以上32以下の評価件数です。</param>
        /// <param name="estimate">成功時に推定結果を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>有限な推定結果を作成できた場合はtrueです。</returns>
        public static bool TryEstimate(double[] samples, int startIndex, int count, out LinearTrendEstimate estimate, out LinearTrendError error)
        {
            if (samples == null)
            {
                estimate = default;
                error = LinearTrendError.NullSamples;
                return false;
            }

            if (startIndex < 0)
            {
                estimate = default;
                error = LinearTrendError.InvalidStartIndex;
                return false;
            }

            if (count < 2 || count > MaximumSampleCount)
            {
                estimate = default;
                error = LinearTrendError.InvalidSampleCount;
                return false;
            }

            if (startIndex > samples.Length - count)
            {
                estimate = default;
                error = LinearTrendError.RangeOutOfBounds;
                return false;
            }

            for (var offset = 0; offset < count; offset++)
            {
                var sample = samples[startIndex + offset];
                if (double.IsNaN(sample) || double.IsInfinity(sample))
                {
                    estimate = default;
                    error = LinearTrendError.NonFiniteSample;
                    return false;
                }
            }

            if (!LinearTrendMath.TryCalculate(samples, startIndex, count, out estimate))
            {
                error = LinearTrendError.ResultOutOfRange;
                return false;
            }

            error = LinearTrendError.None;
            return true;
        }
    }
}
