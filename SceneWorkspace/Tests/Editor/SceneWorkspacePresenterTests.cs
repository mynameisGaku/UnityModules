using System;
using System.Linq;
using NUnit.Framework;
using SceneWorkspace.Editor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneWorkspace.Editor.Tests
{
    [TestFixture]
    internal sealed class SceneWorkspacePresenterTests
    {
        private SceneWorkspaceProfile profile;

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
                UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void UiStepsStayInTopToBottomOrderWithoutArrowGlyphs()
        {
            Assert.That(SceneWorkspaceUiText.OrderedSteps, Is.EqualTo(new[]
            {
                "\u2460 作業セットを選ぶ",
                "\u2461 シーン構成を設定",
                "\u2462 差分を確認",
                "\u2463 内容を確認",
                "\u2464 作業セットを切り替える"
            }));
            Assert.That(SceneWorkspaceUiText.OrderedSteps.Any(text => text.Contains("->") || text.Contains("\u2192")), Is.False);
        }

        [Test]
        public void ProfileEditInvalidatesPreviewAndConfirmation()
        {
            profile = SceneWorkspaceTestData.CreateProfileAsset();
            var plan = ReadyPlan();
            var presenter = new SceneWorkspacePresenter(
                preview: selected => plan,
                capture: () => null,
                apply: selected => null,
                writeProfile: (selected, capture) => SceneWorkspaceValidation.Success);
            presenter.SetProfile(profile);
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.NotifyProfileChanged();

            Assert.That(presenter.Plan, Is.Null);
            Assert.That(presenter.ConfirmationAccepted, Is.False);
            Assert.That(presenter.CanApply, Is.False);
        }

        [Test]
        public void ApplyRemovesConsumedPlanFromVisibleState()
        {
            profile = SceneWorkspaceTestData.CreateProfileAsset();
            var plan = ReadyPlan();
            var applied = new SceneWorkspaceApplyResult(true, true, SceneWorkspaceError.None, "done", false, false, SceneWorkspaceError.None, string.Empty);
            var presenter = new SceneWorkspacePresenter(preview: selected => plan, apply: selected => applied);
            presenter.SetProfile(profile);
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Apply();

            Assert.That(presenter.Plan, Is.Null);
            Assert.That(presenter.Result, Is.SameAs(applied));
            Assert.That(presenter.ConfirmationAccepted, Is.False);
        }

        [Test]
        public void UnexpectedPreviewExceptionKeepsDetailsOutOfUserMessage()
        {
            profile = SceneWorkspaceTestData.CreateProfileAsset();
            var presenter = new SceneWorkspacePresenter(preview: selected => throw new InvalidOperationException("internal detail"));
            presenter.SetProfile(profile);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: internal detail");

            presenter.Preview();

            Assert.That(presenter.Plan, Is.Null);
            Assert.That(presenter.Message, Does.Contain("コンソール"));
            Assert.That(presenter.Message, Does.Not.Contain("internal detail"));
        }

        [Test]
        public void UnexpectedCaptureExceptionKeepsDetailsOutOfUserMessage()
        {
            profile = SceneWorkspaceTestData.CreateProfileAsset();
            var presenter = new SceneWorkspacePresenter(capture: () => throw new InvalidOperationException("capture internal detail"));
            presenter.SetProfile(profile);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: capture internal detail");

            presenter.CaptureIntoProfile();

            Assert.That(presenter.Capture.Error, Is.EqualTo(SceneWorkspaceError.CaptureFailed));
            Assert.That(presenter.Message, Does.Contain("コンソール"));
            Assert.That(presenter.Message, Does.Not.Contain("capture internal detail"));
        }

        [Test]
        public void UnexpectedApplyExceptionKeepsDetailsOutOfUserMessage()
        {
            profile = SceneWorkspaceTestData.CreateProfileAsset();
            var plan = ReadyPlan();
            var presenter = new SceneWorkspacePresenter(
                preview: selected => plan,
                apply: selected => throw new InvalidOperationException("apply internal detail"));
            presenter.SetProfile(profile);
            presenter.Preview();
            presenter.SetConfirmation(true);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: apply internal detail");

            presenter.Apply();

            Assert.That(presenter.Result.ApplyError, Is.EqualTo(SceneWorkspaceError.ApplyFailed));
            Assert.That(presenter.Message, Does.Contain("コンソール"));
            Assert.That(presenter.Message, Does.Not.Contain("apply internal detail"));
        }

        private static SceneWorkspacePlan ReadyPlan()
        {
            var current = SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true));
            var target = SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Gameplay", 0, true, true));
            return SceneWorkspacePlanner.Create(current, target, 100L);
        }
    }
}
