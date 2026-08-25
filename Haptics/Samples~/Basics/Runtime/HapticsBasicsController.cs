// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Haptics.Samples
{
    /// <summary>application ownerとしてserviceを1つ保持し、各intentと任意patternの再生結果をGUIへ表示する。</summary>
    [AddComponentMenu("StudioGaku/Haptics Basics Controller")]
    [DefaultExecutionOrder(100)]
    public sealed class HapticsBasicsController : MonoBehaviour
    {
        private static readonly HapticsPattern CustomPattern = new HapticsPattern(
            new HapticsStep(30, 0.5f),
            new HapticsStep(60, 0f),
            new HapticsStep(90, 1f));

        private HapticsService _service;
        private string _lastResult = "Not played yet";

        /// <summary>現在のcapabilityとIsSupported表示。service生成前は準備中の文言。</summary>
        public string StatusText => _service == null
            ? "Service not ready"
            : $"Capability={_service.Capability}, IsSupported={_service.IsSupported}";

        /// <summary>最後の再生呼出し結果。testからも参照する。</summary>
        public string LastResultText => _lastResult;

        /// <summary>ownerとしてserviceを1つ生成する。EditorではNoOp driverになる。</summary>
        private void Awake()
        {
            _service = new HapticsService();
        }

        /// <summary>ownerの寿命でserviceを明示的に解放する。</summary>
        private void OnDestroy()
        {
            if (_service == null) return;

            _service.Dispose();
            _service = null;
        }

        /// <summary>intentを標準patternへ解決して再生し、結果文字列を更新する。</summary>
        /// <param name="intent">定義済み7種のいずれか。</param>
        public void PlayIntent(HapticsIntent intent)
        {
            if (_service == null)
            {
                _lastResult = $"{intent}: Service not ready";
                return;
            }

            var played = _service.TryPlay(intent, out var error);
            _lastResult = played
                ? $"{intent}: Played"
                : $"{intent}: Skipped ({error})";
        }

        /// <summary>任意patternを再生し、結果文字列を更新する。</summary>
        public void PlayCustomPattern()
        {
            if (_service == null)
            {
                _lastResult = "Custom pattern: Service not ready";
                return;
            }

            var played = _service.TryPlayPattern(CustomPattern, out var error);
            _lastResult = played
                ? "Custom pattern: Played"
                : $"Custom pattern: Skipped ({error})";
        }

        /// <summary>capability表示、実機とEditorの差、intent button列、結果を即席GUIへ描画する。</summary>
        private void OnGUI()
        {
            GUILayout.Label(StatusText);
            GUILayout.Label(
                Application.isEditor
                    ? "Editor: capability None, no vibration. Build to Android/iOS device to feel it."
                    : DeviceHint());
            GUILayout.Space(8f);

            foreach (HapticsIntent intent in Enum.GetValues(typeof(HapticsIntent)))
            {
                if (GUILayout.Button($"Play {intent}"))
                {
                    PlayIntent(intent);
                }
            }

            if (GUILayout.Button("Play Custom Pattern"))
            {
                PlayCustomPattern();
            }

            GUILayout.Space(8f);
            GUILayout.Label(_lastResult);
        }

        private static string DeviceHint()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return "Android device: waveform vibration depends on device capability.";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS device: single system vibrate approximation.";
                default:
                    return "This platform has no vibration support (capability None).";
            }
        }
    }
}
