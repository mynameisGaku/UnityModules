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
                "\u2460 対象を選ぶ",
                "\u2461 再生中の値を記録する",
                "\u2462 再生終了後に差分を見る",
                "\u2463 変更内容を確認する",
                "\u2464 変更を反映して結果を見る"
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

        [TestCase(PlayModeTuningError.None, "問題なし")]
        [TestCase(PlayModeTuningError.ApplyFailed, "変更を反映できませんでした")]
        [TestCase(PlayModeTuningError.RollbackFailed, "元の値へ戻せませんでした")]
        public void ErrorValuesHaveJapaneseDisplayText(PlayModeTuningError error, string expected)
        {
            Assert.That(PlayModeTuningDisplayText.Error(error), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownErrorKeepsNumericValue()
        {
            Assert.That(PlayModeTuningDisplayText.Error((PlayModeTuningError)999), Is.EqualTo("不明な失敗（999）"));
        }

        [TestCase(PlayModeTuningPhase.Idle, "未開始")]
        [TestCase(PlayModeTuningPhase.Capturable, "値を記録可能")]
        [TestCase(PlayModeTuningPhase.Completed, "完了")]
        public void PhaseValuesHaveJapaneseDisplayText(PlayModeTuningPhase phase, string expected)
        {
            Assert.That(PlayModeTuningDisplayText.Phase(phase), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownPhaseAndValueKindKeepNumericValues()
        {
            Assert.That(PlayModeTuningDisplayText.Phase((PlayModeTuningPhase)999), Is.EqualTo("不明な段階（999）"));
            Assert.That(PlayModeTuningDisplayText.ValueKind((PlayModeTuningValueKind)999), Is.EqualTo("不明な値の種類（999）"));
        }

        [Test]
        public void EveryDefinedEnumValueHasKnownDisplayText()
        {
            foreach (PlayModeTuningError error in System.Enum.GetValues(typeof(PlayModeTuningError)))
                Assert.That(PlayModeTuningDisplayText.Error(error), Does.Not.StartWith("不明"), error.ToString());
            foreach (PlayModeTuningPhase phase in System.Enum.GetValues(typeof(PlayModeTuningPhase)))
                Assert.That(PlayModeTuningDisplayText.Phase(phase), Does.Not.StartWith("不明"), phase.ToString());
            foreach (PlayModeTuningValueKind valueKind in System.Enum.GetValues(typeof(PlayModeTuningValueKind)))
                Assert.That(PlayModeTuningDisplayText.ValueKind(valueKind), Does.Not.StartWith("不明"), valueKind.ToString());
        }
    }
}
