using NUnit.Framework;

namespace GameplayTiming.Tests
{
    [TestFixture]
    public sealed class ChargeCooldownTests
    {
        [TestCase(1, 1L)]
        [TestCase(32, long.MaxValue)]
        public void CreateRules_Boundaries_Succeed(int maximumCharges, long interval)
        {
            Assert.That(ChargeCooldown.TryCreateRules(maximumCharges, interval, out var rules, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            Assert.That(rules.MaximumCharges, Is.EqualTo(maximumCharges));
            Assert.That(rules.RechargeIntervalTicks, Is.EqualTo(interval));
        }

        [TestCase(0, ChargeCooldownError.InvalidMaximumCharges)]
        [TestCase(33, ChargeCooldownError.InvalidMaximumCharges)]
        public void CreateRules_InvalidMaximum_Fails(int maximumCharges, ChargeCooldownError expected)
        {
            Assert.That(ChargeCooldown.TryCreateRules(maximumCharges, 10, out var rules, out var error), Is.False);
            Assert.That(rules, Is.EqualTo(default(ChargeCooldownRules)));
            Assert.That(error, Is.EqualTo(expected));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void CreateRules_InvalidInterval_Fails(long interval)
        {
            Assert.That(ChargeCooldown.TryCreateRules(3, interval, out var rules, out var error), Is.False);
            Assert.That(rules, Is.EqualTo(default(ChargeCooldownRules)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.InvalidRechargeInterval));
        }

        [TestCase(3, 0L)]
        [TestCase(2, 110L)]
        [TestCase(0, 110L)]
        public void CreateState_InitialCharges_UsesCanonicalSchedule(int charges, long expectedNext)
        {
            var rules = Rules();
            Assert.That(ChargeCooldown.TryCreateState(rules, 100, charges, out var state, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            AssertState(state, charges, 100, expectedNext);
        }

        [Test]
        public void CreateState_InvalidTick_PrecedesInitialCharges()
        {
            Assert.That(ChargeCooldown.TryCreateState(Rules(), -1, 99, out var state, out var error), Is.False);
            Assert.That(state, Is.EqualTo(default(ChargeCooldownState)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.InvalidTick));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void CreateState_InvalidInitialCharges_Fails(int charges)
        {
            Assert.That(ChargeCooldown.TryCreateState(Rules(), 0, charges, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.InvalidInitialCharges));
        }

        [Test]
        public void CreateState_NextTickOverflow_FailsWithoutPartialState()
        {
            Assert.That(ChargeCooldown.TryCreateState(Rules(), long.MaxValue - 5, 2, out var state, out var error), Is.False);
            Assert.That(state, Is.EqualTo(default(ChargeCooldownState)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.TickOverflow));
        }

        [Test]
        public void RestoreState_ValidPartialState_RoundTrips()
        {
            Assert.That(ChargeCooldown.TryRestoreState(Rules(), 1, 115, 120, out var state, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            AssertState(state, 1, 115, 120);
        }

        [TestCase(3, 100L, 110L)]
        [TestCase(2, 100L, 0L)]
        [TestCase(2, 100L, 100L)]
        [TestCase(-1, 100L, 110L)]
        [TestCase(4, 100L, 110L)]
        public void RestoreState_InvalidShape_Fails(int charges, long last, long next)
        {
            Assert.That(ChargeCooldown.TryRestoreState(Rules(), charges, last, next, out var state, out var error), Is.False);
            Assert.That(state, Is.EqualTo(default(ChargeCooldownState)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.InvalidState));
        }

        [Test]
        public void Advance_FullState_OnlyMovesObservationTick()
        {
            var state = State(3, 100);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), 999, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            Assert.That(result.PreviousState, Is.EqualTo(state));
            AssertState(result.State, 3, 999, 0);
            Assert.That(result.ChargesRestored, Is.Zero);
            Assert.That(result.ChargeSpent, Is.False);
            Assert.That(result.IsReady, Is.True);
        }

        [Test]
        public void Advance_BeforeNextTick_DoesNotRestore()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), 109, out var result, out _), Is.True);
            AssertState(result.State, 0, 109, 110);
            Assert.That(result.ChargesRestored, Is.Zero);
        }

        [Test]
        public void Advance_ExactNextTick_RestoresOne()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), 110, out var result, out _), Is.True);
            AssertState(result.State, 1, 110, 120);
            Assert.That(result.ChargesRestored, Is.EqualTo(1));
        }

