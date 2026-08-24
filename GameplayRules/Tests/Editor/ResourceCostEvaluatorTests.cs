using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayResources.Tests
{
    [TestFixture]
    public sealed class ResourceCostEvaluatorTests
    {
        [Test]
        public void TryEvaluate_NullBalances_TakesPrecedence()
        {
            Assert.That(ResourceCostEvaluator.TryEvaluate(null, null, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(ResourceCostError.NullBalances));
        }

        [Test]
        public void TryEvaluate_NullCosts_ReturnsExplicitFailure()
        {
            Assert.That(ResourceCostEvaluator.TryEvaluate(Array.Empty<ResourceAmount>(), null, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(ResourceCostError.NullCosts));
        }

        [Test]
        public void TryEvaluate_TooManyBalances_ReturnsExplicitFailure()
        {
            var balances = Enumerable.Range(1, ResourceCostEvaluator.MaximumEntryCount + 1).Select(id => Amount(id, 1d)).ToArray();
            AssertFailure(balances, new[] { Amount(1, 1d) }, ResourceCostError.InvalidBalanceCount);
        }

        [TestCase(0)]
        [TestCase(ResourceCostEvaluator.MaximumEntryCount + 1)]
        public void TryEvaluate_InvalidCostCount_ReturnsExplicitFailure(int count)
        {
            var costs = Enumerable.Range(1, count).Select(id => Amount(id, 1d)).ToArray();
            AssertFailure(Array.Empty<ResourceAmount>(), costs, ResourceCostError.InvalidCostCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryEvaluate_InvalidBalanceId_ReturnsExplicitFailure(int resourceId)
        {
            AssertFailure(new[] { Amount(resourceId, 1d) }, new[] { Amount(1, 1d) }, ResourceCostError.InvalidResourceId);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void TryEvaluate_InvalidCostId_ReturnsExplicitFailure(int resourceId)
        {
            AssertFailure(Array.Empty<ResourceAmount>(), new[] { Amount(resourceId, 1d) }, ResourceCostError.InvalidResourceId);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteBalance_ReturnsExplicitFailure(double amount)
        {
            AssertFailure(new[] { Amount(1, amount) }, new[] { Amount(1, 1d) }, ResourceCostError.NonFiniteAmount);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteCost_ReturnsExplicitFailure(double amount)
        {
            AssertFailure(new[] { Amount(1, 1d) }, new[] { Amount(1, amount) }, ResourceCostError.NonFiniteAmount);
        }

        [Test]
        public void TryEvaluate_NegativeBalance_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Amount(1, -0.01d) }, new[] { Amount(1, 1d) }, ResourceCostError.NegativeAmount);
        }

        [Test]
        public void TryEvaluate_NegativeCost_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Amount(1, 1d) }, new[] { Amount(1, -0.01d) }, ResourceCostError.NegativeAmount);
        }

        [Test]
        public void TryEvaluate_DuplicateBalanceId_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Amount(1, 5d), Amount(1, 6d) }, new[] { Amount(1, 1d) }, ResourceCostError.DuplicateBalanceId);
        }

        [Test]
        public void TryEvaluate_DuplicateCostId_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Amount(1, 5d) }, new[] { Amount(1, 1d), Amount(1, 2d) }, ResourceCostError.DuplicateCostId);
        }

        [Test]
        public void TryEvaluate_MultipleResourcesAffordable_ReturnsRemainingPlan()
        {
            var evaluation = Evaluate(
                new[] { Amount(1, 100d), Amount(2, 40d), Amount(3, 7d) },
                new[] { Amount(1, 25d), Amount(2, 10d), Amount(3, 7d) });

            Assert.That(evaluation.CanPay, Is.True);
            AssertLine(evaluation, 0, 1, 100d, 25d, 75d, 0d, true);
            AssertLine(evaluation, 1, 2, 40d, 10d, 30d, 0d, true);
            AssertLine(evaluation, 2, 3, 7d, 7d, 0d, 0d, true);
        }

        [Test]
        public void TryEvaluate_OneShortage_ReturnsAllLinesWithoutPartialMutation()
        {
            var evaluation = Evaluate(
                new[] { Amount(1, 100d), Amount(2, 3d) },
                new[] { Amount(1, 25d), Amount(2, 10d) });

            Assert.That(evaluation.CanPay, Is.False);
            AssertLine(evaluation, 0, 1, 100d, 25d, 75d, 0d, true);
            AssertLine(evaluation, 1, 2, 3d, 10d, 0d, 7d, false);
        }

        [Test]
        public void TryEvaluate_MissingBalance_IsEvaluatedAsZero()
        {
            var evaluation = Evaluate(Array.Empty<ResourceAmount>(), new[] { Amount(9, 4d) });
            Assert.That(evaluation.CanPay, Is.False);
            AssertLine(evaluation, 0, 9, 0d, 4d, 0d, 4d, false);
        }

        [Test]
        public void TryEvaluate_ZeroCostWithoutBalance_IsAffordable()
        {
            var evaluation = Evaluate(Array.Empty<ResourceAmount>(), new[] { Amount(9, 0d) });
            Assert.That(evaluation.CanPay, Is.True);
            AssertLine(evaluation, 0, 9, 0d, 0d, 0d, 0d, true);
        }

        [Test]
        public void TryEvaluate_OutputFollowsCostInputOrder()
        {
            var evaluation = Evaluate(
                new[] { Amount(2, 20d), Amount(1, 10d), Amount(3, 30d) },
                new[] { Amount(3, 3d), Amount(1, 1d), Amount(2, 2d) });

            AssertLine(evaluation, 0, 3, 30d, 3d, 27d, 0d, true);
            AssertLine(evaluation, 1, 1, 10d, 1d, 9d, 0d, true);
            AssertLine(evaluation, 2, 2, 20d, 2d, 18d, 0d, true);
        }

        [Test]
        public void TryEvaluate_UnusedBalances_DoNotCreateLines()
        {
            var evaluation = Evaluate(new[] { Amount(1, 5d), Amount(2, 99d) }, new[] { Amount(1, 2d) });
            Assert.That(evaluation.LineCount, Is.EqualTo(1));
            AssertLine(evaluation, 0, 1, 5d, 2d, 3d, 0d, true);
        }

        [Test]
        public void TryEvaluate_MaximumFiniteAmount_RemainsFinite()
        {
            var evaluation = Evaluate(new[] { Amount(1, double.MaxValue) }, new[] { Amount(1, double.MaxValue) });
            AssertLine(evaluation, 0, 1, double.MaxValue, double.MaxValue, 0d, 0d, true);
        }

        [Test]
        public void TryEvaluate_MaximumEntryCounts_AreAccepted()
        {
            var balances = Enumerable.Range(1, ResourceCostEvaluator.MaximumEntryCount).Select(id => Amount(id, id)).ToArray();
            var costs = Enumerable.Range(1, ResourceCostEvaluator.MaximumEntryCount).Select(id => Amount(id, id)).ToArray();
            var evaluation = Evaluate(balances, costs);
            Assert.That(evaluation.CanPay, Is.True);
            Assert.That(evaluation.LineCount, Is.EqualTo(ResourceCostEvaluator.MaximumEntryCount));
        }

        [Test]
        public void TryEvaluate_DoesNotMutateInputArrays()
        {
            var balances = new[] { Amount(2, 20d), Amount(1, 10d) };
            var costs = new[] { Amount(1, 3d), Amount(2, 4d) };
            var balanceBefore = balances.ToArray();
            var costBefore = costs.ToArray();
            Evaluate(balances, costs);
            Assert.That(balances, Is.EqualTo(balanceBefore));
            Assert.That(costs, Is.EqualTo(costBefore));
        }

        [Test]
        public void Evaluation_RemainsImmutableAfterInputMutation()
        {
            var balances = new[] { Amount(1, 10d) };
            var costs = new[] { Amount(1, 4d) };
            var evaluation = Evaluate(balances, costs);
            balances[0] = Amount(1, 999d);
            costs[0] = Amount(1, 999d);
            AssertLine(evaluation, 0, 1, 10d, 4d, 6d, 0d, true);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetLine_InvalidIndex_ReturnsFalse(int index)
        {
            var evaluation = Evaluate(new[] { Amount(1, 10d) }, new[] { Amount(1, 4d) });
            Assert.That(evaluation.TryGetLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(ResourceCostLine)));
        }

        [Test]
        public void SameInputs_ReturnBitStableLines()
        {
            var balances = new[] { Amount(1, 0.3d), Amount(2, 0.2d) };
            var costs = new[] { Amount(2, 0.1d), Amount(1, 0.2d) };
            var first = Evaluate(balances, costs);
            var second = Evaluate(balances, costs);
            for (var index = 0; index < first.LineCount; index++)
            {
                Assert.That(first.TryGetLine(index, out var left), Is.True);
                Assert.That(second.TryGetLine(index, out var right), Is.True);
                Assert.That(BitConverter.DoubleToInt64Bits(left.RemainingAmount), Is.EqualTo(BitConverter.DoubleToInt64Bits(right.RemainingAmount)));
                Assert.That(left, Is.EqualTo(right));
            }
        }

        [Test]
        public void ValueEquality_UsesAllFields()
        {
            Assert.That(Amount(1, 2d), Is.EqualTo(Amount(1, 2d)));
            Assert.That(Amount(1, 2d) == Amount(1, 2d), Is.True);
            Assert.That(Amount(1, 2d) != Amount(2, 2d), Is.True);
            var evaluation = Evaluate(new[] { Amount(1, 5d) }, new[] { Amount(1, 2d) });
            Assert.That(evaluation.TryGetLine(0, out var line), Is.True);
            Assert.That(line == line, Is.True);
            Assert.That(line.GetHashCode(), Is.EqualTo(line.GetHashCode()));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyNineGameplayResourcesTypes()
        {
            var actual = typeof(ResourceCostEvaluator).Assembly.GetExportedTypes().Where(type => string.Equals(type.Namespace, "GameplayResources", StringComparison.Ordinal)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            var expected = new[]
            {
                typeof(ResourceAmount),
                typeof(ResourceChangeResult),
                typeof(ResourceCostError),
                typeof(ResourceCostEvaluation),
                typeof(ResourceCostEvaluator),
                typeof(ResourceCostLine),
                typeof(ResourceMeter),
                typeof(ResourceMeterError),
                typeof(ResourceSpendPolicy)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static ResourceAmount Amount(int resourceId, double amount) => new ResourceAmount(resourceId, amount);

        private static ResourceCostEvaluation Evaluate(ResourceAmount[] balances, ResourceAmount[] costs)
        {
            Assert.That(ResourceCostEvaluator.TryEvaluate(balances, costs, out var evaluation, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(ResourceCostError.None));
            Assert.That(evaluation, Is.Not.Null);
            return evaluation;
        }

        private static void AssertFailure(ResourceAmount[] balances, ResourceAmount[] costs, ResourceCostError expected)
        {
            Assert.That(ResourceCostEvaluator.TryEvaluate(balances, costs, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertLine(ResourceCostEvaluation evaluation, int index, int resourceId, double available, double required, double remaining, double deficit, bool affordable)
        {
            Assert.That(evaluation.TryGetLine(index, out var line), Is.True);
            Assert.That(line.ResourceId, Is.EqualTo(resourceId));
            Assert.That(line.AvailableAmount, Is.EqualTo(available));
            Assert.That(line.RequiredAmount, Is.EqualTo(required));
            Assert.That(line.RemainingAmount, Is.EqualTo(remaining));
            Assert.That(line.DeficitAmount, Is.EqualTo(deficit));
            Assert.That(line.IsAffordable, Is.EqualTo(affordable));
        }
    }
}
