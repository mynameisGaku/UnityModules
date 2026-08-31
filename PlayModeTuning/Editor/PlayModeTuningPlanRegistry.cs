using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>登録した同一の反映予定だけを受け入れ、エンジン変更前に使用済みへ移します。</summary>
    internal sealed class PlayModeTuningPlanRegistry
    {
        private const int MaximumEntries = 64;
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly Queue<Guid> order = new Queue<Guid>();

        internal void Register(PlayModeTuningPlan plan)
        {
            if (plan == null || plan.Nonce == Guid.Empty)
                throw new ArgumentException("登録する反映予定には空でない一回限りの識別子が必要です。", nameof(plan));
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

        /// <summary>エンジン変更前の保存失敗に限り、同一の反映予定を未使用へ戻します。</summary>
        internal void RestoreBeforeMutation(PlayModeTuningPlan plan)
        {
            if (plan == null || !entries.TryGetValue(plan.Nonce, out var entry) || !ReferenceEquals(plan, entry.Plan))
                return;
            entry.Consumed = false;
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
