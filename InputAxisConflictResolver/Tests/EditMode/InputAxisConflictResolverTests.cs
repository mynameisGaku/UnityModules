using NUnit.Framework;

namespace InputAxisConflict.Tests
{
    [TestFixture]
    public sealed class InputAxisConflictResolverTests
    {
        [Test]
        public void TryCreate_InvalidPolicy_ReturnsError()
        {
            Assert.That(InputAxisConflictResolver.TryCreate((InputAxisConflictPolicy)99, 10, out var resolver, out var error), Is.False);
            Assert.That(resolver, Is.Null);
            Assert.That(error, Is.EqualTo(InputAxisConflictError.InvalidPolicy));
        }

        [TestCase(InputAxisConflictPolicy.Neutral)]
        [TestCase(InputAxisConflictPolicy.NegativeWins)]
        [TestCase(InputAxisConflictPolicy.PositiveWins)]
        [TestCase(InputAxisConflictPolicy.LastPressedWins)]
        public void TryCreate_DefinedPolicy_StartsNeutral(InputAxisConflictPolicy policy)
        {
            AssertStatus(Create(policy, 100).Snapshot(), 100, false, false, 0, false);
        }

        [Test]
        public void TrySample_NegativeOnly_ResolvesNegative()
        {
            var resolver = Create(InputAxisConflictPolicy.Neutral, 100);
            resolver.TrySample(100, true, false, out var status, out _);
            AssertStatus(status, 100, true, false, -1, true);
            Assert.That(status.NegativePressedThisSample, Is.True);
        }

        [Test]
        public void TrySample_PositiveOnly_ResolvesPositive()
        {
            var resolver = Create(InputAxisConflictPolicy.Neutral, 100);
            resolver.TrySample(100, false, true, out var status, out _);
            AssertStatus(status, 100, false, true, 1, true);
            Assert.That(status.PositivePressedThisSample, Is.True);
        }

        [TestCase(InputAxisConflictPolicy.Neutral, 0)]
        [TestCase(InputAxisConflictPolicy.NegativeWins, -1)]
        [TestCase(InputAxisConflictPolicy.PositiveWins, 1)]
        public void TrySample_SimultaneousPress_UsesFixedPolicy(InputAxisConflictPolicy policy, int expected)
        {
            var resolver = Create(policy, 100);
            resolver.TrySample(100, true, true, out var status, out _);
            AssertStatus(status, 100, true, true, expected, expected != 0);
            Assert.That(status.HasConflict, Is.True);
        }

        [Test]
        public void TrySample_LastPressedWins_PositiveEdgeWins()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, true, false, out _, out _);
            resolver.TrySample(101, true, true, out var status, out _);
            AssertStatus(status, 101, true, true, 1, true);
        }

        [Test]
        public void TrySample_LastPressedWins_NegativeEdgeWins()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, false, true, out _, out _);
            resolver.TrySample(101, true, true, out var status, out _);
            AssertStatus(status, 101, true, true, -1, true);
        }

        [Test]
        public void TrySample_LastPressedWins_SameTickEdgesTieToNeutral()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, true, true, out var status, out _);
            AssertStatus(status, 100, true, true, 0, false);
        }

        [Test]
        public void TrySample_ReleaseWinner_FallsBackToHeldSide()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, true, false, out _, out _);
            resolver.TrySample(101, true, true, out _, out _);
            resolver.TrySample(102, true, false, out var status, out _);
            AssertStatus(status, 102, true, false, -1, true);
            Assert.That(status.PositiveReleasedThisSample, Is.True);
        }

        [Test]
        public void TrySample_ReleaseBoth_ReturnsNeutral()
        {
            var resolver = Create(InputAxisConflictPolicy.PositiveWins, 100);
            resolver.TrySample(100, true, true, out _, out _);
            resolver.TrySample(101, false, false, out var status, out _);
            AssertStatus(status, 101, false, false, 0, true);
            Assert.That(status.NegativeReleasedThisSample, Is.True);
            Assert.That(status.PositiveReleasedThisSample, Is.True);
        }

        [Test]
        public void TrySample_HeldSnapshot_DoesNotRepeatEdgesOrChange()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, true, false, out _, out _);
            resolver.TrySample(101, true, false, out var status, out _);
            AssertStatus(status, 101, true, false, -1, false);
            Assert.That(status.NegativePressedThisSample, Is.False);
        }

        [Test]
        public void TrySample_BackwardTick_IsMutationFree()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(102, true, false, out _, out _);
            Assert.That(resolver.TrySample(101, false, true, out var status, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InputAxisConflictError.TickMovedBackward));
            AssertStatus(status, 102, true, false, -1, false);
        }

        [Test]
        public void Snapshot_ClearsEdgeAndChangeFlags()
        {
            var resolver = Create(InputAxisConflictPolicy.PositiveWins, 100);
            resolver.TrySample(100, true, true, out _, out _);
            var status = resolver.Snapshot();
            AssertStatus(status, 100, true, true, 1, false);
            Assert.That(status.NegativePressedThisSample || status.PositivePressedThisSample || status.NegativeReleasedThisSample || status.PositiveReleasedThisSample, Is.False);
        }

        [Test]
        public void Reset_ClearsInputsAndAllowsNewTimeline()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(101, true, true, out _, out _);
            resolver.Reset(7);
            AssertStatus(resolver.Snapshot(), 7, false, false, 0, false);
            Assert.That(resolver.TrySample(7, false, true, out var status, out _), Is.True);
            Assert.That(status.ResolvedValue, Is.EqualTo(1));
        }

        [Test]
        public void FiveStepScenario_ResolvesLastPressedAndTie()
        {
            var resolver = Create(InputAxisConflictPolicy.LastPressedWins, 100);
            resolver.TrySample(100, true, false, out var negative, out _);
            resolver.TrySample(101, true, true, out var positiveWins, out _);
            resolver.TrySample(102, true, false, out var fallback, out _);
            resolver.TrySample(103, false, false, out var released, out _);
            resolver.TrySample(104, true, true, out var tie, out _);
            Assert.That(negative.ResolvedValue, Is.EqualTo(-1));
            Assert.That(positiveWins.ResolvedValue, Is.EqualTo(1));
            Assert.That(fallback.ResolvedValue, Is.EqualTo(-1));
            Assert.That(released.ResolvedValue, Is.Zero);
            Assert.That(tie.ResolvedValue, Is.Zero);
            Assert.That(tie.HasConflict, Is.True);
        }

        private static InputAxisConflictResolver Create(InputAxisConflictPolicy policy, ulong tick)
        {
            Assert.That(InputAxisConflictResolver.TryCreate(policy, tick, out var resolver, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputAxisConflictError.None));
            return resolver;
        }

        private static void AssertStatus(InputAxisConflictStatus status, ulong tick, bool negative, bool positive, int value, bool changed)
        {
            Assert.That(status.CurrentTick, Is.EqualTo(tick));
            Assert.That(status.NegativePressed, Is.EqualTo(negative));
            Assert.That(status.PositivePressed, Is.EqualTo(positive));
            Assert.That(status.ResolvedValue, Is.EqualTo(value));
            Assert.That(status.ResolutionChanged, Is.EqualTo(changed));
        }
    }
}
