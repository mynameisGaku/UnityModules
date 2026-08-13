using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>辞書に起きた変更 1 件。</summary>
    public readonly struct DictionaryChangedEvent<TKey, TValue>
    {
        public readonly CollectionChange Kind;
        public readonly TKey Key;
        public readonly TValue NewValue;
        public readonly TValue OldValue;

        public DictionaryChangedEvent(CollectionChange kind, TKey key, TValue newValue, TValue oldValue)
        {
            Kind = kind;
            Key = key;
            NewValue = newValue;
            OldValue = oldValue;
        }
    }

    /// <summary>
    /// キー単位で変更を通知する辞書。
    /// <para>
    /// <see cref="ObservableList{T}"/> の辞書版で、使いどころも同じ ——
    /// 表示が全件の再構築ではなく該当行の差し替えで済むようにするため。
    /// 所持アイテムの個数表示、プレイヤー一覧、装備スロットなど。
    /// </para>
    /// </summary>
    public sealed class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _map;

        public ObservableDictionary(IEqualityComparer<TKey> comparer = null) =>
            _map = new Dictionary<TKey, TValue>(comparer);

        /// <summary>変更 1 件ごとに発火する。</summary>
        public event Action<DictionaryChangedEvent<TKey, TValue>> Changed;

        /// <summary>件数が変わったときだけ発火する。</summary>
        public event Action<int> CountChanged;

        public int Count => _map.Count;
        public bool IsReadOnly => false;

        public Dictionary<TKey, TValue>.KeyCollection Keys => _map.Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values => _map.Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => _map.Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => _map.Values;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _map.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _map.Values;

        public TValue this[TKey key]
        {
            get => _map[key];
            set
            {
                if (_map.TryGetValue(key, out var old))
                {
                    if (EqualityComparer<TValue>.Default.Equals(old, value)) return;

                    _map[key] = value;
                    Changed?.Invoke(new DictionaryChangedEvent<TKey, TValue>(CollectionChange.Replaced, key, value, old));
                    return;
                }

                _map[key] = value;
                Changed?.Invoke(new DictionaryChangedEvent<TKey, TValue>(CollectionChange.Added, key, value, default));
                CountChanged?.Invoke(_map.Count);
            }
        }

        public void Add(TKey key, TValue value)
        {
            _map.Add(key, value);
            Changed?.Invoke(new DictionaryChangedEvent<TKey, TValue>(CollectionChange.Added, key, value, default));
            CountChanged?.Invoke(_map.Count);
        }

        public bool Remove(TKey key)
        {
            if (!_map.TryGetValue(key, out var old)) return false;

            _map.Remove(key);
            Changed?.Invoke(new DictionaryChangedEvent<TKey, TValue>(CollectionChange.Removed, key, default, old));
            CountChanged?.Invoke(_map.Count);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>全消去。件数分ではなく <see cref="CollectionChange.Reset"/> を 1 回だけ流す。</summary>
        public void Clear()
        {
            if (_map.Count == 0) return;

            _map.Clear();
            Changed?.Invoke(new DictionaryChangedEvent<TKey, TValue>(CollectionChange.Reset, default, default, default));
            CountChanged?.Invoke(0);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
            _map.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_map).CopyTo(array, arrayIndex);

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _map.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
    }
}
