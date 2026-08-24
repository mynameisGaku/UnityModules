using System;

namespace GameplayMetrics
{
    /// <summary>最大32件の有限sampleをoldest-first FIFO窓として保持し、再現可能な統計を返す。</summary>
    public sealed class RollingSampleWindow
    {
        /// <summary>許可する最大容量。</summary>
        public const int MaximumCapacity = 32;

        private readonly double[] _samples;
        private int _start;
        private int _count;

        private RollingSampleWindow(int capacity)
        {
            _samples = new double[capacity];
        }

        /// <summary>窓が保持できるsample上限。</summary>
        public int Capacity => _samples.Length;

        /// <summary>現在保持するsample件数。</summary>
        public int Count => _count;

        /// <summary>現在の件数・境界値・平均・oldest/newestを返す。</summary>
        public SampleWindowSnapshot Snapshot => RollingSampleStatistics.CreateSnapshot(_samples, _start, _count);

        /// <summary>指定容量の空窓を作成する。</summary>
        /// <param name="capacity">1以上32以下の固定容量。</param>
        /// <param name="window">成功時の空窓。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時InvalidCapacity。</param>
        /// <returns>作成に成功したならtrue。</returns>
        public static bool TryCreate(int capacity, out RollingSampleWindow window, out SampleWindowError error)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
            {
                window = null;
                error = SampleWindowError.InvalidCapacity;
                return false;
            }

            window = new RollingSampleWindow(capacity);
            error = SampleWindowError.None;
            return true;
        }

        /// <summary>有限sampleをnewestとして追加し、満杯ならoldestを1件退避する。</summary>
        /// <param name="sample">追加する有限sample。</param>
        /// <returns>追加値、退避値、前後snapshotを持つ結果。非有限値では状態不変。</returns>
        public SampleWindowAddResult Add(double sample)
        {
            var previous = Snapshot;
            if (!IsFinite(sample)) return SampleWindowAddResult.Failure(SampleWindowError.InvalidSample, sample, previous);

            var hadEviction = _count == Capacity;
            var evicted = hadEviction ? _samples[_start] : 0d;
            if (hadEviction)
            {
                _samples[_start] = sample;
                _start = (_start + 1) % Capacity;
            }
            else
            {
                _samples[(_start + _count) % Capacity] = sample;
                _count++;
            }

            return SampleWindowAddResult.Success(sample, hadEviction, evicted, previous, Snapshot);
        }

        /// <summary>全sampleを除去し、同じ容量の空窓へ戻す。</summary>
        public void Clear()
        {
            Array.Clear(_samples, 0, _samples.Length);
            _start = 0;
            _count = 0;
        }

        /// <summary>現在のsampleをoldest-first indexで取得する。</summary>
        /// <param name="oldestFirstIndex">0がoldestとなるindex。</param>
        /// <param name="sample">成功時の有限sample。失敗時は0。</param>
        /// <param name="error">成功時None、範囲外ならIndexOutOfRange。</param>
        /// <returns>取得に成功したならtrue。</returns>
        public bool TryGetSampleAt(int oldestFirstIndex, out double sample, out SampleWindowError error)
        {
            if (oldestFirstIndex < 0 || oldestFirstIndex >= _count)
            {
                sample = 0d;
                error = SampleWindowError.IndexOutOfRange;
                return false;
            }

            sample = _samples[(_start + oldestFirstIndex) % Capacity];
            error = SampleWindowError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
