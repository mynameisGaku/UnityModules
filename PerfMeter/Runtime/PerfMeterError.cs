// SPDX-License-Identifier: MIT

namespace PerfMeter
{
    /// <summary>PerfMeterの各操作が報告する失敗理由。</summary>
    public enum PerfMeterError
    {
        /// <summary>成功。失敗はない。</summary>
        None,
        /// <summary>NaNやInfinityなど非有限の数値を入力した。</summary>
        NonFiniteValue,
        /// <summary>負の数値を入力した。</summary>
        NegativeValue,
        /// <summary>容量が1〜65536の許可範囲外。</summary>
        InvalidCapacity,
        /// <summary>percentileが(0,100]の許可範囲外。</summary>
        InvalidPercentile,
        /// <summary>spike閾値が負。</summary>
        InvalidThreshold,
        /// <summary>Dispose後のsamplerを操作した。</summary>
        SamplerDisposed
    }
}
