using System.Collections.Generic;

namespace InputGate
{
    /// <summary>1回の所有期間に属する取得権と、任意スレッドから届く解放要求を管理する。</summary>
    internal sealed class InputGateGeneration
    {
        private readonly object _sync = new object();
        private readonly HashSet<long> _activeLeaseIds = new HashSet<long>();
        private readonly Queue<long> _pendingReleases = new Queue<long>();
        private readonly HashSet<long> _pendingReleaseIds = new HashSet<long>();
        private InputGateController _controller;
        private long _nextLeaseId;
        private bool _closed;

        /// <summary>解放要求の通知先となるControllerを持つ世代を作る。</summary>
        /// <param name="controller">この世代を所有するController。</param>
        internal InputGateGeneration(InputGateController controller)
        {
            _controller = controller;
        }

        /// <summary>新しい取得権を登録する。</summary>
        /// <returns>登録した取得権の識別子。世代終了後は0。</returns>
        internal long Add()
        {
            lock (_sync)
            {
                if (_closed) return 0L;
                var leaseId = ++_nextLeaseId;
                _activeLeaseIds.Add(leaseId);
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
                return !_closed && _activeLeaseIds.Contains(leaseId) && !_pendingReleaseIds.Contains(leaseId);
            }
        }

        /// <summary>Disposeされた取得権を待機列へ移し、Controllerへ安全に通知する。</summary>
        /// <param name="leaseId">解放する取得権の識別子。</param>
        internal void RequestRelease(long leaseId)
        {
            InputGateController controller;
            lock (_sync)
            {
                if (_closed || !_activeLeaseIds.Contains(leaseId) || !_pendingReleaseIds.Add(leaseId)) return;
                _pendingReleases.Enqueue(leaseId);
                controller = _controller;
            }

            controller?.OnLeaseReleaseQueued(this);
        }

        /// <summary>待機中の解放要求を反映し、残っている取得権数を返す。</summary>
        /// <param name="activeLeaseCount">反映後の有効な取得権数。</param>
        /// <returns>待機中の解放要求が1件以上あった場合はtrue。</returns>
        internal bool DrainPending(out int activeLeaseCount)
        {
            lock (_sync)
            {
                var hadPending = _pendingReleases.Count > 0;
                while (_pendingReleases.Count > 0)
                {
                    var leaseId = _pendingReleases.Dequeue();
                    _activeLeaseIds.Remove(leaseId);
                    _pendingReleaseIds.Remove(leaseId);
                }

                activeLeaseCount = _closed ? 0 : _activeLeaseIds.Count;
                return hadPending;
            }
        }

        /// <summary>現在有効な取得権数。</summary>
        internal int ActiveLeaseCount
        {
            get
            {
                lock (_sync) return _closed ? 0 : _activeLeaseIds.Count;
            }
        }

        /// <summary>世代を無効化し、全取得権とController参照を切り離す。</summary>
        internal void Close()
        {
            lock (_sync)
            {
                if (_closed) return;
                _closed = true;
                _activeLeaseIds.Clear();
                _pendingReleases.Clear();
                _pendingReleaseIds.Clear();
                _controller = null;
            }
        }
    }
}
