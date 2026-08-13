using System;
using System.Collections.Generic;
using Containers.Gameplay;
using NUnit.Framework;

namespace Containers.Tests
{
    /// <summary>
    /// ヒープ、抽選、タグ階層の検証。
    /// 特に Alias 法は「動いているように見えて分布が偏る」壊れ方をするので、統計で確認する。
    /// </summary>
    public sealed class QueueAndGameplayTests
    {
        // ── PriorityQueue ───────────────────────────────────────────────────

        [Test]
        public void PriorityQueue_優先度の小さい順に出る()
        {
            var queue = new PriorityQueue<string, int>();
            queue.Enqueue("中", 5);
            queue.Enqueue("小", 1);
            queue.Enqueue("大", 9);
            queue.Enqueue("最小", 0);

            Assert.AreEqual("最小", queue.Dequeue());
            Assert.AreEqual("小", queue.Dequeue());
            Assert.AreEqual("中", queue.Dequeue());
            Assert.AreEqual("大", queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void PriorityQueue_大量投入でも整列が崩れない()
        {
            var random = new Random(12345);
            var queue = new PriorityQueue<int, int>();
            var expected = new List<int>();

            for (var i = 0; i < 500; i++)
            {
                var priority = random.Next(0, 1000);
                queue.Enqueue(priority, priority);
                expected.Add(priority);
            }

            expected.Sort();

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], queue.Dequeue(), $"{i} 番目の取り出しが昇順になっていない");
            }
        }

        [Test]
        public void PriorityQueue_EnqueueDequeueは最小を返す()
        {
            var queue = new PriorityQueue<string, int>();
            queue.Enqueue("既存", 5);

            // 新しい方が小さいので、そのまま返る（積まれない）。
            Assert.AreEqual("新規", queue.EnqueueDequeue("新規", 1));
            Assert.AreEqual(1, queue.Count);

            // 新しい方が大きいので、既存が出て新しい方が残る。
            Assert.AreEqual("既存", queue.EnqueueDequeue("大きい", 9));
        }

        // ── IndexedPriorityQueue ────────────────────────────────────────────

        [Test]
        public void IndexedPriorityQueue_優先度を下げると順序が入れ替わる()
        {
            var queue = new IndexedPriorityQueue<string, float>();
            queue.Enqueue("a", 10f);
            queue.Enqueue("b", 20f);

            Assert.IsTrue(queue.TryDecreasePriority("b", 1f));

            Assert.IsTrue(queue.TryDequeue(out var first, out _));
            Assert.AreEqual("b", first, "引き下げがヒープに反映されていない");
        }

        [Test]
        public void IndexedPriorityQueue_悪くなる方向の引き下げは拒否される()
        {
            var queue = new IndexedPriorityQueue<string, float>();
            queue.Enqueue("a", 5f);

            Assert.IsFalse(queue.TryDecreasePriority("a", 9f));
            Assert.IsTrue(queue.TryGetPriority("a", out var priority));
            Assert.AreEqual(5f, priority);
        }

