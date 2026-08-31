using System;
using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningDtoTests
    {
        [Test]
        public void PlanDefensivelyCopiesChangeCollection()
        {
            var source = new[] { new PlayModeTuningChange("target", "type", "path", PlayModeTuningValueKind.Float, "1", "2") };
            var plan = new PlayModeTuningPlan(PlayModeTuningError.None, string.Empty, Guid.NewGuid(), Guid.NewGuid(), "revision", source);
            source[0] = null;
            Assert.That(plan.Changes.Count, Is.EqualTo(1));
            Assert.That(plan.Changes[0], Is.Not.Null);
        }

        [Test]
        public void PropertySelectionNormalizesNullPath()
        {
            var selection = new PlayModeTuningPropertySelection(null, null);
            Assert.That(selection.PropertyPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SessionTerminalFlagMatchesCompletedAndStaleOnly()
        {
            Assert.That(new PlayModeTuningSession(Guid.NewGuid(), PlayModeTuningPhase.Completed, PlayModeTuningError.None, string.Empty, 1, 1).IsTerminal, Is.True);
            Assert.That(new PlayModeTuningSession(Guid.NewGuid(), PlayModeTuningPhase.Stale, PlayModeTuningError.StaleSession, string.Empty, 1, 1).IsTerminal, Is.True);
            Assert.That(new PlayModeTuningSession(Guid.NewGuid(), PlayModeTuningPhase.Previewed, PlayModeTuningError.None, string.Empty, 1, 1).IsTerminal, Is.False);
        }

        [Test]
        public void RegistryRejectsCopiedPlanWithSameNonceAndRevision()
        {
            var registry = new PlayModeTuningPlanRegistry();
            var sessionId = Guid.NewGuid();
            var nonce = Guid.NewGuid();
            var original = new PlayModeTuningPlan(PlayModeTuningError.None, string.Empty, sessionId, nonce, "revision", Array.Empty<PlayModeTuningChange>());
            var copy = new PlayModeTuningPlan(PlayModeTuningError.None, string.Empty, sessionId, nonce, "revision", Array.Empty<PlayModeTuningChange>());
            registry.Register(original);
            Assert.That(registry.TryConsume(copy), Is.EqualTo(PlayModeTuningError.StalePlan));
            Assert.That(registry.TryConsume(original), Is.EqualTo(PlayModeTuningError.None));
        }

        [Test]
        public void RegistryConsumesExactPlanOnlyOnce()
        {
            var registry = new PlayModeTuningPlanRegistry();
            var plan = new PlayModeTuningPlan(PlayModeTuningError.None, string.Empty, Guid.NewGuid(), Guid.NewGuid(), "revision", Array.Empty<PlayModeTuningChange>());
            registry.Register(plan);
            Assert.That(registry.TryConsume(plan), Is.EqualTo(PlayModeTuningError.None));
            Assert.That(registry.TryConsume(plan), Is.EqualTo(PlayModeTuningError.PlanAlreadyConsumed));
        }
    }
}
