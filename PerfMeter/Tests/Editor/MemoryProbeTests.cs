// SPDX-License-Identifier: MIT

using NUnit.Framework;
using PerfMeter;
using UnityEngine;
using UnityEngine.Profiling;

namespace PerfMeter.Editor.Tests
{
    [TestFixture]
    public sealed class MemoryProbeTests
    {
        [Test]
        public void Capture_WithoutFrameNumber_ManagedBytesPositiveAndFrameUnknown()
        {
            var snapshot = MemoryProbe.CaptureMemorySnapshot();

            Assert.That(snapshot.ManagedBytes, Is.GreaterThan(0L));
            Assert.That(snapshot.CapturedAtFrame, Is.EqualTo(-1));
        }

        [Test]
        public void Capture_WithFrameNumber_ReportsGivenFrame()
        {
            var snapshot = MemoryProbe.CaptureMemorySnapshot(12345);

            Assert.That(snapshot.ManagedBytes, Is.GreaterThan(0L));
            Assert.That(snapshot.CapturedAtFrame, Is.EqualTo(12345));
        }

        [Test]
        public void Capture_ProfilerReportedBytes_FollowsProfilerEnabledState()
        {
            var snapshot = MemoryProbe.CaptureMemorySnapshot();

            if (Profiler.enabled)
            {
                Assert.That(snapshot.ProfilerReportedBytes, Is.GreaterThanOrEqualTo(0L));
            }
            else
            {
                Assert.That(snapshot.ProfilerReportedBytes, Is.EqualTo(-1L));
            }
        }

        [Test]
        public void Snapshot_ValueEquality_UsesAllFields()
        {
            var baseline = new MemorySnapshot(100L, -1L, 7);
            var same = new MemorySnapshot(100L, -1L, 7);
            var variants = new[]
            {
                new MemorySnapshot(101L, -1L, 7),
                new MemorySnapshot(100L, 50L, 7),
                new MemorySnapshot(100L, -1L, 8)
            };

            Assert.That(baseline.Equals(same), Is.True);
            Assert.That(baseline, Is.EqualTo(same));
            Assert.That(baseline.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(baseline == same, Is.True);
            Assert.That(baseline != same, Is.False);

            foreach (var variant in variants)
            {
                Assert.That(baseline.Equals(variant), Is.False);
                Assert.That(baseline != variant, Is.True);
            }
        }
    }
}
