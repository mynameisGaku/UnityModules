using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace AudioControl.Tests
{
    public sealed class AudioControlGenerationTests
    {
        [Test]
        public void MainThreadDispose_ReleasesImmediatelyAndOnlyOnce()
        {
            var calls = 0;
            var generation = new AudioControlGeneration(Thread.CurrentThread.ManagedThreadId, _ => calls++);
            var token = new AudioControlToken(generation, 10, 128);

            token.Dispose();
            token.Dispose();

            Assert.That(token.IsActive, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void WorkerDispose_QueuesManagedReleaseUntilDrain()
        {
            var calls = 0;
            var generation = new AudioControlGeneration(Thread.CurrentThread.ManagedThreadId, _ => calls++);
            var token = new AudioControlToken(generation, 25, 64);
            var thread = new Thread(token.Dispose);

            thread.Start();
            thread.Join();
            var pending = new List<long>();
            generation.DrainPendingReleases(pending);

            Assert.That(token.IsActive, Is.False);
            Assert.That(calls, Is.Zero);
            Assert.That(pending, Is.EqualTo(new[] { 25L }));
        }

        [Test]
        public void ClosedGeneration_IgnoresStaleHandle()
        {
            var calls = 0;
            var generation = new AudioControlGeneration(Thread.CurrentThread.ManagedThreadId, _ => calls++);
            var token = new AudioControlToken(generation, 1, 128);
            generation.Close();

            token.Dispose();
            var pending = new List<long>();
            generation.DrainPendingReleases(pending);

            Assert.That(calls, Is.Zero);
            Assert.That(pending, Is.Empty);
        }
    }
}
