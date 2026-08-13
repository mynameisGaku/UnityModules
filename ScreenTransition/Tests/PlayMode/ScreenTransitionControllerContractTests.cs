using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScreenTransition.Tests.PlayMode
{
    /// <summary>Controllerの受理条件、通知境界、Unity寿命終了時の完了を検証する。</summary>
    public sealed class ScreenTransitionControllerTests
    {
        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        private readonly List<PanelSettings> _panelSettings = new List<PanelSettings>();

        /// <summary>各testが作ったUnity Objectを破棄する。</summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = _gameObjects.Count - 1; i >= 0; i--)
            {
                if (_gameObjects[i] != null) UnityEngine.Object.DestroyImmediate(_gameObjects[i]);
            }

            for (var i = _panelSettings.Count - 1; i >= 0; i--)
            {
                if (_panelSettings[i] != null) UnityEngine.Object.DestroyImmediate(_panelSettings[i]);
            }

            _gameObjects.Clear();
            _panelSettings.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>同時に届いた2件目をBusyで返し、最初の要求と表示状態を変えない。</summary>
        [Test]
        public async Task ExecuteAsync_WhileActive_ReturnsBusyWithoutChangingAcceptedRequest()
        {
            var controller = CreateController(true, out _);
            var firstAwaitable = controller.CoverAsync(Color.black, 10f, ScreenTransitionEasing.Linear);
            var acceptedStatus = controller.Status;

            var second = await controller.RevealAsync(Color.red, 1f);

            Assert.That(second.Error, Is.EqualTo(ScreenTransitionError.Busy));
            Assert.That(controller.Status.Request.Operation, Is.EqualTo(acceptedStatus.Request.Operation));
            Assert.That(controller.Status.Request.Color, Is.EqualTo(acceptedStatus.Request.Color));
            controller.enabled = false;
            var first = await firstAwaitable;
            Assert.That(first.Error, Is.EqualTo(ScreenTransitionError.ApplicationExiting));
        }

        /// <summary>durationが0の要求を同じ呼出内で完了し、完了後はBusyを解除する。</summary>
        [Test]
        public async Task ExecuteAsync_ZeroDuration_CompletesAndReturnsIdle()
        {
            var controller = CreateController(true, out _);

            var result = await controller.CoverAsync(new Color(0f, 0f, 0f, 0.75f), 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(controller.Status.Opacity, Is.EqualTo(0.75f).Within(0.00001f));
        }

        /// <summary>PanelSettingsが無い場合は要求を受理せずSurfaceUnavailableで返す。</summary>
        [Test]
        public async Task ExecuteAsync_WithoutPanelSettings_ReturnsSurfaceUnavailable()
        {
            var controller = CreateController(false, out _);
            var finishedCalls = 0;
            controller.Finished += _ => finishedCalls++;

            var result = await controller.CoverAsync(Color.black, 1f);

            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.SurfaceUnavailable));
            Assert.That(finishedCalls, Is.Zero);
            Assert.That(controller.IsBusy, Is.False);
        }

        /// <summary>不正要求は表示要素を変えずInvalidRequestで返す。</summary>
        [Test]
        public async Task ExecuteAsync_InvalidRequest_DoesNotTouchSurface()
        {
            var controller = CreateController(true, out var document);
            var overlay = document.rootVisualElement.Q<VisualElement>("screen-transition-overlay");
            var displayBefore = overlay.style.display;
            var colorBefore = overlay.style.backgroundColor;

            var result = await controller.CoverAsync(Color.red, float.NaN);

            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
            Assert.That(overlay.style.display, Is.EqualTo(displayBefore));
            Assert.That(overlay.style.backgroundColor, Is.EqualTo(colorBefore));
        }

        /// <summary>Finished通知から同じControllerへ再入した要求をBusyで返し、再帰実行しない。</summary>
        [Test]
        public async Task Finished_ReentrantExecute_ReturnsBusy()
        {
            var controller = CreateController(true, out _);
            Awaitable<ScreenTransitionResult> reentrantAwaitable = default;
            var captured = false;
            controller.Finished += _ =>
            {
                reentrantAwaitable = controller.RevealAsync(Color.black, 0f);
                captured = true;
            };

            var outer = await controller.CoverAsync(Color.black, 0f);
            var reentrant = await reentrantAwaitable;

            Assert.That(outer.IsSuccess, Is.True, outer.Message);
            Assert.That(captured, Is.True);
            Assert.That(reentrant.Error, Is.EqualTo(ScreenTransitionError.Busy));
        }

        /// <summary>Awaitable継続処理が次の要求を開始して例外を出しても、次の要求を前の失敗として上書きしない。</summary>
        [Test]
        public async Task AwaitableContinuation_StartsNextRequestThenThrows_PreservesNextRequest()
        {
            const string failureMessage = "screen-transition-awaitable-continuation";
            var controller = CreateController(true, out _);
            var firstAwaitable = controller.CoverAsync(Color.black, 0.001f, ScreenTransitionEasing.Linear);
            var firstAwaiter = firstAwaitable.GetAwaiter();
            var secondRequest = ScreenTransitionRequest.Reveal(new Color(0.8f, 0.1f, 0.2f, 0.6f), 0.05f, ScreenTransitionEasing.Linear);
            Awaitable<ScreenTransitionResult> secondAwaitable = default;
            ScreenTransitionResult firstResult = default;
            var continuationCalled = false;
            var secondWasActive = false;
            var loggedFailures = 0;
            void CountLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && condition.IndexOf(failureMessage, StringComparison.Ordinal) >= 0) loggedFailures++;
            }

            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CountLog;
            try
            {
                firstAwaiter.OnCompleted(() =>
                {
                    firstResult = firstAwaiter.GetResult();
                    secondAwaitable = controller.ExecuteAsync(secondRequest);
                    secondWasActive = controller.IsBusy &&
                                      controller.Status.Phase == ScreenTransitionPhase.Transitioning &&
                                      controller.Status.Request.Operation == secondRequest.Operation &&
                                      controller.Status.Request.Color == secondRequest.Color &&
                                      controller.Status.Request.Duration == secondRequest.Duration &&
                                      controller.Status.Request.Easing == secondRequest.Easing;
                    continuationCalled = true;
                    throw new InvalidOperationException(failureMessage);
                });

                for (var i = 0; i < 10 && !continuationCalled; i++) await Awaitable.NextFrameAsync();

                Assert.That(continuationCalled, Is.True, "最初の要求のAwaitable継続処理が呼ばれていません");
                Assert.That(firstResult.IsSuccess, Is.True, firstResult.Message);
                Assert.That(secondWasActive, Is.True, "継続処理から開始した要求が実行中として保持されていません");

                var secondResult = await secondAwaitable;

                Assert.That(secondResult.IsSuccess, Is.True, secondResult.Message);
                Assert.That(secondResult.Request.Operation, Is.EqualTo(secondRequest.Operation));
                Assert.That(secondResult.Request.Color, Is.EqualTo(secondRequest.Color));
                Assert.That(secondResult.Request.Duration, Is.EqualTo(secondRequest.Duration));
                Assert.That(secondResult.Request.Easing, Is.EqualTo(secondRequest.Easing));
                Assert.That(loggedFailures, Is.EqualTo(1));
                Assert.That(controller.IsBusy, Is.False);
                Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            }
            finally
            {
                Application.logMessageReceived -= CountLog;
            }
        }

        /// <summary>Finished通知内の無効化でsurfaceを外しても、完了復帰後のIdle不透明度を残さない。</summary>
        [Test]
        public async Task Finished_DisablesController_ClearsDetachedSurfaceStatus()
        {
            var controller = CreateController(true, out var document);
            controller.Finished += _ => controller.enabled = false;

            var result = await controller.CoverAsync(Color.black, 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(controller.enabled, Is.False);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(controller.Status.Opacity, Is.Zero);
            Assert.That(document.rootVisualElement.Q<VisualElement>("screen-transition-overlay"), Is.Null);
        }

        /// <summary>実行中の無効化では、Awaitableの継続を呼ぶ前にsurfaceを取り外す。</summary>
        [Test]
        public void Disable_ActiveRequest_DetachesSurfaceBeforeAwaitableContinuation()
        {
            var controller = CreateController(true, out var document);
            var operation = controller.CoverAsync(Color.black, 10f);
            var awaiter = operation.GetAwaiter();
            var continuationCalled = false;
            var surfaceWasPresent = true;
            var completedError = ScreenTransitionError.None;
            awaiter.OnCompleted(() =>
            {
                completedError = awaiter.GetResult().Error;
                surfaceWasPresent = document.rootVisualElement.Q<VisualElement>("screen-transition-overlay") != null;
                continuationCalled = true;
            });

            controller.enabled = false;

            Assert.That(continuationCalled, Is.True, "無効化で待機中の要求が完了していません");
            Assert.That(completedError, Is.EqualTo(ScreenTransitionError.ApplicationExiting));
            Assert.That(surfaceWasPresent, Is.False, "Awaitable継続より後までsurfaceが残っています");
        }

        /// <summary>状態通知中の寿命変更では古い状態を後続observerへ配信せず、通知中Busyを保つ。</summary>
        [Test]
        public async Task StatusChanged_DisablesController_StopsStaleSnapshotForLaterObservers()
        {
            var controller = CreateController(true, out _);
            var disabled = false;
            var laterPhases = new List<ScreenTransitionPhase>();
            controller.StatusChanged += status =>
            {
                if (disabled || status.Phase != ScreenTransitionPhase.Transitioning) return;
                disabled = true;
                controller.enabled = false;
            };
            controller.StatusChanged += status =>
            {
                Assert.That(status.Phase, Is.EqualTo(controller.Status.Phase));
                Assert.That(status.Progress, Is.EqualTo(controller.Status.Progress));
                Assert.That(status.Opacity, Is.EqualTo(controller.Status.Opacity));
                Assert.That(controller.IsBusy, Is.True, "状態通知中にBusyが解除されています");
                laterPhases.Add(status.Phase);
            };

            var result = await controller.CoverAsync(Color.black, 10f);

            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.ApplicationExiting));
            Assert.That(laterPhases, Is.EqualTo(new[] { ScreenTransitionPhase.Failed, ScreenTransitionPhase.Idle }));
            Assert.That(controller.IsBusy, Is.False);
        }

        /// <summary>最終Idle通知内でUIDocumentを無効化すると、古い不透明度を止めてsurfaceなしのIdleへ収束する。</summary>
        [Test]
        public async Task StatusChanged_FinalIdleDisablesDocument_NormalizesBeforeLaterObserversAndAwaitable()
        {
            var controller = CreateController(true, out var document);
            var laterIdleOpacities = new List<float>();
            controller.StatusChanged += status =>
            {
                if (status.Phase == ScreenTransitionPhase.Idle && status.Opacity > 0f) document.enabled = false;
            };
            controller.StatusChanged += status =>
            {
                if (status.Phase == ScreenTransitionPhase.Idle) laterIdleOpacities.Add(status.Opacity);
            };

            var result = await controller.CoverAsync(Color.black, 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(laterIdleOpacities, Is.EqualTo(new[] { 0f }));
            Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(controller.Status.Opacity, Is.Zero);
            var root = document.rootVisualElement;
            Assert.That(root == null || root.Q<VisualElement>("screen-transition-overlay") == null, Is.True);
            Assert.That(controller.IsBusy, Is.False);
        }

        /// <summary>最終Idle通知内でoverlayだけを外しても、後続observerへ古い不透明度を配信しない。</summary>
        [Test]
        public async Task StatusChanged_FinalIdleRemovesOverlay_NormalizesBeforeLaterObservers()
        {
            var controller = CreateController(true, out var document);
            var laterIdleOpacities = new List<float>();
            controller.StatusChanged += status =>
            {
                if (status.Phase != ScreenTransitionPhase.Idle || status.Opacity <= 0f) return;
                document.rootVisualElement.Q<VisualElement>("screen-transition-overlay")?.RemoveFromHierarchy();
            };
            controller.StatusChanged += status =>
            {
                if (status.Phase == ScreenTransitionPhase.Idle) laterIdleOpacities.Add(status.Opacity);
            };

            var result = await controller.CoverAsync(Color.black, 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(laterIdleOpacities, Is.EqualTo(new[] { 0f }));
            Assert.That(controller.Status.Opacity, Is.Zero);
            Assert.That(controller.IsBusy, Is.False);
        }

        /// <summary>Domain Reloadを無効にした再生でも、再有効化時に終了フラグを次の実行へ持ち越さない。</summary>
        [Test]
        public async Task ReEnable_AfterApplicationQuitCallback_CanExecuteNextRequest()
        {
            var controller = CreateController(true, out _);
            controller.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);
            controller.enabled = false;
            controller.enabled = true;

            var result = await controller.CoverAsync(Color.black, 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(controller.Status.Opacity, Is.EqualTo(1f));
        }

        /// <summary>1つの通知先が例外を出しても後続通知と要求完了を止めない。</summary>
        [Test]
        public async Task StatusChanged_ObserverException_IsolatedAndLoggedOnceDuringContinuousFailure()
        {
            var controller = CreateController(true, out _);
            var healthyCalls = 0;
            var loggedFailures = 0;
            void CountLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && condition.IndexOf("screen-transition-observer", StringComparison.Ordinal) >= 0) loggedFailures++;
            }

            controller.StatusChanged += _ => throw new InvalidOperationException("screen-transition-observer");
            controller.StatusChanged += _ => healthyCalls++;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CountLog;
            try
            {
                var result = await controller.CoverAsync(Color.black, 0f);

                Assert.That(result.IsSuccess, Is.True, result.Message);
                Assert.That(healthyCalls, Is.EqualTo(2));
                Assert.That(loggedFailures, Is.EqualTo(1));
            }
            finally
            {
                Application.logMessageReceived -= CountLog;
            }
        }

        /// <summary>Controller無効化は実行中要求をApplicationExitingで完了し、表示要素とBusyを残さない。</summary>
        [Test]
        public async Task Disable_ActiveRequest_CompletesAndRemovesSurface()
        {
            var controller = CreateController(true, out var document);
            var pending = controller.CoverAsync(Color.black, 10f);
            Assert.That(document.rootVisualElement.Q<VisualElement>("screen-transition-overlay"), Is.Not.Null);

            controller.enabled = false;
            var result = await pending;

            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.ApplicationExiting));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(controller.Status.Opacity, Is.Zero);
            Assert.That(document.rootVisualElement.Q<VisualElement>("screen-transition-overlay"), Is.Null);
        }

        /// <summary>Cover完了後の無効化でも、存在しない表示要素に合わせてIdle不透明度を0へ戻す。</summary>
        [Test]
        public async Task Disable_AfterCovered_ClearsDetachedSurfaceOpacity()
        {
            var controller = CreateController(true, out var document);
            Assert.That((await controller.CoverAsync(Color.black, 0f)).IsSuccess, Is.True);
            Assert.That(controller.Status.Opacity, Is.EqualTo(1f));

            controller.enabled = false;

            Assert.That(controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(controller.Status.Opacity, Is.Zero);
            Assert.That(document.rootVisualElement.Q<VisualElement>("screen-transition-overlay"), Is.Null);
        }

        /// <summary>無効化後に再有効化すると新しい表示要素を用意し、次の要求を処理できる。</summary>
        [Test]
        public async Task ReEnable_AfterDisable_CanExecuteNextRequest()
        {
            var controller = CreateController(true, out var document);
            controller.enabled = false;
            controller.enabled = true;
            Assert.That(controller.Status.Opacity, Is.Zero);

            var result = await controller.RevealAsync(Color.black, 0f);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(document.rootVisualElement.Q<VisualElement>("screen-transition-overlay"), Is.Not.Null);
        }

        /// <summary>early failureはFinishedを発生させず、受理済み要求だけを通知する。</summary>
        [Test]
        public async Task Finished_EarlyFailure_IsNotPublished()
        {
            var controller = CreateController(true, out _);
            var results = new List<ScreenTransitionResult>();
            controller.Finished += results.Add;

            var invalid = await controller.CoverAsync(Color.black, -1f);
            var success = await controller.CoverAsync(Color.black, 0f);

            Assert.That(invalid.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
            Assert.That(success.IsSuccess, Is.True, success.Message);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].IsSuccess, Is.True);
        }

        private ScreenTransitionController CreateController(bool assignPanelSettings, out UIDocument document)
        {
            var gameObject = new GameObject("ScreenTransitionControllerTests");
            gameObject.SetActive(false);
            _gameObjects.Add(gameObject);

            document = gameObject.AddComponent<UIDocument>();
            if (assignPanelSettings)
            {
                var settings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettings.Add(settings);
                document.panelSettings = settings;
            }

            var controller = gameObject.AddComponent<ScreenTransitionController>();
            gameObject.SetActive(true);
            return controller;
        }
    }
}
