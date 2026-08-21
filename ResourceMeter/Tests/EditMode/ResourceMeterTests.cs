using NUnit.Framework;

namespace GameplayResources.Tests
{
    public sealed class ResourceMeterTests
    {
        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TryCreate_InvalidCapacity_Fails(double capacity)
        {
            Assert.That(ResourceMeter.TryCreate(capacity, 0d, out var meter, out var error), Is.False);
            Assert.That(meter, Is.Null);
            Assert.That(error, Is.EqualTo(ResourceMeterError.InvalidCapacity));
        }

        [TestCase(double.NaN, ResourceMeterError.NonFiniteValue)]
        [TestCase(double.NegativeInfinity, ResourceMeterError.NonFiniteValue)]
        [TestCase(-0.001d, ResourceMeterError.ValueOutOfRange)]
        [TestCase(100.001d, ResourceMeterError.ValueOutOfRange)]
        public void TryCreate_InvalidInitialValue_Fails(double current, ResourceMeterError expected)
        {
            Assert.That(ResourceMeter.TryCreate(100d, current, out var meter, out var error), Is.False);
            Assert.That(meter, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void TryCreate_ValidState_ExposesReconstructableProperties()
        {
            var meter = Create(100d, 40d);
            Assert.That(meter.Capacity, Is.EqualTo(100d));
            Assert.That(meter.Current, Is.EqualTo(40d));
            Assert.That(meter.Normalized, Is.EqualTo(0.4d));
            Assert.That(meter.IsEmpty, Is.False);
            Assert.That(meter.IsFull, Is.False);
        }

        [Test]
        public void Restore_WithinCapacity_AppliesFullAmount()
        {
            AssertResult(Create(100d, 40d).Restore(30d), 40d, 70d, 100d, 30d, 30d, 0d, true, true, false, false);
        }

        [Test]
        public void Restore_Overflow_ClampsAndReportsUnappliedAmount()
        {
            var result = Create(100d, 40d).Restore(80d);
            AssertResult(result, 40d, 100d, 100d, 80d, 60d, 20d, false, true, false, true);
            Assert.That(result.BecameFull, Is.True);
        }

        [Test]
        public void Restore_AtFull_DoesNotClaimTransition()
        {
            var result = Create(100d, 100d).Restore(30d);
            AssertResult(result, 100d, 100d, 100d, 30d, 0d, 30d, false, false, false, true);
            Assert.That(result.BecameFull, Is.False);
        }

        [Test]
        public void Restore_Zero_IsFullyAppliedWithoutChange()
        {
            AssertResult(Create(100d, 40d).Restore(0d), 40d, 40d, 100d, 0d, 0d, 0d, true, false, false, false);
        }

        [Test]
        public void Spend_PartialPolicyWithEnoughValue_AppliesFullAmount()
        {
            AssertResult(Create(100d, 40d).Spend(30d, ResourceSpendPolicy.AllowPartial), 40d, 10d, 100d, -30d, -30d, 0d, true, true, false, false);
        }

        [Test]
        public void Spend_PartialPolicyWhenInsufficient_EmptiesAndReportsRemainder()
        {
            var result = Create(100d, 40d).Spend(50d, ResourceSpendPolicy.AllowPartial);
            AssertResult(result, 40d, 0d, 100d, -50d, -40d, -10d, false, true, true, false);
            Assert.That(result.BecameEmpty, Is.True);
        }

        [Test]
        public void Spend_RequireFullWhenInsufficient_PreservesState()
        {
            AssertResult(Create(100d, 40d).Spend(50d, ResourceSpendPolicy.RequireFull), 40d, 40d, 100d, -50d, 0d, -50d, false, false, false, false);
        }

        [Test]
        public void Spend_RequireFullExact_EmptiesAndReportsFullApplication()
        {
            var result = Create(100d, 40d).Spend(40d, ResourceSpendPolicy.RequireFull);
            AssertResult(result, 40d, 0d, 100d, -40d, -40d, 0d, true, true, true, false);
            Assert.That(result.BecameEmpty, Is.True);
        }

        [TestCase(ResourceSpendPolicy.AllowPartial)]
        [TestCase(ResourceSpendPolicy.RequireFull)]
        public void Spend_Zero_IsFullyAppliedWithoutChange(ResourceSpendPolicy policy)
        {
            AssertResult(Create(100d, 40d).Spend(0d, policy), 40d, 40d, 100d, 0d, 0d, 0d, true, false, false, false);
        }

        [Test]
        public void Spend_InvalidPolicy_DoesNotMutateState()
        {
            var meter = Create(100d, 40d);
            var result = meter.Spend(10d, (ResourceSpendPolicy)99);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ResourceMeterError.InvalidPolicy));
            Assert.That(meter.Current, Is.EqualTo(40d));
        }

