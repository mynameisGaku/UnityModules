using NUnit.Framework;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class PlayModeTuningFingerprintTests
    {
        [Test]
        public void SameOrdinalTokensProduceSameSha256()
        {
            var first = PlayModeTuningFingerprint.Compute(new[] { "a", "b", "c" });
            var second = PlayModeTuningFingerprint.Compute(new[] { "a", "b", "c" });
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Length, Is.EqualTo(64));
        }

        [Test]
        public void LengthPrefixesPreventTokenBoundaryCollision()
        {
            var first = PlayModeTuningFingerprint.Compute(new[] { "ab", "c" });
            var second = PlayModeTuningFingerprint.Compute(new[] { "a", "bc" });
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void OrdinalOrderChangesRevision()
        {
            var first = PlayModeTuningFingerprint.Compute(new[] { "A", "a" });
            var second = PlayModeTuningFingerprint.Compute(new[] { "a", "A" });
            Assert.That(second, Is.Not.EqualTo(first));
        }
    }
}
