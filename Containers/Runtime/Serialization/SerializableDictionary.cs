using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// Unity がシリアライズでき、Inspector に表示できる辞書。
    /// <para>
    /// Unity のシリアライザは配列と <see cref="List{T}"/> は扱えるが
    /// <see cref="Dictionary{TKey,TValue}"/> は扱えない。そのせいで enum → プレハブのような
    /// 対応表が「List ＋ 線形探索」になりがちだった。ここでは実行時は本物の辞書のまま、
    /// ディスク上は 2 本の並列リストとして持ち、シリアライズのコールバックで相互に変換する。
    /// </para>
    /// <para>
    /// Unity にシリアライズさせるには具体型で継承する：
    /// <code>
    /// [Serializable] public sealed class LootTableMap : SerializableDictionary&lt;EnemyKind, LootTable&gt; { }
    /// </code>
    /// </para>
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> :
        IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>,
        ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> _keys = new List<TKey>();
        [SerializeField] private List<TValue> _values = new List<TValue>();

        [NonSerialized] private Dictionary<TKey, TValue> _map = new Dictionary<TKey, TValue>();

        /// <summary>
        /// 実行時 API 経由で中身が変わったか。
        /// <para>
        /// これが無いと、Inspector 編集を守るための「空なら書き戻さない」判定が、
        /// 実行時の <see cref="Clear"/> まで無かったことにしてしまう ——
        /// 消したはずのエントリが次のデシリアライズで全部戻る。
        /// 「誰が最後に書き換えたか」で分岐する必要がある。
        /// </para>
        /// </summary>
        [NonSerialized] private bool _runtimeDirty;

        /// <summary>
        /// シリアライズされたキー配列のうち、先に出たキーと重複している位置。
        /// 実行時は最初の 1 件だけが採用され、ここに記録された分は捨てられる。
        /// </summary>
        [NonSerialized] public readonly List<int> DuplicateKeyIndices = new List<int>();

        /// <summary>空の辞書を作る。</summary>
        public SerializableDictionary() { }

        /// <summary>既存の辞書から中身をコピーして作る。</summary>
        /// <param name="source">コピー元。</param>
        public SerializableDictionary(IDictionary<TKey, TValue> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var pair in source) _map[pair.Key] = pair.Value;

            // コード側で作った中身なので、保存対象として印を付ける。
            // 忘れると OnBeforeSerialize が書き戻しを飛ばし、空のまま保存される。
            _runtimeDirty = true;
        }

        /// <summary>
        /// 実体の辞書。
        /// <para>
        /// <b>ここを直接書き換えた場合は <see cref="MarkDirty"/> を呼ぶこと。</b>
        /// このクラスは実行時 API を経由した変更しか検知できないので、
        /// 印を付けないと変更が保存されず、次のデシリアライズで巻き戻る。
        /// </para>
        /// </summary>
        public Dictionary<TKey, TValue> AsDictionary() => _map;

        /// <summary>
        /// <see cref="AsDictionary"/> 経由で中身を書き換えたことを知らせる。
        /// 次のシリアライズで保存されるようになる。
        /// </summary>
        public void MarkDirty() => _runtimeDirty = true;

        /// <summary>要素数。</summary>
        public int Count => _map.Count;

        /// <summary>常に false。</summary>
        public bool IsReadOnly => false;

        /// <summary>キーの一覧。</summary>
        public Dictionary<TKey, TValue>.KeyCollection Keys => _map.Keys;

        /// <summary>値の一覧。</summary>
        public Dictionary<TKey, TValue>.ValueCollection Values => _map.Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => _map.Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => _map.Values;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _map.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _map.Values;

        /// <summary>値の読み書き。</summary>
        public TValue this[TKey key]
        {
            get => _map[key];
            set
            {
                _map[key] = value;
                _runtimeDirty = true;
            }
        }

        /// <summary>値を追加する。キーが既にあれば例外。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">値。</param>
        public void Add(TKey key, TValue value)
        {
            _map.Add(key, value);
            _runtimeDirty = true;
        }

        /// <summary>キーが存在するか。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        /// <summary>値を読む。無ければ false。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);

        /// <summary>エントリを削除する。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(TKey key)
        {
            if (!_map.Remove(key)) return false;

            _runtimeDirty = true;
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _map.Clear();
            _runtimeDirty = true;
        }

        /// <summary>値を読む。無ければ <paramref name="fallback"/>。</summary>
        /// <param name="key">キー。</param>
        /// <param name="fallback">未登録のときに返す値。</param>
        public TValue GetValueOrDefault(TKey key, TValue fallback = default) =>
            _map.TryGetValue(key, out var value) ? value : fallback;

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
            _map.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!((ICollection<KeyValuePair<TKey, TValue>>)_map).Remove(item)) return false;

            _runtimeDirty = true;
            return true;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_map).CopyTo(array, arrayIndex);

        /// <summary>
        /// 辞書の中身を、Unity が保存する 2 本のリストに展開する。
        /// <para>
        /// 書き戻すのは実行時 API で中身を変えた場合だけ。Inspector が
        /// <c>_keys</c> / <c>_values</c> を直接編集した直後に書き戻すと、
        /// まだ辞書に反映されていない編集内容を消してしまう。
        /// </para>
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            _keys.Clear();
            _values.Clear();

            foreach (var pair in _map)
            {
                _keys.Add(pair.Key);
                _values.Add(pair.Value);
            }
        }

        /// <summary>
        /// 2 本のリストから辞書を組み直す。重複したキーは最初の 1 件を残し、
        /// 後から来た位置を <see cref="DuplicateKeyIndices"/> に記録する。
        /// </summary>
        public void OnAfterDeserialize()
        {
            _map.Clear();
            DuplicateKeyIndices.Clear();
            _runtimeDirty = false;   // ここではリストが正であり、辞書はその写し

            var count = Math.Min(_keys.Count, _values.Count);
            for (var i = 0; i < count; i++)
            {
                var key = _keys[i];
                if (key == null) continue;

                if (_map.ContainsKey(key))
                {
                    DuplicateKeyIndices.Add(i);
                    continue;
                }

                _map.Add(key, _values[i]);
            }
        }

        /// <summary>(キー, 値) を走査する構造体列挙子を返す。</summary>
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _map.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
    }
}
