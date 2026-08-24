using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayDecision.Tests
{
    public sealed class StableScoreSelectorTests
    {
        [Test]
        public void TrySelect_NullCandidates_ReturnsExplicitError()
        {
            AssertFailure(null, 0, 0.1d, StableScoreError.NullCandidates);
        }

        [Test]
        public void TrySelect_EmptyCandidates_ReturnsExplicitError()
        {
            AssertFailure(Array.Empty<StableScoreCandidate>(), 0, 0.1d, StableScoreError.InvalidCandidateCount);
        }

        [Test]
        public void TrySelect_TooManyCandidates_ReturnsExplicitError()
        {
            AssertFailure(new StableScoreCandidate[StableScoreSelector.MaximumCandidateCount + 1], 0, 0.1d, StableScoreError.InvalidCandidateCount);
        }

        [Test]
        public void TrySelect_NegativeCurrentIdentifier_ReturnsExplicitError()
        {
            AssertFailure(Candidates((1, 0.5d)), -1, 0.1d, StableScoreError.InvalidCurrentIdentifier);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.0001d)]
        [TestCase(1.0001d)]
        public void TrySelect_InvalidMinimumAdvantage_ReturnsExplicitError(double value)
        {
            AssertFailure(Candidates((1, 0.5d)), 0, value, StableScoreError.InvalidMinimumAdvantage);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TrySelect_InvalidCandidateIdentifier_ReturnsExplicitError(int identifier)
        {
            AssertFailure(Candidates((1, 0.5d), (identifier, 0.6d)), 0, 0.1d, StableScoreError.InvalidCandidateIdentifier);
        }

        [Test]
        public void TrySelect_DuplicateCandidateIdentifier_ReturnsExplicitError()
        {
            AssertFailure(Candidates((7, 0.5d), (7, 0.6d)), 0, 0.1d, StableScoreError.DuplicateCandidateIdentifier);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.0001d)]
        [TestCase(1.0001d)]
        public void TrySelect_InvalidScore_ReturnsExplicitError(double score)
        {
            AssertFailure(Candidates((1, score)), 0, 0.1d, StableScoreError.InvalidScore);
        }

        [Test]
        public void TrySelect_ValidationPrecedenceChecksCurrentBeforeCandidate()
        {
            AssertFailure(Candidates((0, double.NaN)), -1, 0.1d, StableScoreError.InvalidCurrentIdentifier);
        }

        [Test]
        public void TrySelect_ValidationPrecedenceChecksMarginBeforeCandidate()
        {
            AssertFailure(Candidates((0, double.NaN)), 0, double.NaN, StableScoreError.InvalidMinimumAdvantage);
        }

        [Test]
        public void TrySelect_ZeroAndOneBoundariesAreAccepted()
        {
            Assert.That(StableScoreSelector.TrySelect(Candidates((1, 0d), (2, 1d)), 1, 1d, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(StableScoreError.None));
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(2));
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.SwitchedByMinimumAdvantage));
        }

        [Test]
        public void TrySelect_WithoutCurrent_SelectsHighestScore()
        {
            var result = Select(Candidates((10, 0.35d), (20, 0.9d), (30, 0.6d)), 0, 0.2d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(20));
            Assert.That(result.SelectedInputIndex, Is.EqualTo(1));
            Assert.That(result.SelectedScore, Is.EqualTo(0.9d));
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.SelectedWithoutCurrent));
            Assert.That(result.CurrentWasAvailable, Is.False);
            Assert.That(result.ChangedFromRequestedCurrent, Is.False);
        }

        [Test]
        public void TrySelect_WithoutCurrent_EqualScoresUseFirstInput()
        {
            var result = Select(Candidates((30, 0.8d), (20, 0.8d), (10, 0.8d)), 0, 0d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(30));
            Assert.That(result.BestCandidateIdentifier, Is.EqualTo(30));
        }

        [Test]
        public void TrySelect_MissingCurrent_ReplacesWithBestCandidate()
        {
            var result = Select(Candidates((1, 0.4d), (2, 0.7d)), 99, 0.5d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(2));
            Assert.That(result.RequestedCurrentIdentifier, Is.EqualTo(99));
            Assert.That(result.CurrentWasAvailable, Is.False);
            Assert.That(result.CurrentInputIndex, Is.EqualTo(-1));
            Assert.That(result.CurrentScore, Is.Zero);
            Assert.That(result.ChangedFromRequestedCurrent, Is.True);
            Assert.That(result.SwitchedFromAvailableCurrent, Is.False);
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.ReplacedMissingCurrent));
        }

        [Test]
        public void TrySelect_OnlyCurrentCandidate_KeepsCurrent()
        {
            var result = Select(Candidates((7, 0.2d)), 7, 1d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(7));
            Assert.That(result.HasChallenger, Is.False);
            Assert.That(result.ChallengerCandidateIdentifier, Is.Zero);
            Assert.That(result.ChallengerInputIndex, Is.EqualTo(-1));
            Assert.That(result.ChallengerScore, Is.Zero);
            Assert.That(result.ChallengerAdvantage, Is.Zero);
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.KeptOnlyCurrent));
        }

        [Test]
        public void TrySelect_LowerChallenger_KeepsCurrent()
        {
            var result = Select(Candidates((1, 0.8d), (2, 0.4d)), 1, 0d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(1));
            Assert.That(result.ChallengerCandidateIdentifier, Is.EqualTo(2));
            Assert.That(result.ChallengerAdvantage, Is.EqualTo(-0.4d).Within(1e-12d));
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.KeptCurrentTieOrLower));
        }

        [Test]
        public void TrySelect_EqualChallenger_KeepsCurrentEvenAtZeroMargin()
        {
            var result = Select(Candidates((10, 0.75d), (20, 0.75d)), 20, 0d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(20));
            Assert.That(result.BestCandidateIdentifier, Is.EqualTo(10));
            Assert.That(result.ChallengerCandidateIdentifier, Is.EqualTo(10));
            Assert.That(result.ChallengerAdvantage, Is.Zero);
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.KeptCurrentTieOrLower));
        }

        [Test]
        public void TrySelect_PositiveButInsufficientAdvantage_KeepsCurrent()
        {
            var result = Select(Candidates((1, 0.62d), (2, 0.68d)), 1, 0.1d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(1));
            Assert.That(result.ChallengerAdvantage, Is.EqualTo(0.06d).Within(1e-12d));
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.KeptCurrentBelowMinimumAdvantage));
        }

        [Test]
        public void TrySelect_ExactMinimumAdvantage_Switches()
        {
            var result = Select(Candidates((1, 0.5d), (2, 0.75d)), 1, 0.25d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(2));
            Assert.That(result.ChallengerAdvantage, Is.EqualTo(0.25d));
            Assert.That(result.ChangedFromRequestedCurrent, Is.True);
            Assert.That(result.SwitchedFromAvailableCurrent, Is.True);
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.SwitchedByMinimumAdvantage));
        }

        [Test]
        public void TrySelect_ZeroMarginSwitchesOnAnyStrictlyHigherScore()
        {
            var result = Select(Candidates((1, 0.5d), (2, 0.5000001d)), 1, 0d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(2));
            Assert.That(result.Reason, Is.EqualTo(StableScoreDecisionReason.SwitchedByMinimumAdvantage));
        }

        [Test]
        public void TrySelect_BestChallengerUsesStableFirstTie()
        {
            var result = Select(Candidates((1, 0.2d), (20, 0.8d), (30, 0.8d)), 1, 0.5d);
            Assert.That(result.ChallengerCandidateIdentifier, Is.EqualTo(20));
            Assert.That(result.ChallengerInputIndex, Is.EqualTo(1));
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(20));
        }

        [Test]
        public void TrySelect_BestCandidateMayRemainCurrent()
        {
            var result = Select(Candidates((1, 0.9d), (2, 0.7d), (3, 0.8d)), 1, 0.1d);
            Assert.That(result.BestCandidateIdentifier, Is.EqualTo(1));
            Assert.That(result.ChallengerCandidateIdentifier, Is.EqualTo(3));
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(1));
        }

        [Test]
        public void Selection_ExposesRequestedCurrentBestAndThreshold()
        {
            var result = Select(Candidates((10, 0.25d), (20, 0.75d)), 10, 0.4d);
            Assert.That(result.RequestedCurrentIdentifier, Is.EqualTo(10));
            Assert.That(result.CurrentWasAvailable, Is.True);
            Assert.That(result.CurrentInputIndex, Is.Zero);
            Assert.That(result.CurrentScore, Is.EqualTo(0.25d));
            Assert.That(result.BestCandidateIdentifier, Is.EqualTo(20));
            Assert.That(result.BestCandidateInputIndex, Is.EqualTo(1));
            Assert.That(result.BestCandidateScore, Is.EqualTo(0.75d));
            Assert.That(result.MinimumAdvantage, Is.EqualTo(0.4d));
        }

        [Test]
        public void CandidateLinesPreserveInputOrderAndRoles()
        {
            var result = Select(Candidates((10, 0.4d), (20, 0.8d), (30, 0.7d)), 10, 0.3d);
            Assert.That(result.CandidateCount, Is.EqualTo(3));
            AssertLine(result, 0, 10, 0.4d, true, false, false);
            AssertLine(result, 1, 20, 0.8d, false, true, true);
            AssertLine(result, 2, 30, 0.7d, false, false, false);
        }

        [Test]
        public void CandidateLinesMarkKeptCurrentAndBestCurrent()
        {
            var result = Select(Candidates((10, 0.9d), (20, 0.8d)), 10, 0.5d);
            AssertLine(result, 0, 10, 0.9d, true, true, true);
            AssertLine(result, 1, 20, 0.8d, false, false, false);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TryGetCandidateLine_InvalidIndexReturnsFalse(int index)
        {
            var result = Select(Candidates((1, 0.1d), (2, 0.2d)), 0, 0d);
            Assert.That(result.TryGetCandidateLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(StableScoreCandidateLine)));
        }

        [Test]
        public void SelectionDoesNotChangeWhenInputArrayChanges()
        {
            var candidates = Candidates((1, 0.3d), (2, 0.9d));
            var result = Select(candidates, 1, 0.2d);
            candidates[0] = new StableScoreCandidate(99, 1d);
            candidates[1] = new StableScoreCandidate(100, 0d);
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(2));
            AssertLine(result, 0, 1, 0.3d, true, false, false);
            AssertLine(result, 1, 2, 0.9d, false, true, true);
        }

        [Test]
        public void MaximumCandidateCountIsAccepted()
        {
            var candidates = Enumerable.Range(1, StableScoreSelector.MaximumCandidateCount)
                .Select(index => new StableScoreCandidate(index, index / (double)StableScoreSelector.MaximumCandidateCount))
                .ToArray();
            var result = Select(candidates, 1, 0d);
            Assert.That(result.CandidateCount, Is.EqualTo(StableScoreSelector.MaximumCandidateCount));
            Assert.That(result.SelectedCandidateIdentifier, Is.EqualTo(StableScoreSelector.MaximumCandidateCount));
        }

        [Test]
        public void RepeatedSelectionIsDeterministic()
        {
            var candidates = Candidates((1, 0.45d), (2, 0.61d), (3, 0.61d));
            var first = Select(candidates, 1, 0.1d);
            var second = Select(candidates, 1, 0.1d);
            Assert.That(second.SelectedCandidateIdentifier, Is.EqualTo(first.SelectedCandidateIdentifier));
            Assert.That(second.ChallengerCandidateIdentifier, Is.EqualTo(first.ChallengerCandidateIdentifier));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            for (var index = 0; index < first.CandidateCount; index++)
            {
                Assert.That(first.TryGetCandidateLine(index, out var firstLine), Is.True);
                Assert.That(second.TryGetCandidateLine(index, out var secondLine), Is.True);
                Assert.That(secondLine.CandidateIdentifier, Is.EqualTo(firstLine.CandidateIdentifier));
                Assert.That(secondLine.IsSelected, Is.EqualTo(firstLine.IsSelected));
            }
        }

        [Test]
        public void RuntimeAssemblyExportsOnlyDocumentedPublicTypes()
        {
            var names = typeof(StableScoreSelector).Assembly.GetExportedTypes().Select(type => type.FullName).OrderBy(value => value).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "GameplayDecision.StableScoreCandidate",
                "GameplayDecision.StableScoreCandidateLine",
                "GameplayDecision.StableScoreDecisionReason",
                "GameplayDecision.StableScoreError",
                "GameplayDecision.StableScoreSelection",
                "GameplayDecision.StableScoreSelector"
            }, names);
        }

        private static StableScoreCandidate[] Candidates(params (int identifier, double score)[] values)
        {
            return values.Select(value => new StableScoreCandidate(value.identifier, value.score)).ToArray();
        }

        private static StableScoreSelection Select(StableScoreCandidate[] candidates, int currentIdentifier, double minimumAdvantage)
        {
            Assert.That(StableScoreSelector.TrySelect(candidates, currentIdentifier, minimumAdvantage, out var selection, out var error), Is.True);
            Assert.That(error, Is.EqualTo(StableScoreError.None));
            Assert.That(selection, Is.Not.Null);
            return selection;
        }

        private static void AssertFailure(StableScoreCandidate[] candidates, int currentIdentifier, double minimumAdvantage, StableScoreError expected)
        {
            Assert.That(StableScoreSelector.TrySelect(candidates, currentIdentifier, minimumAdvantage, out var selection, out var error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertLine(StableScoreSelection result, int index, int identifier, double score, bool current, bool best, bool selected)
        {
            Assert.That(result.TryGetCandidateLine(index, out var line), Is.True);
            Assert.That(line.CandidateIdentifier, Is.EqualTo(identifier));
            Assert.That(line.InputIndex, Is.EqualTo(index));
            Assert.That(line.Score, Is.EqualTo(score));
            Assert.That(line.IsCurrent, Is.EqualTo(current));
            Assert.That(line.IsBestCandidate, Is.EqualTo(best));
            Assert.That(line.IsSelected, Is.EqualTo(selected));
        }
    }
}
