// SPDX-License-Identifier: MIT

using NUnit.Framework;

namespace Haptics.Editor.Tests
{
    /// <summary>ResolveDefaultがEditorで必ずNoOpを返すことを確認する。</summary>
    internal sealed class HapticsDriversTests
    {
        [Test]
        public void ResolveDefault_InEditor_ReturnsNoOp()
        {
            var driver = HapticsDrivers.ResolveDefault();

            Assert.That(driver, Is.InstanceOf<UnityNoOpHapticsDriver>());
        }

        [Test]
        public void NoOpDriver_ReportsNoneAndNeverVibrates()
        {
            var driver = new UnityNoOpHapticsDriver();

            Assert.That(driver.Capability, Is.EqualTo(HapticsCapability.None));
            Assert.That(
                driver.TryVibrate(HapticsPattern.Presets.Get(HapticsIntent.ImpactHeavy)),
                Is.False);
            Assert.That(driver.TryVibrate(null), Is.False);
        }

        [Test]
        public void PlatformStubDrivers_CompileAndDegradeToNoOpInEditor()
        {
            var android = new UnityAndroidHapticsDriver();
            var ios = new UnityIOSHapticsDriver();

            Assert.That(android.Capability, Is.EqualTo(HapticsCapability.None));
            Assert.That(ios.Capability, Is.EqualTo(HapticsCapability.None));
            Assert.That(
                android.TryVibrate(HapticsPattern.Presets.Get(HapticsIntent.SelectionTick)),
                Is.False);
            Assert.That(
                ios.TryVibrate(HapticsPattern.Presets.Get(HapticsIntent.SelectionTick)),
                Is.False);

            android.Dispose();
            ios.Dispose();
        }
    }
}
