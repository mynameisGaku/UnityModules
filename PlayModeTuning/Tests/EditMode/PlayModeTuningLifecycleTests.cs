using NUnit.Framework;

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
    }
}
