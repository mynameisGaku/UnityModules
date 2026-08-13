using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers.Gameplay
{
    /// <summary>
    /// 重み付き抽選。ドロップテーブル、ランダムイベント、スポーン種別の選択。
    /// <para>
    /// 素朴な実装は毎回テーブルを線形に舐めて O(n) だが、抽選回数の方が要素数より圧倒的に多いのが
    /// この手のテーブルの常なので、ここでは前処理で <b>Alias 法</b>の表を作り、
    /// 1 回の抽選を <b>O(1)</b> にしている。要素 100 件のテーブルを毎フレーム引いても差が出ない。
    /// </para>
    /// <para>
    /// 重みを変更すると表は自動的に作り直される。ビルドは要素数に比例するので、
    /// 抽選のたびに重みを書き換える使い方には向かない。
    /// </para>
    /// <para>
    /// Inspector で編集したい場合は、具体型で継承したうえで
    /// <c>[SerializeField]</c> のフィールドとして持つこと：
    /// <code>
    /// [Serializable] public sealed class LootTable : WeightedRandomList&lt;ItemDrop&gt; { }
    /// </code>
    /// </para>
    /// </summary>
    [Serializable]
    public class WeightedRandomList<T> : ISerializationCallbackReceiver
    {
        // Unity は private かつ readonly のフィールドを保存しない。
        // [SerializeField] を付けたうえで readonly を外さないと、[Serializable] を
        // 付けていても Inspector には何も出ず、実行時も空のまま復元される。
        [SerializeField] private List<T> _items = new List<T>();
        [SerializeField] private List<float> _weights = new List<float>();

        // Alias 法の表。_probability[i] は「i 番をそのまま採用する確率」、
        // 外れたら _alias[i] を採用する。
        private float[] _probability;
        private int[] _alias;
        private bool _tableValid;
        private float _totalWeight;

        /// <summary>要素数。</summary>
        public int Count => _items.Count;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>何もしない。保存前に必要な整形は無い。</summary>
        public void OnBeforeSerialize() { }

        /// <summary>
        /// 読み込み後に Alias 表を無効化する。表は重みから作られるので、
        /// 作り直さないと Inspector で編集した重みが抽選に反映されない。
        /// </summary>
        public void OnAfterDeserialize()
        {
            if (_items == null) _items = new List<T>();
            if (_weights == null) _weights = new List<float>();

            _tableValid = false;
        }

        /// <summary>重みの合計。</summary>
        public float TotalWeight
        {
            get
            {
                if (!_tableValid) Rebuild();
                return _totalWeight;
            }
        }

        public T ItemAt(int index) => _items[index];
        public float WeightAt(int index) => _weights[index];

        /// <summary>要素を追加する。重みは正の値でなければならない。</summary>
        public void Add(T item, float weight)
        {
            if (weight <= 0f) throw new ArgumentOutOfRangeException(nameof(weight), "重みは正の値が必要。");

            _items.Add(item);
            _weights.Add(weight);
            _tableValid = false;
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
            _weights.RemoveAt(index);
            _tableValid = false;
        }

        /// <summary>重みを差し替える。次の抽選時に表が作り直される。</summary>
        public void SetWeight(int index, float weight)
        {
            if (weight <= 0f) throw new ArgumentOutOfRangeException(nameof(weight), "重みは正の値が必要。");

            _weights[index] = weight;
            _tableValid = false;
        }

        public void Clear()
        {
            _items.Clear();
            _weights.Clear();
            _tableValid = false;
        }

        /// <summary>Unity の共有乱数で 1 つ引く。</summary>
        public T Draw()
        {
            if (_items.Count == 0) throw new InvalidOperationException("空のテーブルからは抽選できない。");
            return Draw(UnityEngine.Random.value, UnityEngine.Random.value);
        }

        /// <summary>
        /// 乱数源を明示して引く。<see cref="System.Random"/> を渡せば、
        /// シード固定でリプレイやテストの再現ができる。
        /// </summary>
        public T Draw(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (_items.Count == 0) throw new InvalidOperationException("空のテーブルからは抽選できない。");

            return Draw((float)random.NextDouble(), (float)random.NextDouble());
        }

        /// <summary>0〜1 の乱数 2 つを外から与えて引く。決定論が要る場面向け。</summary>
        /// <param name="bucketRoll">どの箱を見るかを決める 0〜1 の値。</param>
        /// <param name="coinRoll">箱の中で本体と別名のどちらを採るかを決める 0〜1 の値。</param>
        public T Draw(float bucketRoll, float coinRoll)
        {
            if (_items.Count == 0) throw new InvalidOperationException("空のテーブルからは抽選できない。");
            if (!_tableValid) Rebuild();

            var bucket = Mathf.Min((int)(bucketRoll * _items.Count), _items.Count - 1);

            // coinRoll は 1.0 を取りうる（UnityEngine.Random.value は上端を含む）。
            // 確率 1 の箱では比較が偽になるが、その箱の別名は自分自身なので結果は変わらない。
            return coinRoll < _probability[bucket] ? _items[bucket] : _items[_alias[bucket]];
        }

        /// <summary>重複ありで <paramref name="count"/> 個引く。</summary>
        public void DrawMany(int count, FastList<T> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            for (var i = 0; i < count; i++) results.Add(Draw());
        }

        /// <summary>
        /// ある要素が引かれる確率（0〜1）。デバッグ表示やテーブルのバランス確認に使う。
        /// </summary>
        public float ProbabilityOf(int index)
        {
            if (!_tableValid) Rebuild();
            return _totalWeight <= 0f ? 0f : _weights[index] / _totalWeight;
        }

        /// <summary>
        /// Vose の Alias 法で表を作る。重みを平均で正規化し、
        /// 「平均より小さい箱」と「大きい箱」を突き合わせて各箱をちょうど 1 単位に埋めていく。
        /// </summary>
        private void Rebuild()
        {
            var count = _items.Count;
            _probability = new float[count];
            _alias = new int[count];
            _totalWeight = 0f;

            // 別名の初期値は自分自身。確率 1 の箱に別名が割り当てられないまま残っても、
            // 0 番目の要素に化けるのではなくその箱自身が返る。
            for (var i = 0; i < count; i++) _alias[i] = i;

            if (count == 0)
            {
                _tableValid = true;
                return;
            }

            for (var i = 0; i < count; i++) _totalWeight += _weights[i];

            var scaled = new float[count];
            for (var i = 0; i < count; i++) scaled[i] = _weights[i] * count / _totalWeight;

            var small = new Stack<int>(count);
            var large = new Stack<int>(count);

            for (var i = count - 1; i >= 0; i--)
            {
                if (scaled[i] < 1f) small.Push(i);
                else large.Push(i);
            }

            while (small.Count > 0 && large.Count > 0)
            {
                var less = small.Pop();
                var more = large.Pop();

                _probability[less] = scaled[less];
                _alias[less] = more;

                // 使った分を差し引いて、余りを再分類する。
                scaled[more] -= 1f - scaled[less];
                if (scaled[more] < 1f) small.Push(more);
                else large.Push(more);
            }

            // 浮動小数の誤差で残ったものは確率 1 として扱う。
            while (large.Count > 0) _probability[large.Pop()] = 1f;
            while (small.Count > 0) _probability[small.Pop()] = 1f;

            _tableValid = true;
        }
    }
}
