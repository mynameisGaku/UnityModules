using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GameplayMetrics.Tests
{
    [TestFixture]
    public sealed class RollingSampleWindowTests
    {
        [TestCase(1)]
        [TestCase(RollingSampleWindow.MaximumCapacity)]
        public void TryCreate_BoundaryCapacity_CreatesEmptyWindow(int capacity)
        {
            Assert.That(RollingSampleWindow.TryCreate(capacity, out var window, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SampleWindowError.None));
            Assert.That(window.Capacity, Is.EqualTo(capacity));
            Assert.That(window.Count, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(RollingSampleWindow.MaximumCapacity + 1)]
        public void TryCreate_InvalidCapacity_ReturnsExplicitFailure(int capacity)
        {
            Assert.That(RollingSampleWindow.TryCreate(capacity, out var window, out var error), Is.False);
            Assert.That(window, Is.Null);
            Assert.That(error, Is.EqualTo(SampleWindowError.InvalidCapacity));
        }

        [Test]
        public void Snapshot_EmptyWindow_HasCanonicalZeroFields()
        {
            var window = Create(3);
            var snapshot = window.Snapshot;

            Assert.That(snapshot.Capacity, Is.EqualTo(3));
            Assert.That(snapshot.Count, Is.Zero);
            Assert.That(snapshot.HasSamples, Is.False);
            Assert.That(snapshot.Minimum, Is.Zero);
            Assert.That(snapshot.Maximum, Is.Zero);
            Assert.That(snapshot.Mean, Is.Zero);
            Assert.That(snapshot.Oldest, Is.Zero);
            Assert.That(snapshot.Newest, Is.Zero);
        }

        [Test]
        public void Add_FirstSample_ReturnsPreviousAndCurrentSnapshots()
        {
            var window = Create(3);
            var result = window.Add(10d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(SampleWindowError.None));
            Assert.That(result.AddedSample, Is.EqualTo(10d));
            Assert.That(result.HadEviction, Is.False);
            Assert.That(result.EvictedSample, Is.Zero);
            Assert.That(result.PreviousSnapshot.Count, Is.Zero);
            AssertSnapshot(result.CurrentSnapshot, 3, 1, 10d, 10d, 10d, 10d, 10d);
        }

        [Test]
        public void Add_UntilCapacity_PreservesOldestFirstOrder()
        {
            var window = Create(3);
            window.Add(10d);
            window.Add(20d);
            window.Add(30d);

            AssertSamples(window, 10d, 20d, 30d);
            AssertSnapshot(window.Snapshot, 3, 3, 10d, 30d, 20d, 10d, 30d);
        }

        [Test]
        public void Add_WhenFull_EvictsExactlyOldest()
        {
            var window = Filled(3, 10d, 20d, 30d);
            var result = window.Add(40d);

            Assert.That(result.HadEviction, Is.True);
            Assert.That(result.EvictedSample, Is.EqualTo(10d));
            Assert.That(result.PreviousSnapshot.Oldest, Is.EqualTo(10d));
            AssertSnapshot(result.CurrentSnapshot, 3, 3, 20d, 40d, 30d, 20d, 40d);
            AssertSamples(window, 20d, 30d, 40d);
        }

        [Test]
        public void Add_AfterMultipleWraps_RemainsFifo()
        {
            var window = Create(3);
            for (var sample = 1; sample <= 8; sample++) window.Add(sample);

            AssertSamples(window, 6d, 7d, 8d);
            AssertSnapshot(window.Snapshot, 3, 3, 6d, 8d, 7d, 6d, 8d);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Add_NonFiniteSample_FailsWithoutMutation(double sample)
        {
            var window = Filled(3, 10d, 20d);
            var before = window.Snapshot;
            var result = window.Add(sample);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(SampleWindowError.InvalidSample));
            Assert.That(result.PreviousSnapshot, Is.EqualTo(before));
            Assert.That(result.CurrentSnapshot, Is.EqualTo(before));
            Assert.That(window.Snapshot, Is.EqualTo(before));
            AssertSamples(window, 10d, 20d);
        }

        [Test]
        public void Add_PositiveAndNegativeZero_AcceptsIndependentSamples()
        {
            var window = Create(2);
            Assert.That(window.Add(0d).Succeeded, Is.True);
            Assert.That(window.Add(-0d).Succeeded, Is.True);
            Assert.That(window.Count, Is.EqualTo(2));
            Assert.That(window.Snapshot.Mean, Is.Zero);
        }

        [Test]
        public void Snapshot_UnsortedSamples_ComputesMinimumMaximumAndMean()
        {
            var window = Filled(4, 8d, -4d, 2d, 10d);
            AssertSnapshot(window.Snapshot, 4, 4, -4d, 10d, 4d, 8d, 10d);
        }

        [Test]
        public void Snapshot_OppositeExtremeSamples_AvoidsOverflow()
        {
            var window = Filled(2, -double.MaxValue, double.MaxValue);
            var snapshot = window.Snapshot;

            Assert.That(double.IsInfinity(snapshot.Mean), Is.False);
            Assert.That(snapshot.Mean, Is.Zero);
            Assert.That(snapshot.Minimum, Is.EqualTo(-double.MaxValue));
            Assert.That(snapshot.Maximum, Is.EqualTo(double.MaxValue));
        }

        [Test]
        public void Snapshot_MaximumSamples_AvoidsSumOverflow()
        {
            var window = Filled(3, double.MaxValue, double.MaxValue, double.MaxValue);
            Assert.That(window.Snapshot.Mean, Is.EqualTo(double.MaxValue));
        }

        [Test]
        public void Clear_FullWindow_ResetsStateAndRetainsCapacity()
        {
            var window = Filled(3, 1d, 2d, 3d);
            window.Clear();

            Assert.That(window.Capacity, Is.EqualTo(3));
            Assert.That(window.Count, Is.Zero);
            Assert.That(window.Snapshot.HasSamples, Is.False);
        }

        [Test]
        public void Clear_ThenAdd_StartsFreshOrder()
        {
            var window = Filled(2, 1d, 2d);
            window.Clear();
            window.Add(9d);

            AssertSamples(window, 9d);
            Assert.That(window.Snapshot.Oldest, Is.EqualTo(9d));
        }

        [Test]
        public void TryGetSampleAt_ValidIndices_ReturnsOldestFirst()
        {
            var window = Filled(3, 10d, 20d, 30d);
            for (var index = 0; index < 3; index++)
            {
                Assert.That(window.TryGetSampleAt(index, out var sample, out var error), Is.True);
                Assert.That(sample, Is.EqualTo((index + 1) * 10d));
                Assert.That(error, Is.EqualTo(SampleWindowError.None));
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        public void TryGetSampleAt_InvalidIndex_ReturnsExplicitFailure(int index)
        {
            var window = Create(3);
            Assert.That(window.TryGetSampleAt(index, out var sample, out var error), Is.False);
            Assert.That(sample, Is.Zero);
            Assert.That(error, Is.EqualTo(SampleWindowError.IndexOutOfRange));
        }

        [Test]
        public void TryGetSampleAt_AfterWrap_UsesLogicalIndices()
        {
            var window = Filled(3, 1d, 2d, 3d);
            window.Add(4d);
            window.Add(5d);
            AssertSamples(window, 3d, 4d, 5d);
        }

        [Test]
        public void Add_DuplicateValues_AreIndependentFifoEntries()
        {
            var window = Filled(2, 4d, 4d);
            var result = window.Add(4d);

            Assert.That(result.HadEviction, Is.True);
            Assert.That(result.EvictedSample, Is.EqualTo(4d));
            Assert.That(window.Count, Is.EqualTo(2));
        }

        [Test]
        public void CapacityOne_EachAdditionalSampleEvictsPrevious()
        {
            var window = Filled(1, 2d);
            var result = window.Add(7d);

            Assert.That(result.EvictedSample, Is.EqualTo(2d));
            AssertSnapshot(window.Snapshot, 1, 1, 7d, 7d, 7d, 7d, 7d);
        }

        [Test]
        public void Evict_CurrentMinimum_RecomputesMinimum()
        {
            var window = Filled(3, -10d, 5d, 7d);
            window.Add(6d);
            Assert.That(window.Snapshot.Minimum, Is.EqualTo(5d));
        }

        [Test]
        public void Evict_CurrentMaximum_RecomputesMaximum()
        {
            var window = Filled(3, 10d, 5d, 7d);
            window.Add(6d);
            Assert.That(window.Snapshot.Maximum, Is.EqualTo(7d));
        }

        [Test]
        public void Snapshot_ValueEquality_UsesAllFields()
        {
            var first = Filled(3, 1d, 2d).Snapshot;
            var second = Filled(3, 1d, 2d).Snapshot;
            var different = Filled(3, 1d, 3d).Snapshot;

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void AddResult_ValueEquality_UsesAllFields()
        {
            var first = Create(2).Add(1d);
            var second = Create(2).Add(1d);
            var different = Create(2).Add(2d);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void PublicApi_ExportsExactlyFourRuntimeTypes()
        {
            var names = typeof(RollingSampleWindow).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GameplayMetrics", StringComparison.Ordinal)).Select(type => type.FullName).OrderBy(name => name).ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "GameplayMetrics.RollingSampleWindow",
                "GameplayMetrics.SampleWindowAddResult",
                "GameplayMetrics.SampleWindowError",
                "GameplayMetrics.SampleWindowSnapshot"
            }));
        }

        [Test]
        public void PublicApi_WindowMethodsStayBounded()
        {
            var methods = typeof(RollingSampleWindow).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(method => method.Name).OrderBy(name => name).ToArray();
            Assert.That(methods, Is.EquivalentTo(new[] { "Add", "Clear", "TryCreate", "TryGetSampleAt", "get_Capacity", "get_Count", "get_Snapshot" }));
        }

        [Test]
        public void RuntimeAssembly_HasNoUnityEngineReference()
        {
            var references = typeof(RollingSampleWindow).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            Assert.That(references.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
        }

        private static RollingSampleWindow Create(int capacity)
        {
            Assert.That(RollingSampleWindow.TryCreate(capacity, out var window, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SampleWindowError.None));
            return window;
        }

        private static RollingSampleWindow Filled(int capacity, params double[] samples)
        {
            var window = Create(capacity);
            foreach (var sample in samples) Assert.That(window.Add(sample).Succeeded, Is.True);
            return window;
        }

        private static void AssertSamples(RollingSampleWindow window, params double[] expected)
        {
            Assert.That(window.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(window.TryGetSampleAt(index, out var actual, out var error), Is.True);
                Assert.That(error, Is.EqualTo(SampleWindowError.None));
                Assert.That(actual, Is.EqualTo(expected[index]));
            }
        }

        private static void AssertSnapshot(SampleWindowSnapshot snapshot, int capacity, int count, double minimum, double maximum, double mean, double oldest, double newest)
        {
            Assert.That(snapshot.Capacity, Is.EqualTo(capacity));
            Assert.That(snapshot.Count, Is.EqualTo(count));
            Assert.That(snapshot.HasSamples, Is.EqualTo(count > 0));
            Assert.That(snapshot.Minimum, Is.EqualTo(minimum).Within(1e-12d));
            Assert.That(snapshot.Maximum, Is.EqualTo(maximum).Within(1e-12d));
            Assert.That(snapshot.Mean, Is.EqualTo(mean).Within(1e-12d));
            Assert.That(snapshot.Oldest, Is.EqualTo(oldest).Within(1e-12d));
            Assert.That(snapshot.Newest, Is.EqualTo(newest).Within(1e-12d));
        }
    }
}
