using System;
using System.Collections.Generic;
using System.Threading;

namespace AudioControl
{
    internal sealed class AudioControlGeneration
    {
        private readonly object _gate = new object();
        private readonly Queue<long> _pendingReleases = new Queue<long>();
        private readonly int _ownerThreadId;
        private Action<long> _mainThreadRelease;
        private bool _closed;

        internal AudioControlGeneration(int ownerThreadId, Action<long> mainThreadRelease)
        {
            _ownerThreadId = ownerThreadId;
            _mainThreadRelease = mainThreadRelease;
        }

        internal void ReleaseFromHandle(long voiceId)
        {
            Action<long> immediate = null;
            lock (_gate)
            {
                if (_closed)
                {
                    return;
                }

                if (Thread.CurrentThread.ManagedThreadId == _ownerThreadId)
                {
                    immediate = _mainThreadRelease;
                }
                else
                {
                    _pendingReleases.Enqueue(voiceId);
                }
            }

            if (immediate == null)
            {
                return;
            }

            try
            {
                immediate(voiceId);
            }
            catch
            {
                // Disposeはno-throw契約です。Controller側の次回更新で非active tokenを回収します。
            }
        }

        internal void DrainPendingReleases(List<long> destination)
        {
            lock (_gate)
            {
                while (_pendingReleases.Count > 0)
                {
                    destination.Add(_pendingReleases.Dequeue());
                }
            }
        }

        internal void Close()
        {
            lock (_gate)
            {
                _closed = true;
                _mainThreadRelease = null;
                _pendingReleases.Clear();
            }
        }
    }
}
