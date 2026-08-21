using System;

namespace GameplayTiming
{
    /// <summary>Charge Cooldownの最大charge数と1 chargeの回復間隔を保持します。</summary>
    public readonly struct ChargeCooldownRules : IEquatable<ChargeCooldownRules>
    {
        internal ChargeCooldownRules(int maximumCharges, long rechargeIntervalTicks)
        {
            MaximumCharges = maximumCharges;
            RechargeIntervalTicks = rechargeIntervalTicks;
        }

        /// <summary>保持できる最大charge数を取得します。</summary>
        public int MaximumCharges { get; }
        /// <summary>1 chargeを回復するために必要なtick数を取得します。</summary>
        public long RechargeIntervalTicks { get; }

        /// <summary>2つのrulesが同じ設定かを返します。</summary>
        /// <param name="other">比較するrulesです。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(ChargeCooldownRules other) => MaximumCharges == other.MaximumCharges && RechargeIntervalTicks == other.RechargeIntervalTicks;
        /// <summary>指定したobjectが同じrulesかを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じrulesである場合はtrueです。</returns>
        public override bool Equals(object obj) => obj is ChargeCooldownRules other && Equals(other);
        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked { return (MaximumCharges * 397) ^ RechargeIntervalTicks.GetHashCode(); }
        }
        /// <summary>2つのrulesが等しいかを返します。</summary>
        /// <param name="left">左側のrulesです。</param>
        /// <param name="right">右側のrulesです。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(ChargeCooldownRules left, ChargeCooldownRules right) => left.Equals(right);
        /// <summary>2つのrulesが異なるかを返します。</summary>
        /// <param name="left">左側のrulesです。</param>
        /// <param name="right">右側のrulesです。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(ChargeCooldownRules left, ChargeCooldownRules right) => !left.Equals(right);
    }
}
