using System;

namespace GameplayProgression
{
    /// <summary>段階を識別する正のIDと、その段階が始まる有限thresholdを表します。</summary>
    public readonly struct ThresholdTier : IEquatable<ThresholdTier>
    {
        internal ThresholdTier(int id, double minimumValue)
        {
            Id = id;
            MinimumValue = minimumValue;
        }

        /// <summary>段階を識別する正の値を取得します。</summary>
        public int Id { get; }

        /// <summary>この段階を選択するinclusiveな最小値を取得します。</summary>
        public double MinimumValue { get; }

        /// <summary>2つの段階が同じIDとthresholdを持つかを返します。</summary>
        /// <param name="other">比較する段階です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(ThresholdTier other)
        {
            return Id == other.Id && MinimumValue.Equals(other.MinimumValue);
        }

        /// <summary>指定したobjectが同じ段階かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ段階である場合はtrueです。</returns>
        public override bool Equals(object obj)
        {
            return obj is ThresholdTier other && Equals(other);
        }

        /// <summary>IDとthresholdから決まるhash codeを返します。</summary>
        /// <returns>IDとthresholdから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ MinimumValue.GetHashCode();
            }
        }

        /// <summary>2つの段階が等しいかを返します。</summary>
        /// <param name="left">左側の段階です。</param>
        /// <param name="right">右側の段階です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(ThresholdTier left, ThresholdTier right)
        {
            return left.Equals(right);
        }

        /// <summary>2つの段階が異なるかを返します。</summary>
        /// <param name="left">左側の段階です。</param>
        /// <param name="right">右側の段階です。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(ThresholdTier left, ThresholdTier right)
        {
            return !left.Equals(right);
        }
    }
}
