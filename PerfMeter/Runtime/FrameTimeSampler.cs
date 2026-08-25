// SPDX-License-Identifier: MIT

using System;

namespace PerfMeter
{
    /// <summary>frame timeを有界リングバッファへ蓄えて決定論的な統計を返す純粋計測class。寿命が明確なownerがnewして毎frame AddFrameを呼ぶ。</summary>
    public sealed class FrameTimeSampler : IDisposable
    {
        /// <summary>許可する最小容量。</summary>
        public const int MinimumCapacity = 1;

        /// <summary>許可する最大容量。</summary>
        public const int MaximumCapacity = 65536;

        private readonly double[] _samples;
        private readonly double[] _sortedScratch;
        private readonly int _capacity;
        private int _start;
        private int _count;
        private int _pendingSpikes;
        private int _totalSpikes;
        private double _spikeThresholdSeconds;
        private bool _disposed;

        /// <summary>指定容量のsamplerを作る。内部配列はこの時点でcapacity分だけ確保し、以降のAddFrameでは確保しない。</summary>
        /// <param name="capacityFrames">保持するframe上限。1以上65536以下。</param>
        /// <exception cref="ArgumentOutOfRangeException">capacityFramesが1未満または65536超。</exception>
        public FrameTimeSampler(int capacityFrames)
        {
            if (capacityFrames < MinimumCapacity || capacityFrames > MaximumCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityFrames), capacityFrames, $"capacityFramesは{MinimumCapacity}〜{MaximumCapacity}の範囲で指定してください。");
            }

