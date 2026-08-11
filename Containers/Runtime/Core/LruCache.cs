using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 上限を超えたら「最後に使われたのが一番古いもの」から捨てる、件数制限つきキャッシュ。
    /// <para>
    /// 上限の無いキャッシュは善意で書かれたメモリリークでしかないので、この型は容量を必須にしている。
    /// 使用順のリストは <see cref="LinkedList{T}"/> ではなく配列上の双方向リンクで持つので、
    /// <b>エントリごとのノード確保が起きない</b>。
    /// </para>
    /// </summary>
    public sealed class LruCache<TKey, TValue>
    {
        private struct Entry
        {
            public TKey Key;
            public TValue Value;
            public int Older;   // 捨てられる側へ
            public int Newer;   // 直近使われた側へ
        }

        private const int Nil = -1;

        private readonly Dictionary<TKey, int> _index;
        private readonly Entry[] _entries;
        private readonly Stack<int> _freeSlots;

        private int _mostRecent = Nil;
        private int _leastRecent = Nil;
        private int _count;

        /// <summary>捨てられたエントリを通知する。追い出し・削除・全消去のいずれでも発火する。</summary>
        public event Action<TKey, TValue> Evicted;

        /// <summary>容量を指定して作る。</summary>
        /// <param name="capacity">保持するエントリ数の上限。</param>
        /// <param name="comparer">キーの比較方法。</param>
        public LruCache(int capacity, IEqualityComparer<TKey> comparer = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            _entries = new Entry[capacity];
            _index = new Dictionary<TKey, int>(capacity, comparer);
            _freeSlots = new Stack<int>(capacity);

            for (var i = capacity - 1; i >= 0; i--) _freeSlots.Push(i);
        }

        /// <summary>保持できる上限。</summary>
        public int Capacity { get; }

        /// <summary>今入っているエントリ数。</summary>
        public int Count => _count;

        /// <summary>値を読み、そのエントリを「直近使った」扱いにする。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!_index.TryGetValue(key, out var slot))
            {
                value = default;
                return false;
            }

            Touch(slot);
            value = _entries[slot].Value;
            return true;
        }

        /// <summary>使用順を変えずに読む。デバッグ表示や監視に使う。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryPeek(TKey key, out TValue value)
        {
            if (!_index.TryGetValue(key, out var slot))
            {
                value = default;
                return false;
            }

            value = _entries[slot].Value;
            return true;
        }

        /// <summary>値の読み書き。読み取りは使用順を更新する。</summary>
        public TValue this[TKey key]
        {
            get => TryGetValue(key, out var value)
                ? value
                : throw new KeyNotFoundException($"キー {key} はキャッシュされていない。");
            set => Set(key, value);
        }

        /// <summary>登録または更新する。上限を超えると最も古いものが捨てられる。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">値。</param>
        public void Set(TKey key, TValue value)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _entries[existing].Value = value;
                Touch(existing);
                return;
            }

            if (_count == Capacity) EvictLeastRecent();

            var slot = _freeSlots.Pop();
            ref var entry = ref _entries[slot];
            entry.Key = key;
            entry.Value = value;
            entry.Older = Nil;
            entry.Newer = Nil;

            _index[key] = slot;
            _count++;
            LinkAsMostRecent(slot);
        }

        /// <summary>キャッシュされていれば返し、無ければ作って登録する。</summary>
        /// <param name="key">キー。</param>
        /// <param name="factory">値を作る関数。</param>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (TryGetValue(key, out var value)) return value;

            value = factory(key);
            Set(key, value);
            return value;
        }

        /// <summary>キーが入っているか。使用順は変えない。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(TKey key) => _index.ContainsKey(key);

        /// <summary>エントリを取り除く。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(TKey key)
        {
            if (!_index.TryGetValue(key, out var slot)) return false;

            var evictedValue = _entries[slot].Value;
            Unlink(slot);
            _index.Remove(key);
            _entries[slot] = default;
            _freeSlots.Push(slot);
            _count--;

            Evicted?.Invoke(key, evictedValue);
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            if (Evicted != null)
            {
                for (var slot = _mostRecent; slot != Nil; slot = _entries[slot].Older)
                {
                    Evicted.Invoke(_entries[slot].Key, _entries[slot].Value);
                }
            }

            Array.Clear(_entries, 0, _entries.Length);
            _index.Clear();
            _freeSlots.Clear();
            for (var i = Capacity - 1; i >= 0; i--) _freeSlots.Push(i);

            _mostRecent = Nil;
            _leastRecent = Nil;
            _count = 0;
        }

        private void EvictLeastRecent()
        {
            var slot = _leastRecent;
            if (slot == Nil) return;

            var key = _entries[slot].Key;
            var value = _entries[slot].Value;

            Unlink(slot);
            _index.Remove(key);
            _entries[slot] = default;
            _freeSlots.Push(slot);
            _count--;

            Evicted?.Invoke(key, value);
        }

        private void Touch(int slot)
        {
            if (_mostRecent == slot) return;
            Unlink(slot);
            LinkAsMostRecent(slot);
        }

        private void LinkAsMostRecent(int slot)
        {
            ref var entry = ref _entries[slot];
            entry.Newer = Nil;
            entry.Older = _mostRecent;

            if (_mostRecent != Nil) _entries[_mostRecent].Newer = slot;
            _mostRecent = slot;
            if (_leastRecent == Nil) _leastRecent = slot;
        }

        private void Unlink(int slot)
        {
            ref var entry = ref _entries[slot];

            if (entry.Older != Nil) _entries[entry.Older].Newer = entry.Newer;
            else _leastRecent = entry.Newer;

            if (entry.Newer != Nil) _entries[entry.Newer].Older = entry.Older;
            else _mostRecent = entry.Older;

            entry.Older = Nil;
            entry.Newer = Nil;
        }

        /// <summary>直近使った順に並んだキー。列挙しても順序は変わらない。</summary>
        public IEnumerable<TKey> KeysByRecency()
        {
            for (var slot = _mostRecent; slot != Nil; slot = _entries[slot].Older)
            {
                yield return _entries[slot].Key;
            }
        }
    }
}
