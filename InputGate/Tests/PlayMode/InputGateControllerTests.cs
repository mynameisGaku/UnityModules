using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace InputGate.PlayMode.Tests
{
    /// <summary>PlayerInputの実Action Mapに対する入れ子停止、復元、所有競合、外部変更を検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class InputGateControllerTests
    {
        private GameObject _host;
        private InputActionAsset _asset;
        private PlayerInput _playerInput;
        private InputGateController _controller;
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _jump;
        private InputActionMap _ui;
        private InputAction _submit;

        /// <summary>各テスト前に静的所有者を消し、inactive host上へ設定済みControllerを作る。</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputGateController.ResetStaticOwners();
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _gameplay = _asset.AddActionMap("Gameplay");
            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _jump = _gameplay.AddAction("Jump", InputActionType.Button);
            _ui = _asset.AddActionMap("UI");
            _submit = _ui.AddAction("Submit", InputActionType.Button);

            _host = new GameObject("Input Gate Test Host");
            _host.SetActive(false);
            _playerInput = _host.AddComponent<PlayerInput>();
            _playerInput.actions = _asset;
            _controller = _host.AddComponent<InputGateController>();
            _controller.ConfigureForTests(_playerInput, new[] { "Gameplay" });
            _host.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(_controller.Status.IsReady, Is.True, _controller.Status.Error.ToString());
        }

        /// <summary>各テスト後にAction状態、Component、ScriptableObjectを確実に破棄する。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_asset != null) UnityEngine.Object.Destroy(_asset);
            yield return null;
            InputGateController.ResetStaticOwners();
        }

        /// <summary>最初の取得で対象Mapだけ停止し、最後の解放で部分有効状態まで正確に復元する。</summary>
        [UnityTest]
        public IEnumerator NestedLeases_StopGameplayOnly_AndRestorePartialBaseline()
        {
            _move.Enable();
            _submit.Enable();
            Assert.That(_jump.enabled, Is.False);

            Assert.That(_controller.TryAcquire(out var first, out var firstError), Is.True, firstError.ToString());
            Assert.That(_controller.TryAcquire(out var second, out var secondError), Is.True, secondError.ToString());

            Assert.That(_move.enabled, Is.False);
            Assert.That(_jump.enabled, Is.False);
            Assert.That(_submit.enabled, Is.True);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(2));

            first.Dispose();
            Assert.That(_move.enabled, Is.False);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(1));

            second.Dispose();
            Assert.That(_move.enabled, Is.True);
            Assert.That(_jump.enabled, Is.False);
            Assert.That(_submit.enabled, Is.True);
            Assert.That(_controller.Status.IsBlocking, Is.False);
            yield return null;
        }

        /// <summary>Controller無効化は健康な停止を復元し、全leaseを即座に無効化する。</summary>
        [UnityTest]
        public IEnumerator DisableController_RestoresBaselineAndInvalidatesLease()
        {
            _gameplay.Enable();
            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());

            _controller.enabled = false;

            Assert.That(lease.IsActive, Is.False);
            Assert.That(_move.enabled, Is.True);
            Assert.That(_jump.enabled, Is.True);
            Assert.That(_controller.Status.Error, Is.EqualTo(InputGateError.ControllerUnavailable));
            yield return null;
        }

        /// <summary>停止中の外部有効化はfail closedで検出し、その外部状態を上書きしない。</summary>
        [UnityTest]
        public IEnumerator ExternalEnable_FaultsAndPreservesExternalState()
        {
            _gameplay.Enable();
            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());

            _move.Enable();
            yield return null;

            Assert.That(_controller.Status.IsReady, Is.False);
            Assert.That(_controller.Status.Error, Is.EqualTo(InputGateError.ExternalActionStateChanged));
            Assert.That(lease.IsActive, Is.False);
            Assert.That(_move.enabled, Is.True);

            _controller.enabled = false;
            Assert.That(_move.enabled, Is.True);
        }

        /// <summary>worker Disposeはleaseを即無効化し、Action復元を次の主スレッドUpdateで1度だけ行う。</summary>
        [UnityTest]
        public IEnumerator WorkerDispose_DefersRestoreToControllerUpdate()
        {
            _gameplay.Enable();
            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());

            Task.Run(() => lease.Dispose()).GetAwaiter().GetResult();

            Assert.That(lease.IsActive, Is.False);
            Assert.That(_move.enabled, Is.False);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            yield return null;
            Assert.That(_move.enabled, Is.True);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.Zero);
        }

        /// <summary>状態通知中の再取得はBusyで拒否し、通知例外が後続通知と取得完了を妨げない。</summary>
        [UnityTest]
        public IEnumerator StatusCallback_ReentryIsBusy_AndExceptionIsIsolated()
        {
            _gameplay.Enable();
            var callbackError = InputGateError.None;
            var healthyObserverCalled = false;
            _controller.StatusChanged += status =>
            {
                if (!status.IsBlocking) return;
                Assert.That(_controller.TryAcquire(out _, out callbackError), Is.False);
                throw new InvalidOperationException("expected observer failure");
            };
            _controller.StatusChanged += status => healthyObserverCalled |= status.IsBlocking;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: expected observer failure");

            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());

            Assert.That(callbackError, Is.EqualTo(InputGateError.Busy));
            Assert.That(healthyObserverCalled, Is.True);
            lease.Dispose();
            yield return null;
        }

        /// <summary>通知中にControllerを無効化すると取得を失敗へ収束させ、停止前状態を復元する。</summary>
        [UnityTest]
        public IEnumerator StatusCallback_DisablesController_AcquisitionReturnsFailure()
        {
            _gameplay.Enable();
            _controller.StatusChanged += status =>
            {
                if (status.IsBlocking) _controller.enabled = false;
            };

            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.False);

            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(InputGateError.ControllerUnavailable));
            Assert.That(_move.enabled, Is.True);
            Assert.That(_controller.Status.IsBlocking, Is.False);
            yield return null;
        }

        /// <summary>Action停止が同期発火したcanceled callback内の無効化でも、古い停止状態を再通知しない。</summary>
        [UnityTest]
        public IEnumerator CanceledCallback_DisablesController_CleansBeforeAcquisitionReturns()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            _jump.AddBinding("<Gamepad>/buttonSouth").WithInteraction("hold(duration=10)");
            _gameplay.Enable();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(_jump.phase, Is.EqualTo(InputActionPhase.Started));
            var canceled = false;
            var blockingNotifications = 0;
            _controller.StatusChanged += status =>
            {
                if (status.IsBlocking) blockingNotifications++;
            };
            _jump.canceled += _ =>
            {
                canceled = true;
                _controller.enabled = false;
            };

            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.False);

            Assert.That(canceled, Is.True);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(InputGateError.ControllerUnavailable));
            Assert.That(blockingNotifications, Is.Zero);
            Assert.That(_controller.Status.IsBlocking, Is.False);
            Assert.That(_move.enabled, Is.True);
            Assert.That(_jump.enabled, Is.True);
            InputSystem.RemoveDevice(gamepad);
            yield return null;
        }

        /// <summary>Action停止callback内の無効化と再有効化は古い世代を失敗させ、次の取得用に新しい所有を準備する。</summary>
        [UnityTest]
        public IEnumerator CanceledCallback_DisablesAndReenablesController_RestartsWithFreshGeneration()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            _controller.enabled = false;
            _controller.ConfigureForTests(_playerInput, new[] { "Gameplay", "UI" });
            _controller.enabled = true;
            yield return null;
            _jump.AddBinding("<Gamepad>/buttonSouth").WithInteraction("hold(duration=10)");
            _gameplay.Enable();
            _ui.Enable();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(_jump.phase, Is.EqualTo(InputActionPhase.Started));
            var canceled = false;
            var uiDisableEvents = 0;
            Action<object, InputActionChange> actionChangeCallback = (target, change) =>
            {
                if (ReferenceEquals(target, _ui) && change == InputActionChange.ActionMapDisabled) uiDisableEvents++;
                if (ReferenceEquals(target, _submit) && change == InputActionChange.ActionDisabled) uiDisableEvents++;
            };
            InputSystem.onActionChange += actionChangeCallback;
            Action<InputAction.CallbackContext> callback = _ =>
            {
                canceled = true;
                _controller.enabled = false;
                _controller.enabled = true;
            };
            _jump.canceled += callback;

            Assert.That(_controller.TryAcquire(out var interruptedLease, out var interruptedError), Is.False);

            Assert.That(canceled, Is.True);
            Assert.That(interruptedLease, Is.Null);
            Assert.That(interruptedError, Is.EqualTo(InputGateError.ControllerUnavailable));
            Assert.That(_controller.enabled, Is.True);
            Assert.That(_controller.Status.IsReady, Is.True, _controller.Status.Error.ToString());
            Assert.That(_controller.Status.IsBlocking, Is.False);
            Assert.That(_move.enabled, Is.True);
            Assert.That(_jump.enabled, Is.True);
            Assert.That(_submit.enabled, Is.True);
            Assert.That(uiDisableEvents, Is.Zero, "lifecycle中断後に残りのUI Mapを停止してはいけません。");

            _jump.canceled -= callback;
            InputSystem.onActionChange -= actionChangeCallback;
            Assert.That(_controller.TryAcquire(out var nextLease, out var nextError), Is.True, nextError.ToString());
            Assert.That(nextLease.IsActive, Is.True);
            nextLease.Dispose();
            InputSystem.RemoveDevice(gamepad);
            yield return null;
        }

        /// <summary>Action停止callback内のAction Asset交換を取得成功として返さず、外部状態へ追従しない。</summary>
        [UnityTest]
        public IEnumerator CanceledCallback_ReplacesActionAsset_AcquisitionFaultsImmediately()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var replacement = UnityEngine.Object.Instantiate(_asset);
            _controller.enabled = false;
            _controller.ConfigureForTests(_playerInput, new[] { "Gameplay", "UI" });
            _controller.enabled = true;
            yield return null;
            _jump.AddBinding("<Gamepad>/buttonSouth").WithInteraction("hold(duration=10)");
            _gameplay.Enable();
            _ui.Enable();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(_jump.phase, Is.EqualTo(InputActionPhase.Started));
            _jump.canceled += _ =>
            {
                _playerInput.actions = replacement;
                _ui.Enable();
            };

            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.False);

            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(InputGateError.ExternalActionStateChanged));
            Assert.That(_controller.Status.IsReady, Is.False);
            Assert.That(_controller.Status.Error, Is.EqualTo(InputGateError.ExternalActionStateChanged));
            Assert.That(_submit.enabled, Is.True, "Action Asset交換後に残りの旧Mapを書き換えてはいけません。");
            _controller.enabled = false;
            _playerInput.actions = _asset;
            InputSystem.RemoveDevice(gamepad);
            UnityEngine.Object.Destroy(replacement);
            yield return null;
        }

        /// <summary>停止中のAction Asset交換直後に無効化しても、cleanupは旧Mapへ保存状態を書き戻さない。</summary>
        [UnityTest]
        public IEnumerator ActionAssetReplacementThenDisable_PreservesExternalOwnershipBoundary()
        {
            var replacement = UnityEngine.Object.Instantiate(_asset);
            _gameplay.Enable();
            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());
            Assert.That(_move.enabled, Is.False);

            _playerInput.actions = replacement;
            _controller.enabled = false;

            Assert.That(lease.IsActive, Is.False);
            Assert.That(_controller.Status.Error, Is.EqualTo(InputGateError.ExternalActionStateChanged));
            Assert.That(_move.enabled, Is.False, "外部所有へ切り替わった旧Mapをcleanupで復元してはいけません。");
            Assert.That(replacement.FindActionMap("Gameplay", true).enabled, Is.False);

            _playerInput.actions = _asset;
            UnityEngine.Object.Destroy(replacement);
            yield return null;
        }

        /// <summary>終了復元callbackから再有効化されても、Subsystem再初期化前に新しい所有を開始しない。</summary>
        [UnityTest]
        public IEnumerator ApplicationQuit_RestoreCallbackCannotRestartOwnership()
        {
            _move.Enable();
            Assert.That(_controller.TryAcquire(out var lease, out var error), Is.True, error.ToString());
            var callbackReached = false;
            Action<object, InputActionChange> callback = (target, change) =>
            {
                if (change != InputActionChange.ActionEnabled || !ReferenceEquals(target, _move)) return;
                callbackReached = true;
                _controller.enabled = false;
                _controller.enabled = true;
            };
            InputSystem.onActionChange += callback;

            _controller.gameObject.SendMessage("OnApplicationQuit", SendMessageOptions.RequireReceiver);

            InputSystem.onActionChange -= callback;
            Assert.That(callbackReached, Is.True);
            Assert.That(lease.IsActive, Is.False);
            Assert.That(_controller.Status.IsReady, Is.False);
            Assert.That(_controller.Status.IsBlocking, Is.False);
            Assert.That(_controller.Status.Error, Is.EqualTo(InputGateError.ApplicationExiting));
            Assert.That(_controller.TryAcquire(out var rejectedLease, out var rejectedError), Is.False);
            Assert.That(rejectedLease, Is.Null);
            Assert.That(rejectedError, Is.EqualTo(InputGateError.ApplicationExiting));

            InputGateController.ResetStaticOwners();
            yield return null;
        }

        /// <summary>有効なControllerへのworker取得要求はUnity状態へ触れずMainThreadRequiredを返す。</summary>
        [UnityTest]
        public IEnumerator WorkerAcquire_ReturnsMainThreadRequiredWithoutStateChange()
        {
            _gameplay.Enable();
            InputGateLease lease = null;
            var error = InputGateError.None;

            var acquired = Task.Run(() => _controller.TryAcquire(out lease, out error)).GetAwaiter().GetResult();

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(InputGateError.MainThreadRequired));
            Assert.That(_gameplay.enabled, Is.True);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.Zero);
            yield return null;
        }

        /// <summary>同じ実Action Mapを別Controllerが所有できず、状態を変更しない。</summary>
        [UnityTest]
        public IEnumerator SameRuntimeMap_SecondControllerReportsOwnershipConflict()
        {
            var otherHost = new GameObject("Conflicting Gate");
            otherHost.SetActive(false);
            var other = otherHost.AddComponent<InputGateController>();
            other.ConfigureForTests(_playerInput, new[] { "Gameplay" });
            otherHost.SetActive(true);
            yield return null;

            Assert.That(other.Status.IsReady, Is.False);
            Assert.That(other.Status.Error, Is.EqualTo(InputGateError.OwnerAlreadyExists));
            Assert.That(_gameplay.enabled, Is.False);
            UnityEngine.Object.Destroy(otherHost);
            yield return null;
        }

        /// <summary>map名が同じでも別Action Asset instanceなら別playerとして同時所有できる。</summary>
        [UnityTest]
        public IEnumerator DistinctRuntimeMapInstances_CanBeOwnedIndependently()
        {
            var otherAsset = UnityEngine.Object.Instantiate(_asset);
            var otherHost = new GameObject("Independent Gate");
            otherHost.SetActive(false);
            var otherPlayer = otherHost.AddComponent<PlayerInput>();
            otherPlayer.actions = otherAsset;
            var other = otherHost.AddComponent<InputGateController>();
            other.ConfigureForTests(otherPlayer, new[] { "Gameplay" });
            otherHost.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(other.Status.IsReady, Is.True, other.Status.Error.ToString());
            Assert.That(_controller.TryAcquire(out var first, out var firstError), Is.True, firstError.ToString());
            Assert.That(other.TryAcquire(out var second, out var secondError), Is.True, secondError.ToString());
            Assert.That(first.IsActive, Is.True);
            Assert.That(second.IsActive, Is.True);

            first.Dispose();
            second.Dispose();
            UnityEngine.Object.Destroy(otherHost);
            UnityEngine.Object.Destroy(otherAsset);
            yield return null;
        }

        /// <summary>inactive GameObject上でAwake前の取得要求はmain thread誤判定ではなく利用不可を返す。</summary>
        [UnityTest]
        public IEnumerator NeverActivatedController_ReturnsControllerUnavailable()
        {
            var inactive = new GameObject("Inactive Gate");
            inactive.SetActive(false);
            var controller = inactive.AddComponent<InputGateController>();

            Assert.That(controller.TryAcquire(out var lease, out var error), Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(error, Is.EqualTo(InputGateError.ControllerUnavailable));

            UnityEngine.Object.Destroy(inactive);
            yield return null;
        }
    }
}