        [TestCase(double.NaN, ResourceMeterError.NonFiniteAmount)]
        [TestCase(double.PositiveInfinity, ResourceMeterError.NonFiniteAmount)]
        [TestCase(-0.001d, ResourceMeterError.NegativeAmount)]
        public void Restore_InvalidAmount_DoesNotMutateState(double amount, ResourceMeterError expected)
        {
            var meter = Create(100d, 40d);
            var result = meter.Restore(amount);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(meter.Current, Is.EqualTo(40d));
        }

        [TestCase(double.NaN, ResourceMeterError.NonFiniteAmount)]
        [TestCase(double.NegativeInfinity, ResourceMeterError.NonFiniteAmount)]
        [TestCase(-1d, ResourceMeterError.NegativeAmount)]
        public void Spend_InvalidAmount_DoesNotMutateState(double amount, ResourceMeterError expected)
        {
            var meter = Create(100d, 40d);
            var result = meter.Spend(amount, ResourceSpendPolicy.AllowPartial);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(meter.Current, Is.EqualTo(40d));
        }

        [Test]
        public void Restore_MaxFiniteAmount_ClampsWithoutOverflow()
        {
            var result = Create(100d, 40d).Restore(double.MaxValue);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CurrentValue, Is.EqualTo(100d));
            Assert.That(result.AppliedDelta, Is.EqualTo(60d));
            Assert.That(result.UnappliedDelta, Is.EqualTo(double.MaxValue));
        }

        [Test]
        public void Spend_MaxFiniteAmount_PartialClampsWithoutOverflow()
        {
            var result = Create(100d, 40d).Spend(double.MaxValue, ResourceSpendPolicy.AllowPartial);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CurrentValue, Is.Zero);
            Assert.That(result.AppliedDelta, Is.EqualTo(-40d));
            Assert.That(result.UnappliedDelta, Is.EqualTo(-double.MaxValue));
        }

        [Test]
        public void TryReset_ValidValue_ReconstructsState()
        {
            var meter = Create(100d, 40d);
            Assert.That(meter.TryReset(75d, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ResourceMeterError.None));
            Assert.That(meter.Current, Is.EqualTo(75d));
            Assert.That(meter.Normalized, Is.EqualTo(0.75d));
        }

        [TestCase(double.NaN, ResourceMeterError.NonFiniteValue)]
        [TestCase(-1d, ResourceMeterError.ValueOutOfRange)]
        [TestCase(101d, ResourceMeterError.ValueOutOfRange)]
        public void TryReset_InvalidValue_PreservesState(double value, ResourceMeterError expected)
        {
            var meter = Create(100d, 40d);
            Assert.That(meter.TryReset(value, out var error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(meter.Current, Is.EqualTo(40d));
        }

        [Test]
        public void Sequence_CanBeReconstructedFromPublicCurrent()
        {
            var first = Create(100d, 40d);
            first.Restore(30d);
            first.Spend(20d, ResourceSpendPolicy.RequireFull);
            var second = Create(100d, 40d);
            second.Restore(30d);
            second.Spend(20d, ResourceSpendPolicy.RequireFull);
            Assert.That(first.Current, Is.EqualTo(50d));
            Assert.That(second.Current, Is.EqualTo(first.Current));
        }

        [Test]
        public void Result_EqualityAndDefaultValidity_AreExplicit()
        {
            var first = Create(100d, 40d).Restore(30d);
            var second = Create(100d, 40d).Restore(30d);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            var empty = default(ResourceChangeResult);
            Assert.That(empty.Succeeded, Is.False);
            Assert.That(empty.IsEmpty, Is.False);
            Assert.That(empty.IsFull, Is.False);
        }

        private static ResourceMeter Create(double capacity, double current)
        {
            Assert.That(ResourceMeter.TryCreate(capacity, current, out var meter, out var error), Is.True, error.ToString());
            return meter;
        }

        private static void AssertResult(ResourceChangeResult result, double previous, double current, double capacity, double requested, double applied, double unapplied, bool fully, bool changed, bool empty, bool full)
        {
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            Assert.That(result.PreviousValue, Is.EqualTo(previous));
            Assert.That(result.CurrentValue, Is.EqualTo(current));
            Assert.That(result.Capacity, Is.EqualTo(capacity));
            Assert.That(result.RequestedDelta, Is.EqualTo(requested));
            Assert.That(result.AppliedDelta, Is.EqualTo(applied));
            Assert.That(result.UnappliedDelta, Is.EqualTo(unapplied));
            Assert.That(result.WasFullyApplied, Is.EqualTo(fully));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.IsEmpty, Is.EqualTo(empty));
            Assert.That(result.IsFull, Is.EqualTo(full));
            Assert.That(result.Error, Is.EqualTo(ResourceMeterError.None));
        }
    }
}
