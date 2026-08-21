using System;

namespace GameplayProgression
{
    /// <summary>有限thresholdを昇順に保持し、評価値を現在tierと次tierまでの進捗へ変換します。</summary>
    public sealed class ThresholdTierTable
    {
        /// <summary>1つのtableへ登録できる最大tier数です。</summary>
        public const int MaximumTierCount = 32;

        private readonly ThresholdTier[] _tiers;
        private int _count;

        private ThresholdTierTable(int capacity)
        {
            _tiers = new ThresholdTier[capacity];
        }

        /// <summary>登録可能なtier数を取得します。</summary>
        public int Capacity => _tiers.Length;
        /// <summary>現在登録されているtier数を取得します。</summary>
        public int Count => _count;

        /// <summary>1以上32以下の容量から空のtableを作成します。</summary>
        /// <param name="capacity">登録可能なtier数です。</param>
        /// <param name="table">成功時に作成されたtableを返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>作成できた場合はtrueです。</returns>
        public static bool TryCreate(int capacity, out ThresholdTierTable table, out ThresholdTierError error)
        {
            if (capacity < 1 || capacity > MaximumTierCount)
            {
                table = null;
                error = ThresholdTierError.InvalidCapacity;
                return false;
            }

            table = new ThresholdTierTable(capacity);
            error = ThresholdTierError.None;
            return true;
        }

        /// <summary>正のIDと有限thresholdを持つtierをthreshold昇順の位置へ追加します。</summary>
        /// <param name="tierId">重複しない正のtier IDです。</param>
        /// <param name="minimumValue">このtierが始まるinclusiveな有限値です。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>追加できた場合はtrueです。</returns>
        public bool TryAddTier(int tierId, double minimumValue, out ThresholdTierError error)
        {
            if (tierId <= 0)
            {
                error = ThresholdTierError.InvalidTierId;
                return false;
            }

            if (!IsFinite(minimumValue))
            {
                error = ThresholdTierError.InvalidMinimumValue;
                return false;
            }

            for (var index = 0; index < _count; index++)
            {
                if (_tiers[index].Id == tierId)
                {
                    error = ThresholdTierError.DuplicateTierId;
                    return false;
                }

                if (_tiers[index].MinimumValue.Equals(minimumValue))
                {
                    error = ThresholdTierError.DuplicateMinimumValue;
                    return false;
                }
            }

            if (_count == Capacity)
            {
                error = ThresholdTierError.CapacityExceeded;
                return false;
            }

            var insertionIndex = 0;
            while (insertionIndex < _count && _tiers[insertionIndex].MinimumValue < minimumValue) insertionIndex++;
            for (var index = _count; index > insertionIndex; index--) _tiers[index] = _tiers[index - 1];
            _tiers[insertionIndex] = new ThresholdTier(tierId, minimumValue);
            _count++;
            error = ThresholdTierError.None;
            return true;
        }

        /// <summary>指定IDのtierを削除し、後続tierを詰めます。</summary>
        /// <param name="tierId">削除する正のtier IDです。</param>
        /// <param name="removedTier">成功時に削除されたtierを返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>削除できた場合はtrueです。</returns>
        public bool TryRemoveTier(int tierId, out ThresholdTier removedTier, out ThresholdTierError error)
        {
            if (tierId <= 0)
            {
                removedTier = default;
                error = ThresholdTierError.InvalidTierId;
                return false;
            }

            var removalIndex = -1;
            for (var index = 0; index < _count; index++)
            {
                if (_tiers[index].Id == tierId)
                {
                    removalIndex = index;
                    break;
                }
            }

            if (removalIndex < 0)
            {
                removedTier = default;
                error = ThresholdTierError.TierNotFound;
                return false;
            }

            removedTier = _tiers[removalIndex];
            for (var index = removalIndex; index < _count - 1; index++) _tiers[index] = _tiers[index + 1];
            _count--;
            _tiers[_count] = default;
            error = ThresholdTierError.None;
            return true;
        }

        /// <summary>threshold昇順のindexからtierを取得します。</summary>
        /// <param name="index">0以上Count未満のindexです。</param>
        /// <param name="tier">成功時に対応tierを返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>取得できた場合はtrueです。</returns>
        public bool TryGetTierAt(int index, out ThresholdTier tier, out ThresholdTierError error)
        {
            if (index < 0 || index >= _count)
            {
                tier = default;
                error = ThresholdTierError.IndexOutOfRange;
                return false;
            }

            tier = _tiers[index];
            error = ThresholdTierError.None;
            return true;
        }

        /// <summary>有限値を現在tier、次tier、0以上1以下の段階内進捗へ評価します。</summary>
        /// <param name="value">評価する有限値です。</param>
        /// <param name="evaluation">成功時に再構築可能な評価結果を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>評価できた場合はtrueです。</returns>
        public bool TryEvaluate(double value, out ThresholdTierEvaluation evaluation, out ThresholdTierError error)
        {
            if (!IsFinite(value))
            {
                evaluation = default;
                error = ThresholdTierError.InvalidQueryValue;
                return false;
            }

            if (_count == 0)
            {
                evaluation = default;
                error = ThresholdTierError.TableEmpty;
                return false;
            }

            var low = 0;
            var high = _count - 1;
            var currentIndex = -1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                if (_tiers[middle].MinimumValue <= value)
                {
                    currentIndex = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (currentIndex < 0)
            {
                evaluation = new ThresholdTierEvaluation(value, false, -1, default, true, _tiers[0], 0d);
                error = ThresholdTierError.None;
                return true;
            }

            var current = _tiers[currentIndex];
            var nextIndex = currentIndex + 1;
            if (nextIndex >= _count)
            {
                evaluation = new ThresholdTierEvaluation(value, true, currentIndex, current, false, default, 1d);
                error = ThresholdTierError.None;
                return true;
            }

            var next = _tiers[nextIndex];
            var progress = ThresholdTierMath.InverseLerp(current.MinimumValue, next.MinimumValue, value);
            evaluation = new ThresholdTierEvaluation(value, true, currentIndex, current, true, next, progress);
            error = ThresholdTierError.None;
            return true;
        }

        /// <summary>全tierを削除し、容量を維持した空tableへ戻します。</summary>
        public void Clear()
        {
            Array.Clear(_tiers, 0, _count);
            _count = 0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
