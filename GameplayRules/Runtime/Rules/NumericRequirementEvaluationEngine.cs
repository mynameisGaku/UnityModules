using System;

namespace GameplayRules
{
    internal static class NumericRequirementEvaluationEngine
    {
        internal static bool TryEvaluate(NumericRequirement[] requirements, out NumericRequirementEvaluation evaluation)
        {
            var lines = new NumericRequirementLine[requirements.Length];
            var allSatisfied = true;
            for (var index = 0; index < requirements.Length; index++)
            {
                var requirement = requirements[index];
                var delta = requirement.ActualValue - requirement.ExpectedValue;
                var absoluteDelta = Math.Abs(delta);
                if (double.IsNaN(delta) || double.IsInfinity(delta) || double.IsNaN(absoluteDelta) || double.IsInfinity(absoluteDelta))
                {
                    evaluation = null;
                    return false;
                }

                var satisfied = EvaluateComparison(requirement, absoluteDelta);
                lines[index] = new NumericRequirementLine(requirement, delta, absoluteDelta, satisfied);
                allSatisfied &= satisfied;
            }

            evaluation = new NumericRequirementEvaluation(allSatisfied, lines);
            return true;
        }

        private static bool EvaluateComparison(NumericRequirement requirement, double absoluteDelta)
        {
            switch (requirement.Comparison)
            {
                case NumericRequirementComparison.AtLeast: return requirement.ActualValue >= requirement.ExpectedValue;
                case NumericRequirementComparison.AtMost: return requirement.ActualValue <= requirement.ExpectedValue;
                case NumericRequirementComparison.GreaterThan: return requirement.ActualValue > requirement.ExpectedValue;
                case NumericRequirementComparison.LessThan: return requirement.ActualValue < requirement.ExpectedValue;
                case NumericRequirementComparison.EqualWithinTolerance: return absoluteDelta <= requirement.Tolerance;
                case NumericRequirementComparison.OutsideTolerance: return absoluteDelta > requirement.Tolerance;
                default: return false;
            }
        }
    }
}
