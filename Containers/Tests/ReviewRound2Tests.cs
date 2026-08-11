using System;
using System.Collections.Generic;
using Containers.Gameplay;
using Containers.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Containers.Tests
{
    /// <summary>
    /// 2 回目のコードレビューで見つかった不具合の再発防止テスト。
    /// このうち数件は、1 回目の修正そのものが作り込んだ問題。
    /// </summary>
    public sealed class ReviewRound2Tests
    {
        // ── ExpiringCache / UnityObjectMap：掃除中の再入 ───────────────────

        [Test]
        public void ExpiringCache_Expiredハンドラから触っても二重発火しない()
        {
            var cache = new ExpiringCache<string, int>(defaultLifetime: 1d) { SweepInterval = 2 };
            var expirations = new Dictionary<string, int>();

            cache.Expired += (key, _) =>
            {
                expirations.TryGetValue(key, out var count);
                expirations[key] = count + 1;

                // ハンドラからキャッシュに触ると Tick 経由で Sweep へ再入する。
                cache.Set("replacement", 0);
            };

            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.SetTime(2d);

            cache.Sweep();

            Assert.AreEqual(1, expirations["a"], "a の期限切れ通知が二重に飛んでいる");
            Assert.AreEqual(1, expirations["b"], "b の期限切れ通知が二重に飛んでいる");
        }

        [Test]
        public void ExpiringCache_Expiredハンドラから触っても走査が打ち切られない()
        {
            var cache = new ExpiringCache<int, int>(defaultLifetime: 1d) { SweepInterval = 1 };
            var expired = 0;

            cache.Expired += (_, __) =>
            {
                expired++;
                cache.TryGetValue(999, out _);   // 再入を誘発する
            };

            for (var i = 0; i < 10; i++) cache.Set(i, i);
            cache.SetTime(5d);

            cache.Sweep();

            Assert.AreEqual(10, expired, "再入で外側の走査が途中で止まっている");
        }

        // ── SerializableDictionary：コード側で作った中身が保存されるか ─────

        [Test]
        public void SerializableDictionary_コピーコンストラクタの中身が保存される()
        {
            var source = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            var map = new SerializableDictionary<string, int>(source);

            // 印が付いていないと書き戻しが飛ばされ、空のまま保存される。
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            Assert.AreEqual(2, map.Count, "コンストラクタで入れた中身が保存されていない");
            Assert.AreEqual(1, map["a"]);
        }

        [Test]
        public void SerializableHashSet_コピーコンストラクタの中身が保存される()
        {
            var set = new SerializableHashSet<string>(new[] { "x", "y" });

            set.OnBeforeSerialize();
            set.OnAfterDeserialize();

            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void SerializableDictionary_MarkDirtyで実体経由の変更が保存される()
        {
            var map = new SerializableDictionary<string, int>();
            map.Add("a", 1);
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            // 実体を直接いじった場合は自分で印を付ける契約。
            map.AsDictionary()["b"] = 2;
            map.MarkDirty();

            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            Assert.AreEqual(2, map.Count, "MarkDirty 後の変更が保存されていない");
        }

        // ── ScheduledEventQueue：実行中の追加で古いイベントが取り残されない ─

        [Test]
        public void ScheduledEventQueue_実行中に積んでも期限切れの後続が実行される()
        {
            var queue = new ScheduledEventQueue();
            var order = new List<string>();

            queue.ScheduleAt(1d, () =>
            {
                order.Add("A");

                // 実行中に、今すでに期限を過ぎている時刻へ積む。
                // これがヒープの根に来ると、後続の B が取り残される実装だった。
                queue.ScheduleAt(0.5d, () => order.Add("C"));
            });

            queue.ScheduleAt(2d, () => order.Add("B"));

            queue.Advance(5d);

            CollectionAssert.Contains(order, "B", "実行中の追加によって、期限切れの B が飛ばされている");
            CollectionAssert.AreEqual(new[] { "A", "B" }, order, "実行中に積んだ C は次回に回るべき");

            queue.Advance(0d);
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order);
        }

        [Test]
        public void ScheduledEventQueue_0秒後の自己再スケジュールでも止まる()
        {
            var queue = new ScheduledEventQueue();
            var fired = 0;

            void Reschedule()
            {
                fired++;
                if (fired < 1000) queue.ScheduleAfter(0d, Reschedule);
            }

            queue.ScheduleAfter(0d, Reschedule);

            Assert.AreEqual(1, queue.Advance(1d));
            Assert.AreEqual(1, fired);
        }

        // ── MultiMap：バケットの使い回しによる取り違え ─────────────────────

        [Test]
        public void MultiMap_削除をまたいで保持した参照が別キーに混ざらない()
        {
            var map = new MultiMap<string, int>();

            var bucketForA = map["a"];
            map.Add("a", 1);
            map.Remove("a", 1);        // 空になり、キーごと消える

            map.Add("b", 2);           // ここで使い回されると bucketForA が b のバケットになる
            bucketForA.Add(99);

            Assert.AreEqual(1, map.CountFor("b"), "保持していた参照経由で別キーに値が紛れ込んでいる");
            Assert.AreEqual(2, map["b"][0]);
        }

        [Test]
        public void MultiMap_Clear後の参照が新しいバケットを汚さない()
        {
            var map = new MultiMap<string, int>();
            var bucket = map["a"];
            map.Add("a", 1);

            map.Clear();
            map.Add("b", 2);

            bucket.Add(99);

            Assert.AreEqual(1, map.CountFor("b"));
        }

        // ── SpatialHashGrid：独自比較子の一貫性 ───────────────────────────

        private sealed class Agent
        {
            public int Id;
        }

        private sealed class AgentIdComparer : IEqualityComparer<Agent>
        {
            public bool Equals(Agent a, Agent b) => a.Id == b.Id;
            public int GetHashCode(Agent agent) => agent.Id;
        }

        [Test]
        public void SpatialHashGrid_独自比較子で等しい別インスタンスでも削除できる()
        {
            var grid = new SpatialHashGrid<Agent>(cellSize: 2f, new AgentIdComparer());

            var original = new Agent { Id = 7 };
            grid.Insert(original, Vector3.zero);

            // 比較子上は同一。セル側が既定の等値比較で消していると、ここで取り残される。
            var equivalent = new Agent { Id = 7 };
            Assert.IsTrue(grid.Remove(equivalent));

            using var results = TempList<Agent>.Rent();
            grid.QueryRadius(Vector3.zero, 1f, results.List);

            Assert.AreEqual(0, results.List.Count, "削除したはずの要素がセルに残っている");
            Assert.AreEqual(0, grid.Count);
        }

        // ── TimerCollection：バッチ中のキャンセルと再入 ───────────────────

        [Test]
        public void TimerCollection_同じTickで止めたタイマーは発火しない()
        {
            var timers = new TimerCollection();
            var firedB = false;

            SlotHandle handleB = default;

            timers.After(1f, () => timers.Cancel(handleB));
            handleB = timers.After(1f, () => firedB = true);

            timers.Tick(2f);

            Assert.IsFalse(firedB, "同じ Tick 内で止めたタイマーが発火している");
        }

        [Test]
        public void TimerCollection_コールバックからTickに再入しても取りこぼさない()
        {
            var timers = new TimerCollection();
            var fired = 0;
            var reentered = false;

            for (var i = 0; i < 5; i++)
            {
                timers.After(1f, () =>
                {
                    fired++;

                    if (reentered) return;

                    reentered = true;
                    timers.Tick(0.01f);   // 作業用配列が共有だと外側の残りが消える
                });
            }

            timers.Tick(2f);

            Assert.AreEqual(5, fired, "再入によって残りのコールバックが落ちている");
        }

        [Test]
        public void TimerCollection_繰り返しタイマーは発火後も生きている()
        {
            var timers = new TimerCollection();
            var fired = 0;
            var handle = timers.Every(1f, () => fired++);

            timers.Tick(1.5f);

            Assert.AreEqual(1, fired);
            Assert.IsTrue(timers.IsActive(handle), "繰り返しタイマーが消えている");
        }

        // ── GameplayTagContainer：一括操作の通知 ──────────────────────────

        [Test]
        public void GameplayTagContainer_RemoveMatchingがChangedを発火する()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");
            container.Add("Status.Debuff.Burn");

            var removed = new List<string>();
            container.Changed += (tag, added) =>
            {
                if (!added) removed.Add(tag.Name);
            };

            container.RemoveMatching(GameplayTag.Get("Status.Debuff"));

            Assert.AreEqual(2, removed.Count, "一括解除が通知されていない");
        }

        [Test]
        public void GameplayTagContainer_ClearがChangedを発火する()
        {
            var container = new GameplayTagContainer();
            container.Add("A.B");

            var removed = 0;
            container.Changed += (_, added) =>
            {
                if (!added) removed++;
            };

            container.Clear();

            Assert.AreEqual(1, removed, "Clear が通知されていない");
        }

        // ── WeightedRandomList：読み込み後の表の作り直し ───────────────────

        [Test]
        public void WeightedRandomList_読み込み後に確率が重みへ追従する()
        {
            var table = new WeightedRandomList<string>();
            table.Add("a", 1f);
            table.Add("b", 1f);

            Assert.AreEqual(0.5f, table.ProbabilityOf(0), 0.0001f);

            // Inspector で重みを編集した状況の再現。表を無効化しないと古い確率のままになる。
            table.OnAfterDeserialize();
            table.SetWeight(0, 3f);

            Assert.AreEqual(0.75f, table.ProbabilityOf(0), 0.0001f);
        }

        // ── ChunkedList / DynamicAabbTree：末尾削除と全消去 ────────────────

        [Test]
        public void ChunkedList_末尾をRemoveAtSwapBackしても残りが壊れない()
        {
            var list = new ChunkedList<string>(chunkSizeLog2: 2);
            for (var i = 0; i < 6; i++) list.Add($"item{i}");

            list.RemoveAtSwapBack(5);   // 末尾そのもの

            Assert.AreEqual(5, list.Count);
            for (var i = 0; i < 5; i++) Assert.AreEqual($"item{i}", list[i]);
        }

        [Test]
        public void ChunkedList_中間をRemoveAtSwapBackすると末尾が入る()
        {
            var list = new ChunkedList<int>(chunkSizeLog2: 2);
            for (var i = 0; i < 6; i++) list.Add(i);

            list.RemoveAtSwapBack(1);

            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(5, list[1]);
        }
    }
}