        [Test]
        public void IndexedPriorityQueue_同じ要素は一度しか入らない()
        {
            var queue = new IndexedPriorityQueue<string, float>();
            queue.EnqueueOrDecrease("a", 5f);
            queue.EnqueueOrDecrease("a", 3f);
            queue.EnqueueOrDecrease("a", 8f);

            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.TryGetPriority("a", out var priority));
            Assert.AreEqual(3f, priority, "最良の優先度が保たれていない");
        }

        // ── TopNBuffer ──────────────────────────────────────────────────────

        [Test]
        public void TopNBuffer_上位N件だけが残る()
        {
            var top = new TopNBuffer<string, int>(3);

            for (var i = 1; i <= 10; i++) top.Offer($"item{i}", i);

            Assert.AreEqual(3, top.Count);

            using var results = TempList<string>.Rent();
            top.CopySorted(results.List);

            CollectionAssert.AreEqual(new[] { "item10", "item9", "item8" }, results.List.ToArray());
        }

        [Test]
        public void TopNBuffer_下位のスコアは弾かれる()
        {
            var top = new TopNBuffer<string, int>(2);
            top.Offer("a", 100);
            top.Offer("b", 90);

            Assert.IsFalse(top.Offer("c", 1), "上位に入らない候補が採用されている");
            Assert.AreEqual(2, top.Count);
        }

        // ── WeightedRandomList ──────────────────────────────────────────────

        [Test]
        public void WeightedRandomList_確率が重みに比例する()
        {
            var table = new WeightedRandomList<string>();
            table.Add("common", 70f);
            table.Add("uncommon", 25f);
            table.Add("rare", 5f);

            Assert.AreEqual(0.70f, table.ProbabilityOf(0), 0.0001f);
            Assert.AreEqual(0.25f, table.ProbabilityOf(1), 0.0001f);
            Assert.AreEqual(0.05f, table.ProbabilityOf(2), 0.0001f);
        }

        [Test]
        public void WeightedRandomList_Alias法の分布が重みと一致する()
        {
            var table = new WeightedRandomList<string>();
            table.Add("a", 60f);
            table.Add("b", 30f);
            table.Add("c", 10f);

            var random = new Random(4242);
            var counts = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 };

            const int draws = 60000;
            for (var i = 0; i < draws; i++) counts[table.Draw(random)]++;

            // 6 万回引けば、統計的な揺れは 2 ポイントに収まる。
            Assert.AreEqual(0.60f, counts["a"] / (float)draws, 0.02f, "a の出現率が重みと合わない");
            Assert.AreEqual(0.30f, counts["b"] / (float)draws, 0.02f, "b の出現率が重みと合わない");
            Assert.AreEqual(0.10f, counts["c"] / (float)draws, 0.02f, "c の出現率が重みと合わない");
        }

        [Test]
        public void WeightedRandomList_重みを変えると表が作り直される()
        {
            var table = new WeightedRandomList<string>();
            table.Add("a", 50f);
            table.Add("b", 50f);

            Assert.AreEqual(0.5f, table.ProbabilityOf(0), 0.0001f);

            table.SetWeight(0, 150f);

            Assert.AreEqual(0.75f, table.ProbabilityOf(0), 0.0001f, "重み変更が反映されていない");
        }

        // ── ShuffleBag ──────────────────────────────────────────────────────

        [Test]
        public void ShuffleBag_一周する間に全要素がちょうど一度ずつ出る()
        {
            var bag = new ShuffleBag<int>(seed: 99);
            for (var i = 0; i < 5; i++) bag.Add(i);

            var seen = new List<int>();
            for (var i = 0; i < 5; i++) seen.Add(bag.Next());

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, seen, "一周の中で重複または欠落がある");
            Assert.AreEqual(0, bag.Remaining);
        }

        [Test]
        public void ShuffleBag_引き切ったら自動的に補充される()
        {
            var bag = new ShuffleBag<int>(seed: 7);
            bag.Add(1);
            bag.Add(2);

            for (var i = 0; i < 10; i++) Assert.DoesNotThrow(() => bag.Next());
        }

        [Test]
        public void ShuffleBag_コピー数が出現頻度になる()
        {
            var bag = new ShuffleBag<string>(seed: 3);
            bag.Add("multi", 3);
            bag.Add("single", 1);

            var counts = new Dictionary<string, int> { ["multi"] = 0, ["single"] = 0 };
            for (var i = 0; i < 4; i++) counts[bag.Next()]++;

            Assert.AreEqual(3, counts["multi"]);
            Assert.AreEqual(1, counts["single"]);
        }

        // ── GameplayTag ─────────────────────────────────────────────────────

        [Test]
        public void GameplayTag_親タグで子タグに当たる()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");

            Assert.IsTrue(container.HasTag("Status.Debuff.Poison"), "完全一致で当たらない");
            Assert.IsTrue(container.HasTag("Status.Debuff"), "親タグで当たらない");
            Assert.IsTrue(container.HasTag("Status"), "祖先タグで当たらない");
            Assert.IsFalse(container.HasTag("Status.Buff"), "無関係な兄弟タグに当たってしまう");
        }

        [Test]
        public void GameplayTag_完全一致判定は階層を無視する()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");

            Assert.IsTrue(container.HasTagExact(GameplayTag.Get("Status.Debuff.Poison")));
            Assert.IsFalse(container.HasTagExact(GameplayTag.Get("Status.Debuff")),
                "明示的に持っていない祖先が完全一致になっている");
        }

        [Test]
        public void GameplayTag_子孫をまとめて外せる()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");
            container.Add("Status.Debuff.Burn");
            container.Add("Status.Buff.Haste");

            var removed = container.RemoveMatching(GameplayTag.Get("Status.Debuff"));

            Assert.AreEqual(2, removed);
            Assert.IsFalse(container.HasTag("Status.Debuff"));
            Assert.IsTrue(container.HasTag("Status.Buff.Haste"), "無関係なタグまで消えている");
        }

        [Test]
        public void GameplayTag_一部を外しても他のタグの祖先は残る()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");
            container.Add("Status.Debuff.Burn");

            container.Remove(GameplayTag.Get("Status.Debuff.Poison"));

            Assert.IsTrue(container.HasTag("Status.Debuff"),
                "残っているタグが要求する祖先まで消えている");
        }

        [Test]
        public void GameplayTag_距離の異なる階層を区別する()
        {
            var fire = GameplayTag.Get("Ability.Fire");
            var burn = GameplayTag.Get("Ability.Fire.Burn");

            Assert.IsTrue(fire.Matches(burn), "親は子に一致すべき");
            Assert.IsFalse(burn.Matches(fire), "子が親に一致してしまっている");
            Assert.AreEqual(2, fire.Depth);
            Assert.AreEqual(3, burn.Depth);
        }

        // ── LoopingList ─────────────────────────────────────────────────────

        [Test]
        public void LoopingList_負方向でも正しく折り返す()
        {
            var list = new LoopingList<string>(new[] { "a", "b", "c" });

            Assert.AreEqual("a", list.Current);
            Assert.AreEqual("c", list.Previous(), "負方向の折り返しが壊れている");
            Assert.AreEqual("a", list.Next());
        }

        [Test]
        public void LoopingList_条件に合う要素まで進める()
        {
            var list = new LoopingList<int>(new[] { 1, 2, 3, 4 });

            Assert.IsTrue(list.NextWhere(v => v % 2 == 0));
            Assert.AreEqual(2, list.Current);
        }

        [Test]
        public void LoopingList_空でも例外を投げない()
        {
            var list = new LoopingList<int>();

            Assert.IsFalse(list.HasSelection);
            Assert.DoesNotThrow(() => list.Next());
            Assert.DoesNotThrow(() => list.Previous());
        }

        // ── Counter ─────────────────────────────────────────────────────────

        [Test]
        public void Counter_足りない消費は状態を変えずに失敗する()
        {
            var counter = new Counter<string>();
            counter.Add("矢", 3);

            Assert.IsFalse(counter.TryTake("矢", 5));
            Assert.AreEqual(3, counter["矢"], "失敗した消費で個数が減っている");

            Assert.IsTrue(counter.TryTake("矢", 3));
            Assert.AreEqual(0, counter["矢"]);
            Assert.IsFalse(counter.Contains("矢"), "0 になったエントリが残っている");
        }
    }
}
