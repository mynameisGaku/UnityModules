using System;
using NUnit.Framework;

namespace SimulationClock.Tests
{
    /// <summary>整数時間から固定step範囲を作る純粋契約を検証する。</summary>
    public sealed class FixedStepClockTests
    {
        private static readonly FixedStepClockSettings Settings = new FixedStepClockSettings(200_000L, 4);

        /// <summary>正しい設定で初期状態の時計を作る。</summary>
        [Test]
        public void TryCreate_ValidSettings_ReturnsInitialClock()
        {
            Assert.That(FixedStepClock.TryCreate(Settings, out var clock, out var error), Is.True);
            Assert.That(error, Is.EqualTo(FixedStepClockError.None));
            Assert.That(clock.Settings, Is.EqualTo(Settings));
            Assert.That(clock.State, Is.EqualTo(default(FixedStepClockState)));
        }

        /// <summary>0以下のstep時間と範囲外の上限を拒否する。</summary>
        [TestCase(0L, 1)]
        [TestCase(-1L, 1)]
        [TestCase(1L, 0)]
        [TestCase(1L, FixedStepClock.MaximumSupportedStepsPerAdvance + 1)]
        public void TryCreate_InvalidSettings_ReturnsError(long stepTicks, int maxSteps)
        {
            Assert.That(FixedStepClock.TryCreate(new FixedStepClockSettings(stepTicks, maxSteps), out var clock, out var error), Is.False);
            Assert.That(clock, Is.Null);
            Assert.That(error, Is.EqualTo(FixedStepClockError.InvalidSettings));
        }

        /// <summary>同じ設定、状態、入力列が同じ結果と状態を作る。</summary>
        [Test]
        public void Advance_SameInputs_ReproduceExactly()
        {
            var first = Create(Settings);
            var second = Create(Settings);
            foreach (var elapsed in new[] { 70_000L, 130_000L, 450_000L, 1_400_000L, 10_000L })
            {
                Assert.That(first.AdvanceTicks(elapsed), Is.EqualTo(second.AdvanceTicks(elapsed)));
                Assert.That(first.State, Is.EqualTo(second.State));
            }
        }

        /// <summary>上限へ達しない入力は分割方法に関係なく同じ状態へ着く。</summary>
        [Test]
        public void Advance_ChunkedBelowCap_ReachesSameState()
        {
            var chunked = Create(Settings);
            var combined = Create(Settings);
            chunked.AdvanceTicks(70_000L);
            chunked.AdvanceTicks(130_000L);
            var result = combined.AdvanceTicks(200_000L);
            Assert.That(chunked.State, Is.EqualTo(combined.State));
            Assert.That(result.FirstStepIndex, Is.EqualTo(0));
            Assert.That(result.StepCount, Is.EqualTo(1));
        }

        /// <summary>step直前の端数を保持し、境界で1stepだけ返す。</summary>
        [Test]
        public void Advance_ExactBoundary_PreservesThenConsumesRemainder()
        {
            var clock = Create(Settings);
            var before = clock.AdvanceTicks(199_999L);
            var boundary = clock.AdvanceTicks(1L);
            Assert.That(before.StepCount, Is.Zero);
            Assert.That(before.State.RemainderTicks, Is.EqualTo(199_999L));
            Assert.That(boundary.StepCount, Is.EqualTo(1));
            Assert.That(boundary.State.RemainderTicks, Is.Zero);
            Assert.That(boundary.InterpolationAlpha, Is.Zero);
        }

        /// <summary>端数から補間率を0以上1未満で求める。</summary>
        [Test]
        public void Advance_PartialStep_ReturnsInterpolationAlpha()
        {
            var result = Create(Settings).AdvanceTicks(50_000L);
            Assert.That(result.StepCount, Is.Zero);
            Assert.That(result.InterpolationAlpha, Is.EqualTo(0.25d));
        }

        /// <summary>catch-up上限を超えたstepを明示破棄し、端数だけを保持する。</summary>
        [Test]
        public void Advance_Hitch_CapsAndReportsDroppedTime()
        {
            var result = Create(Settings).AdvanceTicks(2_050_000L);
            Assert.That(result.StepCount, Is.EqualTo(4));
            Assert.That(result.DroppedStepCount, Is.EqualTo(6));
            Assert.That(result.DroppedTicks, Is.EqualTo(1_200_000L));
            Assert.That(result.State.CompletedStepCount, Is.EqualTo(4));
            Assert.That(result.State.RemainderTicks, Is.EqualTo(50_000L));
            Assert.That(result.State.TotalDroppedTicks, Is.EqualTo(1_200_000L));
        }

