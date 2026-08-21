using System;

namespace GameplayTiming
{
    /// <summary>1回のadvanceまたはspendによる前後stateと適用内容を保持します。</summary>
    public readonly struct ChargeCooldownResult : IEquatable<ChargeCooldownResult>
    {
        internal ChargeCooldownResult(ChargeCooldownState previousState, ChargeCooldownState state, int chargesRestored, bool chargeSpent)
        {
            PreviousState = previousState;
            State = state;
            ChargesRestored = chargesRestored;
            ChargeSpent = chargeSpent;
        }

        /// <summary>操作前のstateを取得します。</summary>
        public ChargeCooldownState PreviousState { get; }
        /// <summary>操作後のstateを取得します。</summary>
        public ChargeCooldownState State { get; }
        /// <summary>currentTickまでに回復したcharge数を取得します。</summary>
        public int ChargesRestored { get; }
        /// <summary>chargeを1件消費できたかを取得します。</summary>
        public bool ChargeSpent { get; }
        /// <summary>操作後に1件以上のchargeを利用できるかを取得します。</summary>
        public bool IsReady => State.AvailableCharges > 0;

        /// <summary>2つの結果が全fieldで等しいかを返します。</summary>
        /// <param name="other">比較する結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public bool Equals(ChargeCooldownResult other) => PreviousState.Equals(other.PreviousState) && State.Equals(other.State) && ChargesRestored == other.ChargesRestored && ChargeSpent == other.ChargeSpent;
        /// <summary>指定したobjectが同じ結果かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ結果である場合はtrueです。</returns>
        public override bool Equals(object obj) => obj is ChargeCooldownResult other && Equals(other);
        /// <summary>全fieldから決まるhash codeを返します。</summary>
        /// <returns>全fieldから計算したhash codeです。</returns>
        public override int GetHashCode()
        {
            unchecked { return (((PreviousState.GetHashCode() * 397) ^ State.GetHashCode()) * 397 ^ ChargesRestored) * 397 ^ ChargeSpent.GetHashCode(); }
        }
        /// <summary>2つの結果が等しいかを返します。</summary>
        /// <param name="left">左側の結果です。</param>
        /// <param name="right">右側の結果です。</param>
        /// <returns>全fieldが等しい場合はtrueです。</returns>
        public static bool operator ==(ChargeCooldownResult left, ChargeCooldownResult right) => left.Equals(right);
        /// <summary>2つの結果が異なるかを返します。</summary>
        /// <param name="left">左側の結果です。</param>
        /// <param name="right">右側の結果です。</param>
        /// <returns>いずれかのfieldが異なる場合はtrueです。</returns>
        public static bool operator !=(ChargeCooldownResult left, ChargeCooldownResult right) => !left.Equals(right);
    }
}
