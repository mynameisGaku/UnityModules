using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayDecision.Tests
{
    [TestFixture]
    public sealed class UtilityScoreEvaluatorTests
    {
        [Test]
        public void TryEvaluate_Null_ReturnsExplicitFailure()
        {
            AssertFailure(null, UtilityScoreError.NullCandidates);
        }

        [TestCase(0)]
        [TestCase(UtilityScoreEvaluator.MaximumCandidateCount + 1)]
        public void TryEvaluate_InvalidCandidateCount_ReturnsExplicitFailure(int count)
        {
            var candidates = Enumerable.Range(1, count).Select(id => Candidate(id, Factor(1, 0.5d, 1d))).ToArray();
            AssertFailure(candidates, UtilityScoreError.InvalidCandidateCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryEvaluate_InvalidCandidateIdentifier_ReturnsExplicitFailure(int identifier)
        {
            AssertFailure(new[] { Candidate(identifier, Factor(1, 0.5d, 1d)) }, UtilityScoreError.InvalidCandidateIdentifier);
        }

        [Test]
        public void TryEvaluate_DuplicateCandidateIdentifier_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Candidate(1, Factor(1, 0.2d, 1d)), Candidate(1, Factor(2, 0.8d, 1d)) }, UtilityScoreError.DuplicateCandidateIdentifier);
        }

        [TestCase(0)]
        [TestCase(UtilityScoreEvaluator.MaximumFactorCount + 1)]
        public void TryEvaluate_InvalidFactorCount_ReturnsExplicitFailure(int count)
        {
            var factors = Enumerable.Range(1, count).Select(id => Factor(id, 0.5d, 1d)).ToArray();
            AssertFailure(new[] { Candidate(1, factors) }, UtilityScoreError.InvalidFactorCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryEvaluate_InvalidFactorIdentifier_ReturnsExplicitFailure(int identifier)
        {
            AssertFailure(new[] { Candidate(1, Factor(identifier, 0.5d, 1d)) }, UtilityScoreError.InvalidFactorIdentifier);
        }

        [Test]
        public void TryEvaluate_DuplicateFactorIdentifierWithinCandidate_ReturnsExplicitFailure()
        {
            AssertFailure(new[] { Candidate(1, Factor(2, 0.2d, 1d), Factor(2, 0.8d, 1d)) }, UtilityScoreError.DuplicateFactorIdentifier);
        }

        [Test]
        public void TryEvaluate_SameFactorIdentifierAcrossCandidates_IsAccepted()
        {
            var evaluation = Evaluate(Candidate(1, Factor(2, 0.2d, 1d)), Candidate(2, Factor(2, 0.8d, 1d)));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(2));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.0001d)]
        [TestCase(1.0001d)]
        public void TryEvaluate_InvalidUtility_ReturnsExplicitFailure(double utility)
        {
            AssertFailure(new[] { Candidate(1, Factor(1, utility, 1d)) }, UtilityScoreError.InvalidUtility);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        [TestCase(0d)]
        [TestCase(UtilityScoreEvaluator.MaximumWeight + 1d)]
        public void TryEvaluate_InvalidWeight_ReturnsExplicitFailure(double weight)
        {
            AssertFailure(new[] { Candidate(1, Factor(1, 0.5d, weight)) }, UtilityScoreError.InvalidWeight);
        }

        [Test]
        public void TryEvaluate_BoundaryUtilityAndMaximumWeight_AreAccepted()
        {
            var evaluation = Evaluate(Candidate(1, Factor(1, 0d, double.Epsilon), Factor(2, 1d, UtilityScoreEvaluator.MaximumWeight)));
            Assert.That(evaluation.SelectedScore, Is.InRange(0d, 1d));
        }

        [Test]
        public void TryEvaluate_WeightedMean_IsExactFromInputOrder()
        {
            var evaluation = Evaluate(Candidate(10, Factor(1, 0.8d, 3d), Factor(2, 0.2d, 1d)));
            Assert.That(evaluation.SelectedScore, Is.EqualTo(0.65d).Within(1e-12d));
            AssertCandidate(evaluation, 0, 10, 4d, 0.65d);
        }

        [Test]
        public void TryEvaluate_HighestScoreCandidate_IsSelected()
        {
            var evaluation = Evaluate(
                Candidate(10, Factor(1, 0.3d, 1d)),
                Candidate(20, Factor(1, 0.9d, 1d)),
                Candidate(30, Factor(1, 0.6d, 1d)));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(20));
            Assert.That(evaluation.SelectedInputIndex, Is.EqualTo(1));
            Assert.That(evaluation.SelectedScore, Is.EqualTo(0.9d));
        }

        [Test]
        public void TryEvaluate_ExactTie_KeepsFirstInput()
        {
            var evaluation = Evaluate(Candidate(20, Factor(1, 0.75d, 2d)), Candidate(10, Factor(1, 0.75d, 2d)));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(20));
            Assert.That(evaluation.SelectedInputIndex, Is.Zero);
        }

        [Test]
        public void TryEvaluate_WeightChangesSelectionWithoutChangingUtilities()
        {
            var evaluation = Evaluate(
                Candidate(1, Factor(1, 1d, 1d), Factor(2, 0d, 3d)),
                Candidate(2, Factor(1, 1d, 3d), Factor(2, 0d, 1d)));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(2));
            Assert.That(evaluation.SelectedScore, Is.EqualTo(0.75d));
        }

        [Test]
        public void Evaluation_PreservesCandidateInputOrder()
        {
            var evaluation = Evaluate(Candidate(30, Factor(1, 0.3d, 1d)), Candidate(10, Factor(1, 0.9d, 1d)), Candidate(20, Factor(1, 0.6d, 1d)));
            AssertCandidate(evaluation, 0, 30, 1d, 0.3d);
            AssertCandidate(evaluation, 1, 10, 1d, 0.9d);
            AssertCandidate(evaluation, 2, 20, 1d, 0.6d);
        }

        [Test]
        public void CandidateLine_PreservesFactorInputOrderAndContribution()
        {
            var evaluation = Evaluate(Candidate(1, Factor(30, 0.2d, 4d), Factor(10, 0.5d, 2d), Factor(20, 1d, 1d)));
            evaluation.TryGetCandidateLine(0, out var candidate);
            AssertFactor(candidate, 0, 30, 0.2d, 4d, 0.8d);
            AssertFactor(candidate, 1, 10, 0.5d, 2d, 1d);
            AssertFactor(candidate, 2, 20, 1d, 1d, 1d);
        }

        [Test]
        public void Evaluation_ContainsEveryCandidateAndEveryFactor()
        {
            var evaluation = Evaluate(
                Candidate(1, Factor(1, 0.1d, 1d), Factor(2, 0.2d, 2d)),
                Candidate(2, Factor(1, 0.3d, 3d), Factor(2, 0.4d, 4d), Factor(3, 0.5d, 5d)));
            Assert.That(evaluation.CandidateCount, Is.EqualTo(2));
            evaluation.TryGetCandidateLine(0, out var first);
            evaluation.TryGetCandidateLine(1, out var second);
            Assert.That(first.FactorCount, Is.EqualTo(2));
            Assert.That(second.FactorCount, Is.EqualTo(3));
        }

        [Test]
        public void TryEvaluate_MaximumCandidates_IsAccepted()
        {
            var candidates = Enumerable.Range(1, UtilityScoreEvaluator.MaximumCandidateCount).Select(id => Candidate(id, Factor(1, id / 32d, 1d))).ToArray();
            var evaluation = Evaluate(candidates);
            Assert.That(evaluation.CandidateCount, Is.EqualTo(UtilityScoreEvaluator.MaximumCandidateCount));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(UtilityScoreEvaluator.MaximumCandidateCount));
        }

        [Test]
        public void TryEvaluate_MaximumFactors_IsAccepted()
        {
            var factors = Enumerable.Range(1, UtilityScoreEvaluator.MaximumFactorCount).Select(id => Factor(id, id / 16d, id)).ToArray();
            var evaluation = Evaluate(Candidate(1, factors));
            Assert.That(evaluation.TryGetCandidateLine(0, out var line), Is.True);
            Assert.That(line.FactorCount, Is.EqualTo(UtilityScoreEvaluator.MaximumFactorCount));
        }

        [Test]
        public void CandidateConstructor_CopiesFactorArray()
        {
            var factors = new[] { Factor(1, 0.8d, 1d) };
            var candidate = Candidate(1, factors);
            factors[0] = Factor(9, 0d, 1d);
            Assert.That(candidate.TryGetFactor(0, out var factor), Is.True);
            Assert.That(factor.Identifier, Is.EqualTo(1));
            Assert.That(factor.Utility, Is.EqualTo(0.8d));
        }

        [Test]
        public void Evaluation_RemainsImmutableAfterInputArrayMutation()
        {
            var candidates = new[] { Candidate(1, Factor(1, 0.8d, 1d)), Candidate(2, Factor(1, 0.2d, 1d)) };
            var evaluation = Evaluate(candidates);
            candidates[0] = Candidate(99, Factor(9, 0d, 1d));
            Assert.That(evaluation.SelectedCandidateIdentifier, Is.EqualTo(1));
            AssertCandidate(evaluation, 0, 1, 1d, 0.8d);
        }

        [Test]
        public void DefaultCandidate_HasNoFactorsAndFailsExplicitly()
        {
            AssertFailure(new[] { new UtilityScoreCandidate(1, null) }, UtilityScoreError.InvalidFactorCount);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetCandidateLine_InvalidIndex_ReturnsFalse(int index)
        {
            var evaluation = Evaluate(Candidate(1, Factor(1, 0.5d, 1d)));
            Assert.That(evaluation.TryGetCandidateLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(UtilityScoreCandidateLine)));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetFactorLine_InvalidIndex_ReturnsFalse(int index)
        {
            var evaluation = Evaluate(Candidate(1, Factor(1, 0.5d, 1d)));
            evaluation.TryGetCandidateLine(0, out var candidate);
            Assert.That(candidate.TryGetFactorLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(UtilityScoreFactorLine)));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TryGetFactor_InvalidIndex_ReturnsFalse(int index)
        {
            var candidate = Candidate(1, Factor(1, 0.5d, 1d));
            Assert.That(candidate.TryGetFactor(index, out var factor), Is.False);
            Assert.That(factor, Is.EqualTo(default(UtilityScoreFactor)));
        }

        [Test]
        public void SameInput_ReturnsBitStableScoresAndContributions()
        {
            var candidates = new[] { Candidate(1, Factor(1, 0.3d, 0.7d), Factor(2, 0.8d, 1.1d)) };
            var first = Evaluate(candidates);
            var second = Evaluate(candidates);
            first.TryGetCandidateLine(0, out var firstCandidate);
            second.TryGetCandidateLine(0, out var secondCandidate);
            firstCandidate.TryGetFactorLine(1, out var firstFactor);
            secondCandidate.TryGetFactorLine(1, out var secondFactor);
            Assert.That(BitConverter.DoubleToInt64Bits(first.SelectedScore), Is.EqualTo(BitConverter.DoubleToInt64Bits(second.SelectedScore)));
            Assert.That(BitConverter.DoubleToInt64Bits(firstFactor.WeightedUtility), Is.EqualTo(BitConverter.DoubleToInt64Bits(secondFactor.WeightedUtility)));
        }

        [Test]
        public void EveryValidScore_RemainsWithinNormalizedRange()
        {
            var evaluation = Evaluate(
                Candidate(1, Factor(1, 0d, 1d), Factor(2, 1d, 1d)),
                Candidate(2, Factor(1, 0.2d, 5d), Factor(2, 0.9d, 2d)));
            for (var index = 0; index < evaluation.CandidateCount; index++)
            {
                evaluation.TryGetCandidateLine(index, out var line);
                Assert.That(line.Score, Is.InRange(0d, 1d));
            }
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlySevenTypes()
        {
            var actual = typeof(UtilityScoreEvaluator).Assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            var expected = new[]
            {
                typeof(UtilityScoreFactor),
                typeof(UtilityScoreCandidate),
                typeof(UtilityScoreError),
                typeof(UtilityScoreFactorLine),
                typeof(UtilityScoreCandidateLine),
                typeof(UtilityScoreEvaluation),
                typeof(UtilityScoreEvaluator)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static UtilityScoreFactor Factor(int identifier, double utility, double weight) => new UtilityScoreFactor(identifier, utility, weight);

        private static UtilityScoreCandidate Candidate(int identifier, params UtilityScoreFactor[] factors) => new UtilityScoreCandidate(identifier, factors);

        private static UtilityScoreEvaluation Evaluate(params UtilityScoreCandidate[] candidates)
        {
            Assert.That(UtilityScoreEvaluator.TryEvaluate(candidates, out var evaluation, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(UtilityScoreError.None));
            Assert.That(evaluation, Is.Not.Null);
            return evaluation;
        }

        private static void AssertFailure(UtilityScoreCandidate[] candidates, UtilityScoreError expected)
        {
            Assert.That(UtilityScoreEvaluator.TryEvaluate(candidates, out var evaluation, out var error), Is.False);
            Assert.That(evaluation, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertCandidate(UtilityScoreEvaluation evaluation, int index, int identifier, double totalWeight, double score)
        {
            Assert.That(evaluation.TryGetCandidateLine(index, out var line), Is.True);
            Assert.That(line.CandidateIdentifier, Is.EqualTo(identifier));
            Assert.That(line.InputIndex, Is.EqualTo(index));
            Assert.That(line.TotalWeight, Is.EqualTo(totalWeight).Within(1e-12d));
            Assert.That(line.Score, Is.EqualTo(score).Within(1e-12d));
        }

        private static void AssertFactor(UtilityScoreCandidateLine candidate, int index, int identifier, double utility, double weight, double contribution)
        {
            Assert.That(candidate.TryGetFactorLine(index, out var line), Is.True);
            Assert.That(line.FactorIdentifier, Is.EqualTo(identifier));
            Assert.That(line.Utility, Is.EqualTo(utility));
            Assert.That(line.Weight, Is.EqualTo(weight));
            Assert.That(line.WeightedUtility, Is.EqualTo(contribution).Within(1e-12d));
        }
    }
}
