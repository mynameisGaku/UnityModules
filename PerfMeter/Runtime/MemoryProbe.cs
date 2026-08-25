// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace PerfMeter
{
    /// <summary>managed heapとProfiler heapの瞬間値を取得するstatic計測入口。状態を持たず、強制collectionも行わない。</summary>
    public static class MemoryProbe
    {
        /// <summary>現在のmanaged heapサイズと、Profiler有効時のみProfiler reported heapサイズを取得する。</summary>
        /// <param name="currentFrame">取得時のframe番号。呼び出し側がTime.frameCountなどを渡す。</param>
        /// <returns>取得したsnapshot。Profilerが無効な場合はProfilerReportedBytesが-1。</returns>
        public static MemorySnapshot CaptureMemorySnapshot(int currentFrame)
        {
            var managedBytes = GC.GetTotalMemory(false);
            var profilerReportedBytes = Profiler.enabled ? Profiler.usedHeapSizeLong : -1L;
            return new MemorySnapshot(managedBytes, profilerReportedBytes, currentFrame);
        }

        /// <summary>frame番号を持たない版の取得。CapturedAtFrameは-1になる。</summary>
        /// <returns>取得したsnapshot。</returns>
        public static MemorySnapshot CaptureMemorySnapshot()
        {
            return CaptureMemorySnapshot(-1);
        }
    }
}
