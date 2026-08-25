// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;

namespace Haptics.Editor.Tests
{
    /// <summary>HapticsServiceの解決、量子化、検証、寿命契約を確認する。</summary>
    internal sealed class HapticsServiceTests
    {
        [Test]
        public void Constructor_WithNullDriver_ResolvesEditorNoOp()
        {
            using (var service = new HapticsService())
            {
                Assert.That(service.Capability, Is.EqualTo(HapticsCapability.None));
                Assert.That(service.IsSupported, Is.False);
            }
        }

        [Test]
        public void Capability_ExposesDriverCapability()
        {
            var driver = new FakeHapticsDriver
            {
                Capability = HapticsCapability.Vibrate |
                             HapticsCapability.AmplitudeControl |
                             HapticsCapability.PatternWaveform,
            };
            using (var service = new HapticsService(driver))
            {
                Assert.That(service.Capability, Is.EqualTo(driver.Capability));
                Assert.That(service.IsSupported, Is.True);
            }
        }

        [Test]
        public void TryPlay_ResolvesPresetPatternForEveryIntent()
        {
            var intents = new[]
            {
                HapticsIntent.SelectionTick,
                HapticsIntent.ImpactLight,
                HapticsIntent.ImpactMedium,
                HapticsIntent.ImpactHeavy,
                HapticsIntent.NotificationSuccess,
                HapticsIntent.NotificationWarning,
                HapticsIntent.NotificationError,
            };

            foreach (var intent in intents)
            {
                var driver = new FakeHapticsDriver
                {
                    Capability = HapticsCapability.Vibrate |
                                 HapticsCapability.AmplitudeControl |
                                 HapticsCapability.PatternWaveform,
                };
                using (var service = new HapticsService(driver))
                {
                    Assert.That(
                        service.TryPlay(intent, out var error), Is.True, $"{intent} should play.");
                    Assert.That(error, Is.EqualTo(HapticsError.None));

                    Assert.That(driver.RequestCount, Is.EqualTo(1));
                    var expected = HapticsPattern.Presets.Get(intent);
                    Assert.That(driver.Requests[0], Is.SameAs(expected));
                }
            }
        }

