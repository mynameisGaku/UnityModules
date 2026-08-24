using System;

namespace GameplayStats
{
    /// <summary>modifier変更後のstage合計と最終stat値を表すimmutableな結果。</summary>
    public readonly struct StatModifierEvaluationResult : IEquatable<StatModifierEvaluationResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の変更前stat値。失敗時は0。</summary>
        public double PreviousValue { get; }

        /// <summary>成功時の変更後stat値。失敗時は0。</summary>
        public double CurrentValue { get; }

        /// <summary>成功時の有限base値。失敗時は0。</summary>
        public double BaseValue { get; }

        /// <summary>成功時のFlat modifier合計。失敗時は0。</summary>
        public double FlatTotal { get; }

        /// <summary>成功時のAdditivePercent modifier合計。失敗時は0。</summary>
        public double AdditivePercentTotal { get; }

        /// <summary>成功時のMultiplicativeFactor modifier積。modifierが無い場合は1。</summary>
        public double MultiplicativeFactor { get; }

        /// <summary>成功時のmodifier件数。失敗時は0。</summary>
        public int ModifierCount { get; }

        /// <summary>変更対象または失敗原因となったmodifier ID。base変更とclearでは0。</summary>
        public long AffectedModifierId { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public StatModifierError Error { get; }

        /// <summary>有効な評価結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == StatModifierError.None;

        /// <summary>最終stat値が変化したか。</summary>
        public bool Changed => Succeeded && PreviousValue != CurrentValue;

        private StatModifierEvaluationResult(double previousValue, double currentValue, double baseValue, double flatTotal, double additivePercentTotal, double multiplicativeFactor, int modifierCount, long affectedModifierId, StatModifierError error, bool hasValue)
        {
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            BaseValue = baseValue;
            FlatTotal = flatTotal;
            AdditivePercentTotal = additivePercentTotal;
            MultiplicativeFactor = multiplicativeFactor;
            ModifierCount = modifierCount;
            AffectedModifierId = affectedModifierId;
            Error = error;
            _hasValue = hasValue;
        }

        internal static StatModifierEvaluationResult Success(double previousValue, double currentValue, double baseValue, double flatTotal, double additivePercentTotal, double multiplicativeFactor, int modifierCount, long affectedModifierId) => new StatModifierEvaluationResult(previousValue, currentValue, baseValue, flatTotal, additivePercentTotal, multiplicativeFactor, modifierCount, affectedModifierId, StatModifierError.None, true);

        internal static StatModifierEvaluationResult Failure(StatModifierError error, long affectedModifierId) => new StatModifierEvaluationResult(0d, 0d, 0d, 0d, 0d, 0d, 0, affectedModifierId, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(StatModifierEvaluationResult other) => PreviousValue.Equals(other.PreviousValue) && CurrentValue.Equals(other.CurrentValue) && BaseValue.Equals(other.BaseValue) && FlatTotal.Equals(other.FlatTotal) && AdditivePercentTotal.Equals(other.AdditivePercentTotal) && MultiplicativeFactor.Equals(other.MultiplicativeFactor) && ModifierCount == other.ModifierCount && AffectedModifierId == other.AffectedModifierId && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ結果の場合true。</returns>
        public override bool Equals(object obj) => obj is StatModifierEvaluationResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        /// <returns>全出力と成功状態から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PreviousValue.GetHashCode();
                hash = (hash * 397) ^ CurrentValue.GetHashCode();
                hash = (hash * 397) ^ BaseValue.GetHashCode();
                hash = (hash * 397) ^ FlatTotal.GetHashCode();
                hash = (hash * 397) ^ AdditivePercentTotal.GetHashCode();
                hash = (hash * 397) ^ MultiplicativeFactor.GetHashCode();
                hash = (hash * 397) ^ ModifierCount;
                hash = (hash * 397) ^ AffectedModifierId.GetHashCode();
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public static bool operator ==(StatModifierEvaluationResult left, StatModifierEvaluationResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる結果の場合true。</returns>
        public static bool operator !=(StatModifierEvaluationResult left, StatModifierEvaluationResult right) => !left.Equals(right);
    }
}
