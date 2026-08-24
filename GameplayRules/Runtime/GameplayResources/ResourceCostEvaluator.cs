namespace GameplayResources
{
    /// <summary>複数resourceの残量とcostから、stateを変更せず支払可否とresource別の明細を構築する純粋関数を提供します。</summary>
    public static class ResourceCostEvaluator
    {
        /// <summary>1回に受理する残量entryとcost entryそれぞれの最大件数です。</summary>
        public const int MaximumEntryCount = 32;

        /// <summary>resource残量とcostを検証し、cost入力順の支払明細を構築します。</summary>
        /// <param name="balances">0〜32件のresource残量です。costに無いentryは結果へ含めません。</param>
        /// <param name="costs">1〜32件のresource costです。残量に無いresourceは0として評価します。</param>
        /// <param name="evaluation">成功時に全resourceの支払可否と明細を返します。</param>
        /// <param name="error">失敗理由を返します。</param>
        /// <returns>全入力が有効で明細を構築できた場合はtrueです。</returns>
        public static bool TryEvaluate(ResourceAmount[] balances, ResourceAmount[] costs, out ResourceCostEvaluation evaluation, out ResourceCostError error)
        {
            evaluation = null;
            if (balances == null)
            {
                error = ResourceCostError.NullBalances;
                return false;
            }

            if (costs == null)
            {
                error = ResourceCostError.NullCosts;
                return false;
            }

            if (balances.Length > MaximumEntryCount)
            {
                error = ResourceCostError.InvalidBalanceCount;
                return false;
            }

            if (costs.Length < 1 || costs.Length > MaximumEntryCount)
            {
                error = ResourceCostError.InvalidCostCount;
                return false;
            }

            if (!TryValidateEntries(balances, ResourceCostError.DuplicateBalanceId, out error)) return false;
            if (!TryValidateEntries(costs, ResourceCostError.DuplicateCostId, out error)) return false;

            evaluation = ResourceCostEvaluationEngine.Evaluate(balances, costs);
            error = ResourceCostError.None;
            return true;
        }

        private static bool TryValidateEntries(ResourceAmount[] entries, ResourceCostError duplicateError, out ResourceCostError error)
        {
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry.ResourceId <= 0)
                {
                    error = ResourceCostError.InvalidResourceId;
                    return false;
                }

                if (double.IsNaN(entry.Amount) || double.IsInfinity(entry.Amount))
                {
                    error = ResourceCostError.NonFiniteAmount;
                    return false;
                }

                if (entry.Amount < 0d)
                {
                    error = ResourceCostError.NegativeAmount;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (entries[previous].ResourceId != entry.ResourceId) continue;
                    error = duplicateError;
                    return false;
                }
            }

            error = ResourceCostError.None;
            return true;
        }
    }
}
