using System;

namespace GameplayResources
{
    /// <summary>resource IDと有限の非負amountをEvaluatorへ渡す値を表します。</summary>
    public readonly struct ResourceAmount : IEquatable<ResourceAmount>
    {
        /// <summary>resource IDとamountを保持する入力値を作成します。妥当性はEvaluatorが一括検証します。</summary>
        /// <param name="resourceId">正の整数で表すresource IDです。</param>
        /// <param name="amount">有限の非負値で表す残量またはcostです。</param>
        public ResourceAmount(int resourceId, double amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        /// <summary>resourceを識別する正の整数を取得します。</summary>
        public int ResourceId { get; }
        /// <summary>resourceの残量またはcostを取得します。</summary>
        public double Amount { get; }

        /// <summary>resource IDとamountが等しいかを返します。</summary>
        /// <param name="other">比較する入力値です。</param>
        /// <returns>両fieldが等しい場合はtrueです。</returns>
        public bool Equals(ResourceAmount other) => ResourceId == other.ResourceId && Amount.Equals(other.Amount);
        /// <summary>指定objectが同じ入力値かを返します。</summary>
        /// <param name="obj">比較するobjectです。</param>
        /// <returns>同じ入力値である場合はtrueです。</returns>
        public override bool Equals(object obj) => obj is ResourceAmount other && Equals(other);
        /// <summary>resource IDとamountからhash codeを返します。</summary>
        /// <returns>両fieldから計算したhash codeです。</returns>
        public override int GetHashCode() => (ResourceId * 397) ^ Amount.GetHashCode();
        /// <summary>2つの入力値が等しいかを返します。</summary>
        public static bool operator ==(ResourceAmount left, ResourceAmount right) => left.Equals(right);
        /// <summary>2つの入力値が異なるかを返します。</summary>
        public static bool operator !=(ResourceAmount left, ResourceAmount right) => !left.Equals(right);
    }
}
