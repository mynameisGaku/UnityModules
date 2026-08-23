using System.Linq;
using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningUiTests
    {
        [Test]
        public void StepsAreStrictlyOrderedFromOneThroughFive()
        {
            CollectionAssert.AreEqual(new[]
            {
                "\u2460 Targets",
                "\u2461 Capture During Play",
                "\u2462 Preview After Play",
                "\u2463 Review and Confirm",
                "\u2464 Apply Tuning / Result"
            }, PlayModeTuningUiText.OrderedSteps);
        }

        [Test]
        public void PreviewIsBelowCaptureAndApplyIsLast()
        {
            var steps = PlayModeTuningUiText.OrderedSteps.ToArray();
            Assert.That(System.Array.IndexOf(steps, PlayModeTuningUiText.Step3), Is.GreaterThan(System.Array.IndexOf(steps, PlayModeTuningUiText.Step2)));
            Assert.That(steps.Last(), Is.EqualTo(PlayModeTuningUiText.Step5));
        }

        [Test]
        public void EveryStepHasDistinctCircledNumberEscapeResult()
        {
            Assert.That(PlayModeTuningUiText.OrderedSteps.Select(value => value[0]).Distinct().Count(), Is.EqualTo(5));
        }
    }
}
