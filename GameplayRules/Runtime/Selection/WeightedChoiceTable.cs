using System;

namespace GameplaySelection
{
    /// <summary>最大32件の正weightをID昇順の累積区間へ変換するtable。</summary>
    public sealed class WeightedChoiceTable
    {
        /// <summary>保持できるentry件数の上限。</summary>
        public const int MaximumEntryCount = 32;

        private readonly WeightedChoiceEntry[] _entries = new WeightedChoiceEntry[MaximumEntryCount];
        private int _entryCount;
        private double _totalWeight;

        /// <summary>空のtableを作成する。</summary>
        public WeightedChoiceTable()
        {
        }

        /// <summary>現在のentry件数。</summary>
        public int EntryCount => _entryCount;

        /// <summary>ID昇順で加算した現在の有限weight合計。</summary>
        public double TotalWeight => _totalWeight;

        /// <summary>正のIDと有限の正weightを追加する。重複ID、上限到達、合計overflowでは状態を変えない。</summary>
        public WeightedChoiceChangeResult Add(int identifier, double weight)
        {
            var validation = ValidateIdentityAndWeight(identifier, weight);
            if (validation != WeightedChoiceError.None) return Failure(validation, identifier, 0d, weight);

            var foundIndex = FindIndex(identifier);
            if (foundIndex >= 0) return Failure(WeightedChoiceError.DuplicateIdentifier, identifier, _entries[foundIndex].Weight, weight);
            if (_entryCount >= MaximumEntryCount) return Failure(WeightedChoiceError.CapacityReached, identifier, 0d, weight);

            var insertionIndex = ~foundIndex;
            if (!TryCalculateTotalAfterAdd(insertionIndex, weight, out var candidateTotal)) return Failure(WeightedChoiceError.NumericOverflow, identifier, 0d, weight);

            var previousTotal = _totalWeight;
            var previousCount = _entryCount;
            for (var index = _entryCount; index > insertionIndex; index--) _entries[index] = _entries[index - 1];
            _entries[insertionIndex] = new WeightedChoiceEntry(identifier, weight);
            _entryCount++;
            _totalWeight = candidateTotal;
            return new WeightedChoiceChangeResult(true, true, WeightedChoiceError.None, identifier, 0d, weight, previousTotal, _totalWeight, previousCount, _entryCount);
        }

        /// <summary>既存IDのweightを更新する。無効値または合計overflowでは状態を変えない。</summary>
        public WeightedChoiceChangeResult Update(int identifier, double weight)
        {
            var validation = ValidateIdentityAndWeight(identifier, weight);
            if (validation != WeightedChoiceError.None) return Failure(validation, identifier, 0d, weight);

            var index = FindIndex(identifier);
            if (index < 0) return Failure(WeightedChoiceError.EntryNotFound, identifier, 0d, weight);
            var previousWeight = _entries[index].Weight;
            if (previousWeight.Equals(weight)) return new WeightedChoiceChangeResult(true, false, WeightedChoiceError.None, identifier, previousWeight, weight, _totalWeight, _totalWeight, _entryCount, _entryCount);
            if (!TryCalculateTotalAfterUpdate(index, weight, out var candidateTotal)) return Failure(WeightedChoiceError.NumericOverflow, identifier, previousWeight, weight);

            var previousTotal = _totalWeight;
            _entries[index] = new WeightedChoiceEntry(identifier, weight);
            _totalWeight = candidateTotal;
            return new WeightedChoiceChangeResult(true, true, WeightedChoiceError.None, identifier, previousWeight, weight, previousTotal, _totalWeight, _entryCount, _entryCount);
        }

        /// <summary>既存IDのentryを除去する。IDが無効または未登録なら状態を変えない。</summary>
        public WeightedChoiceChangeResult Remove(int identifier)
        {
            if (identifier <= 0) return Failure(WeightedChoiceError.InvalidIdentifier, identifier, 0d, 0d);
            var index = FindIndex(identifier);
            if (index < 0) return Failure(WeightedChoiceError.EntryNotFound, identifier, 0d, 0d);

            var previousWeight = _entries[index].Weight;
            if (!TryCalculateTotalAfterRemove(index, out var candidateTotal)) return Failure(WeightedChoiceError.NumericOverflow, identifier, previousWeight, 0d);
            var previousTotal = _totalWeight;
            var previousCount = _entryCount;
            for (var current = index; current + 1 < _entryCount; current++) _entries[current] = _entries[current + 1];
            _entryCount--;
            _entries[_entryCount] = default;
            _totalWeight = candidateTotal;
            return new WeightedChoiceChangeResult(true, true, WeightedChoiceError.None, identifier, previousWeight, 0d, previousTotal, _totalWeight, previousCount, _entryCount);
        }

        /// <summary>全entryを除去し、weight合計を0へ戻す。</summary>
        public WeightedChoiceChangeResult Clear()
        {
            var previousTotal = _totalWeight;
            var previousCount = _entryCount;
            if (_entryCount == 0) return new WeightedChoiceChangeResult(true, false, WeightedChoiceError.None, 0, 0d, 0d, 0d, 0d, 0, 0);
            Array.Clear(_entries, 0, _entryCount);
            _entryCount = 0;
            _totalWeight = 0d;
            return new WeightedChoiceChangeResult(true, true, WeightedChoiceError.None, 0, 0d, 0d, previousTotal, 0d, previousCount, 0);
        }

