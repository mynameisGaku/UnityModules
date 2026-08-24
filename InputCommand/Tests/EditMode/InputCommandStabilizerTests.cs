using System;
using System.Linq;
using NUnit.Framework;

namespace InputStabilization.Tests
{
    public sealed class InputCommandStabilizerTests
    {
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(InputCommandStabilizer.MaximumRequiredSampleCount + 1)]
        public void TryCreate_InvalidRequiredSampleCount_Fails(int required)
        {
            Assert.That(InputCommandStabilizer.TryCreate(required, 0, out var stabilizer, out var error), Is.False);
            Assert.That(stabilizer, Is.Null);
            Assert.That(error, Is.EqualTo(InputStabilizationError.InvalidRequiredSampleCount));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(InputCommandStabilizer.MaximumRequiredSampleCount)]
        public void TryCreate_ValidBoundary_CapturesInitialCommand(int required)
        {
            Assert.That(InputCommandStabilizer.TryCreate(required, -7, out var stabilizer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputStabilizationError.None));
            Assert.That(stabilizer.RequiredConsecutiveSamples, Is.EqualTo(required));
            Assert.That(stabilizer.CurrentCommand, Is.EqualTo(-7));
            Assert.That(stabilizer.CandidateCommand, Is.EqualTo(-7));
            Assert.That(stabilizer.CandidateSampleCount, Is.Zero);
            Assert.That(stabilizer.HasPendingCandidate, Is.False);
        }

        [Test]
        public void Push_FirstDifferentCommand_StartsPendingCandidate()
        {
            var stabilizer = Create(3, 0);

            var status = stabilizer.Push(4);

            Assert.That(status.CurrentCommand, Is.Zero);
            Assert.That(status.CandidateCommand, Is.EqualTo(4));
            Assert.That(status.CandidateSampleCount, Is.EqualTo(1));
            Assert.That(status.RequiredConsecutiveSamples, Is.EqualTo(3));
            Assert.That(status.Changed, Is.False);
            Assert.That(status.IsPending, Is.True);
        }

        [Test]
        public void Push_ThirdConsecutiveSample_CommitsAndClearsCandidate()
        {
            var stabilizer = Create(3, 0);

            stabilizer.Push(4);
            var second = stabilizer.Push(4);
            var third = stabilizer.Push(4);

            Assert.That(second.CurrentCommand, Is.Zero);
            Assert.That(second.CandidateSampleCount, Is.EqualTo(2));
            Assert.That(third.CurrentCommand, Is.EqualTo(4));
            Assert.That(third.CandidateCommand, Is.EqualTo(4));
            Assert.That(third.CandidateSampleCount, Is.Zero);
            Assert.That(third.Changed, Is.True);
            Assert.That(third.IsPending, Is.False);
        }

        [Test]
        public void Push_DifferentCandidate_RestartsConsecutiveCount()
        {
            var stabilizer = Create(3, 0);
            stabilizer.Push(4);
            stabilizer.Push(4);

            var status = stabilizer.Push(-4);

            Assert.That(status.CurrentCommand, Is.Zero);
            Assert.That(status.CandidateCommand, Is.EqualTo(-4));
            Assert.That(status.CandidateSampleCount, Is.EqualTo(1));
            Assert.That(status.Changed, Is.False);
        }

        [Test]
        public void Push_CurrentCommand_CancelsPendingCandidate()
        {
            var stabilizer = Create(3, 4);
            stabilizer.Push(-4);
            stabilizer.Push(-4);

            var status = stabilizer.Push(4);

            Assert.That(status.CurrentCommand, Is.EqualTo(4));
            Assert.That(status.CandidateCommand, Is.EqualTo(4));
            Assert.That(status.CandidateSampleCount, Is.Zero);
            Assert.That(status.Changed, Is.False);
            Assert.That(status.IsPending, Is.False);
        }

