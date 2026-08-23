using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>Accepts only the exact registered plan object and consumes it before any engine mutation.</summary>
    internal sealed class PlayModeTuningPlanRegistry
    {
        private const int MaximumEntries = 64;
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly Queue<Guid> order = new Queue<Guid>();

        internal void Register(PlayModeTuningPlan plan)
        {
            if (plan == null || plan.Nonce == Guid.Empty)
                throw new ArgumentException("A registered plan requires a non-empty nonce.", nameof(plan));
            entries[plan.Nonce] = new Entry(plan);
            order.Enqueue(plan.Nonce);
            while (order.Count > MaximumEntries)
            {
                var oldest = order.Dequeue();
                entries.Remove(oldest);
            }
        }

        internal PlayModeTuningError TryConsume(PlayModeTuningPlan plan)
        {
            if (plan == null || !entries.TryGetValue(plan.Nonce, out var entry))
                return PlayModeTuningError.StalePlan;
            if (!ReferenceEquals(plan, entry.Plan) || plan.SessionId != entry.Plan.SessionId || !StringComparer.Ordinal.Equals(plan.Revision, entry.Plan.Revision))
                return PlayModeTuningError.StalePlan;
            if (entry.Consumed)
                return PlayModeTuningError.PlanAlreadyConsumed;
            entry.Consumed = true;
            return PlayModeTuningError.None;
        }

        internal void RemoveSession(Guid sessionId)
        {
            var remove = new List<Guid>();
            foreach (var pair in entries)
            {
                if (pair.Value.Plan.SessionId == sessionId)
                    remove.Add(pair.Key);
            }
            foreach (var nonce in remove)
                entries.Remove(nonce);
        }

        private sealed class Entry
        {
            internal Entry(PlayModeTuningPlan plan)
            {
                Plan = plan;
            }

            internal PlayModeTuningPlan Plan { get; }
            internal bool Consumed { get; set; }
        }
    }
}
