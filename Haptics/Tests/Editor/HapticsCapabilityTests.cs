// SPDX-License-Identifier: MIT

using NUnit.Framework;

namespace Haptics.Editor.Tests
{
    /// <summary>HapticsCapability flagsと結合判定helperを確認する。</summary>
    internal sealed class HapticsCapabilityTests
    {
        [Test]
        public void Flags_HaveDocumentedBitValues()
        {
            Assert.That((int)HapticsCapability.None, Is.EqualTo(0));
            Assert.That((int)HapticsCapability.Vibrate, Is.EqualTo(1));
            Assert.That((int)HapticsCapability.AmplitudeControl, Is.EqualTo(2));
            Assert.That((int)HapticsCapability.PatternWaveform, Is.EqualTo(4));
        }

        [Test]
        public void CanVibrate_TrueOnlyWithVibrateFlag()
        {
            Assert.That(HapticsCapability.None.CanVibrate(), Is.False);
            Assert.That(HapticsCapability.Vibrate.CanVibrate(), Is.True);
            Assert.That(
                (HapticsCapability.Vibrate | HapticsCapability.AmplitudeControl).CanVibrate(),
                Is.True);
            Assert.That(HapticsCapability.AmplitudeControl.CanVibrate(), Is.False);
        }

        [Test]
        public void CanControlAmplitude_FollowsSingleFlag()
        {
            Assert.That(HapticsCapability.None.CanControlAmplitude(), Is.False);
            Assert.That(HapticsCapability.Vibrate.CanControlAmplitude(), Is.False);
            Assert.That(HapticsCapability.AmplitudeControl.CanControlAmplitude(), Is.True);
            Assert.That(
                (HapticsCapability.AmplitudeControl | HapticsCapability.PatternWaveform)
                    .CanControlAmplitude(),
                Is.True);
        }

        [Test]
        public void CanPlayWaveformPatterns_FollowsSingleFlag()
        {
            Assert.That(HapticsCapability.None.CanPlayWaveformPatterns(), Is.False);
            Assert.That(HapticsCapability.Vibrate.CanPlayWaveformPatterns(), Is.False);
            Assert.That(HapticsCapability.PatternWaveform.CanPlayWaveformPatterns(), Is.True);
        }

        [Test]
        public void SupportsPrecisePatterns_RequiresBothFlags()
        {
            Assert.That(HapticsCapability.None.SupportsPrecisePatterns(), Is.False);
            Assert.That(HapticsCapability.Vibrate.SupportsPrecisePatterns(), Is.False);
            Assert.That(HapticsCapability.AmplitudeControl.SupportsPrecisePatterns(), Is.False);
            Assert.That(HapticsCapability.PatternWaveform.SupportsPrecisePatterns(), Is.False);
            Assert.That(
                (HapticsCapability.Vibrate | HapticsCapability.AmplitudeControl)
                    .SupportsPrecisePatterns(),
                Is.False);
            Assert.That(
                (HapticsCapability.Vibrate |
                 HapticsCapability.AmplitudeControl |
                 HapticsCapability.PatternWaveform).SupportsPrecisePatterns(),
                Is.True);
        }
    }
}
