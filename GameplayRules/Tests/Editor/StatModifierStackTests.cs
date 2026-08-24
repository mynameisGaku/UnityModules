using NUnit.Framework;

namespace GameplayStats.Tests
{
    public sealed class StatModifierStackTests
    {
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryCreate_NonFiniteBase_Fails(double value)
        {
            Assert.That(StatModifierStack.TryCreate(value, out var stack, out var error), Is.False);
            Assert.That(stack, Is.Null);
            Assert.That(error, Is.EqualTo(StatModifierError.NonFiniteBaseValue));
        }

        [TestCase(0d)]
        [TestCase(100d)]
        [TestCase(-25d)]
        public void TryCreate_FiniteBase_ExposesEmptyEvaluation(double value)
        {
            var stack = Create(value);
            Assert.That(stack.BaseValue, Is.EqualTo(value));
            Assert.That(stack.CurrentValue, Is.EqualTo(value));
            Assert.That(stack.FlatTotal, Is.Zero);
            Assert.That(stack.AdditivePercentTotal, Is.Zero);
            Assert.That(stack.MultiplicativeFactor, Is.EqualTo(1d));
            Assert.That(stack.ModifierCount, Is.Zero);
        }

        [Test]
        public void Add_Flat_AddsBeforeOtherStages()
        {
            AssertResult(Create(100d).Add(10, StatModifierKind.Flat, 15d), 100d, 115d, 100d, 15d, 0d, 1d, 1, 10, true);
        }

        [Test]
        public void Add_AdditivePercent_UsesRatio()
        {
            AssertResult(Create(100d).Add(20, StatModifierKind.AdditivePercent, 0.2d), 100d, 120d, 100d, 0d, 0.2d, 1d, 1, 20, true);
        }

        [Test]
        public void Add_MultiplicativeFactor_MultipliesFinalStage()
        {
            AssertResult(Create(100d).Add(30, StatModifierKind.MultiplicativeFactor, 1.5d), 100d, 150d, 100d, 0d, 0d, 1.5d, 1, 30, true);
        }

