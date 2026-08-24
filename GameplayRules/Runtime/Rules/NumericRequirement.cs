using System;

namespace GameplayRules
{
    /// <summary>識別子、実値、基準値、比較方法、許容差をEvaluatorへ渡す値を表します。</summary>
    public readonly struct NumericRequirement : IEquatable<NumericRequirement>
    {
        /// <summary>1件の数値条件を作成します。妥当性はEvaluatorが一括検証します。</summary>
        public NumericRequirement(int identifier, double actualValue, double expectedValue, NumericRequirementComparison comparison, double tolerance = 0d)
        {
            Identifier = identifier;
            ActualValue = actualValue;
            ExpectedValue = expectedValue;
            Comparison = comparison;
            Tolerance = tolerance;
        }

        /// <summary>条件を識別する正の整数を取得します。</summary>
        public int Identifier { get; }
        /// <summary>呼び出し側が取得した実値を取得します。</summary>
        public double ActualValue { get; }
        /// <summary>判定基準となる値を取得します。</summary>
        public double ExpectedValue { get; }
        /// <summary>実値と基準値の比較方法を取得します。</summary>
        public NumericRequirementComparison Comparison { get; }
        /// <summary>許容差比較で使う有限の非負値を取得します。大小比較では0である必要があります。</summary>
        public double Tolerance { get; }

        /// <summary>2つの数値条件が全fieldで等しいかを返します。</summary>
        public bool Equals(NumericRequirement other)
        {
            return Identifier == other.Identifier
                && ActualValue.Equals(other.ActualValue)
                && ExpectedValue.Equals(other.ExpectedValue)
                && Comparison == other.Comparison
                && Tolerance.Equals(other.Tolerance);
        }

        /// <summary>指定objectが同じ数値条件かを返します。</summary>
        public override bool Equals(object obj) => obj is NumericRequirement other && Equals(other);
        /// <summary>全fieldからhash codeを返します。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Identifier;
                hash = (hash * 397) ^ ActualValue.GetHashCode();
                hash = (hash * 397) ^ ExpectedValue.GetHashCode();
                hash = (hash * 397) ^ (int)Comparison;
                return (hash * 397) ^ Tolerance.GetHashCode();
            }
        }

        /// <summary>2つの数値条件が等しいかを返します。</summary>
        public static bool operator ==(NumericRequirement left, NumericRequirement right) => left.Equals(right);
        /// <summary>2つの数値条件が異なるかを返します。</summary>
        public static bool operator !=(NumericRequirement left, NumericRequirement right) => !left.Equals(right);
    }
}
