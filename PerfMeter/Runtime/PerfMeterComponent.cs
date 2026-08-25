// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PerfMeter
{
    /// <summary>Sceneへ置いた者だけが動く任意利用の計測Component。毎frame Time.deltaTimeを計上しoverlayへ統計を出す。singleton化はしない。</summary>
    [AddComponentMenu("Perf Meter/Perf Meter Component")]
    public sealed class PerfMeterComponent : MonoBehaviour
    {
        private const float OverlayLeftPixels = 8f;
        private const float OverlayTopPixels = 8f;
        private const float OverlayWidthPixels = 520f;
        private const float OverlayHeightPixels = 96f;

        [SerializeField] private int capacityFrames = 600;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private double spikeThresholdSeconds = 0.033;

        private FrameTimeSampler _sampler;
        private MemorySnapshot _lastMemory;
        private int _lastSpikesSinceCheck;
        private bool _lastAddedFrameWasSpike;

        /// <summary>Awakeで構築した計測class。Awakeより前の参照は不定。capacityが範囲外だった場合は許可範囲へ丸めた値で構築する。</summary>
        public FrameTimeSampler Sampler => _sampler;

        /// <summary>統計とbufferを初期化する。spike閾値の設定は保持する。sampler未構築の場合は何もしない。</summary>
        public void ResetStats()
        {
            if (_sampler == null) return;
            _sampler.Reset();
            _lastSpikesSinceCheck = 0;
            _lastAddedFrameWasSpike = false;
        }

        /// <summary>spike判定の閾値秒を差し替える。</summary>
        /// <param name="seconds">新しい閾値秒。0以上の有限値。</param>
        /// <returns>設定に成功したならtrue。sampler未構築または検証失敗ならfalse。</returns>
        public bool SetSpikeThreshold(double seconds)
        {
            return _sampler != null && _sampler.SetSpikeThreshold(seconds, out _);
        }

        private void Awake()
        {
            var capacity = Mathf.Clamp(capacityFrames, FrameTimeSampler.MinimumCapacity, FrameTimeSampler.MaximumCapacity);
            _sampler = new FrameTimeSampler(capacity);
            _sampler.SetSpikeThreshold(Math.Max(0d, spikeThresholdSeconds), out _);
            _lastMemory = MemoryProbe.CaptureMemorySnapshot(Time.frameCount);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            if (_sampler.AddFrame(deltaTime, out _))
            {
                _lastAddedFrameWasSpike = _sampler.SpikeThresholdSeconds > 0d && deltaTime > _sampler.SpikeThresholdSeconds;
            }

            _lastSpikesSinceCheck = _sampler.SpikesSinceLastCheck();
            _lastMemory = MemoryProbe.CaptureMemorySnapshot(Time.frameCount);
        }

        private void OnGUI()
        {
            if (!showOverlay || _sampler == null) return;
            var snapshot = _sampler.CreateSnapshot();
            var text =
                $"fps(avg) {snapshot.AverageFps:F1}   last {snapshot.Last * 1000d:F2} ms\n" +
                $"min {snapshot.Minimum * 1000d:F2} ms   max {snapshot.Maximum * 1000d:F2} ms\n" +
                $"median {snapshot.Median * 1000d:F2} ms   stddev {snapshot.StandardDeviation * 1000d:F2} ms\n" +
                $"spikes {_sampler.TotalSpikes} (+{_lastSpikesSinceCheck})   samples {snapshot.SampleCount}/{_sampler.Capacity}\n" +
                FormatMemoryLine();
            var previousColor = GUI.contentColor;
            GUI.contentColor = _lastAddedFrameWasSpike ? Color.red : Color.white;
            GUI.Label(new Rect(OverlayLeftPixels, OverlayTopPixels, OverlayWidthPixels, OverlayHeightPixels), text);
            GUI.contentColor = previousColor;
        }

        private string FormatMemoryLine()
        {
            var managedMegabytes = _lastMemory.ManagedBytes / (1024d * 1024d);
            if (_lastMemory.ProfilerReportedBytes < 0L)
            {
                return $"memory {managedMegabytes:F1} MB";
            }

            var profilerMegabytes = _lastMemory.ProfilerReportedBytes / (1024d * 1024d);
            return $"memory {managedMegabytes:F1} MB   profiler heap {profilerMegabytes:F1} MB";
        }
    }
}
