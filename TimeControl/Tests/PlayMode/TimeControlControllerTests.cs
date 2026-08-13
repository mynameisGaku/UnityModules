using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TimeControl.Tests.PlayMode
{
    /// <summary>実際のTime.timeScaleで、取得順序、通知、thread境界、外部変更を検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TimeControlControllerTests
    {
        private readonly List<GameObject> _hosts = new List<GameObject>();
        private float _originalTimeScale;
        private float _originalFixedDeltaTime;

        /// <summary>各検証前にglobal時間値を保存し、静的所有者を初期化する。</summary>
        [UnitySetUp]
        public IEnumerator SaveGlobalTimeState()
        {
            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            ResetStaticOwnerForTest();
            yield return null;
        }

        /// <summary>成否にかかわらず全Controllerを破棄し、global時間値を正確に戻す。</summary>
        [UnityTearDown]
        public IEnumerator RestoreGlobalTimeState()
        {
            for (var i = 0; i < _hosts.Count; i++)
            {
                if (_hosts[i] != null) UnityEngine.Object.Destroy(_hosts[i]);
            }

            yield return null;
            _hosts.Clear();
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            ResetStaticOwnerForTest();
        }

        /// <summary>重複を含む取得権を順不同で解放し、常に残存する最小倍率を適用する。</summary>
        [Test]
        public void AcquireAndDispose_NestedDuplicatePauseAndSpeedup_UseMinimumMultiplier()
        {
            var fixedDeltaTime = Time.fixedDeltaTime;
            var controller = CreateController(0.8f);

            AssertAcquire(controller, 2f, out var speedup);
            Assert.That(Time.timeScale, Is.EqualTo(1.6f).Within(0.000001f));
            AssertAcquire(controller, 0.5f, out var slow);
            AssertAcquire(controller, 0.5f, out var duplicateSlow);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.000001f));
            AssertAcquire(controller, 0f, out var pause);
            Assert.That(Time.timeScale, Is.Zero);

            duplicateSlow.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            pause.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.000001f));
            slow.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(1.6f).Within(0.000001f));
            speedup.Dispose();

            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(controller.Status.ActiveLeaseCount, Is.Zero);
            Assert.That(controller.Status.EffectiveMultiplier, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDeltaTime));
        }

        /// <summary>不正倍率と、停止要求で隠れていても単独時に上限を超える倍率を拒否する。</summary>
        [Test]
        public void TryAcquire_InvalidAndUnmaskedOverflow_ReturnsExplicitErrorsWithoutMutation()
        {
            var controller = CreateController(2f);
            AssertAcquire(controller, 0f, out var pause);

            Assert.That(controller.TryAcquire(float.NaN, out var invalidLease, out var invalidError), Is.False);
            Assert.That(invalidLease, Is.Null);
            Assert.That(invalidError, Is.EqualTo(TimeControlError.InvalidMultiplier));
            Assert.That(controller.TryAcquire(50.0001f, out var overflowLease, out var overflowError), Is.False);
            Assert.That(overflowLease, Is.Null);
            Assert.That(overflowError, Is.EqualTo(TimeControlError.EffectiveTimeScaleOutOfRange));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(controller.Status.ActiveLeaseCount, Is.EqualTo(1));

            pause.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        /// <summary>所有者の通知中に既存取得権を解放し、新規取得をBusyで拒否して解放状態を直後に反映する。</summary>
        [Test]
        public void StatusCallback_DisposeAndAcquire_ReleasesAfterOuterNotificationAndRejectsReentry()
        {
            var controller = CreateController(1f);
            AssertAcquire(controller, 0.25f, out var first);
            var snapshots = new List<TimeControlStatus>();
            var reentryError = TimeControlError.None;

            controller.StatusChanged += status =>
            {
                snapshots.Add(status);
                if (status.ActiveLeaseCount != 2) return;
                first.Dispose();
                Assert.That(controller.TryAcquire(0f, out _, out reentryError), Is.False);
            };

            AssertAcquire(controller, 0.5f, out var second);

            Assert.That(reentryError, Is.EqualTo(TimeControlError.Busy));
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            Assert.That(controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(controller.Status.EffectiveMultiplier, Is.EqualTo(0.5f));
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(snapshots.Exists(status => status.ActiveLeaseCount == 2 && status.EffectiveMultiplier == 0.25f), Is.True);
            Assert.That(snapshots.Exists(status => status.ActiveLeaseCount == 1 && status.EffectiveMultiplier == 0.5f), Is.True);
        }

        /// <summary>失敗する通知先を隔離し、後続通知先と取得処理を成功させる。</summary>
        [Test]
        public void StatusChanged_ObserverThrows_IsolatedFromLaterObservers()
        {
            var controller = CreateController(1f);
            var laterObserverCalled = false;
            controller.StatusChanged += _ => throw new InvalidOperationException("time-control-observer-failure");
            controller.StatusChanged += _ => laterObserverCalled = true;
            LogAssert.Expect(LogType.Exception, new Regex("time-control-observer-failure"));

            var acquired = controller.TryAcquire(0.5f, out var lease, out var error);

            Assert.That(acquired, Is.True, error.ToString());
            Assert.That(lease, Is.Not.Null);
            Assert.That(laterObserverCalled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        /// <summary>通知先が外部変更を起こした場合は、その取得呼出し自体を失敗として返し、古いsnapshotを後続へ渡さない。</summary>
        [Test]
        public void StatusCallback_ExternalWrite_FailsInitiatingAcquireAndStopsStaleDelivery()
        {
            var controller = CreateController(1f);
            var laterSnapshots = new List<TimeControlStatus>();
            controller.StatusChanged += status =>
            {
                if (status.IsControlling && status.ActiveLeaseCount == 1) Time.timeScale = 0.9f;
            };
            controller.StatusChanged += laterSnapshots.Add;

            var acquired = controller.TryAcquire(0.5f, out var lease, out var error);

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(Time.timeScale, Is.EqualTo(0.9f));
            Assert.That(controller.IsControlling, Is.False);
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(laterSnapshots.Count, Is.EqualTo(1));
            Assert.That(laterSnapshots[0].Error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(laterSnapshots[0].ActiveLeaseCount, Is.Zero);
        }

        /// <summary>取得通知中の無効化を同期終了として検出し、有効なleaseを呼出側へ返さない。</summary>
        [Test]
        public void StatusCallback_DisablesController_FailsInitiatingAcquire()
        {
            var controller = CreateController(1f);
            controller.StatusChanged += status =>
            {
                if (status.IsControlling && status.ActiveLeaseCount == 1) controller.enabled = false;
            };

            var acquired = controller.TryAcquire(0.5f, out var lease, out var error);

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(TimeControlError.ControllerUnavailable));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        /// <summary>取得通知中の即時破棄を同期終了として検出し、有効なleaseを呼出側へ返さない。</summary>
        [Test]
        public void StatusCallback_DestroysControllerImmediately_FailsInitiatingAcquire()
        {
            var controller = CreateController(1f);
            controller.StatusChanged += status =>
            {
                if (status.IsControlling && status.ActiveLeaseCount == 1) UnityEngine.Object.DestroyImmediate(controller.gameObject);
            };

            var acquired = controller.TryAcquire(0.5f, out var lease, out var error);

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(TimeControlError.ControllerUnavailable));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        /// <summary>frame間の外部変更をUpdateで検出し、外部値を維持して全取得権を無効化する。</summary>
        [UnityTest]
        public IEnumerator Update_ExternalWrite_FailsClosedAndPreservesExternalValue()
        {
            var controller = CreateController(1f);
            AssertAcquire(controller, 0.5f, out var lease);
            Time.timeScale = 0.8f;

            yield return null;

            Assert.That(controller.IsControlling, Is.False);
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(controller.Status.ActiveLeaseCount, Is.Zero);
            Assert.That(lease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
        }

        /// <summary>一度も有効化していないControllerはthread識別前に利用不可として拒否し、global時間値へ触れない。</summary>
        [Test]
        public void TryAcquire_NeverActivatedController_ReturnsUnavailableWithoutGlobalMutation()
        {
            Time.timeScale = 0.75f;
            var fixedDeltaTime = Time.fixedDeltaTime;
            var host = new GameObject("Inactive Time Control Test Host");
            host.SetActive(false);
            _hosts.Add(host);
            var controller = host.AddComponent<TimeControlController>();

            var acquired = controller.TryAcquire(0.5f, out var lease, out var error);

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(TimeControlError.ControllerUnavailable));
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDeltaTime));
        }

        /// <summary>workerからの取得要求をUnity APIへ触れる前に拒否する。</summary>
        [Test]
        public void TryAcquire_FromWorker_ReturnsMainThreadRequired()
        {
            var controller = CreateController(1f);
            var fixedDeltaTime = Time.fixedDeltaTime;
            Assert.That(controller.IsControlling, Is.True);
            Task<(bool Acquired, TimeScaleLease Lease, TimeControlError Error)> operation = Task.Run(() =>
            {
                var acquired = controller.TryAcquire(0.5f, out var lease, out var error);
                return (acquired, lease, error);
            });
            var result = operation.GetAwaiter().GetResult();

            Assert.That(result.Acquired, Is.False);
            Assert.That(result.Lease, Is.Null);
            Assert.That(result.Error, Is.EqualTo(TimeControlError.MainThreadRequired));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDeltaTime));
        }

        /// <summary>worker Disposeは即座に取得権を無効表示へ変え、次のUpdateでglobal値へ反映する。</summary>
        [UnityTest]
        public IEnumerator Dispose_FromWorker_QueuesUntilMainThreadUpdate()
        {
            var controller = CreateController(1f);
            AssertAcquire(controller, 0f, out var pause);
            var operation = Task.Run(() => pause.Dispose());
            operation.GetAwaiter().GetResult();

            Assert.That(pause.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(controller.Status.ActiveLeaseCount, Is.Zero);
        }

        /// <summary>同じframeにworkerから届いた複数解放を1回の状態変更へまとめる。</summary>
        [UnityTest]
        public IEnumerator DisposeBatch_FromWorkers_CoalescesIntoSingleUpdate()
        {
            var controller = CreateController(1f);
            AssertAcquire(controller, 0.25f, out var first);
            AssertAcquire(controller, 0.5f, out var second);
            AssertAcquire(controller, 0.75f, out var remaining);
            var notifications = 0;
            controller.StatusChanged += _ => notifications++;

            Task.WhenAll(Task.Run(() => first.Dispose()), Task.Run(() => second.Dispose())).GetAwaiter().GetResult();
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.False);
            Assert.That(remaining.IsActive, Is.True);
            Assert.That(notifications, Is.Zero);

            yield return null;

            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(controller.Status.EffectiveMultiplier, Is.EqualTo(0.75f));
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
        }

        private TimeControlController CreateController(float baselineTimeScale)
        {
            Time.timeScale = baselineTimeScale;
            var host = new GameObject("Time Control Test Host");
            host.SetActive(false);
            _hosts.Add(host);
            var controller = host.AddComponent<TimeControlController>();
            host.SetActive(true);
            return controller;
        }

        private static void AssertAcquire(TimeControlController controller, float multiplier, out TimeScaleLease lease)
        {
            var acquired = controller.TryAcquire(multiplier, out lease, out var error);
            Assert.That(acquired, Is.True, error.ToString());
            Assert.That(lease, Is.Not.Null);
            Assert.That(lease.Multiplier, Is.EqualTo(multiplier));
            Assert.That(lease.IsActive, Is.True);
        }

        private static void ResetStaticOwnerForTest()
        {
            var method = typeof(TimeControlController).GetMethod("ResetStaticOwner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "SubsystemRegistration用の静的所有者初期化が見つかりません");
            method.Invoke(null, null);
        }
    }
}
