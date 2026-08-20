using NUnit.Framework;

namespace InputThresholding.Tests
{
    public sealed class InputThresholdClassifierTests
    {
        [TestCase(-0.1d, 0.75d)]
        [TestCase(0.25d, 1.1d)]
        [TestCase(0.5d, 0.5d)]
        [TestCase(0.75d, 0.25d)]
        [TestCase(double.NaN, 0.75d)]
        [TestCase(0.25d, double.PositiveInfinity)]
        public void TryCreate_InvalidThresholds_Fails(double releaseThreshold, double pressThreshold)
        {
            Assert.That(InputThresholdClassifier.TryCreate(releaseThreshold, pressThreshold, false, out var classifier, out var error), Is.False);
            Assert.That(classifier.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(InputThresholdClassificationError.InvalidConfiguration));
        }

        [TestCase(0d, 1d)]
        [TestCase(0.25d, 0.75d)]
        [TestCase(0.999998d, 0.999999d)]
        public void TryCreate_ValidThresholds_PreservesConfiguration(double releaseThreshold, double pressThreshold)
        {
            var classifier = Create(releaseThreshold, pressThreshold, true);
            Assert.That(classifier.IsValid, Is.True);
            Assert.That(classifier.ReleaseThreshold, Is.EqualTo(releaseThreshold));
            Assert.That(classifier.PressThreshold, Is.EqualTo(pressThreshold));
            Assert.That(classifier.IsPressed, Is.True);
        }

        [Test]
        public void DefaultClassifier_ReturnsInvalidConfigurationWithoutMutation()
        {
            var classifier = default(InputThresholdClassifier);
            var result = classifier.Sample(1d);
            AssertFailure(result, false, InputThresholdClassificationError.InvalidConfiguration);
            Assert.That(classifier.IsPressed, Is.False);
            Assert.That(classifier.Reset(true), Is.EqualTo(InputThresholdClassificationError.InvalidConfiguration));
        }

        [TestCase(0d)]
        [TestCase(0.25d)]
        [TestCase(0.5d)]
        [TestCase(0.749999d)]
        public void Sample_ReleasedBelowPressThreshold_RemainsReleased(double value)
        {
            var classifier = Create(0.25d, 0.75d, false);
            AssertSuccess(classifier.Sample(value), false, InputThresholdEvent.None);
        }

        [Test]
        public void Sample_ExactPressThreshold_PressesInclusively()
        {
            var classifier = Create(0.25d, 0.75d, false);
            AssertSuccess(classifier.Sample(0.75d), true, InputThresholdEvent.Pressed);
            Assert.That(classifier.IsPressed, Is.True);
        }

        [TestCase(0.250001d)]
        [TestCase(0.5d)]
        [TestCase(0.749999d)]
        [TestCase(1d)]
        public void Sample_PressedAboveReleaseThreshold_RemainsPressed(double value)
        {
            var classifier = Create(0.25d, 0.75d, true);
            AssertSuccess(classifier.Sample(value), true, InputThresholdEvent.None);
        }

        [Test]
        public void Sample_ExactReleaseThreshold_ReleasesInclusively()
        {
            var classifier = Create(0.25d, 0.75d, true);
            AssertSuccess(classifier.Sample(0.25d), false, InputThresholdEvent.Released);
            Assert.That(classifier.IsPressed, Is.False);
        }

        [Test]
        public void Sample_ClampsHighAndLowFiniteValues()
        {
            var classifier = Create(0.25d, 0.75d, false);
            AssertSuccess(classifier.Sample(10d), true, InputThresholdEvent.Pressed);
            AssertSuccess(classifier.Sample(-10d), false, InputThresholdEvent.Released);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Sample_NonFiniteValue_FailsAndPreservesState(double value)
        {
            var classifier = Create(0.25d, 0.75d, true);
            AssertFailure(classifier.Sample(value), true, InputThresholdClassificationError.NonFiniteInput);
            Assert.That(classifier.IsPressed, Is.True);
        }

        [Test]
        public void Reset_ReconstructsStateWithoutChangingThresholds()
        {
            var classifier = Create(0.25d, 0.75d, false);
            Assert.That(classifier.Reset(true), Is.EqualTo(InputThresholdClassificationError.None));
            Assert.That(classifier.IsPressed, Is.True);
            Assert.That(classifier.ReleaseThreshold, Is.EqualTo(0.25d));
            Assert.That(classifier.PressThreshold, Is.EqualTo(0.75d));
            AssertSuccess(classifier.Sample(0.5d), true, InputThresholdEvent.None);
        }

        [Test]
        public void Sample_GoldenSequence_ProducesStableEdges()
        {
            var classifier = Create(0.25d, 0.75d, false);
            AssertSuccess(classifier.Sample(0.1d), false, InputThresholdEvent.None);
            AssertSuccess(classifier.Sample(0.75d), true, InputThresholdEvent.Pressed);
            AssertSuccess(classifier.Sample(0.5d), true, InputThresholdEvent.None);
            AssertSuccess(classifier.Sample(0.25d), false, InputThresholdEvent.Released);
        }

        [Test]
        public void StructCopy_HasIndependentPressedState()
        {
            var original = Create(0.25d, 0.75d, false);
            var copy = original;
            AssertSuccess(copy.Sample(1d), true, InputThresholdEvent.Pressed);
            Assert.That(original.IsPressed, Is.False);
            Assert.That(copy.IsPressed, Is.True);
        }

        [Test]
        public void ResultEquality_IncludesStateEventErrorAndPresence()
        {
            var first = Create(0.25d, 0.75d, false).Sample(1d);
            var second = Create(0.25d, 0.75d, false).Sample(1d);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != default(InputThresholdClassificationResult), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static InputThresholdClassifier Create(double releaseThreshold, double pressThreshold, bool initialIsPressed)
        {
            Assert.That(InputThresholdClassifier.TryCreate(releaseThreshold, pressThreshold, initialIsPressed, out var classifier, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputThresholdClassificationError.None));
            return classifier;
        }

        private static void AssertSuccess(InputThresholdClassificationResult result, bool isPressed, InputThresholdEvent thresholdEvent)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsPressed, Is.EqualTo(isPressed));
            Assert.That(result.Event, Is.EqualTo(thresholdEvent));
            Assert.That(result.Error, Is.EqualTo(InputThresholdClassificationError.None));
            Assert.That(result.StateChanged, Is.EqualTo(thresholdEvent != InputThresholdEvent.None));
        }

        private static void AssertFailure(InputThresholdClassificationResult result, bool isPressed, InputThresholdClassificationError error)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.IsPressed, Is.EqualTo(isPressed));
            Assert.That(result.Event, Is.EqualTo(InputThresholdEvent.None));
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.StateChanged, Is.False);
        }
    }
}
