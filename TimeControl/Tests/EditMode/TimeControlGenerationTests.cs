using System.Threading.Tasks;
using NUnit.Framework;

namespace TimeControl.Tests
{
    /// <summary>取得権の世代分離、重複解放、worker解放列をUnity APIなしで検証する。</summary>
    public sealed class TimeControlGenerationTests
    {
        /// <summary>Disposeは即座に無効表示へ変え、同じ取得権を複数回解放しても1件として扱う。</summary>
        [Test]
        public void Dispose_RepeatedCalls_QueuesSingleReleaseAndBecomesInactive()
        {
            var generation = new TimeControlGeneration(null);
            var leaseId = generation.Add(0.5f);
            var lease = new TimeScaleLease(generation, leaseId, 0.5f);

            lease.Dispose();
            lease.Dispose();

            Assert.That(lease.IsActive, Is.False);
            Assert.That(generation.DrainPending(out var multipliers), Is.True);
            Assert.That(multipliers, Is.Empty);
            Assert.That(generation.DrainPending(out _), Is.False);
        }

        /// <summary>workerからのDisposeもmanaged状態だけを変更し、主スレッド側でまとめて取得できる。</summary>
        [Test]
        public void Dispose_FromWorker_QueuesManagedRelease()
        {
            var generation = new TimeControlGeneration(null);
            var first = new TimeScaleLease(generation, generation.Add(0.25f), 0.25f);
            var second = new TimeScaleLease(generation, generation.Add(0.75f), 0.75f);

            Task.Run(() => first.Dispose()).GetAwaiter().GetResult();

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(generation.DrainPending(out var multipliers), Is.True);
            Assert.That(multipliers, Is.EqualTo(new[] { 0.75f }));
        }

        /// <summary>閉じた世代の取得権は無効となり、その後のDisposeが新しい状態へ影響しない。</summary>
        [Test]
        public void Close_StaleLease_CannotQueueRelease()
        {
            var generation = new TimeControlGeneration(null);
            var lease = new TimeScaleLease(generation, generation.Add(0f), 0f);

            generation.Close();
            lease.Dispose();

            Assert.That(lease.IsActive, Is.False);
            Assert.That(generation.ActiveLeaseCount, Is.Zero);
            Assert.That(generation.DrainPending(out var multipliers), Is.False);
            Assert.That(multipliers, Is.Empty);
        }

        /// <summary>同じ倍率の取得権も別識別子として数え、片方の解放後にもう片方を残す。</summary>
        [Test]
        public void DuplicateMultiplier_IndependentLeaseIds_RemainIndependent()
        {
            var generation = new TimeControlGeneration(null);
            var first = new TimeScaleLease(generation, generation.Add(0.5f), 0.5f);
            var second = new TimeScaleLease(generation, generation.Add(0.5f), 0.5f);

            first.Dispose();
            generation.DrainPending(out var multipliers);

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(generation.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(multipliers, Is.EqualTo(new[] { 0.5f }));
        }
    }
}
