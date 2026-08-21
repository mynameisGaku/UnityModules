using System;

namespace GameplayTiming
{
    /// <summary>利用可能charge数と回復scheduleを再構築可能に保持します。</summary>
    public readonly struct ChargeCooldownState : IEquatable<ChargeCooldownState>
    {
        internal ChargeCooldownState(int availableCharges, long lastEvaluatedTick, long nextRechargeTick)
        {
            AvailableCharges = availableCharges;
            LastEvaluatedTick = lastEvaluatedTick;
            NextRechargeTick = nextRechargeTick;
        }

        /// <summary>現在利用できるcharge数を取得します。</summary>
        public int AvailableCharges { get; }
        /// <summary>このstateを最後に評価したtickを取得します。</summary>
        public long LastEvaluatedTick { get; }
        /// <summary>次の1 chargeが回復するtickを取得します。満量時は0です。</summary>
        public long NextRechargeTick { get; }
        /// <summary>charge回復中であるかを取得します。</summary>
        public bool IsRecharging => NextRechargeTick != 0;

        /// <summary>2つのstateが同じ内容かを返します。</summary>
        /// <param name="other">比較するstateです。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(ChargeCooldownState other) => AvailableCharges == other.AvailableCharges && LastEvaluatedTick == other.LastEvaluatedTick && NextRechargeTick == other.NextRechargeTick;
        /// <summary>指定したobjectが同じstateかを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じstateである場合はtrueです。</returns>
        public override bool Equals(object obj) => obj is ChargeCooldownState other && Equals(other);
        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked { return (((AvailableCharges * 397) ^ LastEvaluatedTick.GetHashCode()) * 397) ^ NextRechargeTick.GetHashCode(); }
        }
        /// <summary>2つのstateが等しいかを返します。</summary>
        /// <param name="left">左側のstateです。</param>
        /// <param name="right">右側のstateです。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(ChargeCooldownState left, ChargeCooldownState right) => left.Equals(right);
        /// <summary>2つのstateが異なるかを返します。</summary>
        /// <param name="left">左側のstateです。</param>
        /// <param name="right">右側のstateです。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(ChargeCooldownState left, ChargeCooldownState right) => !left.Equals(right);
    }
}
