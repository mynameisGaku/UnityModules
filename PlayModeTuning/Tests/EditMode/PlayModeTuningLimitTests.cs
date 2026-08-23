using System.Linq;
using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningLimitTests
    {
        [Test]
        public void Exactly32ComponentsAreAccepted()
        {
            var flow = new PlayModeTuningTestFlow();
            var selections = Enumerable.Range(0, 32).Select(index => FakePlayModeTuningGateway.Selection("c" + index, "value")).ToArray();
            var result = flow.Operations.Start(selections);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Session.ComponentCount, Is.EqualTo(32));
        }

        [Test]
        public void Component33IsRejected()
        {
            var flow = new PlayModeTuningTestFlow();
            var selections = Enumerable.Range(0, 33).Select(index => FakePlayModeTuningGateway.Selection("c" + index, "value")).ToArray();
            var result = flow.Operations.Start(selections);
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.TooManyComponents));
        }

        [Test]
        public void Exactly256PropertiesAreAccepted()
        {
            var flow = new PlayModeTuningTestFlow();
            var selections = Enumerable.Range(0, 256).Select(index => FakePlayModeTuningGateway.Selection("c0", "value" + index)).ToArray();
            var result = flow.Operations.Start(selections);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Session.PropertyCount, Is.EqualTo(256));
        }

        [Test]
        public void Property257IsRejectedBeforeGatewayMutation()
        {
            var flow = new PlayModeTuningTestFlow();
            var selections = Enumerable.Range(0, 257).Select(index => FakePlayModeTuningGateway.Selection("c0", "value" + index)).ToArray();
            var result = flow.Operations.Start(selections);
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.TooManyProperties));
        }

        [Test]
        public void CombinedBaselineAndCaptureOver256KiBIsRejected()
        {
            var flow = new PlayModeTuningTestFlow();
            var selections = Enumerable.Range(0, 40).Select(index => FakePlayModeTuningGateway.Selection("c0", "text" + index)).ToArray();
            for (var index = 0; index < selections.Length; index++)
                flow.Gateway.SetValue("c0", "text" + index, FakePlayModeTuningGateway.TextValue(new string('a', PlayModeTuningValueCodec.MaximumStringUtf8Bytes)));
            flow.Start(selections);
            flow.EnterPlay();
            for (var index = 0; index < selections.Length; index++)
                flow.Gateway.SetValue("c0", "text" + index, FakePlayModeTuningGateway.TextValue(new string('b', PlayModeTuningValueCodec.MaximumStringUtf8Bytes)));
            var result = flow.Operations.CaptureDuringPlay(flow.SessionId);
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.PayloadTooLarge));
            Assert.That(result.Session.Phase, Is.EqualTo(PlayModeTuningPhase.Stale));
        }

        [Test]
        public void DuplicatePropertyIsRejected()
        {
            var flow = new PlayModeTuningTestFlow();
            var selection = FakePlayModeTuningGateway.Selection("c0", "speed");
            var result = flow.Operations.Start(new[] { selection, selection });
            Assert.That(result.Error, Is.EqualTo(PlayModeTuningError.DuplicateProperty));
        }

        [Test]
        public void SecondStartWhileActiveIsRejected()
        {
            var flow = new PlayModeTuningTestFlow();
            var first = flow.Operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "speed") });
            var second = flow.Operations.Start(new[] { FakePlayModeTuningGateway.Selection("c0", "height") });
            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Error, Is.EqualTo(PlayModeTuningError.WrongPhase));
        }
    }
}
