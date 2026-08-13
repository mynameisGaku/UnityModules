using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>コレクションに起きた変更の種類。</summary>
    public enum CollectionChange
    {
        Added,
        Removed,
        Replaced,
        Moved,
        Reset
    }

    /// <summary>変更 1 件の内容。差分だけで UI を更新できるだけの情報を持つ。</summary>
    public readonly struct CollectionChangedEvent<T>
    {
        public readonly CollectionChange Kind;
        public readonly int Index;

        /// <summary><see cref="CollectionChange.Moved"/> のときの移動元。それ以外は -1。</summary>
        public readonly int OldIndex;

        public readonly T NewValue;
        public readonly T OldValue;

        public CollectionChangedEvent(CollectionChange kind, int index, T newValue, T oldValue, int oldIndex = -1)
        {
            Kind = kind;
            Index = index;
            NewValue = newValue;
            OldValue = oldValue;
            OldIndex = oldIndex;
        }
    }

    /// <summary>
    /// 追加・削除・置換・移動を個別に通知するリスト。
    /// <para>
    /// 「変わった」だけを知らせるイベントとの差が本質。UI Toolkit の <c>ListView</c> や
    /// 自前のインベントリ UI は、通知が粗いと<b>毎回全要素を作り直す</b>しかなくなる。
    /// どこがどう変わったかまで渡せば、1 行の差し替えで済む。
    /// </para>
    /// <para>
    /// 変更通知が要らない場面では <see cref="FastList{T}"/> を使うこと。
    /// こちらはイベント発火の分だけ確実に遅い。
    /// </para>
    /// </summary>
    public sealed class ObservableList<T> : IList<T>, IReadOnlyList<T>
    {
        private readonly List<T> _items;

        public ObservableList() => _items = new List<T>();

        public ObservableList(int capacity) => _items = new List<T>(capacity);

        public ObservableList(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _items = new List<T>(source);
        }

        /// <summary>変更 1 件ごとに発火する。</summary>
        public event Action<CollectionChangedEvent<T>> Changed;

        /// <summary>件数が変わったときだけ発火する。「0 件です」表示の切り替えなどに。</summary>
        public event Action<int> CountChanged;

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => _items[index];
            set
            {
                var old = _items[index];
                if (EqualityComparer<T>.Default.Equals(old, value)) return;

                _items[index] = value;
                Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Replaced, index, value, old));
            }
        }

        public void Add(T item)
        {
            _items.Add(item);
            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Added, _items.Count - 1, item, default));
            CountChanged?.Invoke(_items.Count);
        }

        /// <summary>まとめて追加する。通知は 1 件ずつ飛ぶので、受け側は順に処理すればよい。</summary>
        public void AddRange(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var item in source) Add(item);
        }

        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Added, index, item, default));
            CountChanged?.Invoke(_items.Count);
        }

        public bool Remove(T item)
        {
            var index = _items.IndexOf(item);
            if (index < 0) return false;

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            var removed = _items[index];
            _items.RemoveAt(index);

            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Removed, index, default, removed));
            CountChanged?.Invoke(_items.Count);
        }

        /// <summary>要素を並べ替える。UI 側は作り直さず行を動かすだけで済む。</summary>
        public void Move(int oldIndex, int newIndex)
        {
            if ((uint)oldIndex >= (uint)_items.Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if ((uint)newIndex >= (uint)_items.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
            if (oldIndex == newIndex) return;

            var item = _items[oldIndex];
            _items.RemoveAt(oldIndex);
            _items.Insert(newIndex, item);

            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Moved, newIndex, item, item, oldIndex));
        }

        /// <summary>
        /// 全消去。件数分の Removed ではなく <see cref="CollectionChange.Reset"/> を 1 回だけ流す。
        /// 受け側は「作り直せ」と解釈すればよい。
        /// </summary>
        public void Clear()
        {
            if (_items.Count == 0) return;

            _items.Clear();
            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Reset, -1, default, default));
            CountChanged?.Invoke(0);
        }

        /// <summary>
        /// 通知を止めて中身を差し替え、最後に <see cref="CollectionChange.Reset"/> を 1 回流す。
        /// 大量の入れ替えで通知が洪水になるのを避ける。
        /// </summary>
        public void ReplaceAll(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _items.Clear();
            _items.AddRange(source);

            Changed?.Invoke(new CollectionChangedEvent<T>(CollectionChange.Reset, -1, default, default));
            CountChanged?.Invoke(_items.Count);
        }

        public bool Contains(T item) => _items.Contains(item);
        public int IndexOf(T item) => _items.IndexOf(item);
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        /// <summary>通知を出さずに読むための実体。書き換えると購読側とずれるので読み取り専用に扱うこと。</summary>
        public List<T> AsList() => _items;

        public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}
