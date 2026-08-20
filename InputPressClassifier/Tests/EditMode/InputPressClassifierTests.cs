using NUnit.Framework;

namespace InputPressing.Tests
{
    [TestFixture]
    public sealed class InputPressClassifierTests
    {
        [Test]
        public void TryCreate_ZeroThreshold_ReturnsInvalidHoldThreshold()
        {
            Assert.That(InputPressClassifier.TryCreate(0, 10, out var classifier, out var error), Is.False);
            Assert.That(classifier, Is.Null);
            Assert.That(error, Is.EqualTo(InputPressError.InvalidHoldThreshold));
        }

        [Test]
        public void TryCreate_PositiveThreshold_StartsReleasedAtInitialTick()
        {
            var classifier = Create(3, 100);

            AssertStatus(classifier.Snapshot(), 100, false, false, false, false, false, false, false, 0);
            Assert.That(classifier.HoldThresholdTicks, Is.EqualTo(3));
        }

        [Test]
        public void TrySample_PressEdge_StartsPressImmediately()
        {
            var classifier = Create(3, 100);

            Assert.That(classifier.TrySample(100, true, out var status, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputPressError.None));
            AssertStatus(status, 100, true, false, true, false, false, false, false, 0);
        }

        [Test]
        public void TrySample_HeldBeforeThreshold_DoesNotClassify()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);

            classifier.TrySample(102, true, out var status, out _);

