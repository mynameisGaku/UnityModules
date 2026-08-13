using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// 「今生きているもの」の集合を持つ ScriptableObject。各オブジェクトが自分で出入りする。
    /// <para>
    /// 「全ての稼働中の敵」「全ての制圧ポイント」といった問い合わせに対する、
    /// static なマネージャシングルトンの代わり。誰もマネージャを探しに行かなくてよく、
    /// 参照は Inspector で繋がり、シーンをまたいで生かし続ける対象も要らない。
    /// </para>
    /// <para>
    /// 注意点が 1 つ：ScriptableObject の中身はエディタで再生を止めても残るため、
    /// 有効化時に中身を捨てて、前回の実行の残骸を次に持ち込まないようにしている。
    /// </para>
    /// <code>
    /// [Serializable] public sealed class EnemySet : RuntimeSet&lt;Enemy&gt; { }   // アセットの型
    /// void OnEnable()  => _set.Register(this);
    /// void OnDisable() => _set.Unregister(this);
    /// </code>
    /// </summary>
    public abstract class RuntimeSet<T> : ScriptableObject, IReadOnlyList<T> where T : class
    {
        private readonly FastList<T> _items = new FastList<T>();

        /// <summary>要素が加わったときに発火する。</summary>
        public event Action<T> Added;

        /// <summary>要素が抜けたときに発火する。</summary>
        public event Action<T> Removed;

        /// <summary>今入っている数。</summary>
        public int Count => _items.Count;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>位置で要素を取る。並び順は登録順だが、削除で変わる。</summary>
        public T this[int index] => _items[index];

        /// <summary>まだ入っていなければ加える。集合が変化したかを返す。</summary>
        /// <param name="item">加える要素。</param>
        public bool Register(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_items.Contains(item)) return false;

            _items.Add(item);
            Added?.Invoke(item);
            return true;
        }

        /// <summary>取り除く。末尾と入れ替える O(1) の削除なので、並び順は保たれない。</summary>
        /// <param name="item">取り除く要素。</param>
        public bool Unregister(T item)
        {
            if (item == null) return false;
            if (!_items.RemoveSwapBack(item)) return false;

            Removed?.Invoke(item);
            return true;
        }

        /// <summary>含まれているか。</summary>
        /// <param name="item">探す要素。</param>
        public bool Contains(T item) => _items.Contains(item);

        /// <summary>スナップショットを作る。走査中に出入りが起きても安全。</summary>
        public T[] ToArray() => _items.ToArray();

        /// <summary>一様にランダムな 1 件。空なら null。</summary>
        public T Random() => _items.Count == 0 ? null : _items[UnityEngine.Random.Range(0, _items.Count)];

        /// <summary>全て取り除く。</summary>
        public void Clear()
        {
            if (Removed != null)
            {
                for (var i = _items.Count - 1; i >= 0; i--) Removed.Invoke(_items[i]);
            }

            _items.Clear();
        }

        /// <summary>
        /// 前回の実行から残ったエントリを捨てる。
        /// 派生側でオーバーライドする場合は base の呼び出しが必要。
        /// </summary>
        protected virtual void OnEnable() => _items.Clear();

        /// <summary>確保無しで走査する構造体列挙子を返す。</summary>
        public FastList<T>.Enumerator GetEnumerator() => _items.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    }
}
