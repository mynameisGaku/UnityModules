using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers.Gameplay
{
    /// <summary>
    /// 端で折り返す選択カーソルを持つリスト。武器ホイール、タブ、カルーセル、キャラ選択。
    /// <para>
    /// 小さいが毎回書く羽目になる型。<c>(index + 1) % count</c> は前方向なら正しいが、
    /// 後方向の <c>(index - 1) % count</c> は<b>負数になって落ちる</b> ——
    /// これを毎回どこかで踏むので、一度正しく書いて共有する価値がある。
    /// </para>
    /// <para>
    /// 空のときに選択を要求しても例外にはならず、<see cref="HasSelection"/> が false になる。
    /// UI から使う以上、空リストは異常ではなく通常の状態として扱うべきだから。
    /// </para>
    /// </summary>
    public sealed class LoopingList<T> : IReadOnlyList<T>
    {
        private readonly List<T> _items;
        private int _index;

        public LoopingList() => _items = new List<T>();

        public LoopingList(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _items = new List<T>(source);
        }

        /// <summary>選択が変わったときに発火する。</summary>
        public event Action<T> SelectionChanged;

        public int Count => _items.Count;
        public bool HasSelection => _items.Count > 0;

        public T this[int index] => _items[index];

        /// <summary>選択位置。範囲外を設定しても折り返して収まる。</summary>
        public int Index
        {
            get => _index;
            set
            {
                if (_items.Count == 0)
                {
                    _index = 0;
                    return;
                }

                var wrapped = Wrap(value);
                if (wrapped == _index) return;

                _index = wrapped;
                SelectionChanged?.Invoke(_items[_index]);
            }
        }

        /// <summary>今選ばれている要素。空なら <c>default</c>。</summary>
        public T Current => _items.Count == 0 ? default : _items[_index];

        public void Add(T item)
        {
            _items.Add(item);
            if (_items.Count == 1) SelectionChanged?.Invoke(item);
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
            _items.RemoveAt(index);

            if (_items.Count == 0)
            {
                _index = 0;
                return;
            }

            // 選択位置より前が消えたらカーソルもずらし、消えたのが選択自身なら同じ位置に留まる。
            if (index < _index) _index--;
            _index = Wrap(_index);
            SelectionChanged?.Invoke(_items[_index]);
        }

        public void Clear()
        {
            _items.Clear();
            _index = 0;
        }

        /// <summary>次へ。末尾なら先頭へ折り返す。</summary>
        public T Next() => Step(1);

        /// <summary>前へ。先頭なら末尾へ折り返す。</summary>
        public T Previous() => Step(-1);

        /// <summary><paramref name="delta"/> 個ぶん進める。負なら戻る。</summary>
        public T Step(int delta)
        {
            if (_items.Count == 0) return default;

            Index = _index + delta;
            return _items[_index];
        }

        /// <summary>指定した要素を選ぶ。見つからなければ選択は変わらない。</summary>
        public bool Select(T item)
        {
            var index = _items.IndexOf(item);
            if (index < 0) return false;

            Index = index;
            return true;
        }

        /// <summary>
        /// 条件を満たす次の要素へ進む。1 周しても見つからなければ選択は変わらない。
        /// 「弾のある武器だけ切り替える」ような用途向け。
        /// </summary>
        public bool NextWhere(Predicate<T> predicate, int direction = 1)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (_items.Count == 0) return false;

            var step = direction >= 0 ? 1 : -1;

            for (var offset = 1; offset <= _items.Count; offset++)
            {
                var candidate = Wrap(_index + step * offset);
                if (!predicate(_items[candidate])) continue;

                Index = candidate;
                return true;
            }

            return false;
        }

        /// <summary>負数でも正しく折り返す剰余。この 1 行がこの型の存在理由。</summary>
        private int Wrap(int index)
        {
            var count = _items.Count;
            if (count == 0) return 0;

            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}
