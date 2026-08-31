using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningLifecycleTests
    {
        [Test]
        public void DefaultDomainReloadRequiresChangedTokenOnPlayEntry()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var first = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            var started = first.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            var afterReload = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            afterReload.OnEnteredPlayMode();
            Assert.That(started.Succeeded, Is.True);
            Assert.That(afterReload.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Capturable));
        }

        [Test]
        public void DefaultDomainReloadRejectsUnchangedToken()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var operations = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            operations.OnEnteredPlayMode();
            Assert.That(operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.DomainReloadMismatch));
        }

        [Test]
        public void DisableDomainReloadRequiresUnchangedToken()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment(true) };
            var store = new FakePlayModeTuningSessionStore();
            var operations = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment(true);
            operations.OnEnteredPlayMode();
            Assert.That(operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Capturable));
        }

        [Test]
        public void DisableDomainReloadRejectsChangedToken()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment(true) };
            var store = new FakePlayModeTuningSessionStore();
            var first = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            first.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment(true);
            var changed = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            changed.OnEnteredPlayMode();
            Assert.That(changed.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.DomainReloadMismatch));
        }

        [Test]
        public void DisableSceneReloadIsRejectedAtStart()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment(false, true) };
            var operations = new PlayModeTuningOperations(gateway, new FakePlayModeTuningSessionStore(), new PlayModeTuningPlanRegistry(), "domain-a");
            var result = operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.DisableSceneReloadUnsupported));
        }

        [Test]
        public void DisableSceneReloadIsRejectedAgainAtPlayEntry()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var first = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            first.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment(false, true);
            var afterReload = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            afterReload.OnEnteredPlayMode();
            Assert.That(afterReload.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.DisableSceneReloadUnsupported));
        }

        [Test]
        public void LeavingPlayWithoutExplicitCaptureMarksSessionStale()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            flow.ExitPlay();
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
        }

        [Test]
        public void CapturedSessionBecomesReadyOnlyAfterPlayExit()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(2f));
            flow.Capture();
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Captured));
            flow.ExitPlay();
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.ReadyToPreview));
        }

        [Test]
        public void SessionStateDataSurvivesOperationsReplacement()
        {
            var gateway = new FakePlayModeTuningGateway();
            var store = new FakePlayModeTuningSessionStore();
            var first = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            var started = first.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            var second = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            Assert.That(second.GetCurrentSession().SessionId, Is.EqualTo(started.Session.SessionId));
            Assert.That(second.GetCurrentSession().PropertyCount, Is.EqualTo(1));
        }

        [Test]
        public void ResumeMarksCapturableSessionStaleWhenEditorAlreadyReturnedToEditMode()
        {
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var beforePlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            beforePlay.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            var inPlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            inPlay.OnEnteredPlayMode();
            Assert.That(inPlay.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Capturable));

            gateway.Environment = FakePlayModeTuningGateway.EditEnvironment();
            var replacement = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-c");
            replacement.ResumeLifecycle();
            Assert.That(replacement.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(replacement.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.StaleSession));
        }

        [Test]
        public void PlayEntryReadFailureIsVisibleAndCanRetryAfterStorageRecovers()
        {
            const string secretDetail = "PRIVATE_PLAY_ENTRY_LOAD_DO_NOT_DISPLAY";
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var beforePlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            beforePlay.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            var inPlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            store.LoadFailure = new InvalidOperationException(secretDetail);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));

            Assert.That(inPlay.OnEnteredPlayMode(), Is.False);

            store.LoadFailure = null;
            var visibleFailure = inPlay.GetCurrentSession();
            Assert.That(visibleFailure.Phase, Is.EqualTo(PlayModeTuningPhase.Armed));
            Assert.That(visibleFailure.Error, Is.EqualTo(PlayModeTuningError.SessionStorageFailed));
            Assert.That(visibleFailure.Message, Does.Not.Contain(secretDetail));
            Assert.That(inPlay.ResumeLifecycle(EPlayModeTuningObservedTransition.EnteredPlayMode), Is.True);
            Assert.That(inPlay.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Capturable));
            Assert.That(inPlay.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.None));
        }

        [Test]
        public void PlayEntrySaveFailureCanRetryWithoutStartingAnotherSession()
        {
            const string secretDetail = "PRIVATE_PLAY_ENTRY_SAVE_DO_NOT_DISPLAY";
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var beforePlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            beforePlay.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            var inPlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            store.SaveFailureCall = store.SaveCalls + 1;
            store.SaveFailureDetail = secretDetail;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));

            Assert.That(inPlay.OnEnteredPlayMode(), Is.False);
            Assert.That(inPlay.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Armed));
            Assert.That(inPlay.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionStorageFailed));
            Assert.That(inPlay.ResumeLifecycle(EPlayModeTuningObservedTransition.EnteredPlayMode), Is.True);
            Assert.That(inPlay.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Capturable));
        }

        [Test]
        public void EditEntryReadFailureIsVisibleAndCanRetryAfterStorageRecovers()
        {
            const string secretDetail = "PRIVATE_EDIT_ENTRY_LOAD_DO_NOT_DISPLAY";
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            flow.Capture();
            flow.Gateway.Environment = FakePlayModeTuningGateway.EditEnvironment();
            flow.Store.LoadFailure = new InvalidOperationException(secretDetail);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));

            Assert.That(flow.Operations.OnEnteredEditMode(), Is.False);

            flow.Store.LoadFailure = null;
            var visibleFailure = flow.Operations.GetCurrentSession();
            Assert.That(visibleFailure.Phase, Is.EqualTo(PlayModeTuningPhase.Captured));
            Assert.That(visibleFailure.Error, Is.EqualTo(PlayModeTuningError.SessionStorageFailed));
            Assert.That(visibleFailure.Message, Does.Not.Contain(secretDetail));
            Assert.That(flow.Operations.ResumeLifecycle(EPlayModeTuningObservedTransition.EnteredEditMode), Is.True);
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.ReadyToPreview));
        }

        [Test]
        public void MissedPlayEntryBecomesStaleWhenPlayAlreadyEndedBeforeRetry()
        {
            const string secretDetail = "PRIVATE_MISSED_PLAY_ENTRY_DO_NOT_DISPLAY";
            var gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment() };
            var store = new FakePlayModeTuningSessionStore();
            var beforePlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            beforePlay.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment();
            var inPlay = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-b");
            store.LoadFailure = new InvalidOperationException(secretDetail);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));
            Assert.That(inPlay.OnEnteredPlayMode(), Is.False);

            store.LoadFailure = null;
            gateway.Environment = FakePlayModeTuningGateway.EditEnvironment();
            Assert.That(inPlay.ResumeLifecycle(EPlayModeTuningObservedTransition.EnteredPlayMode), Is.True);
            Assert.That(inPlay.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(inPlay.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.StaleSession));
        }

        [Test]
        public void EditEntrySaveFailureCanRetryAfterStorageRecovers()
        {
            const string secretDetail = "PRIVATE_EDIT_ENTRY_SAVE_DO_NOT_DISPLAY";
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            flow.EnterPlay();
            flow.Capture();
            flow.Gateway.Environment = FakePlayModeTuningGateway.EditEnvironment();
            flow.Store.SaveFailureCall = flow.Store.SaveCalls + 1;
            flow.Store.SaveFailureDetail = secretDetail;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));

            Assert.That(flow.Operations.OnEnteredEditMode(), Is.False);
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.Captured));
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionStorageFailed));
            Assert.That(flow.Operations.ResumeLifecycle(EPlayModeTuningObservedTransition.EnteredEditMode), Is.True);
            Assert.That(flow.Operations.GetCurrentSession().Phase, Is.EqualTo(PlayModeTuningPhase.ReadyToPreview));
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.None));
        }
    }
}
