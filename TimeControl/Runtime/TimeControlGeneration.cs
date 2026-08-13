using System.Collections.Generic;

namespace TimeControl
{
    /// <summary>1回の所有期間に属する取得権と、任意スレッドから届く解放要求を管理する。</summary>
    internal sealed class TimeControlGeneration
    {
        private readonly object _sync = new object();
        private readonly Dictionary<long, float> _activeMultipliers = new Dictionary<long, float>();
        private readonly Queue<long> _pendingReleases = new Queue<long>();
        private readonly HashSet<long> _pendingReleaseIds = new HashSet<long>();
        private TimeControlController _controller;
        private long _nextLeaseId;
        private bool _closed;

        /// <summary>解放要求の通知先となるControllerを持つ世代を作る。</summary>
        /// <param name="controller">この世代を所有するController。</param>
        internal TimeControlGeneration(TimeControlController controller)
        {
            _controller = controller;
        }

        /// <summary>新しい取得権を登録する。</summary>
        /// <param name="multiplier">取得権が要求する相対倍率。</param>
        /// <returns>登録した取得権の識別子。</returns>
        internal long Add(float multiplier)
        {
            lock (_sync)
            {
                if (_closed) return 0L;
                var leaseId = ++_nextLeaseId;
                _activeMultipliers.Add(leaseId, multiplier);
                return leaseId;
            }
        }

        /// <summary>取得権が現在の世代で有効ならtrue。</summary>
        /// <param name="leaseId">調べる取得権の識別子。</param>
        /// <returns>未解放かつ世代が有効ならtrue。</returns>
        internal bool IsLeaseActive(long leaseId)
        {
            lock (_sync)
            {
                return !_closed && _activeMultipliers.ContainsKey(leaseId) && !_pendingReleaseIds.Contains(leaseId);
            }
        }

        /// <summary>Disposeされた取得権を待機列へ移し、Controllerへ安全に通知する。</summary>
        /// <param name="leaseId">解放する取得権の識別子。</param>
        internal void RequestRelease(long leaseId)
        {
            TimeControlController controller;
            lock (_sync)
            {
                if (_closed || !_activeMultipliers.ContainsKey(leaseId) || !_pendingReleaseIds.Add(leaseId)) return;
                _pendingReleases.Enqueue(leaseId);
                controller = _controller;
            }

            controller?.OnLeaseReleaseQueued(this);
        }

        /// <summary>待機中の解放要求を破棄し、現在の倍率一覧を複製する。</summary>
        /// <param name="multipliers">現在有効な倍率の複製。</param>
        /// <returns>待機中の解放要求が1件以上あった場合はtrue。</returns>
        internal bool DrainPending(out float[] multipliers)
        {
            lock (_sync)
            {
                var hadPending = _pendingReleases.Count > 0;
                while (_pendingReleases.Count > 0)
                {
                    var leaseId = _pendingReleases.Dequeue();
                    _activeMultipliers.Remove(leaseId);
                    _pendingReleaseIds.Remove(leaseId);
                }

                multipliers = new float[_activeMultipliers.Count];
                _activeMultipliers.Values.CopyTo(multipliers, 0);
                return hadPending;
            }
        }

        /// <summary>現在有効な倍率の複製を返す。</summary>
        /// <returns>呼出時点の倍率一覧。</returns>
        internal float[] SnapshotMultipliers()
        {
            lock (_sync)
            {
                var result = new float[_activeMultipliers.Count];
                _activeMultipliers.Values.CopyTo(result, 0);
                return result;
            }
        }

        /// <summary>現在有効な取得権の数を返す。</summary>
        internal int ActiveLeaseCount
        {
            get
            {
                lock (_sync) return _closed ? 0 : _activeMultipliers.Count;
            }
        }

        /// <summary>世代を無効化し、全取得権とController参照を切り離す。</summary>
        internal void Close()
        {
            lock (_sync)
            {
                if (_closed) return;
                _closed = true;
                _activeMultipliers.Clear();
                _pendingReleases.Clear();
                _pendingReleaseIds.Clear();
                _controller = null;
            }
        }
    }
}
