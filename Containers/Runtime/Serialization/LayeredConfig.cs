using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 積み重ねた層から値を解決するコンテナ。優先度の高い順に、既定値 → 難易度プロファイル →
    /// ユーザー設定 → 実行時のチートやデバッグ上書き、という形で引く。
    /// <para>
    /// 潰さずに層のまま持つ理由は 2 つ。上書きを解除したときに<b>下にあった値へ正しく戻る</b>必要が
    /// あること、そして値がおかしいとき最初に知りたいのが「どの層が決めたのか」であること。
    /// 後者には <see cref="TryResolveWithSource"/> が答える。
    /// </para>
    /// </summary>
    public sealed class LayeredConfig<TKey, TValue>
    {
        private readonly List<Layer> _layers = new List<Layer>();

        private sealed class Layer
        {
            public string Name;
            public int Priority;

            /// <summary>追加された順。同じ優先度どうしの決着に使う。</summary>
            public long Sequence;

            public readonly Dictionary<TKey, TValue> Values = new Dictionary<TKey, TValue>();
        }

        private long _nextSequence;

        /// <summary>解決結果が変わりうる変更が起きたときに発火する。</summary>
        public event Action Changed;

        /// <summary>層の数。</summary>
        public int LayerCount => _layers.Count;

        /// <summary>
        /// 層を作る、または作り直す。<paramref name="priority"/> が大きいほど優先され、
        /// 同値なら後から追加した層が勝つ。
        /// </summary>
        /// <param name="name">層の名前。解決元の表示にも使う。</param>
        /// <param name="priority">優先度。大きいほど強い。</param>
        /// <param name="values">初期値。null 可。</param>
        public void SetLayer(string name, int priority, IEnumerable<KeyValuePair<TKey, TValue>> values = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("層の名前は必須。", nameof(name));

            var layer = FindLayer(name);
            if (layer == null)
            {
                layer = new Layer { Name = name, Sequence = _nextSequence++ };
                _layers.Add(layer);
            }

            layer.Priority = priority;
            layer.Values.Clear();

            if (values != null)
            {
                foreach (var pair in values) layer.Values[pair.Key] = pair.Value;
            }

            Sort();
            Changed?.Invoke();
        }

        /// <summary>指定した層に値を設定する。</summary>
        /// <param name="layerName">対象の層。</param>
        /// <param name="key">キー。</param>
        /// <param name="value">値。</param>
        public void Set(string layerName, TKey key, TValue value)
        {
            var layer = FindLayer(layerName) ?? throw new KeyNotFoundException($"'{layerName}' という層は無い。");
            layer.Values[key] = value;
            Changed?.Invoke();
        }

        /// <summary>指定した層からキーを外し、下の層の値を露出させる。</summary>
        /// <param name="layerName">対象の層。</param>
        /// <param name="key">外すキー。</param>
        public bool Unset(string layerName, TKey key)
        {
            var layer = FindLayer(layerName);
            if (layer == null || !layer.Values.Remove(key)) return false;

            Changed?.Invoke();
            return true;
        }

        /// <summary>層ごと取り除く。</summary>
        /// <param name="name">取り除く層の名前。</param>
        public bool RemoveLayer(string name)
        {
            var layer = FindLayer(name);
            if (layer == null) return false;

            _layers.Remove(layer);
            Changed?.Invoke();
            return true;
        }

        /// <summary>層は残したまま、その中身だけを空にする。</summary>
        /// <param name="name">対象の層。</param>
        public void ClearLayer(string name)
        {
            var layer = FindLayer(name);
            if (layer == null || layer.Values.Count == 0) return;

            layer.Values.Clear();
            Changed?.Invoke();
        }

        /// <summary>値を解決する。どの層も持っていなければ false。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">解決した値。</param>
        public bool TryResolve(TKey key, out TValue value) => TryResolveWithSource(key, out value, out _);

        /// <summary>値を解決し、どの層が答えたかも返す。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">解決した値。</param>
        /// <param name="layerName">値を持っていた層の名前。</param>
        public bool TryResolveWithSource(TKey key, out TValue value, out string layerName)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                if (!_layers[i].Values.TryGetValue(key, out value)) continue;

                layerName = _layers[i].Name;
                return true;
            }

            value = default;
            layerName = null;
            return false;
        }

        /// <summary>値を解決する。無ければ <paramref name="fallback"/>。</summary>
        /// <param name="key">キー。</param>
        /// <param name="fallback">どの層も持っていないときに返す値。</param>
        public TValue Resolve(TKey key, TValue fallback = default) =>
            TryResolve(key, out var value) ? value : fallback;

        /// <summary>どこかの層が定義している全キーを、それぞれ解決した結果として平坦化する。</summary>
        public Dictionary<TKey, TValue> Flatten()
        {
            var result = new Dictionary<TKey, TValue>();

            // 優先度の低い層から順に上書きしていく。
            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                foreach (var pair in _layers[i].Values) result[pair.Key] = pair.Value;
            }

            return result;
        }

        private Layer FindLayer(string name)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                if (string.Equals(_layers[i].Name, name, StringComparison.Ordinal)) return _layers[i];
            }

            return null;
        }

        /// <summary>
        /// 優先度の降順、同値なら追加が新しい順に並べる。
        /// <para>
        /// <see cref="List{T}.Sort(Comparison{T})"/> は安定ソートではないので、優先度だけで比較すると
        /// 同値の層の並びが不定になり、<see cref="SetLayer"/> のたびに入れ替わりうる。
        /// 追加順を第 2 キーにして順序を確定させている。
        /// </para>
        /// </summary>
        private void Sort() => _layers.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.Sequence.CompareTo(a.Sequence);
        });
    }
}
