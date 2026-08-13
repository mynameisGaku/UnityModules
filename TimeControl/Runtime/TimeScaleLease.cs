using System;
using System.Threading;

namespace TimeControl
{
    /// <summary>取得時の相対倍率を、Disposeまで1つのControllerへ要求し続ける解放可能な権利。</summary>
    public sealed class TimeScaleLease : IDisposable
    {
        private TimeControlGeneration _generation;
        private readonly long _leaseId;

        /// <summary>登録済みの取得権を世代と結び付ける。</summary>
        /// <param name="generation">取得権が属する所有世代。</param>
        /// <param name="leaseId">世代内で一意な識別子。</param>
        /// <param name="multiplier">取得時に検査済みの相対倍率。</param>
        internal TimeScaleLease(TimeControlGeneration generation, long leaseId, float multiplier)
        {
            _generation = generation;
            _leaseId = leaseId;
            Multiplier = multiplier;
        }

        /// <summary>取得時に指定した0以上100以下の相対倍率。</summary>
        public float Multiplier { get; }

        /// <summary>現在の所有世代でまだ解放されていない場合はtrue。任意スレッドから確認できる。</summary>
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
