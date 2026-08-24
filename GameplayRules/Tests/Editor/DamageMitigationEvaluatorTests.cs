using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayDamage.Tests
{
    [TestFixture]
    public sealed class DamageMitigationEvaluatorTests
    {
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteDamage_ReturnsExplicitFailure(double damage)
        {
            AssertFailure(damage, Array.Empty<DamageMitigationLayer>(), DamageMitigationError.NonFiniteDamage);
        }

        [Test]
        public void TryEvaluate_NegativeDamage_ReturnsExplicitFailure()
        {
            AssertFailure(-0.01d, Array.Empty<DamageMitigationLayer>(), DamageMitigationError.NegativeDamage);
        }

        [Test]
        public void TryEvaluate_NullLayers_ReturnsExplicitFailure()
        {
            AssertFailure(100d, null, DamageMitigationError.NullLayers);
        }

        [Test]
        public void TryEvaluate_DamageValidation_PrecedesNullLayers()
        {
            AssertFailure(double.NaN, null, DamageMitigationError.NonFiniteDamage);
        }

        [Test]
        public void TryEvaluate_TooManyLayers_ReturnsExplicitFailure()
        {
            var layers = Enumerable.Range(1, DamageMitigationEvaluator.MaximumLayerCount + 1).Select(id => Flat(id, 1d)).ToArray();
            AssertFailure(100d, layers, DamageMitigationError.InvalidLayerCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryEvaluate_InvalidLayerId_ReturnsExplicitFailure(int id)
        {
            AssertFailure(100d, new[] { Flat(id, 1d) }, DamageMitigationError.InvalidLayerId);
        }

        [Test]
        public void TryEvaluate_InvalidKind_ReturnsExplicitFailure()
        {
            AssertFailure(100d, new[] { new DamageMitigationLayer(1, (DamageMitigationKind)99, 1d) }, DamageMitigationError.InvalidKind);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TryEvaluate_NonFiniteLayerValue_ReturnsExplicitFailure(double value)
        {
            AssertFailure(100d, new[] { Flat(1, value) }, DamageMitigationError.NonFiniteValue);
        }

        [Test]
        public void TryEvaluate_NegativeLayerValue_ReturnsExplicitFailure()
        {
            AssertFailure(100d, new[] { Flat(1, -0.01d) }, DamageMitigationError.NegativeValue);
        }

        [Test]
        public void TryEvaluate_RatioAboveOne_ReturnsExplicitFailure()
        {
            AssertFailure(100d, new[] { Ratio(1, 1.0001d) }, DamageMitigationError.RatioOutOfRange);
        }

        [Test]
        public void TryEvaluate_DuplicateLayerId_ReturnsExplicitFailure()
        {
            AssertFailure(100d, new[] { Flat(1, 10d), Ratio(1, 0.2d) }, DamageMitigationError.DuplicateLayerId);
        }

        [Test]
        public void TryEvaluate_EmptyLayers_ReturnsUnchangedDamage()
        {
            var result = Evaluate(100d, Array.Empty<DamageMitigationLayer>());
            Assert.That(result.OriginalDamage, Is.EqualTo(100d));
            Assert.That(result.FinalDamage, Is.EqualTo(100d));
            Assert.That(result.MitigatedDamage, Is.Zero);
            Assert.That(result.StepCount, Is.Zero);
            Assert.That(result.WasFullyMitigated, Is.False);
        }

        [Test]
        public void TryEvaluate_FlatReduction_SubtractsFixedAmount()
        {
            var result = Evaluate(100d, new[] { Flat(10, 25d) });
            Assert.That(result.FinalDamage, Is.EqualTo(75d));
            AssertStep(result, 0, 10, DamageMitigationKind.FlatReduction, 25d, 100d, 25d, 25d, 75d, false);
        }

        [Test]
        public void TryEvaluate_RatioReduction_UsesCurrentDamage()
        {
            var result = Evaluate(100d, new[] { Ratio(20, 0.25d) });
            Assert.That(result.FinalDamage, Is.EqualTo(75d));
            AssertStep(result, 0, 20, DamageMitigationKind.RatioReduction, 0.25d, 100d, 25d, 25d, 75d, false);
        }

        [Test]
        public void TryEvaluate_InputOrder_IsSemanticallyVisible()
        {
            var flatThenRatio = Evaluate(100d, new[] { Flat(1, 20d), Ratio(2, 0.25d) });
            var ratioThenFlat = Evaluate(100d, new[] { Ratio(2, 0.25d), Flat(1, 20d) });
            Assert.That(flatThenRatio.FinalDamage, Is.EqualTo(60d));
            Assert.That(ratioThenFlat.FinalDamage, Is.EqualTo(55d));
        }

        [Test]
        public void TryEvaluate_ExcessFlatReduction_ClampsAtZero()
        {
            var result = Evaluate(100d, new[] { Flat(1, 120d) });
            Assert.That(result.FinalDamage, Is.Zero);
            Assert.That(result.MitigatedDamage, Is.EqualTo(100d));
            Assert.That(result.WasFullyMitigated, Is.True);
            AssertStep(result, 0, 1, DamageMitigationKind.FlatReduction, 120d, 100d, 120d, 100d, 0d, true);
        }

        [Test]
        public void TryEvaluate_LayersAfterZero_StillProduceOrderedSteps()
        {
            var result = Evaluate(30d, new[] { Flat(1, 40d), Ratio(2, 0.5d), Flat(3, 2d) });
            Assert.That(result.StepCount, Is.EqualTo(3));
            AssertStep(result, 1, 2, DamageMitigationKind.RatioReduction, 0.5d, 0d, 0d, 0d, 0d, false);
            AssertStep(result, 2, 3, DamageMitigationKind.FlatReduction, 2d, 0d, 2d, 0d, 0d, true);
        }

        [Test]
        public void TryEvaluate_RatioOne_FullyMitigatesDamage()
        {
            var result = Evaluate(250d, new[] { Ratio(1, 1d) });
            Assert.That(result.FinalDamage, Is.Zero);
            Assert.That(result.WasFullyMitigated, Is.True);
        }

        [Test]
        public void TryEvaluate_ZeroReductions_PreserveDamage()
        {
            var result = Evaluate(80d, new[] { Flat(1, 0d), Ratio(2, 0d) });
            Assert.That(result.FinalDamage, Is.EqualTo(80d));
            Assert.That(result.MitigatedDamage, Is.Zero);
        }

        [Test]
        public void TryEvaluate_MaximumLayerCount_IsAccepted()
        {
            var layers = Enumerable.Range(1, DamageMitigationEvaluator.MaximumLayerCount).Select(id => Flat(id, 1d)).ToArray();
            var result = Evaluate(100d, layers);
            Assert.That(result.StepCount, Is.EqualTo(DamageMitigationEvaluator.MaximumLayerCount));
            Assert.That(result.FinalDamage, Is.EqualTo(68d));
        }

        [Test]
        public void TryEvaluate_DoesNotMutateInputArray()
        {
            var layers = new[] { Flat(2, 20d), Ratio(1, 0.5d) };
            var before = layers.ToArray();
            Evaluate(100d, layers);
            Assert.That(layers.Select(value => value.LayerId), Is.EqualTo(before.Select(value => value.LayerId)));
            Assert.That(layers.Select(value => value.Kind), Is.EqualTo(before.Select(value => value.Kind)));
            Assert.That(layers.Select(value => value.Value), Is.EqualTo(before.Select(value => value.Value)));
        }

        [Test]
        public void Evaluation_TryGetStep_RejectsOutOfRangeIndex()
        {
            var result = Evaluate(10d, new[] { Flat(1, 1d) });
            Assert.That(result.TryGetStep(-1, out _), Is.False);
            Assert.That(result.TryGetStep(1, out _), Is.False);
        }

        [Test]
        public void TryEvaluate_DoubleMaximum_FlatMaximum_ReachesZeroWithoutOverflow()
        {
            var result = Evaluate(double.MaxValue, new[] { Flat(1, double.MaxValue) });
            Assert.That(result.FinalDamage, Is.Zero);
            Assert.That(result.MitigatedDamage, Is.EqualTo(double.MaxValue));
        }

        [Test]
        public void TryEvaluate_MixedLayers_ReturnsReconstructableBreakdown()
        {
            var result = Evaluate(200d, new[] { Flat(7, 20d), Ratio(8, 0.5d), Flat(9, 15d) });
            Assert.That(result.FinalDamage, Is.EqualTo(75d));
            Assert.That(result.MitigatedDamage, Is.EqualTo(125d));
            AssertStep(result, 0, 7, DamageMitigationKind.FlatReduction, 20d, 200d, 20d, 20d, 180d, false);
            AssertStep(result, 1, 8, DamageMitigationKind.RatioReduction, 0.5d, 180d, 90d, 90d, 90d, false);
            AssertStep(result, 2, 9, DamageMitigationKind.FlatReduction, 15d, 90d, 15d, 15d, 75d, false);
        }

        private static DamageMitigationEvaluation Evaluate(double damage, DamageMitigationLayer[] layers)
        {
            Assert.That(DamageMitigationEvaluator.TryEvaluate(damage, layers, out var evaluation, out var error), Is.True);
            Assert.That(error, Is.EqualTo(DamageMitigationError.None));
            Assert.That(evaluation, Is.Not.Null);
            return evaluation;
        }

        private static void AssertFailure(double damage, DamageMitigationLayer[] layers, DamageMitigationError expected)
        {
            Assert.That(DamageMitigationEvaluator.TryEvaluate(damage, layers, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static DamageMitigationLayer Flat(int id, double value) => new DamageMitigationLayer(id, DamageMitigationKind.FlatReduction, value);
        private static DamageMitigationLayer Ratio(int id, double value) => new DamageMitigationLayer(id, DamageMitigationKind.RatioReduction, value);

        private static void AssertStep(DamageMitigationEvaluation evaluation, int index, int id, DamageMitigationKind kind, double value, double input, double requested, double applied, double output, bool clamped)
        {
            Assert.That(evaluation.TryGetStep(index, out var step), Is.True);
            Assert.That(step.LayerId, Is.EqualTo(id));
            Assert.That(step.Kind, Is.EqualTo(kind));
            Assert.That(step.Value, Is.EqualTo(value));
            Assert.That(step.InputDamage, Is.EqualTo(input));
            Assert.That(step.RequestedReduction, Is.EqualTo(requested));
            Assert.That(step.AppliedReduction, Is.EqualTo(applied));
            Assert.That(step.OutputDamage, Is.EqualTo(output));
            Assert.That(step.WasClamped, Is.EqualTo(clamped));
        }
    }
}
