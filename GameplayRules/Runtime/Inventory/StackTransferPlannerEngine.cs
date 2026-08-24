using System;

namespace GameplayInventory
{
    internal static class StackTransferPlannerEngine
    {
        internal static StackTransferPlan Build(StackTransferSource[] sources, StackTransferDestination[] destinations, int requestedUnits, long availableSourceUnits, long availableDestinationRoom)
        {
            var transferredUnits = (int)Math.Min(requestedUnits, Math.Min(availableSourceUnits, availableDestinationRoom));
            var sourceLines = new StackTransferSourceLine[sources.Length];
            var sourceRemaining = transferredUnits;
            for (var index = 0; index < sources.Length; index++)
            {
                var moved = Math.Min(sources[index].AvailableUnits, sourceRemaining);
                sourceLines[index] = new StackTransferSourceLine(index, sources[index].Identifier, sources[index].AvailableUnits, moved);
                sourceRemaining -= moved;
            }

            var destinationLines = new StackTransferDestinationLine[destinations.Length];
            var destinationRemaining = transferredUnits;
            for (var index = 0; index < destinations.Length; index++)
            {
                var room = destinations[index].Capacity - destinations[index].CurrentUnits;
                var received = Math.Min(room, destinationRemaining);
                destinationLines[index] = new StackTransferDestinationLine(index, destinations[index].Identifier, destinations[index].CurrentUnits, destinations[index].Capacity, received);
                destinationRemaining -= received;
            }

            return new StackTransferPlan(requestedUnits, transferredUnits, availableSourceUnits, availableDestinationRoom, sourceLines, destinationLines);
        }
    }
}
