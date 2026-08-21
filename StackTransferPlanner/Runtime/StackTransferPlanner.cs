namespace GameplayInventory
{
    /// <summary>stack stateを変更せず、入力順の整数unit移送計画を構築する純粋関数を提供します。</summary>
    public static class StackTransferPlanner
    {
        /// <summary>1回に指定できる最大source stack数です。</summary>
        public const int MaximumSourceCount = 32;

        /// <summary>1回に指定できる最大destination stack数です。</summary>
        public const int MaximumDestinationCount = 32;

        /// <summary>1 stackまたは1要求に指定できる最大unit数です。</summary>
        public const int MaximumUnitCount = 1_000_000_000;

        /// <summary>source、destination、要求量を検証し、入力順の移送計画を構築します。</summary>
        /// <param name="sources">移送元stack列です。</param>
        /// <param name="destinations">移送先stack列です。</param>
        /// <param name="requestedUnits">移送を要求する非負unit数です。</param>
        /// <param name="plan">成功時に構築される移送計画です。</param>
        /// <param name="error">失敗理由、または成功時のNoneです。</param>
        /// <returns>全入力が有効で移送計画を構築できた場合はtrueです。</returns>
        public static bool TryPlan(StackTransferSource[] sources, StackTransferDestination[] destinations, int requestedUnits, out StackTransferPlan plan, out StackTransferError error)
        {
            plan = null;
            if (sources == null)
            {
                error = StackTransferError.NullSources;
                return false;
            }

            if (sources.Length < 1 || sources.Length > MaximumSourceCount)
            {
                error = StackTransferError.InvalidSourceCount;
                return false;
            }

            if (destinations == null)
            {
                error = StackTransferError.NullDestinations;
                return false;
            }

            if (destinations.Length < 1 || destinations.Length > MaximumDestinationCount)
            {
                error = StackTransferError.InvalidDestinationCount;
                return false;
            }

            if (requestedUnits < 0 || requestedUnits > MaximumUnitCount)
            {
                error = StackTransferError.InvalidRequestedUnits;
                return false;
            }

            var availableSourceUnits = 0L;
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index];
                if (source.Identifier <= 0)
                {
                    error = StackTransferError.InvalidSourceIdentifier;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (sources[previous].Identifier != source.Identifier) continue;
                    error = StackTransferError.DuplicateSourceIdentifier;
                    return false;
                }

                if (source.AvailableUnits < 0 || source.AvailableUnits > MaximumUnitCount)
                {
                    error = StackTransferError.InvalidSourceUnits;
                    return false;
                }

                availableSourceUnits += source.AvailableUnits;
            }

            var availableDestinationRoom = 0L;
            for (var index = 0; index < destinations.Length; index++)
            {
                var destination = destinations[index];
                if (destination.Identifier <= 0)
                {
                    error = StackTransferError.InvalidDestinationIdentifier;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (destinations[previous].Identifier != destination.Identifier) continue;
                    error = StackTransferError.DuplicateDestinationIdentifier;
                    return false;
                }

                if (destination.Capacity < 1 || destination.Capacity > MaximumUnitCount)
                {
                    error = StackTransferError.InvalidDestinationCapacity;
                    return false;
                }

                if (destination.CurrentUnits < 0 || destination.CurrentUnits > destination.Capacity)
                {
                    error = StackTransferError.InvalidDestinationUnits;
                    return false;
                }

                availableDestinationRoom += destination.Capacity - destination.CurrentUnits;
            }

            plan = StackTransferPlannerEngine.Build(sources, destinations, requestedUnits, availableSourceUnits, availableDestinationRoom);
            error = StackTransferError.None;
            return true;
        }
    }
}
