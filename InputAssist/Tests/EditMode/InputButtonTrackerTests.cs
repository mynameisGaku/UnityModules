using NUnit.Framework;

namespace InputAssist.Tests
{
    public sealed class InputButtonTrackerTests
    {
        [Test]
        public void Process_PressHoldRepeatRelease_ReportsEachStage()
        {
            var tracker = CreateTracker(0.3f, 0.4f, 0.1f, 0.25f, 3);

            var pressed = tracker.Process(true, 0f);
            var held = tracker.Process(true, 0.3f);
            var repeated = tracker.Process(true, 0.2f);
            var released = tracker.Process(false, 0f);

            Assert.That(pressed.Events, Is.EqualTo(InputButtonEvent.Pressed));
            Assert.That(held.Events.HasFlag(InputButtonEvent.HoldStarted), Is.True);
            Assert.That(held.IsHeld, Is.True);
            Assert.That(repeated.Events.HasFlag(InputButtonEvent.Repeated), Is.True);
            Assert.That(repeated.RepeatCount, Is.EqualTo(2));
            Assert.That(released.Events, Is.EqualTo(InputButtonEvent.Released));
            Assert.That(released.IsPressed, Is.False);
            Assert.That(tracker.PendingTapCount, Is.Zero);
        }

        [Test]
        public void Process_ShortPress_CompletesSingleTapAfterGap()
        {
            var tracker = CreateTracker(0.3f, 0.5f, 0.1f, 0.2f, 3);

            tracker.Process(true, 0f);
            var released = tracker.Process(false, 0.1f);
            var waiting = tracker.Process(false, 0.09f);
            var completed = tracker.Process(false, 0.11f);

            Assert.That(released.Events.HasFlag(InputButtonEvent.Released), Is.True);
            Assert.That(waiting.Events, Is.EqualTo(InputButtonEvent.None));
            Assert.That(completed.Events, Is.EqualTo(InputButtonEvent.TapCompleted));
            Assert.That(completed.TapCount, Is.EqualTo(1));
        }

        [Test]
        public void Process_ThreeTaps_CompletesImmediatelyAtMaximum()
        {
            var tracker = CreateTracker(0.3f, 0.5f, 0.1f, 0.5f, 3);

            tracker.Process(true, 0f);
            tracker.Process(false, 0f);
            tracker.Process(true, 0.1f);
            tracker.Process(false, 0f);
            tracker.Process(true, 0.1f);
            var completed = tracker.Process(false, 0f);

            Assert.That(completed.Events.HasFlag(InputButtonEvent.TapCompleted), Is.True);
            Assert.That(completed.TapCount, Is.EqualTo(3));
            Assert.That(tracker.PendingTapCount, Is.Zero);
        }

        [Test]
        public void Process_LargeTick_BoundsRepeatCatchUp()
        {
            var tracker = CreateTracker(5f, 0f, 0.01f, 0.2f, 3);
            tracker.Process(true, 0f);

            var result = tracker.Process(true, 10f);

            Assert.That(result.Events.HasFlag(InputButtonEvent.Repeated), Is.True);
            Assert.That(result.RepeatCount, Is.EqualTo(32));
        }

        [Test]
        public void Process_InvalidDelta_PreservesState()
        {
            var tracker = CreateTracker(0.3f, 0.4f, 0.1f, 0.2f, 3);
            tracker.Process(true, 0f);

            var negative = tracker.Process(false, -1f);
            var nonFinite = tracker.Process(false, float.PositiveInfinity);

            Assert.That(negative.Error, Is.EqualTo(InputAssistError.NegativeDeltaTime));
            Assert.That(nonFinite.Error, Is.EqualTo(InputAssistError.NonFiniteInput));
            Assert.That(tracker.IsPressed, Is.True);
        }

        [Test]
        public void Reset_ClearsAllGestureState()
        {
            var tracker = CreateTracker(0.3f, 0.4f, 0.1f, 0.2f, 3);
            tracker.Process(true, 0f);
            tracker.Process(false, 0f);

            tracker.Reset();

            Assert.That(tracker.IsPressed, Is.False);
            Assert.That(tracker.IsHeld, Is.False);
            Assert.That(tracker.PressDuration, Is.Zero);
            Assert.That(tracker.PendingTapCount, Is.Zero);
        }

        [Test]
        public void TryConfigure_InvalidSettings_PreservesDefaults()
        {
            var tracker = new InputButtonTracker();

            var succeeded = tracker.TryConfigure(0.3f, 0.4f, 0f, 0.2f, 3, out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Is.EqualTo(InputAssistError.InvalidConfiguration));
            Assert.That(tracker.Process(true, 0f).Succeeded, Is.True);
        }

        private static InputButtonTracker CreateTracker(float hold, float repeatDelay, float repeatInterval, float tapGap, int maxTaps)
        {
            var tracker = new InputButtonTracker();
            Assert.That(tracker.TryConfigure(hold, repeatDelay, repeatInterval, tapGap, maxTaps, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputAssistError.None));
            return tracker;
        }
    }
}
