using System;
using System.Linq;
using NUnit.Framework;
using SceneWorkspace.Editor;
using UnityEngine;

namespace SceneWorkspace.Tests
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
                "\u2460 Workspace Profile",
                "\u2461 Scene Setup/Capture",
                "\u2462 Preview Changes",
                "\u2463 Review and Confirm",
                "\u2464 Switch Workspace/Result"
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

        private static SceneWorkspacePlan ReadyPlan()
        {
            var current = SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true));
            var target = SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Gameplay", 0, true, true));
            return SceneWorkspacePlanner.Create(current, target, 100L);
        }
    }
}
