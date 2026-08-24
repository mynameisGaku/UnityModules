using System;

namespace GameplayResources
{
    /// <summary>1 resourceの支払前残量、cost、支払後残量、不足量を再構築可能に保持します。</summary>
    public readonly struct ResourceCostLine : IEquatable<ResourceCostLine>
    {
        internal ResourceCostLine(int resourceId, double availableAmount, double requiredAmount, double remainingAmount, double deficitAmount, bool isAffordable)
        {
            ResourceId = resourceId;
            AvailableAmount = availableAmount;
            RequiredAmount = requiredAmount;
            RemainingAmount = remainingAmount;
            DeficitAmount = deficitAmount;
            IsAffordable = isAffordable;
        }

        /// <summary>resourceを識別する正の整数を取得します。</summary>
        public int ResourceId { get; }
        /// <summary>支払前の残量を取得します。未登録resourceは0です。</summary>
        public double AvailableAmount { get; }
        /// <summary>要求されたcostを取得します。</summary>
        public double RequiredAmount { get; }
        /// <summary>支払可能な場合の支払後残量を取得します。不足時は0です。</summary>
        public double RemainingAmount { get; }
        /// <summary>不足量を取得します。支払可能な場合は0です。</summary>
        public double DeficitAmount { get; }
        /// <summary>このresourceのcostを不足なく支払える場合はtrueです。</summary>
        public bool IsAffordable { get; }

        /// <summary>2つのresource明細が全fieldで等しいかを返します。</summary>
        public bool Equals(ResourceCostLine other)
        {
            return ResourceId == other.ResourceId
                && AvailableAmount.Equals(other.AvailableAmount)
                && RequiredAmount.Equals(other.RequiredAmount)
                && RemainingAmount.Equals(other.RemainingAmount)
                && DeficitAmount.Equals(other.DeficitAmount)
                && IsAffordable == other.IsAffordable;
        }

        /// <summary>指定objectが同じresource明細かを返します。</summary>
        public override bool Equals(object obj) => obj is ResourceCostLine other && Equals(other);
        /// <summary>全fieldからhash codeを返します。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ResourceId;
                hash = (hash * 397) ^ AvailableAmount.GetHashCode();
                hash = (hash * 397) ^ RequiredAmount.GetHashCode();
                hash = (hash * 397) ^ RemainingAmount.GetHashCode();
                hash = (hash * 397) ^ DeficitAmount.GetHashCode();
                return (hash * 397) ^ IsAffordable.GetHashCode();
            }
        }

        /// <summary>2つのresource明細が等しいかを返します。</summary>
        public static bool operator ==(ResourceCostLine left, ResourceCostLine right) => left.Equals(right);
        /// <summary>2つのresource明細が異なるかを返します。</summary>
        public static bool operator !=(ResourceCostLine left, ResourceCostLine right) => !left.Equals(right);
    }
}