        /// <summary>ID昇順のindexからentryを取得する。範囲外ではfalseを返す。</summary>
        public bool TryGetEntryAt(int index, out WeightedChoiceEntry entry, out WeightedChoiceError error)
        {
            if (index < 0 || index >= _entryCount)
            {
                entry = default;
                error = WeightedChoiceError.IndexOutOfRange;
                return false;
            }

            entry = _entries[index];
            error = WeightedChoiceError.None;
            return true;
        }

        /// <summary>IDからentryを取得する。IDが無効または未登録ならfalseを返す。</summary>
        public bool TryGetEntry(int identifier, out WeightedChoiceEntry entry, out WeightedChoiceError error)
        {
            if (identifier <= 0)
            {
                entry = default;
                error = WeightedChoiceError.InvalidIdentifier;
                return false;
            }

            var index = FindIndex(identifier);
            if (index < 0)
            {
                entry = default;
                error = WeightedChoiceError.EntryNotFound;
                return false;
            }

            entry = _entries[index];
            error = WeightedChoiceError.None;
            return true;
        }

        /// <summary>0以上1未満のsampleを現在の累積weight区間へ写し、該当entryを返す。</summary>
        public WeightedChoiceSelectionResult Select(double normalizedSample)
        {
            if (!IsFinite(normalizedSample) || normalizedSample < 0d || normalizedSample >= 1d)
                return SelectionFailure(WeightedChoiceError.InvalidSample, 0d);
            if (_entryCount == 0) return SelectionFailure(WeightedChoiceError.EmptyTable, normalizedSample);

            var ticket = normalizedSample * _totalWeight;
            if (ticket >= _totalWeight) ticket = PreviousRepresentable(_totalWeight);
            var intervalStart = 0d;
            for (var index = 0; index < _entryCount; index++)
            {
                var entry = _entries[index];
                var intervalEnd = intervalStart + entry.Weight;
                if (ticket < intervalEnd || index == _entryCount - 1)
                    return new WeightedChoiceSelectionResult(true, WeightedChoiceError.None, normalizedSample, ticket, entry.Identifier, index, entry.Weight, intervalStart, intervalEnd, _totalWeight);
                intervalStart = intervalEnd;
            }

            return SelectionFailure(WeightedChoiceError.NumericOverflow, normalizedSample);
        }

        private WeightedChoiceChangeResult Failure(WeightedChoiceError error, int identifier, double previousWeight, double currentWeight) =>
            new WeightedChoiceChangeResult(false, false, error, identifier, previousWeight, currentWeight, _totalWeight, _totalWeight, _entryCount, _entryCount);

        private WeightedChoiceSelectionResult SelectionFailure(WeightedChoiceError error, double normalizedSample) =>
            new WeightedChoiceSelectionResult(false, error, normalizedSample, 0d, 0, -1, 0d, 0d, 0d, _totalWeight);

        private static WeightedChoiceError ValidateIdentityAndWeight(int identifier, double weight)
        {
            if (identifier <= 0) return WeightedChoiceError.InvalidIdentifier;
            return !IsFinite(weight) || weight <= 0d ? WeightedChoiceError.InvalidWeight : WeightedChoiceError.None;
        }

        private bool TryCalculateTotalAfterAdd(int insertionIndex, double weight, out double total)
        {
            total = 0d;
            var existingIndex = 0;
            for (var resultIndex = 0; resultIndex <= _entryCount; resultIndex++)
            {
                var nextWeight = resultIndex == insertionIndex ? weight : _entries[existingIndex++].Weight;
                if (!TryAddFinite(ref total, nextWeight)) return false;
            }
            return total > 0d;
        }

        private bool TryCalculateTotalAfterUpdate(int updateIndex, double weight, out double total)
        {
            total = 0d;
            for (var index = 0; index < _entryCount; index++)
            {
                var nextWeight = index == updateIndex ? weight : _entries[index].Weight;
                if (!TryAddFinite(ref total, nextWeight)) return false;
            }
            return total > 0d;
        }

        private bool TryCalculateTotalAfterRemove(int removeIndex, out double total)
        {
            total = 0d;
            for (var index = 0; index < _entryCount; index++)
            {
                if (index == removeIndex) continue;
                if (!TryAddFinite(ref total, _entries[index].Weight)) return false;
            }
            return _entryCount == 1 ? total == 0d : total > 0d;
        }

        private static bool TryAddFinite(ref double total, double weight)
        {
            var candidate = total + weight;
            if (!IsFinite(candidate)) return false;
            total = candidate;
            return true;
        }

        private int FindIndex(int identifier)
        {
            var lower = 0;
            var upper = _entryCount - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) / 2);
                var middleIdentifier = _entries[middle].Identifier;
                if (middleIdentifier == identifier) return middle;
                if (middleIdentifier < identifier) lower = middle + 1;
                else upper = middle - 1;
            }
            return ~lower;
        }

        private static double PreviousRepresentable(double value) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1L);

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
