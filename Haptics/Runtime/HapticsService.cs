// SPDX-License-Identifier: MIT

using System;

namespace Haptics
{
    /// <summary>
    /// 明示的なownerが1つ保持する振動service。intent解決、pattern検証、
    /// capability別のamplitude量子化をdriverへ渡す前に担う。
    /// singletonや自動初期化は作らず、寿命はownerがDisposeで明示する。
    /// </summary>
    public sealed class HapticsService : IDisposable
    {
        private readonly IHapticsDriver _driver;
        private bool _isDisposed;

        /// <summary>既定driver解決によりserviceを作る。</summary>
        public HapticsService()
            : this(null)
        {
        }

        /// <summary>driverを指定してserviceを作る。</summary>
        /// <param name="driver">serviceより長く生存するdriver。nullの場合は<see cref="HapticsDrivers.ResolveDefault"/>を使う。</param>
        public HapticsService(IHapticsDriver driver)
        {
            _driver = driver ?? HapticsDrivers.ResolveDefault();
        }

        /// <summary>driverが報告する現在の振動capability。Dispose後も読める。</summary>
        public HapticsCapability Capability => _driver.Capability;

        /// <summary>現在platformが何らかの振動を提供する場合true。</summary>
        public bool IsSupported => Capability != HapticsCapability.None;

        /// <summary>intentを標準patternへ解決して再生する。</summary>
        /// <param name="intent">定義済み7種のいずれか。</param>
        /// <param name="error">失敗reason。成功時は<see cref="HapticsError.None"/>。</param>
        /// <returns>振動要求を受理した場合はtrue。</returns>
        public bool TryPlay(HapticsIntent intent, out HapticsError error)
        {
            if (_isDisposed)
            {
                error = HapticsError.ServiceDisposed;
                return false;
            }

            if (!Enum.IsDefined(typeof(HapticsIntent), intent))
            {
                error = HapticsError.UnknownIntent;
                return false;
            }

            return PlayPattern(HapticsPattern.Presets.Get(intent), out error);
        }

        /// <summary>patternを検証し、capabilityに応じて変換した上で再生する。</summary>
        /// <param name="pattern">再生するpattern。null不可。</param>
        /// <param name="error">失敗reason。成功時は<see cref="HapticsError.None"/>。</param>
        /// <returns>振動要求を受理した場合はtrue。</returns>
        public bool TryPlayPattern(HapticsPattern pattern, out HapticsError error)
        {
            if (_isDisposed)
            {
                error = HapticsError.ServiceDisposed;
                return false;
            }

            if (pattern == null)
            {
                error = HapticsError.NullPattern;
                return false;
            }

            if (!pattern.TryValidate(out error))
            {
                return false;
            }

            return PlayPattern(pattern, out error);
        }

        /// <summary>serviceを停止する。以降の再生呼出しはServiceDisposedで失敗する。</summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            (_driver as IDisposable)?.Dispose();
        }

        private bool PlayPattern(HapticsPattern pattern, out HapticsError error)
        {
            if (!_driver.Capability.CanVibrate())
            {
                error = HapticsError.UnsupportedPlatform;
                return false;
            }

            var request = _driver.Capability.CanControlAmplitude()
                ? pattern
                : QuantizeAmplitude(pattern);

            if (_driver.TryVibrate(request))
            {
                error = HapticsError.None;
                return true;
            }

            error = HapticsError.UnsupportedPlatform;
            return false;
        }

        private static HapticsPattern QuantizeAmplitude(HapticsPattern pattern)
        {
            var source = pattern.Steps;
            var steps = new HapticsStep[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                steps[index] = new HapticsStep(
                    source[index].DurationMilliseconds,
                    source[index].Amplitude > 0f ? 1f : 0f);
            }

            return new HapticsPattern(steps);
        }
    }
}