        [Test]
        public void Push_RequiredOne_CommitsImmediately()
        {
            var stabilizer = Create(1, 0);

            var status = stabilizer.Push(-8);

            Assert.That(status.CurrentCommand, Is.EqualTo(-8));
            Assert.That(status.Changed, Is.True);
            Assert.That(status.IsPending, Is.False);
        }

        [TestCase((short)4)]
        [TestCase((short)-4)]
        [TestCase(short.MaxValue)]
        [TestCase(short.MinValue)]
        public void Push_AllSignedShortCommands_CommitSymmetrically(short command)
        {
            var stabilizer = Create(2, 0);

            stabilizer.Push(command);
            var status = stabilizer.Push(command);

            Assert.That(status.CurrentCommand, Is.EqualTo(command));
            Assert.That(status.Changed, Is.True);
        }

        [Test]
        public void Push_ZeroFromNonZero_UsesSameConsecutiveRule()
        {
            var stabilizer = Create(2, 4);

            Assert.That(stabilizer.Push(0).CurrentCommand, Is.EqualTo(4));
            Assert.That(stabilizer.Push(0).CurrentCommand, Is.Zero);
        }

        [Test]
        public void Push_AfterCommitSameValue_DoesNotReportSecondChange()
        {
            var stabilizer = Create(2, 0);
            stabilizer.Push(4);
            Assert.That(stabilizer.Push(4).Changed, Is.True);

            var stable = stabilizer.Push(4);

            Assert.That(stable.Changed, Is.False);
            Assert.That(stable.IsPending, Is.False);
        }

        [Test]
        public void Reset_ReplacesCurrentAndClearsPending()
        {
            var stabilizer = Create(3, 0);
            stabilizer.Push(4);
            stabilizer.Push(4);

            stabilizer.Reset(-2);

            Assert.That(stabilizer.CurrentCommand, Is.EqualTo(-2));
            Assert.That(stabilizer.CandidateCommand, Is.EqualTo(-2));
            Assert.That(stabilizer.CandidateSampleCount, Is.Zero);
            Assert.That(stabilizer.HasPendingCandidate, Is.False);
        }

        [Test]
        public void Snapshot_DoesNotAdvancePendingCandidate()
        {
            var stabilizer = Create(3, 0);
            var pushed = stabilizer.Push(4);

            var snapshot = stabilizer.Snapshot();

            Assert.That(snapshot, Is.EqualTo(pushed));
            Assert.That(snapshot.Changed, Is.False);
            Assert.That(stabilizer.CandidateSampleCount, Is.EqualTo(1));
        }

        [Test]
        public void Instances_DoNotShareCandidateState()
        {
            var first = Create(2, 0);
            var second = Create(2, 0);

            first.Push(4);
            first.Push(4);

            Assert.That(first.CurrentCommand, Is.EqualTo(4));
            Assert.That(second.CurrentCommand, Is.Zero);
            Assert.That(second.HasPendingCandidate, Is.False);
        }

        [Test]
        public void Status_EqualityAndHash_UseAllFields()
        {
            var first = Create(3, 0);
            var same = Create(3, 0);
            var other = Create(3, 0);
            var firstStatus = first.Push(4);
            var sameStatus = same.Push(4);
            var otherStatus = other.Push(-4);

            Assert.That(firstStatus == sameStatus, Is.True);
            Assert.That(firstStatus != otherStatus, Is.True);
            Assert.That(firstStatus.GetHashCode(), Is.EqualTo(sameStatus.GetHashCode()));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyThreeTypes()
        {
            var exported = typeof(InputCommandStabilizer).Assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                typeof(InputCommandStabilizer),
                typeof(InputCommandStatus),
                typeof(InputStabilizationError)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal)));
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceUnityEngine()
        {
            var references = typeof(InputCommandStabilizer).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();

            Assert.That(references, Has.None.StartsWith("UnityEngine"));
        }

        private static InputCommandStabilizer Create(int required, short initial)
        {
            Assert.That(InputCommandStabilizer.TryCreate(required, initial, out var stabilizer, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputStabilizationError.None));
            return stabilizer;
        }
    }
}
