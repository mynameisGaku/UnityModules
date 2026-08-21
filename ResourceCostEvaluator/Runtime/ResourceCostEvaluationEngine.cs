namespace GameplayResources
{
    internal static class ResourceCostEvaluationEngine
    {
        internal static ResourceCostEvaluation Evaluate(ResourceAmount[] balances, ResourceAmount[] costs)
        {
            var lines = new ResourceCostLine[costs.Length];
            var canPay = true;
            for (var costIndex = 0; costIndex < costs.Length; costIndex++)
            {
                var cost = costs[costIndex];
                var available = FindAvailableAmount(balances, cost.ResourceId);
                var affordable = available >= cost.Amount;
                var remaining = affordable ? available - cost.Amount : 0d;
                var deficit = affordable ? 0d : cost.Amount - available;
                lines[costIndex] = new ResourceCostLine(cost.ResourceId, available, cost.Amount, remaining, deficit, affordable);
                canPay &= affordable;
            }

            return new ResourceCostEvaluation(canPay, lines);
        }

        private static double FindAvailableAmount(ResourceAmount[] balances, int resourceId)
        {
            for (var index = 0; index < balances.Length; index++)
            {
                if (balances[index].ResourceId == resourceId) return balances[index].Amount;
            }

            return 0d;
        }
    }
}
