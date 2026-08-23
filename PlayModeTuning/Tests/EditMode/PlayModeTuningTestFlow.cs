using System;

namespace PlayModeTuning.Editor.Tests
{
    internal sealed class PlayModeTuningTestFlow
    {
        internal PlayModeTuningTestFlow(bool disableDomainReload = false)
        {
            Gateway = new FakePlayModeTuningGateway { Environment = FakePlayModeTuningGateway.EditEnvironment(disableDomainReload) };
            Store = new FakePlayModeTuningSessionStore();
            Registry = new PlayModeTuningPlanRegistry();
            Operations = new PlayModeTuningOperations(Gateway, Store, Registry, "domain-a");
            DisableDomainReload = disableDomainReload;
        }

        internal FakePlayModeTuningGateway Gateway { get; }
        internal FakePlayModeTuningSessionStore Store { get; }
        internal PlayModeTuningPlanRegistry Registry { get; }
        internal PlayModeTuningOperations Operations { get; private set; }
        internal bool DisableDomainReload { get; }
        internal Guid SessionId { get; private set; }

        internal void Start(params PlayModeTuningPropertySelection[] selections)
        {
            var result = Operations.Start(selections);
            if (!result.Succeeded)
                throw new InvalidOperationException(result.Error + ": " + result.Message);
            SessionId = result.Session.SessionId;
        }

        internal void EnterPlay()
        {
            Gateway.Environment = FakePlayModeTuningGateway.PlayEnvironment(DisableDomainReload);
            if (!DisableDomainReload)
                Operations = new PlayModeTuningOperations(Gateway, Store, Registry, "domain-b");
            Operations.OnEnteredPlayMode();
        }

        internal void Capture()
        {
            var result = Operations.CaptureDuringPlay(SessionId);
            if (!result.Succeeded)
                throw new InvalidOperationException(result.Error + ": " + result.Message);
        }

        internal void ExitPlay()
        {
            var session = Store.Current;
            foreach (var property in session.properties)
                Gateway.SetValue(property.targetName, property.propertyPath, property.Baseline);
            Gateway.Environment = FakePlayModeTuningGateway.EditEnvironment(DisableDomainReload);
            Operations.OnEnteredEditMode();
        }

        internal PlayModeTuningPlan Preview()
        {
            return Operations.PreviewAfterPlay(SessionId);
        }
    }
}
