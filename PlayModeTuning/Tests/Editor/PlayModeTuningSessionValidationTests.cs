using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningSessionValidationTests
    {
        [Test]
        public void StoredSessionReadFailureHidesExceptionDetailFromResult()
        {
            const string secretDetail = "PRIVATE_PATH_DO_NOT_DISPLAY";
            var gateway = new FakePlayModeTuningGateway();
            var store = new FakePlayModeTuningSessionStore { LoadFailure = new InvalidOperationException(secretDetail) };
            var operations = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: " + secretDetail));

            var session = operations.GetCurrentSession();

            Assert.That(session.Error, Is.EqualTo(PlayModeTuningError.SessionStorageFailed));
            Assert.That(session.Message, Does.Not.Contain(secretDetail));
            Assert.That(session.Message, Does.Contain("コンソール"));
        }

        [Test]
        public void InvalidPhaseBecomesRecoverableStaleSession()
        {
            var flow = StartedFlow();
            var stored = flow.Store.Current;
            stored.phase = 999;
            flow.Store.Save(stored);
            var invalid = flow.Operations.GetCurrentSession();
            Assert.That(invalid.Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(invalid.Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
            Assert.That(flow.Operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") }).Succeeded, Is.True);
        }

        [Test]
        public void UnsupportedSchemaBecomesSessionDataInvalidWithoutThrowing()
        {
            var gateway = new FakePlayModeTuningGateway();
            var store = new FakePlayModeTuningSessionStore();
            store.Inject(new PlayModeTuningPersistedSession { schemaVersion = 999 });
            var operations = new PlayModeTuningOperations(gateway, store, new PlayModeTuningPlanRegistry(), "domain-a");
            var invalid = operations.GetCurrentSession();
            Assert.That(invalid.Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(invalid.Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        [Test]
        public void EmptyIdentityIsRejectedForActiveSession()
        {
            var flow = StartedFlow();
            var stored = flow.Store.Current;
            stored.sessionId = Guid.Empty.ToString("N");
            flow.Store.Save(stored);
            var invalid = flow.Operations.GetCurrentSession();
            Assert.That(invalid.Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
            Assert.That(invalid.Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        [Test]
        public void DuplicatePersistedPropertyIsRejected()
        {
            var flow = StartedFlow();
            var stored = flow.Store.Current;
            stored.properties.Add(stored.properties[0]);
            flow.Store.Save(stored);
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        [Test]
        public void ComponentSceneMismatchIsRejected()
        {
            var flow = StartedFlow();
            var stored = flow.Store.Current;
            stored.components[0].scenePath = "Assets/OtherScene.unity";
            flow.Store.Save(stored);
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        [Test]
        public void InvalidCapturedPayloadIsRejectedAfterCapture()
        {
            var flow = StartedFlow();
            flow.EnterPlay();
            flow.Gateway.SetValue("c0", "speed", FakePlayModeTuningGateway.FloatValue(2f));
            flow.Capture();
            var stored = flow.Store.Current;
            stored.properties[0].capturedPayload = "not-hex";
            flow.Store.Save(stored);
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        [Test]
        public void PersistedPayloadCannotBypassSessionByteLimit()
        {
            var flow = StartedFlow();
            var stored = flow.Store.Current;
            var source = stored.properties[0];
            var display = new string('a', PlayModeTuningValueCodec.MaximumStringUtf8Bytes);
            var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(display));
            stored.properties = new List<PlayModeTuningPropertyRecord>();
            for (var index = 0; index < PlayModeTuningOperations.MaximumProperties; index++)
                stored.properties.Add(StringProperty(source, "text" + index, payload, display));
            flow.Store.Save(stored);
            Assert.That(flow.Operations.GetCurrentSession().Error, Is.EqualTo(PlayModeTuningError.SessionDataInvalid));
        }

        private static PlayModeTuningTestFlow StartedFlow()
        {
            var flow = new PlayModeTuningTestFlow();
            flow.Start(FakePlayModeTuningGateway.Selection("c0", "speed"));
            return flow;
        }

        private static PlayModeTuningPropertyRecord StringProperty(PlayModeTuningPropertyRecord source, string propertyPath, string payload, string display)
        {
            return new PlayModeTuningPropertyRecord
            {
                componentKey = source.componentKey,
                globalObjectId = source.globalObjectId,
                sceneGuid = source.sceneGuid,
                scenePath = source.scenePath,
                scriptGuid = source.scriptGuid,
                typeName = source.typeName,
                targetName = source.targetName,
                propertyPath = propertyPath,
                propertyType = "String",
                numericType = string.Empty,
                baselineKind = (int)PlayModeTuningValueKind.String,
                baselinePayload = payload,
                baselineDisplay = display
            };
        }
    }
}
