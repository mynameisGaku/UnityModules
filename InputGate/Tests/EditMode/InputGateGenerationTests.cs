using System.Threading.Tasks;
using NUnit.Framework;

namespace InputGate.Tests
{
    /// <summary>取得権の世代分離、重複解放、worker解放列をUnity APIなしで検証する。</summary>
    public sealed class InputGateGenerationTests
    {
        /// <summary>Disposeは即座に無効表示へ変え、重複呼出しを1件として処理する。</summary>
        [Test]
        public void Dispose_RepeatedCalls_QueuesSingleRelease()
        {
            var generation = new InputGateGeneration(null);
            var lease = new InputGateLease(generation, generation.Add());

            lease.Dispose();
            lease.Dispose();

            Assert.That(lease.IsActive, Is.False);
            Assert.That(generation.DrainPending(out var count), Is.True);
            Assert.That(count, Is.Zero);
            Assert.That(generation.DrainPending(out count), Is.False);
            Assert.That(count, Is.Zero);
        }

        /// <summary>workerからのDisposeはmanaged待機列だけを変更し、複数件をまとめて反映できる。</summary>
        [Test]
        public void Dispose_FromWorker_QueuesManagedRelease()
        {
            var generation = new InputGateGeneration(null);
            var first = new InputGateLease(generation, generation.Add());
            var second = new InputGateLease(generation, generation.Add());

            Task.Run(() => first.Dispose()).GetAwaiter().GetResult();

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(generation.DrainPending(out var count), Is.True);
            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>閉じた世代の古いleaseは、その後の新しい所有世代へ影響しない。</summary>
        [Test]
        public void Close_StaleLease_CannotReleaseNewGeneration()
        {
            var oldGeneration = new InputGateGeneration(null);
            var oldLease = new InputGateLease(oldGeneration, oldGeneration.Add());
            oldGeneration.Close();
            var newGeneration = new InputGateGeneration(null);
            var newLease = new InputGateLease(newGeneration, newGeneration.Add());

            oldLease.Dispose();

            Assert.That(oldLease.IsActive, Is.False);
            Assert.That(newLease.IsActive, Is.True);
            Assert.That(newGeneration.DrainPending(out var count), Is.False);
            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>同じ世代の取得権は別識別子で数え、片方の解放後も残りを維持する。</summary>
        [Test]
        public void MultipleLeases_RemainIndependent()
        {
            var generation = new InputGateGeneration(null);
            var first = new InputGateLease(generation, generation.Add());
            var second = new InputGateLease(generation, generation.Add());

            first.Dispose();
            generation.DrainPending(out var count);

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(generation.ActiveLeaseCount, Is.EqualTo(1));
        }
    }
}
