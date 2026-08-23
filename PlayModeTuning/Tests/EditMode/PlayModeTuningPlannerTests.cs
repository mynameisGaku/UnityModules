using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningPlannerTests
    {
        [Test]
        public void ChangesAreOrderedByCanonicalPropertyIdentity()
        {
            var session = CreateSession(
                Property("b", "alpha", 1f, 3f),
                Property("a", "zeta", 2f, 4f),
                Property("a", "alpha", 3f, 5f));
            var plan = PlayModeTuningPlanner.Create(session, Guid.Parse("11111111-1111-1111-1111-111111111111"));
            Assert.That(plan.IsReady, Is.True);
            Assert.That(plan.Changes.Count, Is.EqualTo(3));
            Assert.That(plan.Changes[0].TargetName, Is.EqualTo("a"));
            Assert.That(plan.Changes[0].PropertyPath, Is.EqualTo("alpha"));
            Assert.That(plan.Changes[1].TargetName, Is.EqualTo("a"));
            Assert.That(plan.Changes[1].PropertyPath, Is.EqualTo("zeta"));
            Assert.That(plan.Changes[2].TargetName, Is.EqualTo("b"));
            Assert.That(plan.Changes[2].PropertyPath, Is.EqualTo("alpha"));
        }

        [Test]
        public void GatewaySnapshotUsesDirectIdentityOrderForReversedInput()
        {
            var bAlpha = Property("b", "alpha", 1f, 2f);
            var aZeta = Property("a", "zeta", 1f, 2f);
            var aAlpha = Property("a", "alpha", 1f, 2f);
            var snapshot = new PlayModeTuningGatewaySnapshot(
                new[]
                {
                    Snapshot(bAlpha),
                    Snapshot(aZeta),
                    Snapshot(aAlpha)
                },
                new[]
                {
                    new PlayModeTuningGatewayComponentSnapshot("b", "Assets/FakeScene.unity", "u-b"),
                    new PlayModeTuningGatewayComponentSnapshot("a", "Assets/FakeScene.unity", "u-a")
                });

            Assert.That(snapshot.Properties[0].Record.globalObjectId, Is.EqualTo("GlobalObjectId-a"));
            Assert.That(snapshot.Properties[0].Record.propertyPath, Is.EqualTo("alpha"));
            Assert.That(snapshot.Properties[1].Record.globalObjectId, Is.EqualTo("GlobalObjectId-a"));
            Assert.That(snapshot.Properties[1].Record.propertyPath, Is.EqualTo("zeta"));
            Assert.That(snapshot.Properties[2].Record.globalObjectId, Is.EqualTo("GlobalObjectId-b"));
            Assert.That(snapshot.Properties[2].Record.propertyPath, Is.EqualTo("alpha"));
            Assert.That(snapshot.Components[0].ComponentKey, Is.EqualTo("a"));
            Assert.That(snapshot.Components[1].ComponentKey, Is.EqualTo("b"));
        }

        [Test]
        public void SameStateAndNonceProduceSameRevision()
        {
            var nonce = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var first = PlayModeTuningPlanner.Create(CreateSession(Property("a", "speed", 1f, 2f)), nonce);
            var second = PlayModeTuningPlanner.Create(CreateSession(Property("a", "speed", 1f, 2f)), nonce);
            Assert.That(second.Revision, Is.EqualTo(first.Revision));
        }

        [Test]
        public void ReversedSelectionOrderProducesSameRevision()
        {
            var nonce = Guid.Parse("25252525-2525-2525-2525-252525252525");
            var first = PlayModeTuningPlanner.Create(CreateSession(
                Property("a", "alpha", 1f, 2f),
                Property("a", "zeta", 3f, 4f),
                Property("b", "alpha", 5f, 6f)), nonce);
            var reversed = PlayModeTuningPlanner.Create(CreateSession(
                Property("b", "alpha", 5f, 6f),
                Property("a", "zeta", 3f, 4f),
                Property("a", "alpha", 1f, 2f)), nonce);

            Assert.That(reversed.Revision, Is.EqualTo(first.Revision));
        }

        [Test]
        public void CapturedValueChangesRevision()
        {
            var nonce = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var first = PlayModeTuningPlanner.Create(CreateSession(Property("a", "speed", 1f, 2f)), nonce);
            var second = PlayModeTuningPlanner.Create(CreateSession(Property("a", "speed", 1f, 3f)), nonce);
            Assert.That(second.Revision, Is.Not.EqualTo(first.Revision));
        }

        [Test]
        public void NoDifferenceReturnsNoChanges()
        {
            var plan = PlayModeTuningPlanner.Create(CreateSession(Property("a", "speed", 1f, 1f)), Guid.NewGuid());
            Assert.That(plan.Error, Is.EqualTo(PlayModeTuningError.NoChanges));
            Assert.That(plan.Changes, Is.Empty);
        }

        [Test]
        public void CompositePreviewUsesDistinctRoundTripComponentText()
        {
            var property = new PlayModeTuningPropertyRecord
            {
                componentKey = "a",
                targetName = "a",
                typeName = "FakeType",
                propertyPath = "offset",
                propertyType = "Vector3",
                numericType = string.Empty,
                baselineKind = (int)PlayModeTuningValueKind.Vector3,
                baselinePayload = Vector3Payload(1.001f, 0f, 0f),
                baselineDisplay = "same-rounded-text",
                capturedKind = (int)PlayModeTuningValueKind.Vector3,
                capturedPayload = Vector3Payload(1.002f, 0f, 0f),
                capturedDisplay = "same-rounded-text"
            };
            var plan = PlayModeTuningPlanner.Create(CreateSession(property), Guid.Parse("44444444-4444-4444-4444-444444444444"));
            Assert.That(plan.IsReady, Is.True);
            Assert.That(plan.Changes[0].BeforeValue, Is.Not.EqualTo(plan.Changes[0].AfterValue));
            Assert.That(plan.Changes[0].BeforeValue, Does.StartWith("1.001"));
            Assert.That(plan.Changes[0].AfterValue, Does.StartWith("1.002"));
        }

        private static PlayModeTuningPersistedSession CreateSession(params PlayModeTuningPropertyRecord[] properties)
        {
            return new PlayModeTuningPersistedSession
            {
                sessionId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                properties = new List<PlayModeTuningPropertyRecord>(properties),
                components = new List<PlayModeTuningComponentRecord>
                {
                    new PlayModeTuningComponentRecord { componentKey = "a", baselineUnselectedFingerprint = "u-a" },
                    new PlayModeTuningComponentRecord { componentKey = "b", baselineUnselectedFingerprint = "u-b" }
                }
            };
        }

        private static PlayModeTuningPropertyRecord Property(string component, string path, float before, float after)
        {
            return new PlayModeTuningPropertyRecord
            {
                componentKey = component,
                globalObjectId = "GlobalObjectId-" + component,
                targetName = component,
                typeName = "FakeType",
                propertyPath = path,
                propertyType = "Float",
                numericType = "Float",
                baselineKind = (int)PlayModeTuningValueKind.Float,
                baselinePayload = PlayModeTuningValueCodec.EncodeFloat(before),
                baselineDisplay = before.ToString(),
                capturedKind = (int)PlayModeTuningValueKind.Float,
                capturedPayload = PlayModeTuningValueCodec.EncodeFloat(after),
                capturedDisplay = after.ToString()
            };
        }

        private static PlayModeTuningGatewayPropertySnapshot Snapshot(PlayModeTuningPropertyRecord record)
        {
            return new PlayModeTuningGatewayPropertySnapshot(record, record.Captured);
        }

        private static string Vector3Payload(float x, float y, float z)
        {
            return string.Join(",", PlayModeTuningValueCodec.EncodeFloat(x), PlayModeTuningValueCodec.EncodeFloat(y), PlayModeTuningValueCodec.EncodeFloat(z));
        }
    }
}