        [Test]
        public void TryPlay_RejectsUndefinedIntentWithoutTouchingDriver()
        {
            var driver = new FakeHapticsDriver { Capability = HapticsCapability.Vibrate };
            using (var service = new HapticsService(driver))
            {
                Assert.That(service.TryPlay((HapticsIntent)999, out var error), Is.False);
                Assert.That(error, Is.EqualTo(HapticsError.UnknownIntent));
                Assert.That(driver.RequestCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void TryPlayPattern_PassesAmplitudesThroughWhenDriverControlsAmplitude()
        {
            var driver = new FakeHapticsDriver
            {
                Capability = HapticsCapability.Vibrate | HapticsCapability.AmplitudeControl,
            };
            using (var service = new HapticsService(driver))
            {
                var pattern = new HapticsPattern(
                    new HapticsStep(10, 0.25f),
                    new HapticsStep(20, 0.9f),
                    new HapticsStep(30, 0f));

                Assert.That(service.TryPlayPattern(pattern, out var error), Is.True);
                Assert.That(error, Is.EqualTo(HapticsError.None));
                Assert.That(driver.RequestCount, Is.EqualTo(1));
                Assert.That(driver.Requests[0], Is.SameAs(pattern));
                Assert.That(driver.Requests[0].Steps[0].Amplitude, Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(driver.Requests[0].Steps[1].Amplitude, Is.EqualTo(0.9f).Within(1e-6f));
            }
        }

        [Test]
        public void TryPlayPattern_QuantizesAmplitudeWhenDriverLacksControl()
        {
            var driver = new FakeHapticsDriver { Capability = HapticsCapability.Vibrate };
            using (var service = new HapticsService(driver))
            {
                var pattern = new HapticsPattern(
                    new HapticsStep(10, 0.25f),
                    new HapticsStep(20, 0.9f),
                    new HapticsStep(30, 0f));

                Assert.That(service.TryPlayPattern(pattern, out var error), Is.True);
                Assert.That(error, Is.EqualTo(HapticsError.None));
                Assert.That(driver.RequestCount, Is.EqualTo(1));

                var quantized = driver.Requests[0];
                Assert.That(quantized, Is.Not.SameAs(pattern));
                Assert.That(quantized.Steps.Count, Is.EqualTo(3));
                Assert.That(quantized.Steps[0].DurationMilliseconds, Is.EqualTo(10));
                Assert.That(quantized.Steps[0].Amplitude, Is.EqualTo(1f));
                Assert.That(quantized.Steps[1].Amplitude, Is.EqualTo(1f));
                Assert.That(quantized.Steps[2].DurationMilliseconds, Is.EqualTo(30));
                Assert.That(quantized.Steps[2].Amplitude, Is.EqualTo(0f));

                Assert.That(pattern.Steps[0].Amplitude, Is.EqualTo(0.25f).Within(1e-6f));
            }
        }

        [Test]
        public void TryPlayPattern_SkipsDriverWhenCapabilityLacksVibration()
        {
            var driver = new FakeHapticsDriver { Capability = HapticsCapability.None };
            using (var service = new HapticsService(driver))
            {
                var pattern = new HapticsPattern(new HapticsStep(10, 0.5f));

                Assert.That(service.TryPlayPattern(pattern, out var error), Is.False);
                Assert.That(error, Is.EqualTo(HapticsError.UnsupportedPlatform));
                Assert.That(driver.RequestCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void TryPlay_ReportsUnsupportedPlatformWhenDriverRejects()
        {
            var driver = new FakeHapticsDriver
            {
                Capability = HapticsCapability.Vibrate,
                ResultToReturn = false,
            };
            using (var service = new HapticsService(driver))
            {
                Assert.That(service.TryPlay(HapticsIntent.ImpactLight, out var error), Is.False);
                Assert.That(error, Is.EqualTo(HapticsError.UnsupportedPlatform));
                Assert.That(driver.RequestCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TryPlayPattern_ReturnsNullPatternError()
        {
            var driver = new FakeHapticsDriver { Capability = HapticsCapability.Vibrate };
            using (var service = new HapticsService(driver))
            {
                Assert.That(service.TryPlayPattern(null, out var error), Is.False);
                Assert.That(error, Is.EqualTo(HapticsError.NullPattern));
                Assert.That(driver.RequestCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void Dispose_MakesSubsequentCallsFailWithServiceDisposed()
        {
            var driver = new FakeHapticsDriver { Capability = HapticsCapability.Vibrate };
            var service = new HapticsService(driver);
            service.Dispose();

            Assert.That(service.TryPlay(HapticsIntent.ImpactLight, out var playError), Is.False);
            Assert.That(playError, Is.EqualTo(HapticsError.ServiceDisposed));

            var pattern = new HapticsPattern(new HapticsStep(10, 0.5f));
            Assert.That(service.TryPlayPattern(pattern, out var patternError), Is.False);
            Assert.That(patternError, Is.EqualTo(HapticsError.ServiceDisposed));
            Assert.That(driver.RequestCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var service = new HapticsService(new FakeHapticsDriver());

            Assert.DoesNotThrow(() => service.Dispose());
            Assert.DoesNotThrow(() => service.Dispose());
        }

        [Test]
        public void TryPlay_AcceptsAllDefinedEnumValuesOnly()
        {
            foreach (HapticsIntent intent in Enum.GetValues(typeof(HapticsIntent)))
            {
                var driver = new FakeHapticsDriver { Capability = HapticsCapability.Vibrate };
                using (var service = new HapticsService(driver))
                {
                    Assert.That(
                        service.TryPlay(intent, out _),
                        Is.True,
                        $"{intent} is a defined value and should resolve.");
                }
            }
        }
    }
}
