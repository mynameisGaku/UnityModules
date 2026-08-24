using System;

namespace GameplayStats
{
    /// <summary>ID昇順の有限modifierを3 stageで合成し、常に有限な現在stat値を所有する純粋stack。</summary>
    public sealed class StatModifierStack
    {
        /// <summary>1 stackが保持できるmodifierの最大件数。</summary>
        public const int MaximumModifierCount = 32;

        private readonly StatModifier[] _modifiers = new StatModifier[MaximumModifierCount];
        private int _count;

        /// <summary>modifier適用前の有限base値。</summary>
        public double BaseValue { get; private set; }

        /// <summary>全modifier適用後の有限stat値。</summary>
        public double CurrentValue { get; private set; }

        /// <summary>Flat modifierの現在合計。</summary>
        public double FlatTotal { get; private set; }

        /// <summary>AdditivePercent modifierの現在合計。</summary>
        public double AdditivePercentTotal { get; private set; }

        /// <summary>MultiplicativeFactor modifierの現在積。</summary>
        public double MultiplicativeFactor { get; private set; }

        /// <summary>現在保持するmodifier件数。</summary>
        public int ModifierCount => _count;

        private StatModifierStack(double baseValue)
        {
            BaseValue = baseValue;
            CurrentValue = baseValue;
            MultiplicativeFactor = 1d;
        }

        /// <summary>有限base値を検証して空のstackを作成する。</summary>
        /// <param name="baseValue">modifier適用前の有限値。</param>
        /// <param name="stack">成功時のstack。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時はbase値error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(double baseValue, out StatModifierStack stack, out StatModifierError error)
        {
            if (!IsFinite(baseValue))
            {
                stack = null;
                error = StatModifierError.NonFiniteBaseValue;
                return false;
            }

            stack = new StatModifierStack(NormalizeZero(baseValue));
            error = StatModifierError.None;
            return true;
        }

        /// <summary>一意な正のIDを持つmodifierを追加する。</summary>
        /// <param name="id">callerが割り当てる正の一意ID。</param>
        /// <param name="kind">値を適用するstage。</param>
        /// <param name="value">stageへ適用する有限値。</param>
        /// <returns>成功時は更新後評価。失敗時はstateを変えず明示error。</returns>
        public StatModifierEvaluationResult Add(long id, StatModifierKind kind, double value)
        {
            if (!TryValidateModifier(id, kind, value, out var error)) return StatModifierEvaluationResult.Failure(error, id);
            var index = FindIndex(id, out var found);
            if (found) return StatModifierEvaluationResult.Failure(StatModifierError.DuplicateModifierId, id);
            if (_count == MaximumModifierCount) return StatModifierEvaluationResult.Failure(StatModifierError.CapacityReached, id);

            var previous = CurrentValue;
            for (var move = _count; move > index; move--) _modifiers[move] = _modifiers[move - 1];
            _modifiers[index] = new StatModifier(id, kind, NormalizeZero(value));
            _count++;
            if (TryEvaluate(BaseValue, out var evaluation)) return Commit(previous, evaluation, id);

            _count--;
            for (var move = index; move < _count; move++) _modifiers[move] = _modifiers[move + 1];
            _modifiers[_count] = default;
            return StatModifierEvaluationResult.Failure(StatModifierError.ResultNotFinite, id);
        }

        /// <summary>既存IDのkindと値を置き換える。</summary>
        /// <param name="id">更新する正のID。</param>
        /// <param name="kind">新しい適用stage。</param>
        /// <param name="value">新しい有限値。</param>
        /// <returns>成功時は更新後評価。失敗時はstateを変えず明示error。</returns>
        public StatModifierEvaluationResult Update(long id, StatModifierKind kind, double value)
        {
            if (!TryValidateModifier(id, kind, value, out var error)) return StatModifierEvaluationResult.Failure(error, id);
            var index = FindIndex(id, out var found);
            if (!found) return StatModifierEvaluationResult.Failure(StatModifierError.ModifierNotFound, id);

            var previousModifier = _modifiers[index];
            var previous = CurrentValue;
            _modifiers[index] = new StatModifier(id, kind, NormalizeZero(value));
            if (TryEvaluate(BaseValue, out var evaluation)) return Commit(previous, evaluation, id);
            _modifiers[index] = previousModifier;
            return StatModifierEvaluationResult.Failure(StatModifierError.ResultNotFinite, id);
        }

        /// <summary>既存IDのmodifierを除去する。</summary>
        /// <param name="id">除去する正のID。</param>
        /// <returns>成功時は更新後評価。失敗時はstateを変えず明示error。</returns>
        public StatModifierEvaluationResult Remove(long id)
        {
            if (id <= 0) return StatModifierEvaluationResult.Failure(StatModifierError.InvalidModifierId, id);
            var index = FindIndex(id, out var found);
            if (!found) return StatModifierEvaluationResult.Failure(StatModifierError.ModifierNotFound, id);

            var removed = _modifiers[index];
            var previous = CurrentValue;
            _count--;
            for (var move = index; move < _count; move++) _modifiers[move] = _modifiers[move + 1];
            _modifiers[_count] = default;
            if (TryEvaluate(BaseValue, out var evaluation)) return Commit(previous, evaluation, id);

            for (var move = _count; move > index; move--) _modifiers[move] = _modifiers[move - 1];
            _modifiers[index] = removed;
            _count++;
            return StatModifierEvaluationResult.Failure(StatModifierError.ResultNotFinite, id);
        }

