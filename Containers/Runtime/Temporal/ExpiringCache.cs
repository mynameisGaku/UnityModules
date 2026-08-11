using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 各エントリに寿命を持たせ、期限切れを自動的に落とすキャッシュ。
    /// <para>
    /// <see cref="LruCache{TKey,TValue}"/> が「件数」で捨てるのに対し、こちらは「時間」で捨てる。
    /// 使い分けの基準は、古さそのものが問題になるかどうか ——
    /// 視界判定の結果、経路探索のキャッシュ、サーバから取得したランキング、直近の被弾記録のように、
    /// <b>古い値は少なくなっても正しくない</b>データに向く。
    /// </para>
    /// <para>
    /// 掃除は遅延方式。読むときに期限を見て、たまに全体を掃く。捨てるためだけに毎フレーム走査はしない。
    /// </para>
    /// </summary>
    public sealed class ExpiringCache<TKey, TValue>
    {
        private struct Entry
        {
            public TValue Value;
            public double ExpiresAt;
        }

        private readonly Dictionary<TKey, Entry> _entries;
        private readonly FastList<TKey> _sweepBuffer = new FastList<TKey>();
        private readonly double _defaultLifetime;

        private double _now;
        private int _operationsSinceSweep;

        /// <summary>掃除の最中か。<see cref="Expired"/> ハンドラからの再入を止めるために見る。</summary>
        private bool _sweeping;

        /// <summary>期限切れで落ちたエントリを通知する。解放処理が要る値のときに使う。</summary>
        public event Action<TKey, TValue> Expired;

        /// <param name="defaultLifetime">寿命を明示しなかったときに使う秒数。</param>
        /// <param name="comparer">キーの比較方法。</param>
        public ExpiringCache(double defaultLifetime, IEqualityComparer<TKey> comparer = null)
        {
            if (defaultLifetime <= 0d) throw new ArgumentOutOfRangeException(nameof(defaultLifetime));

            _defaultLifetime = defaultLifetime;
            _entries = new Dictionary<TKey, Entry>(comparer);
        }

        /// <summary>自動掃除を挟む間隔（操作回数）。</summary>
        public int SweepInterval { get; set; } = 64;

        /// <summary>期限切れを含む件数。正確な数が要るなら <see cref="Sweep"/> の後に読む。</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// このキャッシュの時計を進める。<c>Time.time</c> を毎フレーム渡す想定。
        /// これを呼ばない限り期限は進まないので、ポーズ中に寿命が減ることもない。
        /// </summary>
        public void SetTime(double now) => _now = now;

        public void Set(TKey key, TValue value) => Set(key, value, _defaultLifetime);

        public void Set(TKey key, TValue value, double lifetime)
        {
            _entries[key] = new Entry { Value = value, ExpiresAt = _now + lifetime };
            Tick();
        }

        /// <summary>期限切れは「無い」扱いで返す。</summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            Tick();

            if (!_entries.TryGetValue(key, out var entry))
            {
                value = default;
                return false;
            }

            if (entry.ExpiresAt <= _now)
            {
                _entries.Remove(key);
                Expired?.Invoke(key, entry.Value);
                value = default;
                return false;
            }

            value = entry.Value;
            return true;
        }

        /// <summary>未登録または期限切れなら <paramref name="factory"/> で作り直す。</summary>
        public TValue GetOrRefresh(TKey key, Func<TKey, TValue> factory, double lifetime = 0d)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (TryGetValue(key, out var value)) return value;

            value = factory(key);
            Set(key, value, lifetime > 0d ? lifetime : _defaultLifetime);
            return value;
        }

        public bool ContainsKey(TKey key) => TryGetValue(key, out _);

        /// <summary>残り寿命（秒）。無ければ 0。</summary>
        public double RemainingLifetime(TKey key) =>
            _entries.TryGetValue(key, out var entry) ? Math.Max(0d, entry.ExpiresAt - _now) : 0d;

        /// <summary>寿命を延長する。存在しないキーには何もしない。</summary>
        public bool Touch(TKey key, double lifetime = 0d)
        {
            if (!_entries.TryGetValue(key, out var entry)) return false;
            if (entry.ExpiresAt <= _now) return false;

            entry.ExpiresAt = _now + (lifetime > 0d ? lifetime : _defaultLifetime);
            _entries[key] = entry;
            return true;
        }

        public bool Remove(TKey key) => _entries.Remove(key);

        public void Clear()
        {
            _entries.Clear();
            _operationsSinceSweep = 0;
        }

        /// <summary>期限切れを今すぐ全部落とす。落とした件数を返す。</summary>
        public int Sweep()
        {
            // Expired ハンドラがこのキャッシュに触ると Tick 経由で Sweep に再入する。
            // 再入を許すと、まだ外していないキーを内側がもう一度拾って
            // Expired を二重に発火させ、さらに _sweepBuffer を空にして外側の走査を打ち切る。
            if (_sweeping) return 0;

            _sweeping = true;
            _operationsSinceSweep = 0;

            try
            {
                _sweepBuffer.Clear();

                foreach (var pair in _entries)
                {
                    if (pair.Value.ExpiresAt <= _now) _sweepBuffer.Add(pair.Key);
                }

                var removed = 0;
                for (var i = 0; i < _sweepBuffer.Count; i++)
                {
                    var key = _sweepBuffer[i];
                    if (!_entries.TryGetValue(key, out var entry)) continue;

                    // 通知より先に外す。解放処理を行うハンドラが二度呼ばれない。
                    _entries.Remove(key);
                    removed++;
                    Expired?.Invoke(key, entry.Value);
                }

                _sweepBuffer.Clear();
                return removed;
            }
            finally
            {
                _sweeping = false;
            }
        }

        private void Tick()
        {
            if (_sweeping) return;
            if (++_operationsSinceSweep < SweepInterval) return;

            Sweep();
        }
    }
}
