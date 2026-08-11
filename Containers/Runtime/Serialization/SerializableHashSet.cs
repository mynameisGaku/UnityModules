using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// Unity がシリアライズでき、Inspector に表示できる <see cref="HashSet{T}"/>。
    /// 仕組みは <see cref="SerializableDictionary{TKey,TValue}"/> と同じで、
    /// ディスク上はリスト、実行時は集合として持つ。
    /// <para>
    /// Unity にシリアライズさせるには具体型で継承する：
    /// <code>
    /// [Serializable] public sealed class DamageTypeSet : SerializableHashSet&lt;DamageType&gt; { }
    /// </code>
    /// </para>
    /// </summary>
    [Serializable]
    public class SerializableHashSet<T> : ISet<T>, IReadOnlyCollection<T>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<T> _items = new List<T>();

        [NonSerialized] private HashSet<T> _set = new HashSet<T>();

        /// <summary>
        /// 実行時 API 経由で中身が変わったか。書き戻しの可否をこれで決める
        /// （詳細は <see cref="SerializableDictionary{TKey,TValue}"/> の同名フィールドを参照）。
        /// </summary>
        [NonSerialized] private bool _runtimeDirty;

        /// <summary>シリアライズされたリストのうち、先に出た要素と重複している位置。</summary>
        [NonSerialized] public readonly List<int> DuplicateIndices = new List<int>();

        /// <summary>空の集合を作る。</summary>
        public SerializableHashSet() { }

        /// <summary>既存の列から作る。</summary>
        /// <param name="source">コピー元。</param>
        public SerializableHashSet(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var item in source) _set.Add(item);

            // コード側で作った中身なので、保存対象として印を付ける。
            _runtimeDirty = true;
        }

        /// <summary>
        /// 実体の集合。
        /// <b>直接書き換えた場合は <see cref="MarkDirty"/> を呼ぶこと</b>
        /// （理由は <see cref="SerializableDictionary{TKey,TValue}.AsDictionary"/> と同じ）。
        /// </summary>
        public HashSet<T> AsHashSet() => _set;

        /// <summary><see cref="AsHashSet"/> 経由の変更を保存対象として印を付ける。</summary>
        public void MarkDirty() => _runtimeDirty = true;

        /// <summary>要素数。</summary>
        public int Count => _set.Count;

        /// <summary>常に false。</summary>
        public bool IsReadOnly => false;

        /// <summary>要素を追加する。既にあれば false。</summary>
        /// <param name="item">追加する値。</param>
        public bool Add(T item)
        {
            if (!_set.Add(item)) return false;

            _runtimeDirty = true;
            return true;
        }

        void ICollection<T>.Add(T item) => Add(item);

        /// <summary>要素が含まれているか。</summary>
        /// <param name="item">探す値。</param>
        public bool Contains(T item) => _set.Contains(item);

        /// <summary>要素を取り除く。</summary>
        /// <param name="item">取り除く値。</param>
        public bool Remove(T item)
        {
            if (!_set.Remove(item)) return false;

            _runtimeDirty = true;
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _set.Clear();
            _runtimeDirty = true;
        }

        /// <summary>配列にコピーする。</summary>
        /// <param name="array">コピー先。</param>
        /// <param name="arrayIndex">コピー先の開始位置。</param>
        public void CopyTo(T[] array, int arrayIndex) => _set.CopyTo(array, arrayIndex);

        /// <summary>差集合を取る。</summary>
        /// <param name="other">比較対象。</param>
        public void ExceptWith(IEnumerable<T> other)
        {
            _set.ExceptWith(other);
            _runtimeDirty = true;
        }

        /// <summary>積集合を取る。</summary>
        /// <param name="other">比較対象。</param>
        public void IntersectWith(IEnumerable<T> other)
        {
            _set.IntersectWith(other);
            _runtimeDirty = true;
        }

        /// <summary>対称差を取る。</summary>
        /// <param name="other">比較対象。</param>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            _set.SymmetricExceptWith(other);
            _runtimeDirty = true;
        }

        /// <summary>和集合を取る。</summary>
        /// <param name="other">比較対象。</param>
        public void UnionWith(IEnumerable<T> other)
        {
            _set.UnionWith(other);
            _runtimeDirty = true;
        }

        /// <summary>真部分集合か。</summary>
        /// <param name="other">比較対象。</param>
        public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);

        /// <summary>真上位集合か。</summary>
        /// <param name="other">比較対象。</param>
        public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);

        /// <summary>部分集合か。</summary>
        /// <param name="other">比較対象。</param>
        public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);

        /// <summary>上位集合か。</summary>
        /// <param name="other">比較対象。</param>
        public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);

        /// <summary>共通要素があるか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

        /// <summary>同じ要素の集合か。</summary>
        /// <param name="other">比較対象。</param>
        public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

        /// <summary>集合の中身を、Unity が保存するリストに展開する。</summary>
        public void OnBeforeSerialize()
        {
            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            _items.Clear();
            foreach (var item in _set) _items.Add(item);
        }

        /// <summary>リストから集合を組み直し、重複した位置を記録する。</summary>
        public void OnAfterDeserialize()
        {
            _set.Clear();
            DuplicateIndices.Clear();
            _runtimeDirty = false;

            for (var i = 0; i < _items.Count; i++)
            {
                if (!_set.Add(_items[i])) DuplicateIndices.Add(i);
            }
        }

        /// <summary>要素を走査する構造体列挙子を返す。</summary>
        public HashSet<T>.Enumerator GetEnumerator() => _set.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _set.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _set.GetEnumerator();
    }
}
