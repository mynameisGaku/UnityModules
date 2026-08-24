using System;

namespace GameplayProgression
{
    /// <summary>評価値に対応する現在tier、次tier、段階内進捗を再構築可能に保持します。</summary>
    public readonly struct ThresholdTierEvaluation : IEquatable<ThresholdTierEvaluation>
    {
        internal ThresholdTierEvaluation(double queryValue, bool hasCurrentTier, int currentTierIndex, ThresholdTier currentTier, bool hasNextTier, ThresholdTier nextTier, double progressToNext)
        {
            QueryValue = queryValue;
            HasCurrentTier = hasCurrentTier;
            CurrentTierIndex = currentTierIndex;
            CurrentTier = currentTier;
            HasNextTier = hasNextTier;
            NextTier = nextTier;
            ProgressToNext = progressToNext;
        }

        /// <summary>評価に使用した有限値を取得します。</summary>
        public double QueryValue { get; }
        /// <summary>現在tierが存在するかを取得します。</summary>
        public bool HasCurrentTier { get; }
        /// <summary>threshold昇順での現在tierのindexを取得します。現在tierが無い場合は-1です。</summary>
        public int CurrentTierIndex { get; }
        /// <summary>現在tierを取得します。存在確認にはHasCurrentTierを使用します。</summary>
        public ThresholdTier CurrentTier { get; }
        /// <summary>現在値より後に到達するtierが存在するかを取得します。</summary>
        public bool HasNextTier { get; }
        /// <summary>次に到達するtierを取得します。存在確認にはHasNextTierを使用します。</summary>
        public ThresholdTier NextTier { get; }
        /// <summary>現在tierから次tierまでの0以上1以下の進捗を取得します。tier未到達時は0、最終tierでは1です。</summary>
        public double ProgressToNext { get; }

        /// <summary>2つの評価結果が全fieldで等しいかを返します。</summary>
        /// <param name="other">比較する評価結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(ThresholdTierEvaluation other)
        {
            return QueryValue.Equals(other.QueryValue)
                && HasCurrentTier == other.HasCurrentTier
                && CurrentTierIndex == other.CurrentTierIndex
                && CurrentTier.Equals(other.CurrentTier)
                && HasNextTier == other.HasNextTier
                && NextTier.Equals(other.NextTier)
                && ProgressToNext.Equals(other.ProgressToNext);
        }

        /// <summary>指定したobjectが同じ評価結果かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ評価結果である場合はtrueです。</returns>
        public override bool Equals(object obj)
        {
            return obj is ThresholdTierEvaluation other && Equals(other);
        }

        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = QueryValue.GetHashCode();
                hash = (hash * 397) ^ HasCurrentTier.GetHashCode();
                hash = (hash * 397) ^ CurrentTierIndex;
                hash = (hash * 397) ^ CurrentTier.GetHashCode();
                hash = (hash * 397) ^ HasNextTier.GetHashCode();
                hash = (hash * 397) ^ NextTier.GetHashCode();
                return (hash * 397) ^ ProgressToNext.GetHashCode();
            }
        }

        /// <summary>2つの評価結果が等しいかを返します。</summary>
        /// <param name="left">左側の評価結果です。</param>
        /// <param name="right">右側の評価結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(ThresholdTierEvaluation left, ThresholdTierEvaluation right)
        {
            return left.Equals(right);
        }

        /// <summary>2つの評価結果が異なるかを返します。</summary>
        /// <param name="left">左側の評価結果です。</param>
        /// <param name="right">右側の評価結果です。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(ThresholdTierEvaluation left, ThresholdTierEvaluation right)
        {
            return !left.Equals(right);
        }
    }
}
