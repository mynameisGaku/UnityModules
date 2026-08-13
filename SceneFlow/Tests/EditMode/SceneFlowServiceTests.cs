using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneFlow.Tests
{
    /// <summary>偽のScene状態を使い、SceneFlowServiceの事前条件、直列化、完了後検査を確かめる。</summary>
    public sealed class SceneFlowServiceTests
    {
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string GameplayPath = "Assets/Scenes/Gameplay.unity";

        /// <summary>AssetsとPackagesの完全パスだけを受け付け、短名を拒否する。</summary>
        [Test]
        public void SceneReference_AcceptsOnlyFullProjectScenePaths()
        {
            Assert.That(new SceneReference(BootstrapPath).IsValid, Is.True);
            Assert.That(new SceneReference("Packages/com.example.flow/Samples~/Demo.unity").IsValid, Is.True);
            Assert.That(new SceneReference("Gameplay").IsValid, Is.False);
            Assert.That(new SceneReference("Scenes/Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets/../Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Packages/../Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets/./Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets//Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets/Scenes//Gameplay.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets/.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Packages/.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Packages/com.example.flow/.unity").IsValid, Is.False);
            Assert.That(new SceneReference("Assets/Scenes/Gameplay.prefab").IsValid, Is.False);
        }

        /// <summary>追加読込の進捗を単調に通知し、完了後に対象SceneとScene数を確認する。</summary>
        [Test]
        public async Task LoadAdditiveAsync_ReportsMonotonicProgressAndConfirmsPostcondition()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.1f, 0.45f, 0.9f },
                () => backend.Loaded.Add(path));
            var service = new SceneFlowService(backend);
            var statuses = new List<SceneFlowStatus>();
            service.StatusChanged += statuses.Add;

            var result = await service.LoadAdditiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
            Assert.That(backend.LoadCalls, Is.EqualTo(1));
            Assert.That(statuses.Exists(status => status.Phase == SceneFlowPhase.Loading && status.Progress > 0f), Is.True);
            Assert.That(statuses.Exists(status => status.Phase == SceneFlowPhase.Verifying && status.Progress == 1f), Is.True);
            AssertMonotonicLoadingProgress(statuses);
        }

        /// <summary>処理中の2件目をBusyで返し、backendへ2件目を渡さない。</summary>
        [Test]
        public async Task ExecuteAsync_ConcurrentRequestReturnsBusyWithoutCallingBackend()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.2f, 0.9f },
                () => backend.Loaded.Add(path));
            backend.PauseNextFrame = true;
            var service = new SceneFlowService(backend);
            var finished = 0;
            service.Finished += _ => finished++;

            var first = service.LoadAdditiveAsync(new SceneReference(GameplayPath));
            var second = await service.SetActiveAsync(new SceneReference(BootstrapPath));
            backend.ReleasePausedFrame();
            var firstResult = await first;

            Assert.That(second.IsSuccess, Is.False);
            Assert.That(second.Error, Is.EqualTo(SceneFlowError.Busy));
            Assert.That(firstResult.IsSuccess, Is.True, firstResult.Message);
            Assert.That(backend.LoadCalls, Is.EqualTo(1));
            Assert.That(backend.SetActiveCalls, Is.Zero);
            Assert.That(finished, Is.EqualTo(1), "Busyの早期結果はFinishedへ通知しない");
        }

        /// <summary>terminalとFinishedの通知中に再入してもBusyを返し、外側のterminal状態を変えない。</summary>
        [Test]
        public async Task ExecuteAsync_TerminalCallbackReentryReturnsBusyWithoutChangingState()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.ActivePath = BootstrapPath;
            var service = new SceneFlowService(backend);
            var terminalReentry = default(SceneFlowResult);
            var finishedReentry = default(SceneFlowResult);
            var terminalPhaseAfterReentry = default(SceneFlowPhase);
            var finishedPhaseAfterReentry = default(SceneFlowPhase);
            var finishedCalls = 0;

            service.StatusChanged += status =>
            {
                if (status.Phase != SceneFlowPhase.Completed) return;
                terminalReentry = service.SetActiveAsync(new SceneReference(BootstrapPath)).GetAwaiter().GetResult();
                terminalPhaseAfterReentry = service.Status.Phase;
            };
            service.Finished += _ =>
            {
                finishedCalls++;
                finishedReentry = service.SetActiveAsync(new SceneReference(BootstrapPath)).GetAwaiter().GetResult();
                finishedPhaseAfterReentry = service.Status.Phase;
            };

            var result = await service.SetActiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(terminalReentry.Error, Is.EqualTo(SceneFlowError.Busy));
            Assert.That(finishedReentry.Error, Is.EqualTo(SceneFlowError.Busy));
            Assert.That(terminalPhaseAfterReentry, Is.EqualTo(SceneFlowPhase.Completed));
            Assert.That(finishedPhaseAfterReentry, Is.EqualTo(SceneFlowPhase.Completed));
            Assert.That(finishedCalls, Is.EqualTo(1), "再入BusyはFinishedへ通知しない");
            Assert.That(backend.SetActiveCalls, Is.EqualTo(1), "再入要求はbackendへ到達しない");
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
        }

        /// <summary>Idle通知から毎回同期要求を試みてもBusyで返し、再帰せず後続observerへ正しいIdleを渡す。</summary>
        [Test]
        public async Task ExecuteAsync_IdleCallbackReentryReturnsBusyWithoutRecursion()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);
            var reentryAttempts = 0;
            var reentryResult = default(SceneFlowResult);
            var secondObserverIdleCalls = 0;
            var inconsistentStatusCalls = 0;

            service.StatusChanged += status =>
            {
                if (status.Phase != SceneFlowPhase.Idle) return;
                reentryAttempts++;
                reentryResult = service.SetActiveAsync(new SceneReference(GameplayPath)).GetAwaiter().GetResult();
            };
            service.StatusChanged += status =>
            {
                var current = service.Status;
                if (current.Phase != status.Phase ||
                    current.Request.Operation != status.Request.Operation ||
                    !PathEquals(current.Request.Scene.Path, status.Request.Scene.Path) ||
                    current.Progress != status.Progress ||
                    service.IsBusy != status.IsBusy)
                {
                    inconsistentStatusCalls++;
                }

                if (status.Phase == SceneFlowPhase.Idle) secondObserverIdleCalls++;
            };

            var firstResult = await service.SetActiveAsync(new SceneReference(BootstrapPath));

            Assert.That(firstResult.IsSuccess, Is.True, firstResult.Message);
            Assert.That(reentryAttempts, Is.EqualTo(1), "Idle callbackを再帰しない");
            Assert.That(reentryResult.Error, Is.EqualTo(SceneFlowError.Busy));
            Assert.That(secondObserverIdleCalls, Is.EqualTo(1), "後続observerへ現在のIdleを通知する");
            Assert.That(inconsistentStatusCalls, Is.Zero, "callback引数とserviceの現在状態を一致させる");
            Assert.That(backend.SetActiveCalls, Is.Zero, "再入要求はbackendへ到達しない");
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
        }

        /// <summary>各backend境界の例外をOperationFailedへ変換し、terminal通知後にBusyを解除する。</summary>
        [TestCase(BackendFailurePoint.CanLoad)]
        [TestCase(BackendFailurePoint.Snapshot)]
        [TestCase(BackendFailurePoint.CountLoaded)]
        [TestCase(BackendFailurePoint.Load)]
        [TestCase(BackendFailurePoint.Unload)]
        [TestCase(BackendFailurePoint.SetActive)]
        [TestCase(BackendFailurePoint.NextFrame)]
        [TestCase(BackendFailurePoint.IsMainThread)]
        [TestCase(BackendFailurePoint.ExitToken)]
        [TestCase(BackendFailurePoint.LoadedSceneCount)]
        [TestCase(BackendFailurePoint.IsActive)]
        [TestCase(BackendFailurePoint.OperationIsDone)]
        [TestCase(BackendFailurePoint.OperationProgress)]
        public async Task ExecuteAsync_BackendExceptionReturnsOperationFailedAndCompletesNotifications(BackendFailurePoint failurePoint)
        {
            FakeBackend backend;
            SceneFlowRequest request;
            if (failurePoint == BackendFailurePoint.Unload ||
                failurePoint == BackendFailurePoint.LoadedSceneCount ||
                failurePoint == BackendFailurePoint.IsActive)
            {
                backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
                backend.ActivePath = BootstrapPath;
                request = SceneFlowRequest.Unload(new SceneReference(GameplayPath));
            }
            else if (failurePoint == BackendFailurePoint.SetActive)
            {
                backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
                backend.ActivePath = BootstrapPath;
                request = SceneFlowRequest.SetActive(new SceneReference(GameplayPath));
            }
            else
            {
                backend = FakeBackend.WithLoaded(BootstrapPath);
                backend.Loadable.Add(GameplayPath);
                backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                    new[] { 0.2f, 0.9f },
                    () => backend.Loaded.Add(path));
                request = SceneFlowRequest.LoadAdditive(new SceneReference(GameplayPath));
            }

            backend.FailurePoint = failurePoint;
            var service = new SceneFlowService(backend);
            var statuses = new List<SceneFlowStatus>();
            var finishedCalls = 0;
            var phaseDuringFinished = default(SceneFlowPhase);
            service.StatusChanged += statuses.Add;
            service.Finished += _ =>
            {
                finishedCalls++;
                phaseDuringFinished = service.Status.Phase;
            };

            var result = await service.ExecuteAsync(request);

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.OperationFailed));
            Assert.That(statuses.Exists(status => status.Phase == SceneFlowPhase.Failed), Is.True);
            Assert.That(finishedCalls, Is.EqualTo(1));
            Assert.That(phaseDuringFinished, Is.EqualTo(SceneFlowPhase.Failed));
            Assert.That(service.IsBusy, Is.False);
            Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
        }

        /// <summary>壊れた通知先が後続通知、完了結果、Busy解除を止めない。</summary>
        [Test]
        public async Task ExecuteAsync_ObserverExceptionsAreIsolated()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);
            var statusCalls = 0;
            var finishedCalls = 0;
            service.StatusChanged += _ => throw new InvalidOperationException("status failed");
            service.StatusChanged += _ => statusCalls++;
            service.Finished += _ => throw new InvalidOperationException("finished failed");
            service.Finished += _ => finishedCalls++;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                var result = await service.SetActiveAsync(new SceneReference(BootstrapPath));

                Assert.That(result.IsSuccess, Is.True, result.Message);
                Assert.That(statusCalls, Is.GreaterThanOrEqualTo(4));
                Assert.That(finishedCalls, Is.EqualTo(1));
                Assert.That(service.IsBusy, Is.False);
                Assert.That(service.Status.Phase, Is.EqualTo(SceneFlowPhase.Idle));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>Unityメインスレッド以外の扱いではUnity Scene APIへ触れない。</summary>
        [Test]
        public async Task ExecuteAsync_NonMainThreadReturnsExplicitFailure()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.IsMainThread = false;
            var service = new SceneFlowService(backend);
            var finished = 0;
            service.Finished += _ => finished++;

            var result = await service.SetActiveAsync(new SceneReference(BootstrapPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.MainThreadRequired));
            Assert.That(backend.SetActiveCalls, Is.Zero);
            Assert.That(service.IsBusy, Is.False);
            Assert.That(finished, Is.Zero, "MainThreadRequiredの早期結果はFinishedへ通知しない");
        }

        /// <summary>終了トークン発火後の要求をScene APIへ渡さず明示的な失敗にする。</summary>
        [Test]
        public async Task ExecuteAsync_ApplicationExitReturnsExplicitFailure()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Exit.Cancel();
            var service = new SceneFlowService(backend);

            var result = await service.SetActiveAsync(new SceneReference(BootstrapPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ApplicationExiting));
            Assert.That(backend.SetActiveCalls, Is.Zero);
            Assert.That(service.IsBusy, Is.False);
        }

        /// <summary>背景スレッドで作ったサービスがそのスレッドをメイン扱いしない。</summary>
        [Test]
        public void Constructor_BackgroundThreadIsRejected()
        {
            Exception captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new SceneFlowService();
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            });

            thread.Start();
            Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(captured, Is.TypeOf<InvalidOperationException>());
        }

        /// <summary>Unity操作完了と同じフレームで終了した場合も成功結果にしない。</summary>
        [Test]
        public async Task LoadAdditiveAsync_ExitOnCompletionReturnsApplicationExiting()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.9f },
                () =>
                {
                    backend.Loaded.Add(path);
                    backend.Exit.Cancel();
                });
            var service = new SceneFlowService(backend);

            var result = await service.LoadAdditiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ApplicationExiting));
            Assert.That(service.IsBusy, Is.False);
        }

        /// <summary>backendの不正な進捗値を公開せず、その後の正常な進捗を処理できる。</summary>
        [Test]
        public async Task LoadAdditiveAsync_NonFiniteProgressIsNotPublished()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { float.NaN, float.PositiveInfinity, 0.45f, 0.9f },
                () => backend.Loaded.Add(path));
            var service = new SceneFlowService(backend);
            var statuses = new List<SceneFlowStatus>();
            service.StatusChanged += statuses.Add;

            var result = await service.LoadAdditiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(statuses, Is.Not.Empty);
            for (var i = 0; i < statuses.Count; i++)
            {
                Assert.That(float.IsNaN(statuses[i].Progress), Is.False);
                Assert.That(float.IsInfinity(statuses[i].Progress), Is.False);
                Assert.That(statuses[i].Progress, Is.InRange(0f, 1f));
            }
        }

        /// <summary>既に読込済みのSceneを重複して追加読込しない。</summary>
        [Test]
        public async Task LoadAdditiveAsync_LoadedTargetReturnsAlreadyLoaded()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.Loadable.Add(GameplayPath);
            var service = new SceneFlowService(backend);

            var result = await service.LoadAdditiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.AlreadyLoaded));
            Assert.That(backend.LoadCalls, Is.Zero);
        }

        /// <summary>同じパスのSceneが複数なら一意に操作できないため拒否する。</summary>
        [Test]
        public async Task SetActiveAsync_DuplicatePathReturnsAmbiguousScene()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath, GameplayPath);
            var service = new SceneFlowService(backend);

            var result = await service.SetActiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.AmbiguousScene));
            Assert.That(backend.SetActiveCalls, Is.Zero);
        }

        /// <summary>最後のSceneをアンロードしてScene無しにしない。</summary>
        [Test]
        public async Task UnloadAsync_LastLoadedSceneIsRejected()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);

            var result = await service.UnloadAsync(new SceneReference(BootstrapPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.LastSceneCannotBeUnloaded));
            Assert.That(backend.UnloadCalls, Is.Zero);
        }

        /// <summary>有効Sceneを暗黙に切り替えず、先に明示的なSetActiveを要求する。</summary>
        [Test]
        public async Task UnloadAsync_ActiveSceneIsRejected()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.ActivePath = GameplayPath;
            var service = new SceneFlowService(backend);

            var result = await service.UnloadAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ActiveSceneCannotBeUnloaded));
            Assert.That(backend.UnloadCalls, Is.Zero);
        }

        /// <summary>アンロード開始直前の外部変更も再検査で拒否する。</summary>
        [Test]
        public async Task UnloadAsync_TargetBecameActiveBeforeStartIsRejected()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.ActivePath = BootstrapPath;
            backend.OnCountLoaded = path =>
            {
                backend.ActivePath = GameplayPath;
                return backend.Loaded.FindAll(item => PathEquals(item, path)).Count;
            };
            var service = new SceneFlowService(backend);

            var result = await service.UnloadAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ActiveSceneCannotBeUnloaded));
            Assert.That(backend.UnloadCalls, Is.Zero);
        }

        /// <summary>Unity操作完了後に対象が存在しなければ成功として扱わない。</summary>
        [Test]
        public async Task LoadSingleAsync_MissingPostconditionReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(new[] { 0.9f }, () => backend.Loaded.Clear());
            var service = new SceneFlowService(backend);

            var result = await service.LoadSingleAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
            Assert.That(service.IsBusy, Is.False);
        }

        /// <summary>Single読込中に外部から別Sceneが追加された場合はScene数の事後条件で検出する。</summary>
        [Test]
        public async Task LoadSingleAsync_ExternalAdditiveSceneReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.9f },
                () =>
                {
                    backend.Loaded.Clear();
                    backend.Loaded.Add(path);
                    backend.Loaded.Add("Assets/Scenes/External.unity");
                });
            var service = new SceneFlowService(backend);

            var result = await service.LoadSingleAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        /// <summary>Single読込後に対象Sceneだけが残っても有効Sceneでなければ成功にしない。</summary>
        [Test]
        public async Task LoadSingleAsync_TargetNotActiveReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.9f },
                () =>
                {
                    backend.Loaded.Clear();
                    backend.Loaded.Add(path);
                    backend.ActivePath = BootstrapPath;
                });
            var service = new SceneFlowService(backend);

            var result = await service.LoadSingleAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        /// <summary>追加読込中に既存Sceneが同数の別Sceneへ差し替わってもidentity照合で検出する。</summary>
        [Test]
        public async Task LoadAdditiveAsync_ExistingSceneReplacementReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            backend.Loadable.Add(GameplayPath);
            backend.OnLoad = (path, additive) => backend.CreatePendingOperation(
                new[] { 0.9f },
                () =>
                {
                    backend.Loaded.Remove(BootstrapPath);
                    backend.Loaded.Add("Assets/Scenes/External.unity");
                    backend.Loaded.Add(path);
                });
            var service = new SceneFlowService(backend);

            var result = await service.LoadAdditiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        /// <summary>アンロード中に残すSceneが別Sceneへ差し替わってもidentity照合で検出する。</summary>
        [Test]
        public async Task UnloadAsync_RemainingSceneReplacementReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.OnUnloadComplete = path =>
            {
                backend.Loaded.RemoveAll(item => PathEquals(item, path));
                backend.Loaded.Remove(BootstrapPath);
                backend.Loaded.Add("Assets/Scenes/External.unity");
            };
            var service = new SceneFlowService(backend);

            var result = await service.UnloadAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        /// <summary>有効Scene変更中にloaded集合が差し替わってもidentity照合で検出する。</summary>
        [Test]
        public async Task SetActiveAsync_LoadedSceneReplacementReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.OnSetActive = _ =>
            {
                backend.Loaded.Remove(BootstrapPath);
                backend.Loaded.Add("Assets/Scenes/External.unity");
            };
            var service = new SceneFlowService(backend);

            var result = await service.SetActiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        /// <summary>通知中の購読変更は現在のsnapshotを変えず、次回通知から反映する。</summary>
        [Test]
        public async Task StatusChanged_SubscriptionMutationAppliesFromNextNotification()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);
            var firstCalls = 0;
            var addedCalls = 0;
            Action<SceneFlowStatus> added = _ => addedCalls++;
            Action<SceneFlowStatus> first = null;
            first = _ =>
            {
                firstCalls++;
                service.StatusChanged -= first;
                service.StatusChanged += added;
            };
            service.StatusChanged += first;

            var result = await service.SetActiveAsync(new SceneReference(BootstrapPath));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(addedCalls, Is.GreaterThanOrEqualTo(1));
        }

        /// <summary>同じ購読枠の連続失敗を1回だけ記録し、成功後の再失敗は新しい問題として記録する。</summary>
        [Test]
        public async Task StatusChanged_ContinuousFailureIsLoggedOnceUntilRecovery()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);
            var observerCalls = 0;
            var loggedFailures = 0;
            void CountLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && condition.IndexOf("observer-slot-test", StringComparison.Ordinal) >= 0) loggedFailures++;
            }

            service.StatusChanged += _ =>
            {
                observerCalls++;
                if (observerCalls <= 2 || observerCalls >= 4) throw new InvalidOperationException("observer-slot-test");
            };

            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CountLog;
            try
            {
                var result = await service.SetActiveAsync(new SceneReference(BootstrapPath));

                Assert.That(result.IsSuccess, Is.True, result.Message);
                Assert.That(observerCalls, Is.GreaterThanOrEqualTo(4));
                Assert.That(loggedFailures, Is.EqualTo(2));
            }
            finally
            {
                Application.logMessageReceived -= CountLog;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>解除して再購読した通知先は新しい枠となり、以前の失敗抑制を引き継がない。</summary>
        [Test]
        public async Task StatusChanged_ReSubscribeDoesNotInheritFailureSuppression()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath);
            var service = new SceneFlowService(backend);
            var loggedFailures = 0;
            Action<SceneFlowStatus> observer = _ => throw new InvalidOperationException("observer-resubscribe-test");
            void CountLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && condition.IndexOf("observer-resubscribe-test", StringComparison.Ordinal) >= 0) loggedFailures++;
            }

            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CountLog;
            try
            {
                service.StatusChanged += observer;
                Assert.That((await service.SetActiveAsync(new SceneReference(BootstrapPath))).IsSuccess, Is.True);
                service.StatusChanged -= observer;
                service.StatusChanged += observer;
                Assert.That((await service.SetActiveAsync(new SceneReference(BootstrapPath))).IsSuccess, Is.True);

                Assert.That(loggedFailures, Is.EqualTo(2));
            }
            finally
            {
                Application.logMessageReceived -= CountLog;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>有効化APIのtrueだけを信じず、完了後の有効Sceneを再確認する。</summary>
        [Test]
        public async Task SetActiveAsync_FalsePostconditionReturnsExternalSceneChange()
        {
            var backend = FakeBackend.WithLoaded(BootstrapPath, GameplayPath);
            backend.ActivePath = BootstrapPath;
            backend.SetActiveResult = true;
            backend.ApplySetActive = false;
            var service = new SceneFlowService(backend);

            var result = await service.SetActiveAsync(new SceneReference(GameplayPath));

            Assert.That(result.Error, Is.EqualTo(SceneFlowError.ExternalSceneChange));
        }

        private static void AssertMonotonicLoadingProgress(IReadOnlyList<SceneFlowStatus> statuses)
        {
            var previous = 0f;
            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Phase != SceneFlowPhase.Loading) continue;
                Assert.That(statuses[i].Progress, Is.GreaterThanOrEqualTo(previous));
                previous = statuses[i].Progress;
            }
        }

        private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        /// <summary>偽backendが例外を発生させる内部境界。</summary>
        public enum BackendFailurePoint
        {
            /// <summary>例外を発生させない。</summary>
            None,
            /// <summary>読込可否の取得で失敗する。</summary>
            CanLoad,
            /// <summary>読込済みScene一覧の取得で失敗する。</summary>
            Snapshot,
            /// <summary>同一pathのScene数取得で失敗する。</summary>
            CountLoaded,
            /// <summary>Scene読込開始で失敗する。</summary>
            Load,
            /// <summary>Sceneアンロード開始で失敗する。</summary>
            Unload,
            /// <summary>有効Scene切替で失敗する。</summary>
            SetActive,
            /// <summary>次フレーム待機で失敗する。</summary>
            NextFrame,
            /// <summary>メインスレッド判定で失敗する。</summary>
            IsMainThread,
            /// <summary>終了token取得で失敗する。</summary>
            ExitToken,
            /// <summary>読込済みScene総数取得で失敗する。</summary>
            LoadedSceneCount,
            /// <summary>有効Scene判定で失敗する。</summary>
            IsActive,
            /// <summary>非同期操作の完了判定で失敗する。</summary>
            OperationIsDone,
            /// <summary>非同期操作の進捗取得で失敗する。</summary>
            OperationProgress
        }

        /// <summary>1フレームごとに進捗を進め、最後にScene状態を変更できる偽backend。</summary>
        private sealed class FakeBackend : ISceneFlowBackend
        {
            private readonly Queue<PendingOperation> _pending = new Queue<PendingOperation>();
            private AwaitableCompletionSource _pausedFrame;
            private bool _isMainThread;

            private FakeBackend()
            {
                Exit = new CancellationTokenSource();
                Loadable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Loaded = new List<string>();
                _isMainThread = true;
                SetActiveResult = true;
                ApplySetActive = true;
            }

            public CancellationTokenSource Exit { get; }

            public HashSet<string> Loadable { get; }

            public List<string> Loaded { get; }

            public Func<string, bool, ISceneFlowAsyncOperation> OnLoad { get; set; }

            public Func<string, int> OnCountLoaded { get; set; }

            public Action<string> OnUnloadComplete { get; set; }

            public Action<string> OnSetActive { get; set; }

            public bool IsMainThread
            {
                get
                {
                    ThrowIfFailure(BackendFailurePoint.IsMainThread);
                    return _isMainThread;
                }
                set => _isMainThread = value;
            }

            public bool SetActiveResult { get; set; }

            public bool ApplySetActive { get; set; }

            public bool PauseNextFrame { get; set; }

            public BackendFailurePoint FailurePoint { get; set; }

            public string ActivePath { get; set; }

            public int LoadCalls { get; private set; }

            public int UnloadCalls { get; private set; }

            public int SetActiveCalls { get; private set; }

            public CancellationToken ExitToken
            {
                get
                {
                    ThrowIfFailure(BackendFailurePoint.ExitToken);
                    return Exit.Token;
                }
            }

            public int LoadedSceneCount
            {
                get
                {
                    ThrowIfFailure(BackendFailurePoint.LoadedSceneCount);
                    return Loaded.Count;
                }
            }

            public static FakeBackend WithLoaded(params string[] paths)
            {
                var backend = new FakeBackend();
                backend.Loaded.AddRange(paths);
                if (paths.Length > 0) backend.ActivePath = paths[0];
                return backend;
            }

            public bool CanLoad(string path)
            {
                ThrowIfFailure(BackendFailurePoint.CanLoad);
                return Loadable.Contains(path);
            }

            public int CountLoaded(string path)
            {
                ThrowIfFailure(BackendFailurePoint.CountLoaded);
                return OnCountLoaded?.Invoke(path) ?? Loaded.FindAll(item => PathEquals(item, path)).Count;
            }

            public SceneFlowSceneIdentity[] SnapshotLoadedScenes()
            {
                ThrowIfFailure(BackendFailurePoint.Snapshot);
                var result = new SceneFlowSceneIdentity[Loaded.Count];
                var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < Loaded.Count; i++)
                {
                    var path = Loaded[i];
                    occurrences.TryGetValue(path, out var occurrence);
                    occurrences[path] = occurrence + 1;
                    var pathHash = unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(path));
                    result[i] = new SceneFlowSceneIdentity(((ulong)pathHash << 32) | (uint)occurrence, path);
                }

                return result;
            }

            public bool IsActive(string path)
            {
                ThrowIfFailure(BackendFailurePoint.IsActive);
                return PathEquals(ActivePath, path);
            }

            public ISceneFlowAsyncOperation Load(string path, bool additive)
            {
                LoadCalls++;
                ThrowIfFailure(BackendFailurePoint.Load);
                var operation = OnLoad?.Invoke(path, additive);
                return operation == null || (FailurePoint != BackendFailurePoint.OperationIsDone && FailurePoint != BackendFailurePoint.OperationProgress)
                    ? operation
                    : new ThrowingOperation(operation, FailurePoint);
            }

            public ISceneFlowAsyncOperation Unload(string path)
            {
                UnloadCalls++;
                ThrowIfFailure(BackendFailurePoint.Unload);
                return CreatePendingOperation(
                    new[] { 0.5f, 1f },
                    () =>
                    {
                        if (OnUnloadComplete == null) Loaded.RemoveAll(item => PathEquals(item, path));
                        else OnUnloadComplete(path);
                    });
            }

            public bool SetActive(string path)
            {
                SetActiveCalls++;
                ThrowIfFailure(BackendFailurePoint.SetActive);
                if (SetActiveResult && ApplySetActive) ActivePath = path;
                OnSetActive?.Invoke(path);
                return SetActiveResult;
            }

            public Awaitable NextFrame(CancellationToken cancellationToken)
            {
                ThrowIfFailure(BackendFailurePoint.NextFrame);
                cancellationToken.ThrowIfCancellationRequested();
                if (_pending.Count > 0)
                {
                    var operation = _pending.Peek();
                    operation.Advance();
                    if (operation.IsDone) _pending.Dequeue();
                }

                if (PauseNextFrame)
                {
                    PauseNextFrame = false;
                    _pausedFrame = new AwaitableCompletionSource();
                    return _pausedFrame.Awaitable;
                }

                var completion = new AwaitableCompletionSource();
                completion.SetResult();
                return completion.Awaitable;
            }

            public void ReleasePausedFrame()
            {
                var paused = _pausedFrame;
                _pausedFrame = null;
                paused?.SetResult();
            }

            public ISceneFlowAsyncOperation CreatePendingOperation(IReadOnlyList<float> progress, Action complete)
            {
                var operation = new PendingOperation(progress, complete);
                _pending.Enqueue(operation);
                return operation;
            }

            private void ThrowIfFailure(BackendFailurePoint failurePoint)
            {
                if (FailurePoint == failurePoint) throw new InvalidOperationException($"fake backend failure: {failurePoint}");
            }
        }

        /// <summary>偽backendがフレーム単位で進めるScene操作。</summary>
        private sealed class PendingOperation : ISceneFlowAsyncOperation
        {
            private readonly IReadOnlyList<float> _progress;
            private readonly Action _complete;
            private int _index;

            public PendingOperation(IReadOnlyList<float> progress, Action complete)
            {
                _progress = progress;
                _complete = complete;
                _index = -1;
            }

            public bool IsDone { get; private set; }

            public float Progress => _index < 0 ? 0f : _progress[_index];

            public void Advance()
            {
                if (IsDone) return;
                _index++;
                if (_index < _progress.Count - 1) return;

                _complete?.Invoke();
                IsDone = true;
            }
        }

        /// <summary>AsyncOperationのproperty例外を再現する偽操作。</summary>
        private sealed class ThrowingOperation : ISceneFlowAsyncOperation
        {
            private readonly ISceneFlowAsyncOperation _inner;
            private readonly BackendFailurePoint _failurePoint;

            public ThrowingOperation(ISceneFlowAsyncOperation inner, BackendFailurePoint failurePoint)
            {
                _inner = inner;
                _failurePoint = failurePoint;
            }

            public bool IsDone
            {
                get
                {
                    if (_failurePoint == BackendFailurePoint.OperationIsDone) throw new InvalidOperationException("fake operation IsDone failure");
                    return _inner.IsDone;
                }
            }

            public float Progress
            {
                get
                {
                    if (_failurePoint == BackendFailurePoint.OperationProgress) throw new InvalidOperationException("fake operation Progress failure");
                    return _inner.Progress;
                }
            }
        }
    }
}
