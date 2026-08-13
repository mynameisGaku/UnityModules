using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TimeControl.Tests.PlayMode
{
    /// <summary>複数owner、無効化、破棄、scene解放、終了、再有効化でglobal値を安全に引き継ぐことを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TimeControlLifecycleTests
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

        /// <summary>後から有効になったControllerを待機させ、owner終了後の明示取得で所有を引き継ぐ。</summary>
        [Test]
        public void TwoOwners_FirstDisables_ConflictedControllerClaimsOnLaterAcquire()
        {
            var first = CreateController(0.75f, "First Time Control Owner");
            var second = CreateControllerWithoutChangingScale("Second Time Control Owner");
            Assert.That(second.IsControlling, Is.False);
            Assert.That(second.Status.Error, Is.EqualTo(TimeControlError.OwnerAlreadyExists));
            Assert.That(second.TryAcquire(0.5f, out var blockedLease, out var blockedError), Is.False);
            Assert.That(blockedLease, Is.Null);
            Assert.That(blockedError, Is.EqualTo(TimeControlError.OwnerAlreadyExists));

            first.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(second.TryAcquire(0.5f, out var takeoverLease, out var takeoverError), Is.True, takeoverError.ToString());

            Assert.That(takeoverLease, Is.Not.Null);
            Assert.That(second.IsControlling, Is.True);
            Assert.That(second.Status.BaselineTimeScale, Is.EqualTo(0.75f));
            Assert.That(Time.timeScale, Is.EqualTo(0.375f).Within(0.000001f));
        }

        /// <summary>無効化は全取得権を無効化し、外部変更がなければ正確な基準値へ戻す。</summary>
        [Test]
        public void Disable_HealthyOwner_InvalidatesLeasesAndRestoresBaseline()
        {
            var controller = CreateController(0.625f, "Disabled Time Control Owner");
            AssertAcquire(controller, 0f, out var pause);
            var fixedDeltaTime = Time.fixedDeltaTime;

            controller.enabled = false;

            Assert.That(pause.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.625f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDeltaTime));
            Assert.That(controller.IsControlling, Is.False);
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ControllerUnavailable));
        }

        /// <summary>破棄は全取得権を無効化し、外部変更がなければ正確な基準値へ戻す。</summary>
        [UnityTest]
        public IEnumerator Destroy_HealthyOwner_InvalidatesLeasesAndRestoresBaseline()
        {
            var controller = CreateController(0.7f, "Destroyed Time Control Owner");
            AssertAcquire(controller, 0.25f, out var lease);
            var host = controller.gameObject;

            UnityEngine.Object.Destroy(host);
            yield return null;

            Assert.That(lease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.7f));
        }

        /// <summary>一時sceneの解放でControllerが破棄されても基準値を復元する。</summary>
        [UnityTest]
        public IEnumerator SceneUnload_HealthyOwner_RestoresBaseline()
        {
            var scene = SceneManager.CreateScene("Time Control Temporary Scene");
            var controller = CreateController(0.9f, "Scene Time Control Owner");
            SceneManager.MoveGameObjectToScene(controller.gameObject, scene);
            AssertAcquire(controller, 0f, out var lease);

            var unload = SceneManager.UnloadSceneAsync(scene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;

            Assert.That(lease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.9f));
        }

        /// <summary>終了通知はApplicationExitingを残して基準値を戻し、再有効化時は終了状態を消して再取得できる。</summary>
        [Test]
        public void ApplicationQuitThenReEnable_ResetsExitStateAndAllowsNewGeneration()
        {
            var controller = CreateController(1f, "Reenabled Time Control Owner");
            AssertAcquire(controller, 0f, out var oldLease);

            controller.SendMessage("OnApplicationQuit", SendMessageOptions.RequireReceiver);

            Assert.That(oldLease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ApplicationExiting));
            Assert.That(controller.TryAcquire(0.5f, out _, out var exitingError), Is.False);
            Assert.That(exitingError, Is.EqualTo(TimeControlError.ApplicationExiting));

            controller.enabled = false;
            controller.enabled = true;
            Assert.That(controller.IsControlling, Is.True);
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.None));
            Assert.That(controller.TryAcquire(0.5f, out var newLease, out var newError), Is.True, newError.ToString());
            Assert.That(newLease, Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        /// <summary>終了直前の外部変更を上書きせず、終了理由はApplicationExitingとして公開する。</summary>
        [Test]
        public void ApplicationQuit_AfterExternalWrite_PreservesValueAndReportsExit()
        {
            var controller = CreateController(1f, "Exiting Changed Time Control Owner");
            AssertAcquire(controller, 0.5f, out var lease);
            Time.timeScale = 0.85f;

            controller.SendMessage("OnApplicationQuit", SendMessageOptions.RequireReceiver);

            Assert.That(lease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.85f));
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ApplicationExiting));
            Assert.That(controller.Status.EffectiveTimeScale, Is.EqualTo(0.85f));
        }

        /// <summary>外部変更で停止したownerは値を保護し、無効化と再有効化で外部値を新しい基準として取得し直す。</summary>
        [UnityTest]
        public IEnumerator FaultThenReEnable_CapturesPreservedExternalValueAsNewBaseline()
        {
            var controller = CreateController(1f, "Faulted Time Control Owner");
            AssertAcquire(controller, 0.5f, out var oldLease);
            Time.timeScale = 0.8f;
            yield return null;

            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
            Assert.That(oldLease.IsActive, Is.False);
            controller.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            controller.enabled = true;

            Assert.That(controller.IsControlling, Is.True);
            Assert.That(controller.Status.BaselineTimeScale, Is.EqualTo(0.8f));
            AssertAcquire(controller, 0.5f, out _);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.000001f));
        }

        /// <summary>以前の世代の取得権を後からDisposeしても、再有効化後の倍率へ影響しない。</summary>
        [Test]
        public void ReEnable_StaleLeaseDispose_DoesNotAffectNewGeneration()
        {
            var controller = CreateController(1f, "Generation Time Control Owner");
            AssertAcquire(controller, 0f, out var staleLease);
            controller.enabled = false;
            controller.enabled = true;
            AssertAcquire(controller, 0.5f, out var currentLease);

            Assert.DoesNotThrow(() => staleLease.Dispose());

            Assert.That(staleLease.IsActive, Is.False);
            Assert.That(currentLease.IsActive, Is.True);
            Assert.That(controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        /// <summary>終了処理直前の外部変更も基準復元で上書きしない。</summary>
        [Test]
        public void Disable_AfterExternalWrite_PreservesExternalValue()
        {
            var controller = CreateController(1f, "Externally Changed Time Control Owner");
            AssertAcquire(controller, 0.5f, out var lease);
            Time.timeScale = 0.85f;

            controller.enabled = false;

            Assert.That(lease.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.85f));
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.ExternalTimeScaleChanged));
        }

        /// <summary>SubsystemRegistration用初期化後に、新規Controllerが静的ownerを取得できる。</summary>
        [Test]
        public void StaticReset_BeforeOwnersExist_AllowsFreshOwner()
        {
            ResetStaticOwnerForTest();

            var controller = CreateController(1f, "Fresh Time Control Owner");

            Assert.That(controller.IsControlling, Is.True);
            Assert.That(controller.Status.Error, Is.EqualTo(TimeControlError.None));
        }

        private TimeControlController CreateController(float baselineTimeScale, string name)
        {
            Time.timeScale = baselineTimeScale;
            return CreateControllerWithoutChangingScale(name);
        }

        private TimeControlController CreateControllerWithoutChangingScale(string name)
        {
            var host = new GameObject(name);
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