        /// <summary>破棄後も次のstep番号を飛ばさず、利用側へ連続範囲を返す。</summary>
        [Test]
        public void Advance_AfterDrop_ContinuesStepIndices()
        {
            var clock = Create(Settings);
            clock.AdvanceTicks(2_000_000L);
            var next = clock.AdvanceTicks(200_000L);
            Assert.That(next.FirstStepIndex, Is.EqualTo(4));
            Assert.That(next.StepCount, Is.EqualTo(1));
        }

        /// <summary>0経過では状態を変更せず成功を返す。</summary>
        [Test]
        public void Advance_ZeroElapsed_IsSuccessfulNoOp()
        {
            var clock = Create(Settings);
            var before = clock.State;
            var result = clock.Advance(TimeSpan.Zero);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.StepCount, Is.Zero);
            Assert.That(clock.State, Is.EqualTo(before));
        }

        /// <summary>負の経過時間を拒否し、状態を変更しない。</summary>
        [Test]
        public void Advance_NegativeElapsed_DoesNotMutate()
        {
            var clock = Create(Settings);
            clock.AdvanceTicks(50_000L);
            var before = clock.State;
            var result = clock.AdvanceTicks(-1L);
            Assert.That(result.Error, Is.EqualTo(FixedStepClockError.InvalidElapsedTime));
            Assert.That(clock.State, Is.EqualTo(before));
            Assert.That(result.State, Is.EqualTo(before));
        }

        /// <summary>完了step数がlong上限を超える場合に状態を変更しない。</summary>
        [Test]
        public void Advance_CompletedStepOverflow_DoesNotMutate()
        {
            var clock = Create(Settings, new FixedStepClockState(long.MaxValue, 0L, 0L));
            var before = clock.State;
            var result = clock.AdvanceTicks(Settings.StepDurationTicks);
            Assert.That(result.Error, Is.EqualTo(FixedStepClockError.Overflow));
            Assert.That(clock.State, Is.EqualTo(before));
        }

        /// <summary>累積破棄tickがlong上限を超える場合に状態を変更しない。</summary>
        [Test]
        public void Advance_DroppedTickOverflow_DoesNotMutate()
        {
            var clock = Create(new FixedStepClockSettings(10L, 1), new FixedStepClockState(0L, 0L, long.MaxValue - 5L));
            var before = clock.State;
            var result = clock.AdvanceTicks(20L);
            Assert.That(result.Error, Is.EqualTo(FixedStepClockError.Overflow));
            Assert.That(clock.State, Is.EqualTo(before));
        }

        /// <summary>正しい状態へResetし、その位置からstep範囲を返す。</summary>
        [Test]
        public void Reset_ValidState_RestoresClock()
        {
            var clock = Create(Settings);
            var restored = new FixedStepClockState(12L, 50_000L, 400_000L);
            Assert.That(clock.Reset(restored), Is.EqualTo(FixedStepClockError.None));
            var result = clock.AdvanceTicks(150_000L);
            Assert.That(result.FirstStepIndex, Is.EqualTo(12L));
            Assert.That(result.StepCount, Is.EqualTo(1));
            Assert.That(result.State.TotalDroppedTicks, Is.EqualTo(400_000L));
        }

        /// <summary>端数がstep以上の不正状態を拒否し、現在状態を維持する。</summary>
        [Test]
        public void Reset_InvalidState_DoesNotMutate()
        {
            var clock = Create(Settings);
            clock.AdvanceTicks(50_000L);
            var before = clock.State;
            Assert.That(clock.Reset(new FixedStepClockState(0L, Settings.StepDurationTicks, 0L)), Is.EqualTo(FixedStepClockError.InvalidState));
            Assert.That(clock.State, Is.EqualTo(before));
        }

        /// <summary>不正な復元状態では時計を作らない。</summary>
        [TestCase(-1L, 0L, 0L)]
        [TestCase(0L, -1L, 0L)]
        [TestCase(0L, 200_000L, 0L)]
        [TestCase(0L, 0L, -1L)]
        public void TryCreate_InvalidState_ReturnsError(long completed, long remainder, long dropped)
        {
            Assert.That(FixedStepClock.TryCreate(Settings, new FixedStepClockState(completed, remainder, dropped), out var clock, out var error), Is.False);
            Assert.That(clock, Is.Null);
            Assert.That(error, Is.EqualTo(FixedStepClockError.InvalidState));
        }

        private static FixedStepClock Create(FixedStepClockSettings settings, FixedStepClockState state = default)
        {
            Assert.That(FixedStepClock.TryCreate(settings, state, out var clock, out var error), Is.True, error.ToString());
            return clock;
        }
    }
}
