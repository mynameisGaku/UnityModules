namespace GameplayRules
{
    /// <summary>複数の明示数値条件をstate変更なしで全件評価する純粋関数を提供します。</summary>
    public static class NumericRequirementEvaluator
    {
        /// <summary>1回に評価できる最大条件数です。</summary>
        public const int MaximumRequirementCount = 32;

        /// <summary>数値条件を入力順に検証・評価し、全体結果と全明細を構築します。</summary>
        public static bool TryEvaluate(NumericRequirement[] requirements, out NumericRequirementEvaluation evaluation, out NumericRequirementError error)
        {
            evaluation = null;
            if (requirements == null)
            {
                error = NumericRequirementError.NullRequirements;
                return false;
            }

            if (requirements.Length < 1 || requirements.Length > MaximumRequirementCount)
            {
                error = NumericRequirementError.InvalidRequirementCount;
                return false;
            }

            for (var index = 0; index < requirements.Length; index++)
            {
                var requirement = requirements[index];
                if (requirement.Identifier <= 0)
                {
                    error = NumericRequirementError.InvalidIdentifier;
                    return false;
                }

                if (!IsFinite(requirement.ActualValue) || !IsFinite(requirement.ExpectedValue))
                {
                    error = NumericRequirementError.NonFiniteValue;
                    return false;
                }

                if (!IsDefined(requirement.Comparison))
                {
                    error = NumericRequirementError.InvalidComparison;
                    return false;
                }

                if (!IsFinite(requirement.Tolerance) || requirement.Tolerance < 0d || (!UsesTolerance(requirement.Comparison) && requirement.Tolerance != 0d))
                {
                    error = NumericRequirementError.InvalidTolerance;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (requirements[previous].Identifier != requirement.Identifier) continue;
                    error = NumericRequirementError.DuplicateIdentifier;
                    return false;
                }
            }

            if (!NumericRequirementEvaluationEngine.TryEvaluate(requirements, out evaluation))
            {
                error = NumericRequirementError.ResultOutOfRange;
                return false;
            }

            error = NumericRequirementError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsDefined(NumericRequirementComparison comparison)
        {
            return (int)comparison >= (int)NumericRequirementComparison.AtLeast && (int)comparison <= (int)NumericRequirementComparison.OutsideTolerance;
        }

        private static bool UsesTolerance(NumericRequirementComparison comparison)
        {
            return comparison == NumericRequirementComparison.EqualWithinTolerance || comparison == NumericRequirementComparison.OutsideTolerance;
        }
    }
}
