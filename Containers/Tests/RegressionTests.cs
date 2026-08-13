using System;
using System.Collections.Generic;
using System.Threading;
using Containers.Async;
using Containers.Gameplay;
using Containers.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Containers.Tests
{
    /// <summary>
    /// コードレビューで見つかった不具合の再発防止テスト。
    /// 各テストは、修正前のコードで実際に落ちる（もしくは固まる）ことを確認したうえで残している。
    /// </summary>
    public sealed class RegressionTests
    {
        // ── QuadTree / Octree：分割時の自己追加による無限ループ ────────────

        [Test]
        public void QuadTree_分割線上に密集させても分割が終わる()
        {
            var tree = new QuadTree<int>(new Rect(0f, 0f, 10f, 10f), capacity: 2, maxDepth: 6);

            // 中心線に載る点ばかりを入れる。子の矩形は親から 2 回の丸めを経て作られるため、
            // 「親には入るがどの子にも入らない」点が生じうる位置。
            for (var i = 0; i < 64; i++) tree.Insert(i, new Vector2(5f, 5f));

            Assert.AreEqual(64, tree.Count);
        }

        [Test]
        public void QuadTree_分割後も全ての要素を削除できる()
        {
            var tree = new QuadTree<int>(new Rect(0f, 0f, 10f, 10f), capacity: 2, maxDepth: 6);
            var points = new List<Vector2>();

            var random = new System.Random(11);
            for (var i = 0; i < 200; i++)
            {
                // 分割線ちょうどの点を意図的に混ぜる。
                var point = i % 4 == 0
                    ? new Vector2(5f, 5f)
                    : new Vector2((float)random.NextDouble() * 10f, (float)random.NextDouble() * 10f);

                points.Add(point);
                tree.Insert(i, point);
            }

            for (var i = 0; i < points.Count; i++)
            {
                Assert.IsTrue(tree.Remove(i, points[i]), $"{i} 番目を削除できない（内部ノードに取り残されている）");
            }

            Assert.AreEqual(0, tree.Count, "削除後の件数がずれている");
        }

        [Test]
        public void Octree_分割面上に密集させても分割が終わる()
        {
            var tree = new Octree<int>(new Bounds(Vector3.zero, Vector3.one * 10f), capacity: 2, maxDepth: 6);

            for (var i = 0; i < 64; i++) tree.Insert(i, Vector3.zero);

            Assert.AreEqual(64, tree.Count);
        }

        [Test]
        public void Octree_分割後も全ての要素を削除できる()
        {
            var tree = new Octree<int>(new Bounds(Vector3.zero, Vector3.one * 10f), capacity: 2, maxDepth: 6);
            var points = new List<Vector3>();

            var random = new System.Random(22);
            for (var i = 0; i < 200; i++)
            {
                var point = i % 4 == 0
                    ? Vector3.zero
                    : new Vector3(
                        (float)random.NextDouble() * 8f - 4f,
                        (float)random.NextDouble() * 8f - 4f,
                        (float)random.NextDouble() * 8f - 4f);

                points.Add(point);
                tree.Insert(i, point);
            }

            for (var i = 0; i < points.Count; i++)
            {
                Assert.IsTrue(tree.Remove(i, points[i]), $"{i} 番目を削除できない");
            }

            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void QuadTree_分割後も問い合わせで全件見つかる()
        {
            var tree = new QuadTree<int>(new Rect(0f, 0f, 10f, 10f), capacity: 2, maxDepth: 6);
            for (var i = 0; i < 50; i++) tree.Insert(i, new Vector2(5f, 5f));

            using var results = TempList<int>.Rent();
            tree.QueryRect(new Rect(0f, 0f, 10f, 10f), results.List);

            Assert.AreEqual(50, results.List.Count);
        }

        // ── BitSet：配列長の不一致と、64 以上のシフト ──────────────────────

        [Test]
        public void BitSet_配列長が食い違う相手とも和集合が取れる()
        {
            // 200 で作って 299 を立てると、容量 300・8 ワードに伸びる。
            var other = new BitSet(200);
            other.Set(299);

            // 300 で作ると容量 300・5 ワード。ビット数は足りているが配列は短い。
            var target = new BitSet(300);

            Assert.DoesNotThrow(() => target.UnionWith(other), "配列長の差で範囲外アクセスしている");
            Assert.IsTrue(target.Get(299));
        }

        [Test]
        public void BitSet_SetAllは容量を超えたビットを立てない()
        {
            var bits = new BitSet(200);
            bits.Set(299);            // 容量 300 / 8 ワードに拡張される

            bits.SetAll();

            Assert.AreEqual(300, bits.PopCount(), "容量の外にビットが残っている");
        }

        [Test]
        public void BitSet_容量外のビットが列挙に出てこない()
        {
            var bits = new BitSet(70);
            bits.SetAll();

            var max = -1;
            var enumerator = bits.GetEnumerator();
            while (enumerator.MoveNext()) max = enumerator.Current;

            Assert.AreEqual(69, max);
        }

        // ── AsyncQueue：キャンセルした待機者の後始末 ──────────────────────

        [Test]
        public void AsyncQueue_キャンセルした待機者は待機列から外れる()
        {
            var queue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource();

            _ = queue.DequeueAsync(cancellation.Token);
            Assert.AreEqual(1, queue.WaiterCount);

            cancellation.Cancel();

            Assert.AreEqual(0, queue.WaiterCount, "キャンセルした待機者が列に残っている");
        }

        [Test]
        public void AsyncQueue_キャンセル後に積んだ要素が失われない()
        {
            var queue = new AsyncQueue<int>();
            var cancellation = new CancellationTokenSource();

            _ = queue.DequeueAsync(cancellation.Token);
            cancellation.Cancel();

            // 死んだ待機者に渡されると、例外になったうえで要素が消える。
            Assert.DoesNotThrow(() => queue.Enqueue(42));

            Assert.IsTrue(queue.TryDequeue(out var value), "要素が消えている");
            Assert.AreEqual(42, value);
        }

        // ── SerializableDictionary 系：実行時の変更が書き戻されるか ────────

        [Test]
        public void SerializableDictionary_実行時のClearが保存に反映される()
        {
            var map = new SerializableDictionary<string, int>();
            map.Add("a", 1);
            map.Add("b", 2);

            // 保存 → 読み込みの一往復。
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();
            Assert.AreEqual(2, map.Count);

            map.Clear();

            // ここで書き戻さないと、次の読み込みで消したはずの中身が戻る。
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            Assert.AreEqual(0, map.Count, "Clear が保存に反映されず、エントリが復活している");
        }

        [Test]
        public void SerializableDictionary_Inspector側の編集を上書きしない()
        {
            var map = new SerializableDictionary<string, int>();
            map.Add("a", 1);
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            // Inspector が直接リストを編集した状況＝実行時 API を通っていない。
            // この状態で書き戻すと編集内容が消える。
            map.OnBeforeSerialize();
            map.OnAfterDeserialize();

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual(1, map["a"]);
        }

        [Test]
        public void SerializableHashSet_実行時のClearが保存に反映される()
        {
            var set = new SerializableHashSet<string>();
            set.Add("a");
            set.OnBeforeSerialize();
            set.OnAfterDeserialize();

            set.Clear();
            set.OnBeforeSerialize();
            set.OnAfterDeserialize();

            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void GameplayTagContainer_実行時のClearが保存に反映される()
        {
            var container = new GameplayTagContainer();
            container.Add("Status.Debuff.Poison");
            container.OnBeforeSerialize();
            container.OnAfterDeserialize();

            container.Clear();
            container.OnBeforeSerialize();
            container.OnAfterDeserialize();

            Assert.AreEqual(0, container.Count);
            Assert.IsFalse(container.HasTag("Status.Debuff.Poison"));
        }

        [Test]
        public void SerializableQueue_実行時の取り出しが保存に反映される()
        {
            var queue = new SerializableQueue<int>();
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.OnBeforeSerialize();
            queue.OnAfterDeserialize();

            queue.Dequeue();
            queue.OnBeforeSerialize();
            queue.OnAfterDeserialize();

            Assert.AreEqual(1, queue.Count);
        }

        // ── ReactiveProperty：配布中の解除 ────────────────────────────────

        [Test]
        public void ReactiveProperty_配布中の解除で他の購読者が二重に呼ばれない()
        {
            var property = new ReactiveProperty<int>(0);
            var counts = new Dictionary<string, int> { ["x"] = 0, ["y"] = 0, ["z"] = 0 };

            IDisposable subscriptionX = null;

            subscriptionX = property.Subscribe(_ => counts["x"]++, notifyImmediately: false);
            property.Subscribe(_ => counts["y"]++, notifyImmediately: false);
            property.Subscribe(_ =>
            {
                counts["z"]++;
                subscriptionX?.Dispose();   // 配布の途中で先頭を外す
                subscriptionX = null;
            }, notifyImmediately: false);

            property.Value = 1;

            Assert.AreEqual(1, counts["z"], "z が複数回呼ばれている");
            Assert.AreEqual(1, counts["y"], "y が複数回呼ばれているか、飛ばされている");
        }

        [Test]
        public void ReactiveProperty_解除済みの購読者には配られない()
        {
            var property = new ReactiveProperty<int>(0);
            var xCalls = 0;

            var subscriptionX = property.Subscribe(_ => xCalls++, notifyImmediately: false);
            property.Subscribe(_ => subscriptionX.Dispose(), notifyImmediately: false);

            property.Value = 1;

            // 解除は x より後ろの購読者が行うので、x は 1 回だけ呼ばれてよい。
            Assert.AreEqual(1, xCalls);

            property.Value = 2;
            Assert.AreEqual(1, xCalls, "解除後にも配られている");
        }

        // ── LayeredConfig：同一優先度の決着 ───────────────────────────────

        [Test]
        public void LayeredConfig_同一優先度の順序が後からの操作で入れ替わらない()
        {
            var config = new LayeredConfig<string, int>();
            config.SetLayer("first", 10, new[] { new KeyValuePair<string, int>("k", 1) });
            config.SetLayer("second", 10, new[] { new KeyValuePair<string, int>("k", 2) });

            Assert.IsTrue(config.TryResolveWithSource("k", out var value, out var source));
            Assert.AreEqual("second", source, "同値なら後から足した層が勝つはず");
            Assert.AreEqual(2, value);

            // 無関係な層を足すと再ソートが走る。ここで順序が揺れてはいけない。
            for (var i = 0; i < 8; i++)
            {
                config.SetLayer($"noise{i}", 1, null);
                Assert.IsTrue(config.TryResolveWithSource("k", out _, out var again));
                Assert.AreEqual("second", again, $"{i} 回目の再ソートで順序が入れ替わった");
            }
        }

        // ── WeightedRandomList：境界値 ────────────────────────────────────

        [Test]
        public void WeightedRandomList_空のテーブルは例外になる()
        {
            var table = new WeightedRandomList<string>();

            Assert.Throws<InvalidOperationException>(() => table.Draw(0.5f, 0.5f));
        }

        [Test]
        public void WeightedRandomList_coinRollが1でも正しい要素が返る()
        {
            var table = new WeightedRandomList<string>();
            table.Add("a", 1f);
            table.Add("b", 1f);
            table.Add("c", 1f);

            // Random.value は 1.0 を含む。確率 1 の箱で比較が偽になっても、
            // 別名が自分自身なので同じ要素が返らなければならない。
            for (var bucket = 0; bucket < 3; bucket++)
            {
                var roll = bucket / 3f;
                var atZero = table.Draw(roll, 0f);
                var atOne = table.Draw(roll, 1f);

                Assert.IsTrue(atZero == atOne || atOne == table.ItemAt(bucket),
                    $"箱 {bucket} で coinRoll=1 のとき無関係な要素が返る");
            }
        }

        // ── ScheduledEventQueue：自己再スケジュール ───────────────────────

        [Test]
        public void ScheduledEventQueue_0秒後に自分を積み直しても止まる()
        {
            var queue = new ScheduledEventQueue();
            var fired = 0;

            void Reschedule()
            {
                fired++;
                if (fired < 1000) queue.ScheduleAfter(0d, Reschedule);
            }

            queue.ScheduleAfter(0d, Reschedule);

            // 修正前はここが返らない。1 回の Advance で処理するのは
            // 開始時点で積まれていたぶんだけ。
            var executed = queue.Advance(1d);

            Assert.AreEqual(1, executed, "実行開始後に積まれたものまで同じ回で処理している");
            Assert.AreEqual(1, fired);

            queue.Advance(0d);
            Assert.AreEqual(2, fired, "積み直された分が次回に回っていない");
        }

        // ── MultiMap：共有の空リスト ─────────────────────────────────────

        [Test]
        public void MultiMap_未登録キーへの追加が他のキーに漏れない()
        {
            var map = new MultiMap<string, int>();

            map["missing"].Add(1);

            Assert.AreEqual(1, map.CountFor("missing"), "追加が保持されていない");
            Assert.AreEqual(0, map.CountFor("other"), "別のキーに値が漏れている");
        }

        [Test]
        public void MultiMap_別インスタンスに値が漏れない()
        {
            var first = new MultiMap<string, int>();
            first["k"].Add(1);

            var second = new MultiMap<string, int>();

            Assert.AreEqual(0, second.CountFor("k"), "共有インスタンス経由で値が漏れている");
        }

        [Test]
        public void Graph_未登録頂点の辺リストが共有されていない()
        {
            var graph = new Graph<string>();

            var edges = graph.EdgesFrom("missing");
            edges.Add(new Graph<string>.Edge("x", 1f));

            Assert.AreEqual(0, graph.EdgesFrom("other").Count, "共有インスタンスが汚染されている");
        }

        // ── SpscRingBuffer：添字の一周 ───────────────────────────────────

        [Test]
        public void SpscRingBuffer_大量に流しても壊れない()
        {
            var buffer = new SpscRingBuffer<int>(8);

            // 添字は増え続ける。差で比較していないと一周した時点で破綻する。
            for (var i = 0; i < 100000; i++)
            {
                Assert.IsTrue(buffer.TryEnqueue(i), $"{i} 件目を積めない");
                Assert.IsTrue(buffer.TryDequeue(out var value), $"{i} 件目を取り出せない");
                Assert.AreEqual(i, value);
            }

            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void SpscRingBuffer_満杯なら積めない()
        {
            var buffer = new SpscRingBuffer<int>(4);

            for (var i = 0; i < 4; i++) Assert.IsTrue(buffer.TryEnqueue(i));

            Assert.IsFalse(buffer.TryEnqueue(99), "容量を超えて積めてしまう");
        }
    }
}
