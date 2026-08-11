using System.Collections.Generic;
using Containers.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Containers.Tests
{
    /// <summary>
    /// 空間分割・グラフ探索・時間管理・変更通知の検証。
    /// </summary>
    public sealed class SpatialAndStateTests
    {
        // ── SpatialHashGrid ─────────────────────────────────────────────────

        [Test]
        public void SpatialHashGrid_半径内の要素が取れる()
        {
            var grid = new SpatialHashGrid<string>(cellSize: 1f);
            grid.Insert("near", new Vector3(0.5f, 0f, 0.5f));
            grid.Insert("far", new Vector3(50f, 0f, 50f));

            using var results = TempList<string>.Rent();
            grid.QueryRadius(Vector3.zero, 2f, results.List);

            CollectionAssert.Contains(results.List.ToArray(), "near");
            CollectionAssert.DoesNotContain(results.List.ToArray(), "far");
        }

        [Test]
        public void SpatialHashGrid_移動しても二重登録されない()
        {
            var grid = new SpatialHashGrid<string>(cellSize: 1f);
            grid.Insert("mover", Vector3.zero);
            grid.Update("mover", new Vector3(10f, 0f, 10f));

            Assert.AreEqual(1, grid.Count);

            using var atOrigin = TempList<string>.Rent();
            grid.QueryRadius(Vector3.zero, 0.5f, atOrigin.List);
            Assert.AreEqual(0, atOrigin.List.Count, "移動前のセルに残っている");

            using var atTarget = TempList<string>.Rent();
            grid.QueryRadius(new Vector3(10f, 0f, 10f), 0.5f, atTarget.List);
            Assert.AreEqual(1, atTarget.List.Count);
        }

        [Test]
        public void SpatialHashGrid_厳密な距離判定はセル外の候補を落とす()
        {
            var positions = new Dictionary<string, Vector3>
            {
                ["inside"] = new Vector3(0.9f, 0f, 0f),
                ["outside"] = new Vector3(1.9f, 0f, 0f)
            };

            var grid = new SpatialHashGrid<string>(cellSize: 1f);
            foreach (var pair in positions) grid.Insert(pair.Key, pair.Value);

            using var results = TempList<string>.Rent();
            grid.QueryRadiusExact(Vector3.zero, 1f, key => positions[key], results.List);

            CollectionAssert.AreEqual(new[] { "inside" }, results.List.ToArray());
        }

        [Test]
        public void SpatialHashGrid_最近傍を見つけられる()
        {
            var positions = new Dictionary<string, Vector3>
            {
                ["a"] = new Vector3(3f, 0f, 0f),
                ["b"] = new Vector3(1f, 0f, 0f),
                ["c"] = new Vector3(8f, 0f, 0f)
            };

            var grid = new SpatialHashGrid<string>(cellSize: 2f);
            foreach (var pair in positions) grid.Insert(pair.Key, pair.Value);

            Assert.IsTrue(grid.TryFindNearest(Vector3.zero, 20f, key => positions[key], out var nearest));
            Assert.AreEqual("b", nearest);
        }

        // ── Grid2D ──────────────────────────────────────────────────────────

        [Test]
        public void Grid2D_角のセルでは近傍が減る()
        {
            var grid = new Grid2D<int>(4, 4);

            using var corner = TempList<Vector2Int>.Rent();
            grid.GetNeighbours(Vector2Int.zero, corner.List);
            Assert.AreEqual(2, corner.List.Count, "角の 4 近傍は 2 つのはず");

            using var middle = TempList<Vector2Int>.Rent();
            grid.GetNeighbours(new Vector2Int(1, 1), middle.List);
            Assert.AreEqual(4, middle.List.Count);
        }

        [Test]
        public void Grid2D_ワールド座標とセルが往復する()
        {
            var grid = new Grid2D<int>(10, 10, cellSize: 2f);
            var cell = new Vector2Int(3, 4);

            var world = grid.CellToWorld(cell);
            Assert.AreEqual(cell, grid.WorldToCell(world));
        }

        [Test]
        public void Grid2D_範囲外アクセスが安全に落ちる()
        {
            var grid = new Grid2D<int>(2, 2);

            Assert.IsFalse(grid.InBounds(-1, 0));
            Assert.IsFalse(grid.TryGet(5, 5, out _));
            Assert.IsFalse(grid.TrySet(5, 5, 1));
            Assert.AreEqual(-1, grid.GetOrDefault(9, 9, -1));
        }

        // ── HexGrid ─────────────────────────────────────────────────────────

        [Test]
        public void HexGrid_リングの要素数が半径の6倍になる()
        {
            using var ring = TempList<Hex>.Rent();
            HexGrid<int>.Ring(Hex.Zero, 2, ring.List);

            Assert.AreEqual(12, ring.List.Count);

            for (var i = 0; i < ring.List.Count; i++)
            {
                Assert.AreEqual(2, Hex.Distance(Hex.Zero, ring.List[i]), "リング上の距離が半径と一致しない");
            }
        }

        [Test]
        public void HexGrid_スパイラルは中心を含み重複しない()
        {
            using var spiral = TempList<Hex>.Rent();
            HexGrid<int>.Spiral(Hex.Zero, 2, spiral.List);

            // 1 + 6 + 12
            Assert.AreEqual(19, spiral.List.Count);

            var unique = new HashSet<Hex>();
            for (var i = 0; i < spiral.List.Count; i++)
            {
                Assert.IsTrue(unique.Add(spiral.List[i]), "スパイラルに重複がある");
            }
        }

        [Test]
        public void HexGrid_ワールド座標とヘックスが往復する()
        {
            var grid = new HexGrid<int>(hexSize: 1.5f);
            var hex = new Hex(2, -3);

            var world = grid.HexToWorld(hex);
            Assert.AreEqual(hex, grid.WorldToHex(world));
        }

        // ── Graph ───────────────────────────────────────────────────────────

        [Test]
        public void Graph_最小コストの経路を選ぶ()
        {
            var graph = new Graph<string>();
            graph.AddEdge("a", "b", 1f);
            graph.AddEdge("b", "d", 1f);
            graph.AddEdge("a", "c", 10f);
            graph.AddEdge("c", "d", 1f);

            using var path = TempList<string>.Rent();
            Assert.IsTrue(graph.TryFindPath("a", "d", path.List, out var cost));

            CollectionAssert.AreEqual(new[] { "a", "b", "d" }, path.List.ToArray());
            Assert.AreEqual(2f, cost, 0.0001f);
        }

        [Test]
        public void Graph_到達不能なら経路が見つからない()
        {
            var graph = new Graph<string>();
            graph.AddEdge("a", "b");
            graph.AddNode("island");

            using var path = TempList<string>.Rent();
            Assert.IsFalse(graph.TryFindPath("a", "island", path.List, out _));
        }

        [Test]
        public void Graph_トポロジカルソートが依存順に並べる()
        {
            var graph = new Graph<string>();
            graph.AddEdge("鉱石", "インゴット");
            graph.AddEdge("インゴット", "剣");
            graph.AddEdge("木", "柄");
            graph.AddEdge("柄", "剣");

            using var order = TempList<string>.Rent();
            Assert.IsTrue(graph.TopologicalSort(order.List));

            var positions = new Dictionary<string, int>();
            for (var i = 0; i < order.List.Count; i++) positions[order.List[i]] = i;

            Assert.Less(positions["鉱石"], positions["インゴット"]);
            Assert.Less(positions["インゴット"], positions["剣"]);
            Assert.Less(positions["柄"], positions["剣"]);
        }

        [Test]
        public void Graph_循環はトポロジカルソートで検出される()
        {
            var graph = new Graph<string>();
            graph.AddEdge("a", "b");
            graph.AddEdge("b", "c");
            graph.AddEdge("c", "a");

            using var order = TempList<string>.Rent();
            Assert.IsFalse(graph.TopologicalSort(order.List), "循環が検出されていない");
        }

        // ── Trie ────────────────────────────────────────────────────────────

        [Test]
        public void Trie_前方一致で候補が辞書順に並ぶ()
        {
            var trie = new Trie<int>();
            trie.Set("spawn.enemy", 1);
            trie.Set("spawn.item", 2);
            trie.Set("quit", 3);

            using var keys = TempList<string>.Rent();
            trie.KeysWithPrefix("spawn.", keys.List);

            CollectionAssert.AreEqual(new[] { "spawn.enemy", "spawn.item" }, keys.List.ToArray());
        }

        [Test]
        public void Trie_共通の続きを補完できる()
        {
            var trie = new Trie<int>();
            trie.Set("spawn.enemy", 1);
            trie.Set("spawn.entity", 2);

            Assert.AreEqual("spawn.en", trie.LongestCommonCompletion("spawn."));
        }

        [Test]
        public void Trie_前置きと完全一致を区別する()
        {
            var trie = new Trie<int>();
            trie.Set("abc", 1);

            Assert.IsTrue(trie.ContainsPrefix("ab"));
            Assert.IsFalse(trie.ContainsKey("ab"), "前置きが完全一致として扱われている");
        }

        // ── IntervalTree ────────────────────────────────────────────────────

        [Test]
        public void IntervalTree_点を覆う区間だけが返る()
        {
            var tree = new IntervalTree<string>();
            tree.Add(0f, 10f, "早い");
            tree.Add(5f, 15f, "重なり");
            tree.Add(20f, 30f, "遅い");
            tree.Build();

            using var results = TempList<string>.Rent();
            tree.Query(7f, results.List);

            CollectionAssert.AreEquivalent(new[] { "早い", "重なり" }, results.List.ToArray());
        }

        [Test]
        public void IntervalTree_範囲と重なる区間が返る()
        {
            var tree = new IntervalTree<string>();
            tree.Add(0f, 5f, "a");
            tree.Add(10f, 20f, "b");
            tree.Build();

            using var results = TempList<string>.Rent();
            tree.QueryRange(4f, 11f, results.List);

            CollectionAssert.AreEquivalent(new[] { "a", "b" }, results.List.ToArray());
        }

        // ── TimerCollection ─────────────────────────────────────────────────

        [Test]
        public void TimerCollection_期限が来たら一度だけ発火する()
        {
            var timers = new TimerCollection();
            var fired = 0;
            timers.After(1f, () => fired++);

            timers.Tick(0.5f);
            Assert.AreEqual(0, fired);

            timers.Tick(0.6f);
            Assert.AreEqual(1, fired);

            timers.Tick(5f);
            Assert.AreEqual(1, fired, "単発タイマーが複数回発火している");
            Assert.AreEqual(0, timers.Count, "発火後にタイマーが残っている");
        }

        [Test]
        public void TimerCollection_繰り返しは周期がずれない()
        {
            var timers = new TimerCollection();
            var fired = 0;
            timers.Every(1f, () => fired++);

            for (var i = 0; i < 10; i++) timers.Tick(0.5f);

            Assert.AreEqual(5, fired, "5 秒経過で 5 回のはず");
        }

        [Test]
        public void TimerCollection_キャンセルしたタイマーは発火しない()
        {
            var timers = new TimerCollection();
            var fired = false;
            var handle = timers.After(1f, () => fired = true);

            Assert.IsTrue(timers.Cancel(handle));
            timers.Tick(5f);

            Assert.IsFalse(fired);
        }

        // ── ScheduledEventQueue ─────────────────────────────────────────────

        [Test]
        public void ScheduledEventQueue_時刻順に実行される()
        {
            var queue = new ScheduledEventQueue();
            var order = new List<string>();

            queue.ScheduleAfter(3d, () => order.Add("遅い"));
            queue.ScheduleAfter(1d, () => order.Add("早い"));
            queue.ScheduleAfter(2d, () => order.Add("中間"));

            queue.Advance(5d);

            CollectionAssert.AreEqual(new[] { "早い", "中間", "遅い" }, order);
        }

        [Test]
        public void ScheduledEventQueue_同時刻は投入順を保つ()
        {
            var queue = new ScheduledEventQueue();
            var order = new List<int>();

            for (var i = 0; i < 5; i++)
            {
                var captured = i;
                queue.ScheduleAt(1d, () => order.Add(captured));
            }

            queue.Advance(2d);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, order, "同時刻の実行順が投入順になっていない");
        }

        // ── SnapshotHistory ─────────────────────────────────────────────────

        [Test]
        public void SnapshotHistory_過去の時刻を挟む2点が取れる()
        {
            var history = new SnapshotHistory<float>(8);
            history.Record(0d, 0f);
            history.Record(1d, 10f);
            history.Record(2d, 20f);

            Assert.IsTrue(history.TryGetSurrounding(1.5d, out var before, out var after, out var t));

            Assert.AreEqual(10f, before);
            Assert.AreEqual(20f, after);
            Assert.AreEqual(0.5f, t, 0.0001f);
        }

        [Test]
        public void SnapshotHistory_指定時刻以降を切り捨てられる()
        {
            var history = new SnapshotHistory<int>(8);
            for (var i = 0; i < 5; i++) history.Record(i, i);

            var removed = history.TruncateAfter(2d);

            Assert.AreEqual(2, removed);
            Assert.AreEqual(2d, history.NewestTime);
        }

        // ── VersionedValue / ObservableList ─────────────────────────────────

        [Test]
        public void VersionedValue_同じ値の代入では版が進まない()
        {
            var value = new VersionedValue<int>(1);
            var seen = 0;

            Assert.IsTrue(value.HasChangedSince(ref seen));

            value.Value = 1;   // 同値
            Assert.IsFalse(value.HasChangedSince(ref seen), "同値の代入で版が進んでいる");

            value.Value = 2;
            Assert.IsTrue(value.HasChangedSince(ref seen));
        }

        [Test]
        public void ObservableList_変更の種類と位置が通知される()
        {
            var list = new ObservableList<string>();
            var events = new List<CollectionChangedEvent<string>>();
            list.Changed += events.Add;

            list.Add("a");
            list.Insert(0, "b");
            list[1] = "c";
            list.RemoveAt(0);

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(CollectionChange.Added, events[0].Kind);
            Assert.AreEqual(CollectionChange.Added, events[1].Kind);
            Assert.AreEqual(0, events[1].Index);
            Assert.AreEqual(CollectionChange.Replaced, events[2].Kind);
            Assert.AreEqual("a", events[2].OldValue);
            Assert.AreEqual(CollectionChange.Removed, events[3].Kind);
        }

        [Test]
        public void ObservableList_Clearは個別ではなくResetを流す()
        {
            var list = new ObservableList<int> { 1, 2, 3 };
            var events = new List<CollectionChangedEvent<int>>();
            list.Changed += events.Add;

            list.Clear();

            Assert.AreEqual(1, events.Count, "Clear で件数ぶんの通知が飛んでいる");
            Assert.AreEqual(CollectionChange.Reset, events[0].Kind);
        }

        // ── Blackboard ──────────────────────────────────────────────────────

        [Test]
        public void Blackboard_型ごとに独立して保持される()
        {
            var board = new Blackboard();
            var floatKey = BlackboardKey<float>.Named("shared");
            var intKey = BlackboardKey<int>.Named("shared");   // 同名だが別の型

            board.Set(floatKey, 1.5f);
            board.Set(intKey, 42);

            Assert.AreEqual(1.5f, board.Get(floatKey));
            Assert.AreEqual(42, board.Get(intKey), "同名キーが型をまたいで衝突している");
        }

        [Test]
        public void Blackboard_未設定なら既定値が返る()
        {
            var board = new Blackboard();
            var key = BlackboardKey<int>.Named("missing");

            Assert.AreEqual(-1, board.Get(key, -1));
            Assert.IsFalse(board.Has(key));
        }
    }
}