        [Test]
        public void Advance_TickJump_CatchesUpWithoutLoopState()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), 125, out var result, out _), Is.True);
            AssertState(result.State, 2, 125, 130);
            Assert.That(result.ChargesRestored, Is.EqualTo(2));
        }

        [Test]
        public void Advance_EnoughTicks_ClampsAtFullAndClearsSchedule()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), long.MaxValue, out var result, out _), Is.True);
            AssertState(result.State, 3, long.MaxValue, 0);
            Assert.That(result.ChargesRestored, Is.EqualTo(3));
        }

        [Test]
        public void Advance_BackwardTick_Fails()
        {
            Assert.That(ChargeCooldown.TryAdvance(State(2, 100), Rules(), 99, out var result, out var error), Is.False);
            Assert.That(result, Is.EqualTo(default(ChargeCooldownResult)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.TickMovedBackward));
        }

        [Test]
        public void Advance_DefaultState_IsInvalid()
        {
            Assert.That(ChargeCooldown.TryAdvance(default, Rules(), 0, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.InvalidState));
        }

        [Test]
        public void Advance_NextScheduleOverflow_FailsExplicitly()
        {
            Assert.That(ChargeCooldown.TryRestoreState(Rules(), 0, long.MaxValue - 1, long.MaxValue, out var state, out _), Is.True);
            Assert.That(ChargeCooldown.TryAdvance(state, Rules(), long.MaxValue, out var result, out var error), Is.False);
            Assert.That(result, Is.EqualTo(default(ChargeCooldownResult)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.TickOverflow));
        }

        [Test]
        public void Spend_FromFull_StartsRechargeSchedule()
        {
            var state = State(3, 100);
            Assert.That(ChargeCooldown.TrySpend(state, Rules(), 100, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            Assert.That(result.ChargeSpent, Is.True);
            AssertState(result.State, 2, 100, 110);
        }

        [Test]
        public void Spend_WhileRecharging_PreservesOldestSchedule()
        {
            var state = State(3, 100);
            Assert.That(ChargeCooldown.TrySpend(state, Rules(), 100, out var first, out _), Is.True);
            Assert.That(ChargeCooldown.TrySpend(first.State, Rules(), 105, out var second, out _), Is.True);
            AssertState(second.State, 1, 105, 110);
        }

        [Test]
        public void Spend_WhenEmpty_ReturnsSuccessWithoutSpending()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TrySpend(state, Rules(), 109, out var result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ChargeCooldownError.None));
            Assert.That(result.ChargeSpent, Is.False);
            Assert.That(result.IsReady, Is.False);
            AssertState(result.State, 0, 109, 110);
        }

        [Test]
        public void Spend_OnRechargeTick_RestoresThenConsumes()
        {
            var state = State(0, 100);
            Assert.That(ChargeCooldown.TrySpend(state, Rules(), 120, out var result, out _), Is.True);
            Assert.That(result.ChargesRestored, Is.EqualTo(2));
            Assert.That(result.ChargeSpent, Is.True);
            AssertState(result.State, 1, 120, 130);
        }

        [Test]
        public void Spend_FullAtMaximumTick_FailsWithoutMutation()
        {
            var state = State(3, long.MaxValue);
            Assert.That(ChargeCooldown.TrySpend(state, Rules(), long.MaxValue, out var result, out var error), Is.False);
            Assert.That(result, Is.EqualTo(default(ChargeCooldownResult)));
            Assert.That(error, Is.EqualTo(ChargeCooldownError.TickOverflow));
        }

        [Test]
        public void ResultEquality_UsesEveryField()
        {
            var state = State(3, 0);
            ChargeCooldown.TrySpend(state, Rules(), 0, out var first, out _);
            ChargeCooldown.TrySpend(state, Rules(), 0, out var same, out _);
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first == same, Is.True);
            Assert.That(first != default, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        }

        [Test]
        public void RulesAndStateEquality_RoundTrip()
        {
            var rules = Rules();
            ChargeCooldown.TryCreateRules(3, 10, out var sameRules, out _);
            Assert.That(rules, Is.EqualTo(sameRules));
            var state = State(2, 100);
            Assert.That(ChargeCooldown.TryRestoreState(rules, state.AvailableCharges, state.LastEvaluatedTick, state.NextRechargeTick, out var restored, out _), Is.True);
            Assert.That(restored, Is.EqualTo(state));
            Assert.That(restored.GetHashCode(), Is.EqualTo(state.GetHashCode()));
        }

        private static ChargeCooldownRules Rules()
        {
            ChargeCooldown.TryCreateRules(3, 10, out var rules, out _);
            return rules;
        }

        private static ChargeCooldownState State(int charges, long tick)
        {
            ChargeCooldown.TryCreateState(Rules(), tick, charges, out var state, out _);
            return state;
        }

        private static void AssertState(ChargeCooldownState state, int charges, long last, long next)
        {
            Assert.That(state.AvailableCharges, Is.EqualTo(charges));
            Assert.That(state.LastEvaluatedTick, Is.EqualTo(last));
            Assert.That(state.NextRechargeTick, Is.EqualTo(next));
            Assert.That(state.IsRecharging, Is.EqualTo(next != 0));
        }
    }
}
