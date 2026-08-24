using System;

namespace GameplayRules
{
    /// <summary>1条件の入力値、符号付き差、絶対差、判定結果を再構築可能に保持します。</summary>
    public readonly struct NumericRequirementLine : IEquatable<NumericRequirementLine>
    {
        internal NumericRequirementLine(NumericRequirement requirement, double delta, double absoluteDelta, bool isSatisfied)
        {
            Identifier = requirement.Identifier;
            ActualValue = requirement.ActualValue;
            ExpectedValue = requirement.ExpectedValue;
            Comparison = requirement.Comparison;
            Tolerance = requirement.Tolerance;
            Delta = delta;
            AbsoluteDelta = absoluteDelta;
            IsSatisfied = isSatisfied;
        }

        /// <summary>条件識別子を取得します。</summary>
        public int Identifier { get; }
        /// <summary>評価した実値を取得します。</summary>
        public double ActualValue { get; }
        /// <summary>評価した基準値を取得します。</summary>
        public double ExpectedValue { get; }
        /// <summary>評価した比較方法を取得します。</summary>
        public NumericRequirementComparison Comparison { get; }
        /// <summary>評価した許容差を取得します。</summary>
        public double Tolerance { get; }
        /// <summary>実値から基準値を引いた符号付き差を取得します。</summary>
        public double Delta { get; }
        /// <summary>符号付き差の絶対値を取得します。</summary>
        public double AbsoluteDelta { get; }
        /// <summary>この条件を満たした場合はtrueです。</summary>
        public bool IsSatisfied { get; }

        /// <summary>2つの条件明細が全fieldで等しいかを返します。</summary>
        public bool Equals(NumericRequirementLine other)
        {
            return Identifier == other.Identifier
                && ActualValue.Equals(other.ActualValue)
                && ExpectedValue.Equals(other.ExpectedValue)
                && Comparison == other.Comparison
                && Tolerance.Equals(other.Tolerance)
                && Delta.Equals(other.Delta)
                && AbsoluteDelta.Equals(other.AbsoluteDelta)
                && IsSatisfied == other.IsSatisfied;
        }

        /// <summary>指定objectが同じ条件明細かを返します。</summary>
        public override bool Equals(object obj) => obj is NumericRequirementLine other && Equals(other);
        /// <summary>全fieldからhash codeを返します。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Identifier;
                hash = (hash * 397) ^ ActualValue.GetHashCode();
                hash = (hash * 397) ^ ExpectedValue.GetHashCode();
                hash = (hash * 397) ^ (int)Comparison;
                hash = (hash * 397) ^ Tolerance.GetHashCode();
                hash = (hash * 397) ^ Delta.GetHashCode();
                hash = (hash * 397) ^ AbsoluteDelta.GetHashCode();
                return (hash * 397) ^ IsSatisfied.GetHashCode();
            }
        }

        /// <summary>2つの条件明細が等しいかを返します。</summary>
        public static bool operator ==(NumericRequirementLine left, NumericRequirementLine right) => left.Equals(right);
        /// <summary>2つの条件明細が異なるかを返します。</summary>
        public static bool operator !=(NumericRequirementLine left, NumericRequirementLine right) => !left.Equals(right);
    }
}