            AssertStatus(status, 102, true, false, false, false, false, false, false, 2);
        }

        [Test]
        public void TrySample_ThresholdReached_StartsHoldOnce()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);

            classifier.TrySample(103, true, out var started, out _);
            classifier.TrySample(104, true, out var continued, out _);

            AssertStatus(started, 103, true, true, false, true, false, false, false, 3);
            AssertStatus(continued, 104, true, true, false, false, false, false, false, 4);
        }

        [Test]
        public void TrySample_TickJumpAcrossThreshold_StartsHold()
        {
            var classifier = Create(5, 7);
            classifier.TrySample(7, true, out _, out _);

            classifier.TrySample(20, true, out var status, out _);

            Assert.That(status.HoldStarted, Is.True);
            Assert.That(status.PressDurationTicks, Is.EqualTo(13));
        }

        [Test]
        public void TrySample_ReleaseBeforeThreshold_EmitsTap()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);

            classifier.TrySample(102, false, out var status, out _);

            AssertStatus(status, 102, false, false, false, false, true, true, false, 2);
        }

        [Test]
        public void TrySample_ReleaseExactlyAtThreshold_EmitsSkippedHoldAndCompletion()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);

            classifier.TrySample(103, false, out var status, out _);

            AssertStatus(status, 103, false, false, false, true, true, false, true, 3);
        }

        [Test]
        public void TrySample_ReleaseAfterHoldStarted_CompletesWithoutRestartingHold()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(103, true, out _, out _);

            classifier.TrySample(108, false, out var status, out _);

            AssertStatus(status, 108, false, false, false, false, true, false, true, 8);
        }

        [Test]
        public void TrySample_RepeatedRelease_IsSilent()
        {
            var classifier = Create(3, 100);

            classifier.TrySample(105, false, out var status, out _);

            AssertStatus(status, 105, false, false, false, false, false, false, false, 0);
        }

        [Test]
        public void TrySample_SecondPress_RearmsNewClassification()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(101, false, out _, out _);

            classifier.TrySample(102, true, out var started, out _);
            classifier.TrySample(105, true, out var held, out _);

            Assert.That(started.PressStarted, Is.True);
            Assert.That(held.HoldStarted, Is.True);
            Assert.That(held.PressDurationTicks, Is.EqualTo(3));
        }

        [Test]
        public void TrySample_BackwardTickWhileReleased_IsMutationFree()
        {
            var classifier = Create(3, 100);

            Assert.That(classifier.TrySample(99, true, out var status, out var error), Is.False);

            Assert.That(error, Is.EqualTo(InputPressError.TickMovedBackward));
            AssertStatus(status, 100, false, false, false, false, false, false, false, 0);
        }

        [Test]
        public void TrySample_BackwardTickWhilePressed_IsMutationFree()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(102, true, out _, out _);

            Assert.That(classifier.TrySample(101, false, out var status, out var error), Is.False);

            Assert.That(error, Is.EqualTo(InputPressError.TickMovedBackward));
            AssertStatus(status, 102, true, false, false, false, false, false, false, 2);
        }

        [Test]
        public void Snapshot_WhileHolding_ClearsTerminalFlags()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(103, true, out _, out _);

            var status = classifier.Snapshot();

            AssertStatus(status, 103, true, true, false, false, false, false, false, 3);
        }

        [Test]
        public void Snapshot_AfterRelease_DoesNotRetainCompletedDuration()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(105, false, out _, out _);

            AssertStatus(classifier.Snapshot(), 105, false, false, false, false, false, false, false, 0);
        }

        [Test]
        public void Reset_ClearsActivePressAndStartsNewTimeline()
        {
            var classifier = Create(3, 100);
            classifier.TrySample(100, true, out _, out _);
            classifier.TrySample(103, true, out _, out _);

            classifier.Reset(7);

            AssertStatus(classifier.Snapshot(), 7, false, false, false, false, false, false, false, 0);
            Assert.That(classifier.TrySample(7, true, out var status, out _), Is.True);
            Assert.That(status.PressStarted, Is.True);
        }

        [Test]
        public void TrySample_EqualTick_IsAcceptedWithoutElapsedTime()
        {
            var classifier = Create(1, 100);
            classifier.TrySample(100, true, out _, out _);

            Assert.That(classifier.TrySample(100, true, out var status, out var error), Is.True);

            Assert.That(error, Is.EqualTo(InputPressError.None));
            Assert.That(status.HoldStarted, Is.False);
            Assert.That(status.PressDurationTicks, Is.Zero);
        }

        [Test]
        public void TrySample_MaximumThreshold_UsesSubtractionWithoutOverflow()
        {
            var classifier = Create(ulong.MaxValue, 0);
            classifier.TrySample(0, true, out _, out _);

            classifier.TrySample(ulong.MaxValue, true, out var status, out _);

            Assert.That(status.HoldStarted, Is.True);
            Assert.That(status.PressDurationTicks, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void FiveStepScenario_ProducesTapThenHoldCompletion()
        {
            var classifier = Create(3, 100);

            classifier.TrySample(100, true, out var tapPress, out _);
            classifier.TrySample(102, false, out var tapRelease, out _);
            classifier.TrySample(103, true, out var holdPress, out _);
            classifier.TrySample(106, true, out var holdStart, out _);
            classifier.TrySample(108, false, out var holdRelease, out _);

            Assert.That(tapPress.PressStarted, Is.True);
            Assert.That(tapRelease.Tapped, Is.True);
            Assert.That(holdPress.PressStarted, Is.True);
            Assert.That(holdStart.HoldStarted, Is.True);
            Assert.That(holdRelease.HoldCompleted, Is.True);
            Assert.That(holdRelease.PressDurationTicks, Is.EqualTo(5));
        }

        private static InputPressClassifier Create(ulong threshold, ulong initialTick)
        {
            Assert.That(InputPressClassifier.TryCreate(threshold, initialTick, out var classifier, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputPressError.None));
            return classifier;
        }

        private static void AssertStatus(InputPressStatus status, ulong tick, bool isPressed, bool isHolding, bool pressStarted, bool holdStarted, bool released, bool tapped, bool holdCompleted, ulong duration)
        {
            Assert.That(status.CurrentTick, Is.EqualTo(tick));
            Assert.That(status.IsPressed, Is.EqualTo(isPressed));
            Assert.That(status.IsHolding, Is.EqualTo(isHolding));
            Assert.That(status.PressStarted, Is.EqualTo(pressStarted));
            Assert.That(status.HoldStarted, Is.EqualTo(holdStarted));
            Assert.That(status.Released, Is.EqualTo(released));
            Assert.That(status.Tapped, Is.EqualTo(tapped));
            Assert.That(status.HoldCompleted, Is.EqualTo(holdCompleted));
            Assert.That(status.PressDurationTicks, Is.EqualTo(duration));
        }
    }
}
