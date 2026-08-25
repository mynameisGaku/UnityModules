// SPDX-License-Identifier: MIT

using System.Collections;
using NUnit.Framework;
using PerfMeter.Samples;
using UnityEngine;

namespace PerfMeter.Samples.Runtime.Tests
{
    /// <summary>Basics sampleの人工spike計上と統計resetをPlayModeで検証する。</summary>
    public sealed class PerfMeterBasicsControllerTests
    {
        [UnityTest]
        public IEnumerator HeavyFrame_NextUpdateRegistersSpikeSample()
        {
            var host = new GameObject("Perf Meter Basics Test Host");
            var controller = host.AddComponent<PerfMeterBasicsController>();

            Assert.That(controller.Component, Is.Not.Null);
            controller.HeavyFrame();
            yield return null;

            Assert.That(controller.Component.Sampler, Is.Not.Null);
            Assert.That(controller.Component.Sampler.Last, Is.GreaterThanOrEqualTo(0.05d));

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetStats_AfterSamples_ReturnsCountToZero()
        {
            var host = new GameObject("Perf Meter Basics Reset Test Host");
            var controller = host.AddComponent<PerfMeterBasicsController>();
            yield return null;
            yield return null;

            var beforeReset = controller.Component.Sampler.SampleCount;
            Assert.That(beforeReset, Is.GreaterThan(0));

            controller.ResetStats();

            Assert.That(controller.Component.Sampler.SampleCount, Is.Zero);
            Assert.That(controller.Component.Sampler.SampleCount, Is.LessThan(beforeReset));

            Object.Destroy(host);
            yield return null;
        }
    }
}
