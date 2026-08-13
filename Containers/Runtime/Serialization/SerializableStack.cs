using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// Unity がシリアライズできる <see cref="Stack{T}"/>。
    /// Inspector 上の要素 0 はスタックの底で、人が編集するときに自然な並びに合わせている。
    /// </summary>
    [Serializable]
    public class SerializableStack<T> : IReadOnlyCollection<T>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<T> _items = new List<T>();

        [NonSerialized] private Stack<T> _stack = new Stack<T>();

        /// <summary>
        /// 実行時 API 経由で中身が変わったか（詳細は
        /// <see cref="SerializableDictionary{TKey,TValue}"/> の同名フィールドを参照）。
        /// </summary>
        [NonSerialized] private bool _runtimeDirty;

        /// <summary>要素数。</summary>
        public int Count => _stack.Count;

        /// <summary>積む。</summary>
        /// <param name="item">積む値。</param>
        public void Push(T item)
        {
            _stack.Push(item);
            _runtimeDirty = true;
        }

        /// <summary>取り出す。空なら例外。</summary>
        public T Pop()
        {
            var item = _stack.Pop();
            _runtimeDirty = true;
            return item;
        }

        /// <summary>先頭を覗く。空なら例外。</summary>
        public T Peek() => _stack.Peek();

        /// <summary>取り出す。空なら false。</summary>
        /// <param name="item">取り出した値。</param>
        public bool TryPop(out T item)
        {
            if (!_stack.TryPop(out item)) return false;

            _runtimeDirty = true;
            return true;
        }

        /// <summary>先頭を覗く。空なら false。</summary>
        /// <param name="item">先頭の値。</param>
        public bool TryPeek(out T item) => _stack.TryPeek(out item);

        /// <summary>含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(T item) => _stack.Contains(item);

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _stack.Clear();
            _runtimeDirty = true;
        }

        /// <summary>
        /// 実体のスタック。
        /// <b>直接書き換えた場合は <see cref="MarkDirty"/> を呼ぶこと</b>
        /// （理由は <see cref="SerializableDictionary{TKey,TValue}.AsDictionary"/> と同じ）。
        /// </summary>
        public Stack<T> AsStack() => _stack;

        /// <summary><see cref="AsStack"/> 経由の変更を保存対象として印を付ける。</summary>
        public void MarkDirty() => _runtimeDirty = true;

        /// <summary>スタックの中身をリストに展開する。実行時に変更があったときだけ書き戻す。</summary>
        public void OnBeforeSerialize()
        {
            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            _items.Clear();
            // Stack は先頭から列挙されるので、反転して底から並ぶようにする。
            foreach (var item in _stack) _items.Add(item);
            _items.Reverse();
        }

        /// <summary>リストからスタックを組み直す。</summary>
        public void OnAfterDeserialize()
        {
            _stack.Clear();
            _runtimeDirty = false;
            for (var i = 0; i < _items.Count; i++) _stack.Push(_items[i]);
        }

        /// <summary>先頭から走査する構造体列挙子を返す。</summary>
        public Stack<T>.Enumerator GetEnumerator() => _stack.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _stack.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _stack.GetEnumerator();
    }

    /// <summary>
    /// Unity がシリアライズできる <see cref="Queue{T}"/>。Inspector 上の要素 0 が先頭。
    /// </summary>
    [Serializable]
    public class SerializableQueue<T> : IReadOnlyCollection<T>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<T> _items = new List<T>();

        [NonSerialized] private Queue<T> _queue = new Queue<T>();

        /// <summary>
        /// 実行時 API 経由で中身が変わったか（詳細は
        /// <see cref="SerializableDictionary{TKey,TValue}"/> の同名フィールドを参照）。
        /// </summary>
        [NonSerialized] private bool _runtimeDirty;

        /// <summary>要素数。</summary>
        public int Count => _queue.Count;

        /// <summary>末尾に積む。</summary>
        /// <param name="item">積む値。</param>
        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
            _runtimeDirty = true;
        }

        /// <summary>先頭から取り出す。空なら例外。</summary>
        public T Dequeue()
        {
            var item = _queue.Dequeue();
            _runtimeDirty = true;
            return item;
        }

        /// <summary>先頭を覗く。空なら例外。</summary>
        public T Peek() => _queue.Peek();

        /// <summary>先頭から取り出す。空なら false。</summary>
        /// <param name="item">取り出した値。</param>
        public bool TryDequeue(out T item)
        {
            if (!_queue.TryDequeue(out item)) return false;

            _runtimeDirty = true;
            return true;
        }

        /// <summary>先頭を覗く。空なら false。</summary>
        /// <param name="item">先頭の値。</param>
        public bool TryPeek(out T item) => _queue.TryPeek(out item);

        /// <summary>含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(T item) => _queue.Contains(item);

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _queue.Clear();
            _runtimeDirty = true;
        }

        /// <summary>
        /// 実体のキュー。
        /// <b>直接書き換えた場合は <see cref="MarkDirty"/> を呼ぶこと</b>
        /// （理由は <see cref="SerializableDictionary{TKey,TValue}.AsDictionary"/> と同じ）。
        /// </summary>
        public Queue<T> AsQueue() => _queue;

        /// <summary><see cref="AsQueue"/> 経由の変更を保存対象として印を付ける。</summary>
        public void MarkDirty() => _runtimeDirty = true;

        /// <summary>キューの中身をリストに展開する。実行時に変更があったときだけ書き戻す。</summary>
        public void OnBeforeSerialize()
        {
            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            _items.Clear();
            foreach (var item in _queue) _items.Add(item);
        }

        /// <summary>リストからキューを組み直す。</summary>
        public void OnAfterDeserialize()
        {
            _queue.Clear();
            _runtimeDirty = false;
            for (var i = 0; i < _items.Count; i++) _queue.Enqueue(_items[i]);
        }

        /// <summary>先頭から走査する構造体列挙子を返す。</summary>
        public Queue<T>.Enumerator GetEnumerator() => _queue.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _queue.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();
    }
}
