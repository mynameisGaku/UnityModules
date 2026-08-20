using System;
using System.Threading;

namespace InputGate
{
    /// <summary>Disposeまで対象Action Mapの停止を要求し続ける、世代分離された取得権。</summary>
    public sealed class InputGateLease : IDisposable
    {
        private InputGateGeneration _generation;
        private readonly long _leaseId;

        /// <summary>取得権を所有世代と結び付ける。</summary>
        /// <param name="generation">取得権が属する所有世代。</param>
        /// <param name="leaseId">世代内で一意な識別子。</param>
        internal InputGateLease(InputGateGeneration generation, long leaseId)
        {
            _generation = generation;
            _leaseId = leaseId;
        }

        /// <summary>現在の所有世代で未解放ならtrue。任意スレッドから確認できる。</summary>
        public bool IsActive
        {
            get
            {
                var generation = Volatile.Read(ref _generation);
                return generation != null && generation.IsLeaseActive(_leaseId);
            }
        }

        /// <summary>取得権を1度だけ解放する。任意スレッドから呼べ、重複呼出しでも例外を送出しない。</summary>
        public void Dispose()
        {
            var generation = Interlocked.Exchange(ref _generation, null);
            if (generation == null) return;
            generation.RequestRelease(_leaseId);
        }
    }
}
