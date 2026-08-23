using System;
using System.Linq;
using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningOperationsTests
    {
        [Test]
        public void FullManualFlowAppliesCapturedValueAndMarksSceneDirty()
        {
            var flow = ReadyPlan(1f, 3f, out var plan);
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplySucceeded, Is.True);
            Assert.That(result.RollbackAttempted, Is.False);
            Assert.That(flow.Gateway.GetValue("c0", "speed").EqualsExact(FakePlayModeTuningGateway.FloatValue(3f)), Is.True);
            Assert.That(flow.Gateway.MarkDirtyCalls, Is.EqualTo(1));
            Assert.That(result.Session.Phase, Is.EqualTo(PlayModeTuningPhase.Completed));
        }

        [Test]
        public void CaptureCannotRunBeforePlayLifecycleTransition()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            var result = flow.Operations.CaptureDuringPlay(flow.SessionId);
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.WrongPhase));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void PreviewDoesNotMutateGateway()
        {
            var flow = ReadyForPreview(1f, 2f);
            var plan = flow.Preview();
            Assert.That(plan.IsReady, Is.True);
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
            Assert.That(flow.Gateway.MarkDirtyCalls, Is.Zero);
        }

        [Test]
        public void NoChangeCaptureCompletesWithoutPlanOrMutation()
        {
            var flow = ReadyForPreview(1f, 1f);
            var plan = flow.Preview();
            Assert.That(plan.Error, Is.EqualTo(PlayModeTuningError.NoChanges));
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Completed));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void CapturedDisplayTamperingBeforePreviewIsRejected()
        {
            var flow = ReadyForPreview(1f, 2f);
            var stored = flow.Store.Current;
            stored.properties[0].capturedDisplay = "misleading";
            flow.Store.Save(stored);
            var plan = flow.Preview();
            Assert.That(plan.Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void TargetNameTamperingBeforePreviewIsReplacedByResolvedName()
        {
            var flow = ReadyForPreview(1f, 2f);
            var stored = flow.Store.Current;
            stored.properties[0].targetName = "wrong-object";
            flow.Store.Save(stored);
            var plan = flow.Preview();
            Assert.That(plan.IsReady, Is.True);
            Assert.That(plan.Changes[0].TargetName, Is.EqualTo("c0"));
        }

        [Test]
        public void SelectedValueChangedAfterPreviewMakesPlanStaleBeforeApply()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(9f));
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.StaleSession));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void UnselectedTopLevelChangeAfterPreviewMakesPlanStale()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            flow.Gateway.SetUnselected("c0", "external-edit");
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.StaleSession));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void OnValidateUnselectedSideEffectFailsPostVerificationAndResidualFailsRollback()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            flow.Gateway.ChangeUnselectedOnFirstApply = true;
            flow.Gateway.KeepUnselectedResidualOnRollback = true;
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplySucceeded, Is.False);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.VerificationFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.False);
            Assert.That(result.RollbackError, Is.EqualTo(PlayModeTuningError.RollbackFailed));
            Assert.That(flow.Gateway.GetValue("c0", "speed").EqualsExact(FakePlayModeTuningGateway.FloatValue(1f)), Is.True);
        }

        [Test]
        public void OnValidateSideEffectCanReportVerifiedRollbackSeparately()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            flow.Gateway.ChangeUnselectedOnFirstApply = true;
            flow.Gateway.KeepUnselectedResidualOnRollback = false;
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplySucceeded, Is.False);
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RollbackError, Is.EqualTo(PlayModeTuningError.None));
        }

        [Test]
        public void PartialApplyFailureRollsSelectedValueBackAndVerifies()
        {
            var flow = ReadyPlan(1f, 4f, out var plan);
            flow.Gateway.FailApplyCall = 1;
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.ApplyFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(flow.Gateway.GetValue("c0", "speed").EqualsExact(FakePlayModeTuningGateway.FloatValue(1f)), Is.True);
        }

        [Test]
        public void ExplicitSceneDirtyFailureTriggersVerifiedRollback()
        {
            var flow = ReadyPlan(1f, 5f, out var plan);
            flow.Gateway.FailMarkDirty = true;
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.SceneDirtyFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(flow.Gateway.MarkDirtyCalls, Is.EqualTo(1));
            Assert.That(flow.Gateway.GetValue("c0", "speed").EqualsExact(FakePlayModeTuningGateway.FloatValue(1f)), Is.True);
        }

        [Test]
        public void ExactPlanIsSingleUseAfterSuccessfulApply()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            Assert.That(flow.Operations.Apply(plan).ApplySucceeded, Is.True);
            var second = flow.Operations.Apply(plan);
            Assert.That(second.ApplyError, Is.EqualTo(PlayModeTuningError.PlanAlreadyConsumed));
            Assert.That(flow.Gateway.ApplyCalls, Is.EqualTo(1));
        }

        [Test]
        public void CopiedPlanWithSameFieldsIsRejectedBeforeMutation()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            var copy = new PlayModeTuningPlan(plan.Error, plan.Message, plan.SessionId, plan.Nonce, plan.Revision, plan.Changes);
            var result = flow.Operations.Apply(copy);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.StalePlan));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void StalePreflightStillConsumesPlanBeforeAnyWrite()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(8f));
            var first = flow.Operations.Apply(plan);
            var second = flow.Operations.Apply(plan);
            Assert.That(first.ApplyAttempted, Is.False);
            Assert.That(second.ApplySucceeded, Is.False);
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
        }

        [Test]
        public void CapturedPayloadChangedAfterPreviewCannotAlterConfirmedPlan()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            var stored = flow.Store.Current;
            stored.properties[0].capturedPayload = PlayModeTuningValueCodec.EncodeFloat(7f);
            stored.properties[0].capturedDisplay = "7";
            flow.Store.Save(stored);
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.StalePlan));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
            Assert.That(flow.Gateway.GetValue("c0", "speed").EqualsExact(FakePlayModeTuningGateway.FloatValue(1f)), Is.True);
        }

        [Test]
        public void ComponentScenePathChangedAfterPreviewIsRejectedBeforeMutation()
        {
            var flow = ReadyPlan(1f, 2f, out var plan);
            var stored = flow.Store.Current;
            stored.components[0].scenePath = "Assets/OtherScene.unity";
            flow.Store.Save(stored);
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
            Assert.That(flow.Gateway.MarkDirtyCalls, Is.Zero);
        }

        [Test]
        public void RollbackRestoresSelectedUnchangedPropertyMutatedBySideEffect()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Gateway.SetValue("c0", "changed", FakePlayModeTuningGateway.FloatValue(1f));
            flow.Gateway.SetValue("c0", "unchanged", FakePlayModeTuningGateway.FloatValue(5f));
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "changed"), FakePlayModeTuningGateway.Selection("c0", "unchanged"));
            flow.EnterPlay();
            flow.Gateway.SetValue("c0", "changed", FakePlayModeTuningGateway.FloatValue(2f));
            flow.Capture();
            flow.ExitPlay();
            var plan = flow.Preview();
            flow.Gateway.SelectedSideEffectComponent = "c0";
            flow.Gateway.SelectedSideEffectProperty = "unchanged";
            flow.Gateway.SelectedSideEffectValue = FakePlayModeTuningGateway.FloatValue(9f);
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplyError, Is.EqualTo(PlayModeTuningError.VerificationFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(flow.Gateway.GetValue("c0", "changed").EqualsExact(FakePlayModeTuningGateway.FloatValue(1f)), Is.True);
            Assert.That(flow.Gateway.GetValue("c0", "unchanged").EqualsExact(FakePlayModeTuningGateway.FloatValue(5f)), Is.True);
        }

        [Test]
        public void DiscardEndsSessionWithoutMutation()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            var session = flow.Operations.Discard(flow.SessionId);
            Assert.That(session.Phase, Is.EqualTo(PlayModeTuningPhase.Completed));
            Assert.That(flow.Gateway.ApplyCalls, Is.Zero);
            Assert.That(flow.Gateway.MarkDirtyCalls, Is.Zero);
        }

        [Test]
        public void WrongSessionIdCannotCapture()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            var result = flow.Operations.CaptureDuringPlay(Guid.NewGuid());
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.InvalidSession));
        }

        [Test]
        public void WrongSessionIdDiscardReportsErrorAndPreservesActiveSession()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            var result = flow.Operations.Discard(Guid.NewGuid());
            var active = flow.Operations.GetCurrentSession();
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.InvalidSession));
            Assert.That(result.Phase, Is.EqualTo(PlayModeTuningPhase.Armed));
            Assert.That(result.SessionId, Is.EqualTo(flow.SessionId));
            Assert.That(active.SessionId, Is.EqualTo(flow.SessionId));
            Assert.That(active.Phase, Is.EqualTo(PlayModeTuningPhase.Armed));
        }

        [Test]
        public void MultiplePropertiesApplyInCanonicalIdentityOrderAndVerifyAll()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Gateway.SetValue("c1", "alpha", FakePlayModeTuningGateway.FloatValue(5f));
            flow.Gateway.SetValue("c0", "zeta", FakePlayModeTuningGateway.FloatValue(1f));
            flow.Gateway.SetValue("c0", "alpha", FakePlayModeTuningGateway.FloatValue(2f));
            flow.Start(FakePlayModeTuningGateway.Selection("c1", "alpha"), FakePlayModeTuningGateway.Selection("c0", "zeta"), FakePlayModeTuningGateway.Selection("c0", "alpha"));
            flow.EnterPlay();
            flow.Gateway.SetValue("c1", "alpha", FakePlayModeTuningGateway.FloatValue(6f));
            flow.Gateway.SetValue("c0", "zeta", FakePlayModeTuningGateway.FloatValue(3f));
            flow.Gateway.SetValue("c0", "alpha", FakePlayModeTuningGateway.FloatValue(4f));
            flow.Capture();
            flow.ExitPlay();
            var plan = flow.Preview();
            Assert.That(plan.Changes.Select(item => item.TargetName + "|" + item.PropertyPath), Is.EqualTo(new[] { "c0|alpha", "c0|zeta", "c1|alpha" }));
            var result = flow.Operations.Apply(plan);
            Assert.That(result.ApplySucceeded, Is.True);
            Assert.That(flow.Gateway.FirstApplyOrder, Is.EqualTo(new[]
            {
                "GlobalObjectId_V1-2-c0|alpha",
                "GlobalObjectId_V1-2-c0|zeta",
                "GlobalObjectId_V1-2-c1|alpha"
            }));
            Assert.That(flow.Gateway.GetValue("c1", "alpha").EqualsExact(FakePlayModeTuningGateway.FloatValue(6f)), Is.True);
            Assert.That(flow.Gateway.GetValue("c0", "zeta").EqualsExact(FakePlayModeTuningGateway.FloatValue(3f)), Is.True);
            Assert.That(flow.Gateway.GetValue("c0", "alpha").EqualsExact(FakePlayModeTuningGateway.FloatValue(4f)), Is.True);
        }

        private static PlayModeTuningTestFlow ReadyForPreview(float baseline, float captured)
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(baseline));
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(captured));
            flow.Capture();
            flow.ExitPlay();
            return flow;
        }

        private static PlayModeTuningTestFlow ReadyPlan(float baseline, float captured, out PlayModeTuningPlan plan)
        {
            var flow = ReadyForPreview(baseline, captured);
            plan = flow.Preview();
            Assert.That(plan.IsReady, Is.True);
            return flow;
        }
    }
}
