using System.Collections.Generic;
using NUnit.Framework;

namespace InputArbitration.Tests
{
    public sealed class InputCommandArbiterTests
    {
        [Test]
        public void Select_NullCandidates_ReturnsExplicitError()
        {
            AssertFailure(InputCommandArbiter.Select(null), InputCommandArbitrationError.NullCandidates);
        }

        [Test]
        public void Select_EmptyCandidates_SucceedsWithoutSelection()
        {
            AssertNoSelection(InputCommandArbiter.Select(new InputCommandCandidate[0]), 0);
        }

        [Test]
        public void Select_AllIneligible_SucceedsWithoutSelection()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(1, int.MaxValue, false),
                new InputCommandCandidate(2, int.MinValue, false)
            };
            AssertNoSelection(InputCommandArbiter.Select(candidates), 0);
        }

        [Test]
        public void Select_OneEligible_ReturnsItsInputIdentity()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(10, 50, false),
                new InputCommandCandidate(20, -100, true)
            };
            AssertSelection(InputCommandArbiter.Select(candidates), 1, 20, -100, 1);
        }

        [Test]
        public void Select_HighestPriorityWinsRegardlessOfInputOrder()
        {
            var first = new[]
            {
                new InputCommandCandidate(1, 100, true),
                new InputCommandCandidate(2, 300, true),
                new InputCommandCandidate(3, 200, true)
            };
            var second = new[]
            {
                new InputCommandCandidate(3, 200, true),
                new InputCommandCandidate(1, 100, true),
                new InputCommandCandidate(2, 300, true)
            };
            AssertSelection(InputCommandArbiter.Select(first), 1, 2, 300, 3);
            AssertSelection(InputCommandArbiter.Select(second), 2, 2, 300, 3);
        }

        [Test]
        public void Select_EqualPriorityUsesFirstInputIndex()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(30, 100, true),
                new InputCommandCandidate(20, 100, true),
                new InputCommandCandidate(10, 100, true)
            };
            AssertSelection(InputCommandArbiter.Select(candidates), 0, 30, 100, 3);
        }

        [Test]
        public void Select_IneligibleHighPriorityCannotWin()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(1, int.MaxValue, false),
                new InputCommandCandidate(2, int.MinValue, true)
            };
            AssertSelection(InputCommandArbiter.Select(candidates), 1, 2, int.MinValue, 1);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Select_InvalidCommandIdFailsBeforeSelection(int invalidId)
        {
            var candidates = new[]
            {
                new InputCommandCandidate(1, 10, true),
                new InputCommandCandidate(invalidId, 20, false)
            };
            AssertFailure(InputCommandArbiter.Select(candidates), InputCommandArbitrationError.InvalidCommandId);
        }

        [Test]
        public void Select_DuplicateCommandIdFailsEvenWhenOneIsIneligible()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(7, 10, true),
                new InputCommandCandidate(7, 20, false)
            };
            AssertFailure(InputCommandArbiter.Select(candidates), InputCommandArbitrationError.DuplicateCommandId);
        }

        [Test]
        public void Select_TooManyCandidatesFailsBeforeReadingEntries()
        {
            var candidates = new InputCommandCandidate[InputCommandArbiter.MaximumCandidateCount + 1];
            AssertFailure(InputCommandArbiter.Select(candidates), InputCommandArbitrationError.TooManyCandidates);
        }

        [Test]
        public void Select_MaximumCandidateCountAcceptsBoundary()
        {
            var candidates = new InputCommandCandidate[InputCommandArbiter.MaximumCandidateCount];
            for (var index = 0; index < candidates.Length; index++) candidates[index] = new InputCommandCandidate(index + 1, index, index % 2 == 0);
            AssertSelection(InputCommandArbiter.Select(candidates), 62, 63, 62, 32);
        }

        [Test]
        public void Select_GoldenScenarioChoosesDodgeThenEarlierEqualPriority()
        {
            var candidates = new[]
            {
                new InputCommandCandidate(1, 100, true),
                new InputCommandCandidate(2, 200, true),
                new InputCommandCandidate(3, 300, true),
                new InputCommandCandidate(4, 300, true)
            };
            AssertSelection(InputCommandArbiter.Select(candidates), 2, 3, 300, 4);
        }

        [Test]
        public void CandidateEqualityIncludesEveryInputField()
        {
            var first = new InputCommandCandidate(7, 20, true);
            var second = new InputCommandCandidate(7, 20, true);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != new InputCommandCandidate(7, 20, false), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void ResultEqualityIncludesSelectionCountErrorAndPresence()
        {
            var candidates = new[] { new InputCommandCandidate(7, 20, true) };
            var first = InputCommandArbiter.Select(candidates);
            var second = InputCommandArbiter.Select(candidates);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != default(InputCommandArbitrationResult), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static void AssertSelection(InputCommandArbitrationResult result, int index, int commandId, int priority, int eligibleCount)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.HasSelection, Is.True);
            Assert.That(result.SelectedIndex, Is.EqualTo(index));
            Assert.That(result.CommandId, Is.EqualTo(commandId));
            Assert.That(result.Priority, Is.EqualTo(priority));
            Assert.That(result.EligibleCandidateCount, Is.EqualTo(eligibleCount));
            Assert.That(result.Error, Is.EqualTo(InputCommandArbitrationError.None));
        }

        private static void AssertNoSelection(InputCommandArbitrationResult result, int eligibleCount)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.HasSelection, Is.False);
            Assert.That(result.SelectedIndex, Is.EqualTo(-1));
            Assert.That(result.CommandId, Is.Zero);
            Assert.That(result.Priority, Is.Zero);
            Assert.That(result.EligibleCandidateCount, Is.EqualTo(eligibleCount));
            Assert.That(result.Error, Is.EqualTo(InputCommandArbitrationError.None));
        }

        private static void AssertFailure(InputCommandArbitrationResult result, InputCommandArbitrationError error)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.HasSelection, Is.False);
            Assert.That(result.SelectedIndex, Is.EqualTo(-1));
            Assert.That(result.CommandId, Is.Zero);
            Assert.That(result.Priority, Is.Zero);
            Assert.That(result.EligibleCandidateCount, Is.Zero);
            Assert.That(result.Error, Is.EqualTo(error));
        }
    }
}