        [Test]
        public void Composition_UsesDocumentedThreeStageFormula()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 15d);
            stack.Add(20, StatModifierKind.AdditivePercent, 0.2d);
            var result = stack.Add(30, StatModifierKind.MultiplicativeFactor, 1.5d);
            AssertResult(result, 138d, 207d, 100d, 15d, 0.2d, 1.5d, 3, 30, true);
        }

        [Test]
        public void SameIdSet_DifferentInsertionOrder_ProducesSameState()
        {
            var first = Create(80d);
            first.Add(30, StatModifierKind.MultiplicativeFactor, 1.25d);
            first.Add(10, StatModifierKind.Flat, 20d);
            first.Add(20, StatModifierKind.AdditivePercent, -0.1d);
            var second = Create(80d);
            second.Add(20, StatModifierKind.AdditivePercent, -0.1d);
            second.Add(30, StatModifierKind.MultiplicativeFactor, 1.25d);
            second.Add(10, StatModifierKind.Flat, 20d);
            Assert.That(first.CurrentValue, Is.EqualTo(112.5d));
            Assert.That(second.CurrentValue, Is.EqualTo(first.CurrentValue));
            Assert.That(second.FlatTotal, Is.EqualTo(first.FlatTotal));
            Assert.That(second.AdditivePercentTotal, Is.EqualTo(first.AdditivePercentTotal));
            Assert.That(second.MultiplicativeFactor, Is.EqualTo(first.MultiplicativeFactor));
        }

        [Test]
        public void ModifierSnapshots_AreAlwaysSortedById()
        {
            var stack = Create(1d);
            stack.Add(30, StatModifierKind.Flat, 3d);
            stack.Add(10, StatModifierKind.AdditivePercent, 1d);
            stack.Add(20, StatModifierKind.MultiplicativeFactor, 2d);
            Assert.That(stack.TryGetModifierAt(0, out var first), Is.True);
            Assert.That(stack.TryGetModifierAt(1, out var second), Is.True);
            Assert.That(stack.TryGetModifierAt(2, out var third), Is.True);
            Assert.That(new[] { first.Id, second.Id, third.Id }, Is.EqualTo(new long[] { 10, 20, 30 }));
            Assert.That(stack.TryGetModifierAt(-1, out _), Is.False);
            Assert.That(stack.TryGetModifierAt(3, out _), Is.False);
        }

        [Test]
        public void TryGetModifier_FindsExactId()
        {
            var stack = Create(10d);
            stack.Add(5, StatModifierKind.Flat, 2d);
            Assert.That(stack.TryGetModifier(5, out var modifier), Is.True);
            Assert.That(modifier.Id, Is.EqualTo(5));
            Assert.That(modifier.Kind, Is.EqualTo(StatModifierKind.Flat));
            Assert.That(modifier.Value, Is.EqualTo(2d));
            Assert.That(stack.TryGetModifier(6, out _), Is.False);
        }

        [Test]
        public void Add_DuplicateId_PreservesState()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 15d);
            AssertFailure(stack.Add(10, StatModifierKind.Flat, 99d), StatModifierError.DuplicateModifierId, 10);
            AssertState(stack, 100d, 115d, 15d, 0d, 1d, 1);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void Add_InvalidId_PreservesState(long id)
        {
            var stack = Create(100d);
            AssertFailure(stack.Add(id, StatModifierKind.Flat, 10d), StatModifierError.InvalidModifierId, id);
            AssertState(stack, 100d, 100d, 0d, 0d, 1d, 0);
        }

        [Test]
        public void Add_InvalidKind_PreservesState()
        {
            var stack = Create(100d);
            AssertFailure(stack.Add(1, (StatModifierKind)99, 10d), StatModifierError.InvalidModifierKind, 1);
            AssertState(stack, 100d, 100d, 0d, 0d, 1d, 0);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Add_NonFiniteValue_PreservesState(double value)
        {
            var stack = Create(100d);
            AssertFailure(stack.Add(1, StatModifierKind.Flat, value), StatModifierError.NonFiniteModifierValue, 1);
            AssertState(stack, 100d, 100d, 0d, 0d, 1d, 0);
        }

        [Test]
        public void Add_ThirtyThirdModifier_IsRejected()
        {
            var stack = Create(0d);
            for (var id = 1; id <= StatModifierStack.MaximumModifierCount; id++) Assert.That(stack.Add(id, StatModifierKind.Flat, 1d).Succeeded, Is.True);
            AssertFailure(stack.Add(33, StatModifierKind.Flat, 1d), StatModifierError.CapacityReached, 33);
            Assert.That(stack.ModifierCount, Is.EqualTo(32));
            Assert.That(stack.CurrentValue, Is.EqualTo(32d));
        }

        [Test]
        public void Update_ExistingModifier_ReevaluatesInPlace()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 15d);
            var result = stack.Update(10, StatModifierKind.AdditivePercent, 0.5d);
            AssertResult(result, 115d, 150d, 100d, 0d, 0.5d, 1d, 1, 10, true);
        }

        [Test]
        public void Update_MissingModifier_PreservesState()
        {
            var stack = Create(100d);
            AssertFailure(stack.Update(5, StatModifierKind.Flat, 1d), StatModifierError.ModifierNotFound, 5);
            AssertState(stack, 100d, 100d, 0d, 0d, 1d, 0);
        }

        [Test]
        public void Remove_ExistingModifier_ReevaluatesRemainingState()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 15d);
            stack.Add(20, StatModifierKind.AdditivePercent, 0.2d);
            AssertResult(stack.Remove(10), 138d, 120d, 100d, 0d, 0.2d, 1d, 1, 10, true);
        }

        [Test]
        public void Remove_MissingModifier_PreservesState()
        {
            var stack = Create(100d);
            AssertFailure(stack.Remove(5), StatModifierError.ModifierNotFound, 5);
            AssertState(stack, 100d, 100d, 0d, 0d, 1d, 0);
        }

        [Test]
        public void SetBaseValue_ReevaluatesCurrentModifiers()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 20d);
            AssertResult(stack.SetBaseValue(50d), 120d, 70d, 50d, 20d, 0d, 1d, 1, 0, true);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void SetBaseValue_NonFinite_PreservesState(double value)
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 20d);
            AssertFailure(stack.SetBaseValue(value), StatModifierError.NonFiniteBaseValue, 0);
            AssertState(stack, 100d, 120d, 20d, 0d, 1d, 1);
        }

        [Test]
        public void Clear_RemovesEveryModifierAndReturnsBase()
        {
            var stack = Create(100d);
            stack.Add(10, StatModifierKind.Flat, 15d);
            stack.Add(20, StatModifierKind.AdditivePercent, 0.2d);
            AssertResult(stack.Clear(), 138d, 100d, 100d, 0d, 0d, 1d, 0, 0, true);
        }

        [Test]
        public void Add_FlatOverflow_IsRejectedWithoutMutation()
        {
            var stack = Create(double.MaxValue);
            AssertFailure(stack.Add(1, StatModifierKind.Flat, double.MaxValue), StatModifierError.ResultNotFinite, 1);
            AssertState(stack, double.MaxValue, double.MaxValue, 0d, 0d, 1d, 0);
        }

        [Test]
        public void Add_AdditiveOverflow_IsRejectedWithoutMutation()
        {
            var stack = Create(double.MaxValue);
            AssertFailure(stack.Add(1, StatModifierKind.AdditivePercent, 1d), StatModifierError.ResultNotFinite, 1);
            AssertState(stack, double.MaxValue, double.MaxValue, 0d, 0d, 1d, 0);
        }

        [Test]
        public void Update_FactorOverflow_RollsBackModifier()
        {
            var stack = Create(1d);
            stack.Add(1, StatModifierKind.MultiplicativeFactor, double.MaxValue);
            stack.Add(2, StatModifierKind.MultiplicativeFactor, 1d);
            AssertFailure(stack.Update(2, StatModifierKind.MultiplicativeFactor, double.MaxValue), StatModifierError.ResultNotFinite, 2);
            AssertState(stack, 1d, double.MaxValue, 0d, 0d, double.MaxValue, 2);
            Assert.That(stack.TryGetModifier(2, out var modifier), Is.True);
            Assert.That(modifier.Value, Is.EqualTo(1d));
        }

        [Test]
        public void Remove_OverflowingReveal_RollsBackRemoval()
        {
            var stack = Create(1d);
            stack.Add(1, StatModifierKind.MultiplicativeFactor, 0d);
            stack.Add(2, StatModifierKind.MultiplicativeFactor, double.MaxValue);
            stack.Add(3, StatModifierKind.MultiplicativeFactor, double.MaxValue);
            AssertFailure(stack.Remove(1), StatModifierError.ResultNotFinite, 1);
            AssertState(stack, 1d, 0d, 0d, 0d, 0d, 3);
            Assert.That(stack.TryGetModifier(1, out _), Is.True);
        }

        [Test]
        public void SetBaseValue_Overflow_RollsBackBase()
        {
            var stack = Create(1d);
            stack.Add(1, StatModifierKind.MultiplicativeFactor, double.MaxValue);
            AssertFailure(stack.SetBaseValue(2d), StatModifierError.ResultNotFinite, 0);
            AssertState(stack, 1d, double.MaxValue, 0d, 0d, double.MaxValue, 1);
        }

        [Test]
        public void NegativeFiniteModifiers_RemainExplicitAndFinite()
        {
            var stack = Create(100d);
            stack.Add(1, StatModifierKind.Flat, -20d);
            stack.Add(2, StatModifierKind.AdditivePercent, -0.5d);
            var result = stack.Add(3, StatModifierKind.MultiplicativeFactor, -2d);
            AssertResult(result, 40d, -80d, 100d, -20d, -0.5d, -2d, 3, 3, true);
        }

        [Test]
        public void ZeroOutputs_AreNormalized()
        {
            var stack = Create(-0d);
            var result = stack.Add(1, StatModifierKind.Flat, -0d);
            Assert.That(System.BitConverter.DoubleToInt64Bits(stack.BaseValue), Is.Zero);
            Assert.That(System.BitConverter.DoubleToInt64Bits(result.CurrentValue), Is.Zero);
            Assert.That(System.BitConverter.DoubleToInt64Bits(result.FlatTotal), Is.Zero);
        }

        [Test]
        public void ResultAndModifier_EqualityAreValueBased()
        {
            var firstStack = Create(100d);
            var secondStack = Create(100d);
            var first = firstStack.Add(1, StatModifierKind.Flat, 20d);
            var second = secondStack.Add(1, StatModifierKind.Flat, 20d);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            firstStack.TryGetModifierAt(0, out var firstModifier);
            secondStack.TryGetModifierAt(0, out var secondModifier);
            Assert.That(firstModifier == secondModifier, Is.True);
            Assert.That(firstModifier.GetHashCode(), Is.EqualTo(secondModifier.GetHashCode()));
            Assert.That(default(StatModifierEvaluationResult).Succeeded, Is.False);
        }

        private static StatModifierStack Create(double baseValue)
        {
            Assert.That(StatModifierStack.TryCreate(baseValue, out var stack, out var error), Is.True, error.ToString());
            return stack;
        }

        private static void AssertState(StatModifierStack stack, double baseValue, double current, double flat, double additivePercent, double multiplicative, int count)
        {
            Assert.That(stack.BaseValue, Is.EqualTo(baseValue));
            Assert.That(stack.CurrentValue, Is.EqualTo(current));
            Assert.That(stack.FlatTotal, Is.EqualTo(flat));
            Assert.That(stack.AdditivePercentTotal, Is.EqualTo(additivePercent));
            Assert.That(stack.MultiplicativeFactor, Is.EqualTo(multiplicative));
            Assert.That(stack.ModifierCount, Is.EqualTo(count));
        }

        private static void AssertResult(StatModifierEvaluationResult result, double previous, double current, double baseValue, double flat, double additivePercent, double multiplicative, int count, long id, bool changed)
        {
            Assert.That(result.Succeeded, Is.True, result.Error.ToString());
            Assert.That(result.PreviousValue, Is.EqualTo(previous));
            Assert.That(result.CurrentValue, Is.EqualTo(current));
            Assert.That(result.BaseValue, Is.EqualTo(baseValue));
            Assert.That(result.FlatTotal, Is.EqualTo(flat));
            Assert.That(result.AdditivePercentTotal, Is.EqualTo(additivePercent));
            Assert.That(result.MultiplicativeFactor, Is.EqualTo(multiplicative));
            Assert.That(result.ModifierCount, Is.EqualTo(count));
            Assert.That(result.AffectedModifierId, Is.EqualTo(id));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Error, Is.EqualTo(StatModifierError.None));
        }

        private static void AssertFailure(StatModifierEvaluationResult result, StatModifierError error, long id)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.AffectedModifierId, Is.EqualTo(id));
            Assert.That(result.Changed, Is.False);
        }
    }
}
