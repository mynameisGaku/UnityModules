using NUnit.Framework;

namespace AudioControl.Tests
{
    public sealed class AudioPlayRequestTests
    {
        [Test]
        public void Default_IsValidAndUsesDocumentedValues()
        {
            var request = AudioPlayRequest.Default;

            Assert.That(request.IsValid(), Is.True);
            Assert.That(request.Volume, Is.EqualTo(1f));
            Assert.That(request.Pitch, Is.EqualTo(1f));
            Assert.That(request.Loop, Is.False);
            Assert.That(request.FadeInSeconds, Is.EqualTo(0f));
            Assert.That(request.Priority, Is.EqualTo(128));
            Assert.That(request.AllowSteal, Is.True);
        }

        [TestCase(0f, 0.0001f, 0f, 0)]
        [TestCase(1f, 3f, 60f, 255)]
        public void BoundaryValues_AreAccepted(float volume, float pitch, float fade, int priority)
        {
            var request = new AudioPlayRequest(volume, pitch, true, fade, priority, false);

            Assert.That(request.IsValid(), Is.True);
        }

        [TestCase(-0.01f, 1f, 0f, 128)]
        [TestCase(1.01f, 1f, 0f, 128)]
        [TestCase(1f, 0f, 0f, 128)]
        [TestCase(1f, 3.01f, 0f, 128)]
        [TestCase(1f, 1f, -0.01f, 128)]
        [TestCase(1f, 1f, 60.01f, 128)]
        [TestCase(1f, 1f, 0f, -1)]
        [TestCase(1f, 1f, 0f, 256)]
        public void OutOfRangeValues_AreRejected(float volume, float pitch, float fade, int priority)
        {
            var request = new AudioPlayRequest(volume, pitch, false, fade, priority, true);

            Assert.That(request.IsValid(), Is.False);
        }

        [Test]
        public void NonFiniteValues_AreRejected()
        {
            Assert.That(new AudioPlayRequest(float.NaN, 1f, false, 0f, 1, true).IsValid(), Is.False);
            Assert.That(new AudioPlayRequest(1f, float.PositiveInfinity, false, 0f, 1, true).IsValid(), Is.False);
            Assert.That(new AudioPlayRequest(1f, 1f, false, float.NegativeInfinity, 1, true).IsValid(), Is.False);
        }
    }
}
