using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SceneWorkspace.Editor
{
    /// <summary>変更不能な計画の外側で、領域内の世代番号と使用状態を上限付きで管理します。</summary>
    internal static class SceneWorkspacePlanRegistry
    {
        /// <summary>同時に保持する新しい計画の上限です。</summary>
        private const int Capacity = 64;

        /// <summary>登録一覧への同時アクセスを直列化します。</summary>
        private static readonly object Sync = new object();

        /// <summary>世代番号順に保持する計画登録です。</summary>
        private static readonly SortedDictionary<long, Entry> Entries = new SortedDictionary<long, Entry>();

        /// <summary>次に発行する番号の直前値です。</summary>
        private static long generation;

        /// <summary>領域内で重複しない正の世代番号を返します。</summary>
        internal static long NextGeneration()
        {
            return Interlocked.Increment(ref generation);
        }

        /// <summary>準備済み計画と元の設定オブジェクトを単回使用一覧へ登録します。</summary>
        internal static void Register(SceneWorkspacePlan plan, SceneWorkspaceProfile profile)
        {
            if (plan == null || profile == null || !plan.IsReady || plan.Generation <= 0)
                throw new ArgumentException("準備済みの差分計画と作業セット設定を指定してください。");

            lock (Sync)
            {
                RemoveReleasedPlans();
                Entries[plan.Generation] = new Entry(plan, profile);
                while (Entries.Count > Capacity)
                    Entries.Remove(Entries.Keys.First());
            }
        }

        /// <summary>登録済みの同一計画を一度だけ使用済みにし、対応する設定を返します。</summary>
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

        /// <summary>参照先が解放された計画を一覧から取り除きます。</summary>
        private static void RemoveReleasedPlans()
        {
            var released = Entries.Where(item => !item.Value.Plan.TryGetTarget(out _)).Select(item => item.Key).ToArray();
            foreach (var key in released)
                Entries.Remove(key);
        }

        /// <summary>一つの計画の弱い参照、設定、使用状態を保持します。</summary>
        private sealed class Entry
        {
            /// <summary>計画と設定を未使用状態で登録します。</summary>
            internal Entry(SceneWorkspacePlan plan, SceneWorkspaceProfile profile)
            {
                Plan = new WeakReference<SceneWorkspacePlan>(plan);
                Profile = profile;
            }

            /// <summary>計画の寿命を延ばさずに同一性を確認する参照です。</summary>
            internal WeakReference<SceneWorkspacePlan> Plan { get; }

            /// <summary>差分確認に使った設定オブジェクトです。</summary>
            internal SceneWorkspaceProfile Profile { get; }

            /// <summary>計画をすでに切り替え処理へ渡したかを表します。</summary>
            internal bool Consumed { get; set; }
        }
    }
}
