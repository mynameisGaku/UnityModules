using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GenerationalHandles.Tests
{
    public sealed class GenerationHandlePoolTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(GenerationHandlePool.MaximumCapacity + 1)]
        public void Constructor_InvalidCapacity_Throws(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationHandlePool(capacity));
        }

        [Test]
        public void Constructor_InitialState_IsEmpty()
        {
            var pool = new GenerationHandlePool(3);

            Assert.That(pool.Capacity, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.RetiredCount, Is.Zero);
            Assert.That(pool.AvailableCount, Is.EqualTo(3));
        }

        [Test]
        public void Acquire_FirstHandle_UsesSlotZeroGenerationOne()
        {
            var pool = new GenerationHandlePool(2);

            Assert.That(pool.TryAcquire(out var handle, out var error), Is.True);
            Assert.That(error, Is.EqualTo(GenerationHandleError.None));
            Assert.That(handle.Slot, Is.Zero);
            Assert.That(handle.Generation, Is.EqualTo(1u));
            Assert.That(pool.IsActive(handle), Is.True);
        }

        [Test]
        public void Acquire_SequentialHandles_UseAscendingSlots()
        {
            var pool = new GenerationHandlePool(3);

            var first = Acquire(pool);
            var second = Acquire(pool);
            var third = Acquire(pool);

            Assert.That(new[] { first.Slot, second.Slot, third.Slot }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(pool.ActiveCount, Is.EqualTo(3));
            Assert.That(pool.AvailableCount, Is.Zero);
        }

        [Test]
        public void Acquire_WhenFull_ReturnsCapacityReachedWithoutMutation()
        {
            var pool = new GenerationHandlePool(1);
            var active = Acquire(pool);

            Assert.That(pool.TryAcquire(out var rejected, out var error), Is.False);
            Assert.That(rejected, Is.EqualTo(default(GenerationHandle)));
            Assert.That(error, Is.EqualTo(GenerationHandleError.CapacityReached));
            Assert.That(pool.IsActive(active), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_ActiveHandle_InvalidatesItAndAdvancesGeneration()
        {
            var pool = new GenerationHandlePool(1);
            var first = Acquire(pool);

            Assert.That(pool.Release(first), Is.EqualTo(GenerationHandleError.None));
            var second = Acquire(pool);

            Assert.That(pool.IsActive(first), Is.False);
            Assert.That(second.Slot, Is.EqualTo(first.Slot));
            Assert.That(second.Generation, Is.EqualTo(first.Generation + 1));
        }

        [Test]
        public void Acquire_ReusesSmallestReleasedSlot()
        {
            var pool = new GenerationHandlePool(4);
            var zero = Acquire(pool);
            var one = Acquire(pool);
            var two = Acquire(pool);
            Assert.That(pool.Release(two), Is.EqualTo(GenerationHandleError.None));
            Assert.That(pool.Release(zero), Is.EqualTo(GenerationHandleError.None));

            var reusedZero = Acquire(pool);
            var reusedTwo = Acquire(pool);

            Assert.That(reusedZero.Slot, Is.Zero);
            Assert.That(reusedTwo.Slot, Is.EqualTo(2));
            Assert.That(pool.IsActive(one), Is.True);
        }

        [Test]
        public void Release_SameHandleTwice_ReturnsStaleWithoutMutation()
        {
            var pool = new GenerationHandlePool(2);
            var handle = Acquire(pool);
            Assert.That(pool.Release(handle), Is.EqualTo(GenerationHandleError.None));

            Assert.That(pool.Release(handle), Is.EqualTo(GenerationHandleError.StaleHandle));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.AvailableCount, Is.EqualTo(2));
        }

        [Test]
        public void Release_DefaultHandle_ReturnsInvalidHandle()
        {
            var pool = new GenerationHandlePool(1);

            Assert.That(pool.Release(default), Is.EqualTo(GenerationHandleError.InvalidHandle));
            Assert.That(pool.ActiveCount, Is.Zero);
        }

        [Test]
        public void Release_UnallocatedSlot_ReturnsInvalidHandle()
        {
            var pool = new GenerationHandlePool(4);
            var forged = new GenerationHandle(3, 1);

            Assert.That(pool.Release(forged), Is.EqualTo(GenerationHandleError.InvalidHandle));
            Assert.That(pool.ActiveCount, Is.Zero);
        }

        [Test]
        public void Release_WrongGeneration_ReturnsStaleWithoutReleasingActiveHandle()
        {
            var pool = new GenerationHandlePool(1);
            var active = Acquire(pool);
            var forged = new GenerationHandle(active.Slot, active.Generation + 1);

            Assert.That(pool.Release(forged), Is.EqualTo(GenerationHandleError.StaleHandle));
            Assert.That(pool.IsActive(active), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_MaximumGeneration_RetiresSlotPermanently()
        {
            var pool = new GenerationHandlePool(1);
            var active = pool.SetGenerationForTesting(Acquire(pool), uint.MaxValue);

            Assert.That(pool.Release(active), Is.EqualTo(GenerationHandleError.None));
            Assert.That(pool.RetiredCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.Zero);
            Assert.That(pool.TryAcquire(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(GenerationHandleError.CapacityReached));
        }

        [Test]
        public void GoldenTrace_IsReproducibleAcrossPools()
        {
            var first = RunGoldenTrace();
            var second = RunGoldenTrace();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo("0:1|1:1|0:2|2:1|1:2"));
        }

        [Test]
        public void Handle_Default_IsInvalid()
        {
            var handle = default(GenerationHandle);

            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.ToString(), Is.EqualTo("Invalid"));
        }

        [Test]
        public void Handle_EqualityComparisonAndHash_UseBothFields()
        {
            var first = new GenerationHandle(2, 3);
            var same = new GenerationHandle(2, 3);
            var otherSlot = new GenerationHandle(1, 3);
            var otherGeneration = new GenerationHandle(2, 4);

            Assert.That(first == same, Is.True);
            Assert.That(first != otherSlot, Is.True);
            Assert.That(first != otherGeneration, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        }

        [Test]
        public void Handle_CompareTo_OrdersSlotThenGeneration()
        {
            var values = new[]
            {
                new GenerationHandle(2, 1),
                new GenerationHandle(0, 2),
                new GenerationHandle(0, 1)
            };

            Array.Sort(values);

            Assert.That(values.Select(value => $"{value.Slot}:{value.Generation}"), Is.EqualTo(new[] { "0:1", "0:2", "2:1" }));
        }

        [Test]
        public void Handle_ToString_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(new GenerationHandle(12, 34).ToString(), Is.EqualTo("Slot 12 / Generation 34"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyThreeTypes()
        {
            var assembly = typeof(GenerationHandlePool).Assembly;
            var exported = assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GenerationalHandles", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                typeof(GenerationHandle),
                typeof(GenerationHandleError),
                typeof(GenerationHandlePool)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal)));
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceUnityEngine()
        {
            var references = typeof(GenerationHandlePool).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();

            Assert.That(references, Has.None.StartsWith("UnityEngine"));
        }

        private static GenerationHandle Acquire(GenerationHandlePool pool)
        {
            Assert.That(pool.TryAcquire(out var handle, out var error), Is.True);
            Assert.That(error, Is.EqualTo(GenerationHandleError.None));
            return handle;
        }

        private static string RunGoldenTrace()
        {
            var pool = new GenerationHandlePool(3);
            var first = Acquire(pool);
            var second = Acquire(pool);
            Assert.That(pool.Release(first), Is.EqualTo(GenerationHandleError.None));
            var third = Acquire(pool);
            var fourth = Acquire(pool);
            Assert.That(pool.Release(second), Is.EqualTo(GenerationHandleError.None));
            var fifth = Acquire(pool);
            return string.Join("|", new[] { first, second, third, fourth, fifth }.Select(value => $"{value.Slot}:{value.Generation}"));
        }
    }
}