            _capacity = capacityFrames;
            _samples = new double[capacityFrames];
            _sortedScratch = new double[capacityFrames];
        }

        /// <summary>constructorへ渡した容量。SampleCountの上限でもある。</summary>
        public int Capacity => _capacity;

        /// <summary>現在のwindowが保持するsample件数。容量超過後はCapacityに一致する。</summary>
        public int SampleCount => _count;

        /// <summary>直後に追加されたsample秒。windowが空の場合は0。</summary>
        public double Last => _count == 0 ? 0d : _samples[IndexAt(_count - 1)];

        /// <summary>spike判定の閾値秒。0は判定無効。既定値は0。</summary>
        public double SpikeThresholdSeconds => _spikeThresholdSeconds;

        /// <summary>生成またはReset以降のspike合計回数。閾値0では増加しない。Dispose後は0。</summary>
        public int TotalSpikes => _totalSpikes;

        /// <summary>現在windowの平均frame time秒。oldestからnewestの順に加算して求める。sampleが0件の場合は0。</summary>
        public double Average
        {
            get
            {
                if (_count == 0) return 0d;
                return Sum() / _count;
            }
        }

        /// <summary>現在windowの最小frame time秒。sampleが0件の場合は0。</summary>
        public double Minimum
        {
            get
            {
                if (_count == 0) return 0d;
                var minimum = _samples[_start];
                for (var i = 1; i < _count; i++)
                {
                    var sample = _samples[IndexAt(i)];
                    if (sample < minimum) minimum = sample;
                }

                return minimum;
            }
        }

        /// <summary>現在windowの最大frame time秒。sampleが0件の場合は0。</summary>
        public double Maximum
        {
            get
            {
                if (_count == 0) return 0d;
                var maximum = _samples[_start];
                for (var i = 1; i < _count; i++)
                {
                    var sample = _samples[IndexAt(i)];
                    if (sample > maximum) maximum = sample;
                }

                return maximum;
            }
        }

        /// <summary>現在windowの母標準偏差秒。sqrt(Σ(x-μ)²/n)。sampleが2件未満の場合は0。</summary>
        public double StandardDeviation
        {
            get
            {
                if (_count == 0) return 0d;
                var mean = Sum() / _count;
                var squaredSum = 0d;
                for (var i = 0; i < _count; i++)
                {
                    var deviation = _samples[IndexAt(i)] - mean;
                    squaredSum += deviation * deviation;
                }

                return Math.Sqrt(squaredSum / _count);
            }
        }

        /// <summary>現在windowの中央値frame time秒。偶数件では中央2件の平均。sampleが0件の場合は0。</summary>
        public double Median
        {
            get
            {
                if (_count == 0) return 0d;
                CopySortedToScratch();
                var middle = _count / 2;
                return (_count & 1) == 1 ? _sortedScratch[middle] : (_sortedScratch[middle - 1] + _sortedScratch[middle]) * 0.5d;
            }
        }

        /// <summary>平均dtの逆数fps。dt合計が0の場合、windowが空の場合は0。</summary>
        public double AverageFps
        {
            get
            {
                if (_count == 0) return 0d;
                var sum = Sum();
                return sum <= 0d ? 0d : _count / sum;
            }
        }

        /// <summary>1frame分のdelta timeをwindowへ追加する。容量満杯時は最古のsampleを上書きする。失敗時は状態不変。</summary>
        /// <param name="deltaTimeSeconds">追加するframe time秒。0は同一frame二重計上などの用途で許容する。</param>
        /// <param name="error">成功時None。非有限値ならNonFiniteValue、負値ならNegativeValue、Dispose後ならSamplerDisposed。</param>
        /// <returns>追加に成功したならtrue。</returns>
        public bool AddFrame(double deltaTimeSeconds, out PerfMeterError error)
        {
            if (_disposed)
            {
                error = PerfMeterError.SamplerDisposed;
                return false;
            }

            if (double.IsNaN(deltaTimeSeconds) || double.IsInfinity(deltaTimeSeconds))
            {
                error = PerfMeterError.NonFiniteValue;
                return false;
            }

            if (deltaTimeSeconds < 0d)
            {
                error = PerfMeterError.NegativeValue;
                return false;
            }

            _samples[IndexAt(_count)] = deltaTimeSeconds;
            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _start = (_start + 1) % _capacity;
            }

            if (_spikeThresholdSeconds > 0d && deltaTimeSeconds > _spikeThresholdSeconds)
            {
                _pendingSpikes++;
                _totalSpikes++;
            }

            error = PerfMeterError.None;
            return true;
        }

        /// <summary>spike判定の閾値秒を差し替える。0で判定無効。既に計上済みのspike数は変わらない。</summary>
        /// <param name="seconds">新しい閾値秒。0以上の有限値。</param>
        /// <param name="error">成功時None。非有限値ならNonFiniteValue、負値ならInvalidThreshold、Dispose後ならSamplerDisposed。</param>
        /// <returns>設定に成功したならtrue。</returns>
        public bool SetSpikeThreshold(double seconds, out PerfMeterError error)
        {
            if (_disposed)
            {
                error = PerfMeterError.SamplerDisposed;
                return false;
            }

            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                error = PerfMeterError.NonFiniteValue;
                return false;
            }

            if (seconds < 0d)
            {
                error = PerfMeterError.InvalidThreshold;
                return false;
            }

            _spikeThresholdSeconds = seconds;
            error = PerfMeterError.None;
            return true;
        }

        /// <summary>前回呼出し以降に閾値を超えたframe数を返し、計数を0へ戻す。閾値0では常に0を返す。</summary>
        /// <returns>前回呼出し以降のthreshold超過数。</returns>
        public int SpikesSinceLastCheck()
        {
            var pendingSpikes = _pendingSpikes;
            _pendingSpikes = 0;
            return _spikeThresholdSeconds > 0d ? pendingSpikes : 0;
        }

        /// <summary>現在windowの線形補間percentileを返す。rank=(件数-1)*p/100とし、隣接2件を補間する。p=100はMaximum、p=50はMedianと一致する。sampleが0件の場合は他の統計と同じ正準値0を返す。</summary>
        /// <param name="percentile">求めるpercent。0より大きく100以下。</param>
        /// <param name="value">成功時のpercentile値。失敗時は0。</param>
        /// <param name="error">成功時None。percentileが範囲外ならInvalidPercentile、Dispose後ならSamplerDisposed。</param>
        /// <returns>percentileの取得に成功したならtrue。</returns>
        public bool TryGetPercentile(double percentile, out double value, out PerfMeterError error)
        {
            value = 0d;
            if (_disposed)
            {
                error = PerfMeterError.SamplerDisposed;
                return false;
            }

            if (double.IsNaN(percentile) || double.IsInfinity(percentile) || percentile <= 0d || percentile > 100d)
            {
                error = PerfMeterError.InvalidPercentile;
                return false;
            }

            if (_count == 0)
            {
                error = PerfMeterError.None;
                return true;
            }

            CopySortedToScratch();
            var rank = (_count - 1) * (percentile / 100d);
            var lowerIndex = (int)Math.Floor(rank);
            var lowerValue = _sortedScratch[lowerIndex];
            value = lowerIndex + 1 < _count
                ? lowerValue + (_sortedScratch[lowerIndex + 1] - lowerValue) * (rank - lowerIndex)
                : lowerValue;
            error = PerfMeterError.None;
            return true;
        }

        /// <summary>現在windowの全統計を1つの取得タイミングで揃えて返す。Dispose後は全fieldが正準値のsnapshotになる。</summary>
        /// <returns>同じ時点の統計を持つsnapshot。</returns>
        public FrameTimeSnapshot CreateSnapshot()
        {
            return new FrameTimeSnapshot(Last, Average, Minimum, Maximum, Median, StandardDeviation, SampleCount, AverageFps);
        }

        /// <summary>bufferと統計計数を初期状態へ戻す。容量とspike閾値の設定は保持する。Dispose後は何もしない。</summary>
        public void Reset()
        {
            if (_disposed) return;
            _start = 0;
            _count = 0;
            _pendingSpikes = 0;
            _totalSpikes = 0;
        }

        /// <summary>samplerを廃棄する。以降のAddFrame、SetSpikeThreshold、TryGetPercentileはSamplerDisposedで失敗し、統計は空windowの正準値へ戻る。複数回呼出しても安全。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _start = 0;
            _count = 0;
            _pendingSpikes = 0;
            _totalSpikes = 0;
            _spikeThresholdSeconds = 0d;
        }

        private int IndexAt(int oldestFirstIndex) => (_start + oldestFirstIndex) % _capacity;

        private double Sum()
        {
            var sum = 0d;
            for (var i = 0; i < _count; i++) sum += _samples[IndexAt(i)];
            return sum;
        }

        private void CopySortedToScratch()
        {
            for (var i = 0; i < _count; i++) _sortedScratch[i] = _samples[IndexAt(i)];
            Array.Sort(_sortedScratch, 0, _count);
        }
    }
}
