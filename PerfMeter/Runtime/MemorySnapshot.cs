// SPDX-License-Identifier: MIT

namespace PerfMeter
{
    /// <summary>1つの取得タイミングで揃えた簡易メモリsnapshot。全fieldの等価比較を提供する。</summary>
    public readonly struct MemorySnapshot : System.IEquatable<MemorySnapshot>
    {
        /// <summary>全fieldを明示してsnapshotを作る。</summary>
        /// <param name="managedBytes">managed heapサイズbyte数。</param>
        /// <param name="profilerReportedBytes">Profiler reported heapサイズbyte数。取得不可時は-1。</param>
        /// <param name="capturedAtFrame">取得時のframe番号。不明時は-1。</param>
        public MemorySnapshot(long managedBytes, long profilerReportedBytes, int capturedAtFrame)
        {
            ManagedBytes = managedBytes;
            ProfilerReportedBytes = profilerReportedBytes;
            CapturedAtFrame = capturedAtFrame;
        }

        /// <summary>managed heapサイズbyte数。GC.GetTotalMemory(false)の瞬間値。</summary>
        public long ManagedBytes { get; }

        /// <summary>Profiler reported heapサイズbyte数。Profiler無効など取得不可時は-1。</summary>
        public long ProfilerReportedBytes { get; }

        /// <summary>取得時のframe番号。呼び出し側がframe番号を渡さなかった場合は-1。</summary>
        public int CapturedAtFrame { get; }

        /// <summary>全てのfieldが等しい場合はtrueを返す。</summary>
        /// <param name="other">比較するsnapshot。</param>
        /// <returns>全てのfieldが等しい場合はtrue。</returns>
        public bool Equals(MemorySnapshot other)
        {
            return ManagedBytes == other.ManagedBytes &&
                   ProfilerReportedBytes == other.ProfilerReportedBytes &&
                   CapturedAtFrame == other.CapturedAtFrame;
        }

        /// <summary>指定objectが同じsnapshotならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じsnapshotならtrue。</returns>
        public override bool Equals(object obj) => obj is MemorySnapshot other && Equals(other);

        /// <summary>全てのfieldからhash値を返す。</summary>
        /// <returns>snapshotのhash値。</returns>
        public override int GetHashCode() => System.HashCode.Combine(ManagedBytes, ProfilerReportedBytes, CapturedAtFrame);

        /// <summary>左右のsnapshotが等しい場合はtrueを返す。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>左右が等しい場合はtrue。</returns>
        public static bool operator ==(MemorySnapshot left, MemorySnapshot right) => left.Equals(right);

        /// <summary>左右のsnapshotが異なる場合はtrueを返す。</summary>
        /// <param name="left">左側のsnapshot。</param>
        /// <param name="right">右側のsnapshot。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(MemorySnapshot left, MemorySnapshot right) => !left.Equals(right);
    }
}