        /// <summary>base値だけを変更して現在modifierを再評価する。</summary>
        /// <param name="baseValue">新しい有限base値。</param>
        /// <returns>成功時は更新後評価。失敗時はstateを変えず明示error。</returns>
        public StatModifierEvaluationResult SetBaseValue(double baseValue)
        {
            if (!IsFinite(baseValue)) return StatModifierEvaluationResult.Failure(StatModifierError.NonFiniteBaseValue, 0);
            baseValue = NormalizeZero(baseValue);
            if (!TryEvaluate(baseValue, out var evaluation)) return StatModifierEvaluationResult.Failure(StatModifierError.ResultNotFinite, 0);
            var previous = CurrentValue;
            BaseValue = baseValue;
            return Commit(previous, evaluation, 0);
        }

        /// <summary>全modifierを除去してbase値へ戻す。</summary>
        /// <returns>modifier除去後の評価。</returns>
        public StatModifierEvaluationResult Clear()
        {
            var previous = CurrentValue;
            Array.Clear(_modifiers, 0, _count);
            _count = 0;
            var evaluation = new Evaluation(BaseValue, 0d, 0d, 1d);
            return Commit(previous, evaluation, 0);
        }

        /// <summary>ID昇順indexからmodifier snapshotを取得する。</summary>
        /// <param name="index">0以上ModifierCount未満のindex。</param>
        /// <param name="modifier">成功時のmodifier。失敗時はdefault。</param>
        /// <returns>indexが範囲内の場合true。</returns>
        public bool TryGetModifierAt(int index, out StatModifier modifier)
        {
            if (index < 0 || index >= _count)
            {
                modifier = default;
                return false;
            }

            modifier = _modifiers[index];
            return true;
        }

        /// <summary>IDからmodifier snapshotを取得する。</summary>
        /// <param name="id">検索する正のID。</param>
        /// <param name="modifier">成功時のmodifier。失敗時はdefault。</param>
        /// <returns>IDが存在する場合true。</returns>
        public bool TryGetModifier(long id, out StatModifier modifier)
        {
            var index = FindIndex(id, out var found);
            modifier = found ? _modifiers[index] : default;
            return found;
        }

        private StatModifierEvaluationResult Commit(double previous, Evaluation evaluation, long affectedModifierId)
        {
            CurrentValue = evaluation.CurrentValue;
            FlatTotal = evaluation.FlatTotal;
            AdditivePercentTotal = evaluation.AdditivePercentTotal;
            MultiplicativeFactor = evaluation.MultiplicativeFactor;
            return StatModifierEvaluationResult.Success(previous, CurrentValue, BaseValue, FlatTotal, AdditivePercentTotal, MultiplicativeFactor, _count, affectedModifierId);
        }

        private bool TryEvaluate(double baseValue, out Evaluation evaluation)
        {
            var flat = 0d;
            var additivePercent = 0d;
            var multiplicative = 1d;
            for (var index = 0; index < _count; index++)
            {
                var modifier = _modifiers[index];
                switch (modifier.Kind)
                {
                    case StatModifierKind.Flat:
                        flat += modifier.Value;
                        if (!IsFinite(flat)) return Fail(out evaluation);
                        break;
                    case StatModifierKind.AdditivePercent:
                        additivePercent += modifier.Value;
                        if (!IsFinite(additivePercent)) return Fail(out evaluation);
                        break;
                    case StatModifierKind.MultiplicativeFactor:
                        multiplicative *= modifier.Value;
                        if (!IsFinite(multiplicative)) return Fail(out evaluation);
                        break;
                    default:
                        return Fail(out evaluation);
                }
            }

            var afterFlat = baseValue + flat;
            var additiveFactor = 1d + additivePercent;
            if (!IsFinite(afterFlat) || !IsFinite(additiveFactor)) return Fail(out evaluation);
            var afterPercent = afterFlat * additiveFactor;
            if (!IsFinite(afterPercent)) return Fail(out evaluation);
            var current = afterPercent * multiplicative;
            if (!IsFinite(current)) return Fail(out evaluation);
            evaluation = new Evaluation(NormalizeZero(current), NormalizeZero(flat), NormalizeZero(additivePercent), NormalizeZero(multiplicative));
            return true;
        }

        private int FindIndex(long id, out bool found)
        {
            var low = 0;
            var high = _count - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) >> 1);
                var current = _modifiers[middle].Id;
                if (current == id)
                {
                    found = true;
                    return middle;
                }

                if (current < id) low = middle + 1;
                else high = middle - 1;
            }

            found = false;
            return low;
        }

        private static bool TryValidateModifier(long id, StatModifierKind kind, double value, out StatModifierError error)
        {
            if (id <= 0)
            {
                error = StatModifierError.InvalidModifierId;
                return false;
            }

            if (kind != StatModifierKind.Flat && kind != StatModifierKind.AdditivePercent && kind != StatModifierKind.MultiplicativeFactor)
            {
                error = StatModifierError.InvalidModifierKind;
                return false;
            }

            if (!IsFinite(value))
            {
                error = StatModifierError.NonFiniteModifierValue;
                return false;
            }

            error = StatModifierError.None;
            return true;
        }

        private static bool Fail(out Evaluation evaluation)
        {
            evaluation = default;
            return false;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double NormalizeZero(double value) => value == 0d ? 0d : value;

        private readonly struct Evaluation
        {
            internal readonly double CurrentValue;
            internal readonly double FlatTotal;
            internal readonly double AdditivePercentTotal;
            internal readonly double MultiplicativeFactor;

            internal Evaluation(double currentValue, double flatTotal, double additivePercentTotal, double multiplicativeFactor)
            {
                CurrentValue = currentValue;
                FlatTotal = flatTotal;
                AdditivePercentTotal = additivePercentTotal;
                MultiplicativeFactor = multiplicativeFactor;
            }
        }
    }
}
