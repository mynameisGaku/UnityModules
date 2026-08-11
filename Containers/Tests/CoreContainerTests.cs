using System.Collections.Generic;
using NUnit.Framework;

namespace Containers.Tests
{
    /// <summary>
    /// 折り返し・世代・入れ替え削除まわりの検証。
    /// このあたりはテストが無いと必ず壊れる、というのが実装時点での判断だった箇所。
    /// </summary>
    public sealed class CoreContainerTests
    {
        // ── FastList ────────────────────────────────────────────────────────

        [Test]
        public void FastList_RefIndexer_構造体をその場で書き換えられる()
        {
            var list = new FastList<TestStruct>();
            list.Add(new TestStruct { Value = 1 });

            list[0].Value = 42;

            Assert.AreEqual(42, list[0].Value, "ref 経由の代入が反映されていない");
        }

        [Test]
        public void FastList_RemoveAtSwapBack_末尾が穴に入る()
        {
            var list = new FastList<int>();
            for (var i = 0; i < 4; i++) list.Add(i);   // 0,1,2,3

            list.RemoveAtSwapBack(1);

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(3, list[1], "末尾の要素が穴に移っていない");
            CollectionAssert.AreEquivalent(new[] { 0, 3, 2 }, list.ToArray());
        }

        [Test]
        public void FastList_RemoveAll_条件に合うものだけ消え順序が保たれる()
        {
            var list = new FastList<int>();
            for (var i = 0; i < 10; i++) list.Add(i);

            var removed = list.RemoveAll(v => v % 2 == 0);

            Assert.AreEqual(5, removed);
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, list.ToArray());
        }

        [Test]
        public void FastList_Clear_参照を手放す()
        {
            var list = new FastList<string>();
            list.Add("a");
            list.Clear();

            // Clear が配列を消していないと、容量ぶんの参照が残り続ける。
            Assert.IsNull(list.BackingArray[0], "Clear が内部配列の参照を消していない");
        }

        // ── RingBuffer ──────────────────────────────────────────────────────

