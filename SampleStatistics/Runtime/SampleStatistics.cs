namespace GameplayAnalysis
{
    /// <summary>1〜32個の有限sampleを再現可能な要約統計へ変換する純粋関数を提供します。</summary>
    public static class SampleStatistics
    {
        /// <summary>1回に評価できる最大sample数です。</summary>
        public const int MaximumSampleCount = 32;

        /// <summary>配列全体から件数・境界値・平均・母分散・母標準偏差を計算します。</summary>
        /// <param name="samples">1〜32個の有限sampleを持つ配列です。</param>
        /// <param name="result">成功時に要約統計を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>全結果を有限値として表現できた場合はtrueです。</returns>
        public static bool TryAnalyze(double[] samples, out SampleStatisticsResult result, out SampleStatisticsError error)
        {
            if (samples == null)
            {
                result = default;
                error = SampleStatisticsError.NullSamples;
                return false;
            }

            return TryAnalyze(samples, 0, samples.Length, out result, out error);
        }

        /// <summary>配列内の明示範囲から件数・境界値・平均・母分散・母標準偏差を計算します。</summary>
        /// <param name="samples">評価元のsample配列です。</param>
        /// <param name="startIndex">評価を始める0以上のindexです。</param>
        /// <param name="count">1以上32以下の評価件数です。</param>
        /// <param name="result">成功時に要約統計を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>全結果を有限値として表現できた場合はtrueです。</returns>
        public static bool TryAnalyze(double[] samples, int startIndex, int count, out SampleStatisticsResult result, out SampleStatisticsError error)
        {
            if (samples == null)
            {
                result = default;
                error = SampleStatisticsError.NullSamples;
                return false;
            }

            if (startIndex < 0)
            {
                result = default;
                error = SampleStatisticsError.InvalidStartIndex;
                return false;
            }

            if (count < 1 || count > MaximumSampleCount)
            {
                result = default;
                error = SampleStatisticsError.InvalidSampleCount;
                return false;
            }

            if (startIndex > samples.Length - count)
            {
                result = default;
                error = SampleStatisticsError.RangeOutOfBounds;
                return false;
            }

            for (var offset = 0; offset < count; offset++)
            {
                var sample = samples[startIndex + offset];
                if (double.IsNaN(sample) || double.IsInfinity(sample))
                {
                    result = default;
                    error = SampleStatisticsError.NonFiniteSample;
                    return false;
                }
            }

            if (!SampleStatisticsMath.TryCalculate(samples, startIndex, count, out result))
            {
                error = SampleStatisticsError.ResultOutOfRange;
                return false;
            }

            error = SampleStatisticsError.None;
            return true;
        }
    }
}
