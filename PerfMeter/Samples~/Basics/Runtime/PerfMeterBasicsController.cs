// SPDX-License-Identifier: MIT

using System.Threading;
using UnityEngine;

namespace PerfMeter.Samples
{
    /// <summary>Perf Meterの導入直後挙動を確認するBasics sample。同じGameObject上のPerfMeterComponentへ人工spike投入と統計resetを提供する。</summary>
    [RequireComponent(typeof(PerfMeterComponent))]
    [AddComponentMenu("Perf Meter/Perf Meter Basics Controller")]
    public sealed class PerfMeterBasicsController : MonoBehaviour
    {
        private const int HeavyFrameSleepMilliseconds = 120;
        private const float PanelLeftPixels = 8f;
        private const float PanelTopPixels = 112f;
        private const float PanelWidthPixels = 520f;
        private const float LineHeightPixels = 24f;
        private const float ButtonWidthPixels = 220f;

        private PerfMeterComponent _component;

        /// <summary>同じGameObject上の計測component。Awake以降に参照できる。</summary>
        public PerfMeterComponent Component => _component;

        /// <summary>main threadを約120ms停止させる。次のUpdateでTime.deltaTimeが膨らみ、spikeとして計上される。</summary>
        public void HeavyFrame()
        {
            Thread.Sleep(HeavyFrameSleepMilliseconds);
        }

        /// <summary>計測中の統計とbufferを初期化する。</summary>
        public void ResetStats()
        {
            _component.ResetStats();
        }

        private void Awake()
        {
            _component = GetComponent<PerfMeterComponent>();
        }

        private void OnGUI()
        {
            if (_component == null || _component.Sampler == null) return;
            var snapshot = _component.Sampler.CreateSnapshot();
            GUI.Label(
                new Rect(PanelLeftPixels, PanelTopPixels, PanelWidthPixels, LineHeightPixels),
                $"samples {snapshot.SampleCount}/{_component.Sampler.Capacity}   fps(avg) {snapshot.AverageFps:F1}   spikes {_component.Sampler.TotalSpikes}");
            if (GUI.Button(new Rect(PanelLeftPixels, PanelTopPixels + LineHeightPixels, ButtonWidthPixels, LineHeightPixels), $"Heavy Frame ({HeavyFrameSleepMilliseconds}ms)"))
            {
                HeavyFrame();
            }

            if (GUI.Button(new Rect(PanelLeftPixels, PanelTopPixels + LineHeightPixels * 2f, ButtonWidthPixels, LineHeightPixels), "Reset Stats"))
            {
                ResetStats();
            }
        }
    }
}