        [Test]
        public void RingBuffer_満杯からのPushBackで最古が押し出される()
        {
            var ring = new RingBuffer<int>(3);
            ring.PushBack(1);
            ring.PushBack(2);
            ring.PushBack(3);
            ring.PushBack(4);   // 1 が落ちる

            Assert.AreEqual(3, ring.Count);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, ring.ToArray());
        }

        [Test]
        public void RingBuffer_満杯からのPushFrontで最新が押し出される()
        {
            var ring = new RingBuffer<int>(3);
            ring.PushBack(1);
            ring.PushBack(2);
            ring.PushBack(3);
            ring.PushFront(0);   // 3 が落ちる

            Assert.AreEqual(3, ring.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ring.ToArray());
        }

        [Test]
        public void RingBuffer_折り返した状態でも添字が古い順になる()
        {
            var ring = new RingBuffer<int>(3);
            for (var i = 0; i < 5; i++) ring.PushBack(i);   // 2,3,4 が残る

            Assert.AreEqual(2, ring[0]);
            Assert.AreEqual(3, ring[1]);
            Assert.AreEqual(4, ring[2]);
            Assert.AreEqual(2, ring.Front);
            Assert.AreEqual(4, ring.Back);
        }

        [Test]
        public void RingBuffer_押し出された要素を受け取れる()
        {
            var ring = new RingBuffer<int>(2);
            ring.PushBack(1);
            ring.PushBack(2);

            var evicted = ring.PushBack(3, out var dropped);

            Assert.IsTrue(evicted);
            Assert.AreEqual(1, dropped);
        }

        // ── Deque ───────────────────────────────────────────────────────────

        [Test]
        public void Deque_折り返した状態で伸びても順序が保たれる()
        {
            var deque = new Deque<int>(2);
            deque.PushBack(1);
            deque.PushBack(2);
            deque.PopFront();      // head が進む
            deque.PushBack(3);     // ここで折り返す
            deque.PushBack(4);     // 容量を超えて伸びる

            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, deque.ToArray());
        }

        [Test]
        public void Deque_両端から出し入れできる()
        {
            var deque = new Deque<int>();
            deque.PushFront(2);
            deque.PushFront(1);
            deque.PushBack(3);

            Assert.AreEqual(1, deque.PopFront());
            Assert.AreEqual(3, deque.PopBack());
            Assert.AreEqual(1, deque.Count);
        }

        // ── SlotMap ─────────────────────────────────────────────────────────

        [Test]
        public void SlotMap_削除済みハンドルは無効になる()
        {
            var map = new SlotMap<string>();
            var handle = map.Add("最初");

            map.Remove(handle);

            Assert.IsFalse(map.IsAlive(handle), "削除したハンドルが生きたままになっている");
            Assert.IsFalse(map.TryGetValue(handle, out _));
        }

        [Test]
        public void SlotMap_再利用されたスロットを古いハンドルで掴めない()
        {
            var map = new SlotMap<string>();
            var old = map.Add("最初");
            map.Remove(old);

            var fresh = map.Add("次");   // 同じスロットが再利用される

            Assert.AreEqual(old.Index, fresh.Index, "前提：スロットが再利用されること");
            Assert.AreNotEqual(old.Generation, fresh.Generation, "世代が進んでいない");
            Assert.IsFalse(map.IsAlive(old), "古いハンドルが新しい値を指してしまっている");
            Assert.IsTrue(map.IsAlive(fresh));
        }

        [Test]
        public void SlotMap_Clearで既存ハンドルが全て無効になる()
        {
            var map = new SlotMap<int>();
            var a = map.Add(1);
            var b = map.Add(2);

            map.Clear();

            Assert.IsFalse(map.IsAlive(a));
            Assert.IsFalse(map.IsAlive(b));
            Assert.AreEqual(0, map.Count);
        }

        [Test]
        public void SlotMap_走査は生きている要素だけを返す()
        {
            var map = new SlotMap<int>();
            var a = map.Add(1);
            map.Add(2);
            map.Add(3);
            map.Remove(a);

            var seen = new List<int>();
            var enumerator = map.GetEnumerator();
            while (enumerator.MoveNext()) seen.Add(enumerator.CurrentValue);

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, seen);
        }

        // ── SparseSet ───────────────────────────────────────────────────────

        [Test]
        public void SparseSet_削除しても残りが正しく引ける()
        {
            var set = new SparseSet<string>();
            set.Set(5, "五");
            set.Set(1, "一");
            set.Set(9, "九");

            set.Remove(1);   // 末尾の 9 が穴に移る

            Assert.IsFalse(set.Contains(1));
            Assert.IsTrue(set.TryGetValue(9, out var nine));
            Assert.AreEqual("九", nine, "入れ替え後に疎索引が更新されていない");
            Assert.IsTrue(set.TryGetValue(5, out var five));
            Assert.AreEqual("五", five);
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void SparseSet_Clear後に古いidが残留しない()
        {
            var set = new SparseSet<int>();
            set.Set(3, 30);
            set.Clear();

            Assert.IsFalse(set.Contains(3), "Clear 後に疎索引の残骸が残っている");
        }

        [Test]
        public void SparseSet_疎配列を超えるidでも伸びる()
        {
            var set = new SparseSet<int>(idCapacity: 4);
            set.Set(1000, 7);

            Assert.IsTrue(set.TryGetValue(1000, out var value));
            Assert.AreEqual(7, value);
        }

        // ── BitSet ──────────────────────────────────────────────────────────

        [Test]
        public void BitSet_立てたビットだけが昇順に列挙される()
        {
            var bits = new BitSet(200);
            bits.Set(0);
            bits.Set(63);
            bits.Set(64);      // ワード境界をまたぐ
            bits.Set(199);

            var seen = new List<int>();
            var enumerator = bits.GetEnumerator();
            while (enumerator.MoveNext()) seen.Add(enumerator.Current);

            CollectionAssert.AreEqual(new[] { 0, 63, 64, 199 }, seen);
            Assert.AreEqual(4, bits.PopCount());
        }

        [Test]
        public void FixedBitSet64_境界ビットを扱える()
        {
            var bits = new FixedBitSet64();
            bits.Set(0);
            bits.Set(63);

            Assert.IsTrue(bits[0]);
            Assert.IsTrue(bits[63]);
            Assert.AreEqual(2, bits.PopCount());
            Assert.AreEqual(0, bits.LowestSetBit());
        }

        // ── LruCache ────────────────────────────────────────────────────────

        [Test]
        public void LruCache_最も古く使ったものから捨てられる()
        {
            var cache = new LruCache<string, int>(2);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.TryGetValue("a", out _);   // a を直近使用に
            cache.Set("c", 3);               // b が落ちるはず

            Assert.IsTrue(cache.ContainsKey("a"));
            Assert.IsFalse(cache.ContainsKey("b"), "直近使用の更新が効いていない");
            Assert.IsTrue(cache.ContainsKey("c"));
        }

        [Test]
        public void LruCache_TryPeekは使用順を変えない()
        {
            var cache = new LruCache<string, int>(2);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.TryPeek("a", out _);   // 順序は変わらないはず
            cache.Set("c", 3);           // a が落ちる

            Assert.IsFalse(cache.ContainsKey("a"), "TryPeek が使用順を変えてしまっている");
        }

        [Test]
        public void LruCache_容量を超えても件数が保たれる()
        {
            var cache = new LruCache<int, int>(8);
            for (var i = 0; i < 100; i++) cache.Set(i, i);

            Assert.AreEqual(8, cache.Count);
        }

        private struct TestStruct
        {
            public int Value;
        }
    }
}
