using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SceneWorkspace.Editor
{
    /// <summary>Owns a bounded domain-local generation and consumption registry outside immutable plans.</summary>
    internal static class SceneWorkspacePlanRegistry
    {
        private const int Capacity = 64;
        private static readonly object Sync = new object();
        private static readonly SortedDictionary<long, Entry> Entries = new SortedDictionary<long, Entry>();
        private static long generation;

        internal static long NextGeneration()
        {
            return Interlocked.Increment(ref generation);
        }

        internal static void Register(SceneWorkspacePlan plan, SceneWorkspaceProfile profile)
        {
            if (plan == null || profile == null || !plan.IsReady || plan.Generation <= 0)
                throw new ArgumentException("A ready plan and profile are required.");

            lock (Sync)
            {
                RemoveReleasedPlans();
                Entries[plan.Generation] = new Entry(plan, profile);
                while (Entries.Count > Capacity)
                    Entries.Remove(Entries.Keys.First());
            }
        }

        internal static SceneWorkspaceError TryConsume(SceneWorkspacePlan plan, out SceneWorkspaceProfile profile)
        {
            profile = null;
            if (plan == null || plan.Generation <= 0)
                return SceneWorkspaceError.StalePlan;

            lock (Sync)
            {
                if (!Entries.TryGetValue(plan.Generation, out var entry))
                    return SceneWorkspaceError.StalePlan;
                if (!entry.Plan.TryGetTarget(out var registered) || !ReferenceEquals(registered, plan))
                    return SceneWorkspaceError.StalePlan;
                if (entry.Consumed)
                    return SceneWorkspaceError.PlanAlreadyConsumed;
                entry.Consumed = true;
                profile = entry.Profile;
                return profile == null ? SceneWorkspaceError.StalePlan : SceneWorkspaceError.None;
            }
        }

        private static void RemoveReleasedPlans()
        {
            var released = Entries.Where(item => !item.Value.Plan.TryGetTarget(out _)).Select(item => item.Key).ToArray();
            foreach (var key in released)
                Entries.Remove(key);
        }

        private sealed class Entry
        {
            internal Entry(SceneWorkspacePlan plan, SceneWorkspaceProfile profile)
            {
                Plan = new WeakReference<SceneWorkspacePlan>(plan);
                Profile = profile;
            }

            internal WeakReference<SceneWorkspacePlan> Plan { get; }
            internal SceneWorkspaceProfile Profile { get; }
            internal bool Consumed { get; set; }
        }
    }
}
