using System;
using System.Threading;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>
    /// SceneManagerの非同期操作を1件ずつ実行し、事前条件と完了後のScene状態を結果として返す。
    /// 利用側が所有し、Unityメインスレッドからだけ使う。
    /// </summary>
    public sealed class SceneFlowService
    {
        private readonly ISceneFlowBackend _backend;
        private ObserverSlot<Action<SceneFlowStatus>>[] _statusChangedObservers = Array.Empty<ObserverSlot<Action<SceneFlowStatus>>>();
        private ObserverSlot<Action<SceneFlowResult>>[] _finishedObservers = Array.Empty<ObserverSlot<Action<SceneFlowResult>>>();
        private SceneFlowStatus _status;
        private long _statusVersion;
        private bool _busy;
        private bool _isDispatchingCallback;

        /// <summary>UnityのSceneManagerへ接続するサービスを作る。Unityメインスレッドから生成する。</summary>
        /// <exception cref="InvalidOperationException">Unityのメインスレッド以外から生成した場合。</exception>
        public SceneFlowService() : this(new UnitySceneFlowBackend())
        {
        }

        /// <summary>テスト可能なScene操作境界を指定してサービスを作る。</summary>
        /// <param name="backend">Sceneの実状態と非同期操作を提供する内部境界。</param>
        internal SceneFlowService(ISceneFlowBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _status = new SceneFlowStatus(SceneFlowPhase.Idle, default, 0f);
        }

        /// <summary>現在の処理段階と進捗。</summary>
        public SceneFlowStatus Status => _status;

        /// <summary>別の要求を処理中ならtrue。</summary>
        public bool IsBusy => _busy;

        /// <summary>受理した要求の段階または進捗が変わったときに通知する。通知先の例外は他の通知先と処理本体へ伝播しない。</summary>
        public event Action<SceneFlowStatus> StatusChanged
        {
            add => _statusChangedObservers = AddObserver(_statusChangedObservers, value);
            remove => _statusChangedObservers = RemoveObserver(_statusChangedObservers, value);
        }

        /// <summary>受理した要求が成功または失敗で確定したときに通知する。BusyとMainThreadRequiredの早期結果は通知しない。</summary>
        public event Action<SceneFlowResult> Finished
        {
            add => _finishedObservers = AddObserver(_finishedObservers, value);
            remove => _finishedObservers = RemoveObserver(_finishedObservers, value);
        }

        /// <summary>現在のSceneを置き換えて読み込む。</summary>
        /// <param name="scene">読み込むSceneの完全パス参照。</param>
        /// <returns>完了後のScene集合まで確認した結果。</returns>
        public Awaitable<SceneFlowResult> LoadSingleAsync(SceneReference scene) => ExecuteAsync(SceneFlowRequest.LoadSingle(scene));

        /// <summary>現在のSceneを残して追加読込する。同じパスが既に読込済みなら拒否する。</summary>
        /// <param name="scene">追加読込するSceneの完全パス参照。</param>
        /// <returns>既存Sceneの保持と対象Sceneの追加を確認した結果。</returns>
        public Awaitable<SceneFlowResult> LoadAdditiveAsync(SceneReference scene) => ExecuteAsync(SceneFlowRequest.LoadAdditive(scene));

        /// <summary>読込済みSceneをアンロードする。有効Sceneと最後のSceneは拒否する。</summary>
        /// <param name="scene">アンロードする読込済みSceneの完全パス参照。</param>
        /// <returns>対象だけがScene集合から除かれたことを確認した結果。</returns>
        public Awaitable<SceneFlowResult> UnloadAsync(SceneReference scene) => ExecuteAsync(SceneFlowRequest.Unload(scene));

        /// <summary>読込済みSceneを有効Sceneにする。</summary>
        /// <param name="scene">有効にする読込済みSceneの完全パス参照。</param>
        /// <returns>Scene集合を変えずに対象を有効化したことを確認した結果。</returns>
        public Awaitable<SceneFlowResult> SetActiveAsync(SceneReference scene) => ExecuteAsync(SceneFlowRequest.SetActive(scene));

        /// <summary>
        /// 1件のScene要求を実行する。同じサービスの処理中またはcallback通知中に届いた要求はUnity APIへ触れずBusyで返す。
        /// </summary>
        /// <param name="request">実行する操作と対象Scene。</param>
        /// <returns>完了後のScene状態まで確認した結果。</returns>
        public async Awaitable<SceneFlowResult> ExecuteAsync(SceneFlowRequest request)
        {
            if (_busy || _isDispatchingCallback)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.Busy, "別のScene要求を処理中、または状態通知中です。");
            }

            bool isMainThread;
            try
            {
                isMainThread = _backend.IsMainThread;
            }
            catch (Exception exception)
            {
                _busy = true;
                SetStatus(SceneFlowPhase.Validating, request, 0f);
                var failure = SceneFlowResult.Failure(request, SceneFlowError.OperationFailed, $"Scene操作に失敗しました: {GetExceptionMessage(exception)}");
                return CompleteAcceptedRequest(request, failure);
            }

            if (!isMainThread)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.MainThreadRequired, "SceneFlowServiceはUnityメインスレッドから呼んでください。");
            }

            _busy = true;
            SetStatus(SceneFlowPhase.Validating, request, 0f);

            SceneFlowResult result;
            try
            {
                result = Validate(request);
                if (result.Error == SceneFlowError.None) result = await ExecuteValidatedAsync(request);
            }
            catch (OperationCanceledException) when (_backend.ExitToken.IsCancellationRequested)
            {
                result = SceneFlowResult.Failure(request, SceneFlowError.ApplicationExiting, "Play Modeまたはアプリケーションが終了しています。");
            }
            catch (Exception exception)
            {
                result = SceneFlowResult.Failure(request, SceneFlowError.OperationFailed, $"Scene操作に失敗しました: {GetExceptionMessage(exception)}");
            }

            return CompleteAcceptedRequest(request, result);
        }

        private SceneFlowResult CompleteAcceptedRequest(SceneFlowRequest request, SceneFlowResult result)
        {
            var terminalPhase = result.IsSuccess ? SceneFlowPhase.Completed : SceneFlowPhase.Failed;
            SetStatus(terminalPhase, request, result.IsSuccess ? 1f : _status.Progress);
            InvokeFinished(result);

            _busy = false;
            SetStatus(SceneFlowPhase.Idle, default, 0f);
            return result;
        }

        private SceneFlowResult Validate(SceneFlowRequest request)
        {
            if (!Enum.IsDefined(typeof(SceneFlowOperation), request.Operation))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.InvalidRequest, "Scene操作の種類が不正です。");
            }

            if (!request.Scene.IsValid)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.InvalidRequest, "AssetsまたはPackagesから始まる完全なSceneパスを指定してください。");
            }

            if (_backend.ExitToken.IsCancellationRequested)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ApplicationExiting, "Play Modeまたはアプリケーションが終了しています。");
            }

            switch (request.Operation)
            {
                case SceneFlowOperation.LoadSingle:
                    return ValidateLoad(request, false);
                case SceneFlowOperation.LoadAdditive:
                    return ValidateLoad(request, true);
                case SceneFlowOperation.Unload:
                    return ValidateUnload(request);
                case SceneFlowOperation.SetActive:
                    return ValidateLoadedTarget(request);
                default:
                    return SceneFlowResult.Failure(request, SceneFlowError.InvalidRequest, "Scene操作の種類が不正です。");
            }
        }

        private SceneFlowResult ValidateLoad(SceneFlowRequest request, bool additive)
        {
            if (!_backend.CanLoad(request.Scene.Path))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.SceneNotInBuild, "現在のPlayerまたはBuild Profileから対象Sceneを読み込めません。");
            }

            if (additive && _backend.CountLoaded(request.Scene.Path) > 0)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.AlreadyLoaded, "対象Sceneは既に読み込まれています。");
            }

            return SceneFlowResult.Success(request);
        }

        private SceneFlowResult ValidateUnload(SceneFlowRequest request)
        {
            var target = ValidateLoadedTarget(request);
            if (!target.IsSuccess) return target;

            if (_backend.LoadedSceneCount <= 1)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.LastSceneCannotBeUnloaded, "最後の読込済みSceneはアンロードできません。");
            }

            if (_backend.IsActive(request.Scene.Path))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ActiveSceneCannotBeUnloaded, "有効Sceneをアンロードする前に別のSceneを有効にしてください。");
            }

            return SceneFlowResult.Success(request);
        }

        private SceneFlowResult ValidateLoadedTarget(SceneFlowRequest request)
        {
            var matches = _backend.CountLoaded(request.Scene.Path);
            if (matches == 0)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.NotLoaded, "対象Sceneは読み込まれていません。");
            }

            if (matches > 1)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.AmbiguousScene, "同じパスのSceneが複数あり、対象を一意に決められません。");
            }

            return SceneFlowResult.Success(request);
        }

        private async Awaitable<SceneFlowResult> ExecuteValidatedAsync(SceneFlowRequest request)
        {
            switch (request.Operation)
            {
                case SceneFlowOperation.LoadSingle:
                    return await ExecuteLoadAsync(request, false);
                case SceneFlowOperation.LoadAdditive:
                    return await ExecuteLoadAsync(request, true);
                case SceneFlowOperation.Unload:
                    return await ExecuteUnloadAsync(request);
                case SceneFlowOperation.SetActive:
                    return ExecuteSetActive(request);
                default:
                    return SceneFlowResult.Failure(request, SceneFlowError.InvalidRequest, "Scene操作の種類が不正です。");
            }
        }

        private async Awaitable<SceneFlowResult> ExecuteLoadAsync(SceneFlowRequest request, bool additive)
        {
            SetStatus(SceneFlowPhase.Loading, request, 0f);
            var scenesBefore = _backend.SnapshotLoadedScenes();

            if (additive && CountPath(scenesBefore, request.Scene.Path) > 0)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.AlreadyLoaded, "対象Sceneは処理開始前に外部から読み込まれました。");
            }

            var operation = _backend.Load(request.Scene.Path, additive);
            if (operation == null)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.OperationFailed, "Sceneの読込操作を開始できませんでした。");
            }

            await WaitForOperationAsync(request, operation, true);
            SetStatus(SceneFlowPhase.Verifying, request, 1f);

            var scenesAfter = _backend.SnapshotLoadedScenes();
            var matches = CountPath(scenesAfter, request.Scene.Path);
            if (matches > 1)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.AmbiguousScene, "完了時に同じパスのSceneが複数見つかりました。");
            }

            if (matches != 1)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ExternalSceneChange, "読込完了後に対象Sceneを確認できませんでした。外部のSceneManager操作を確認してください。");
            }

            var hasExpectedSceneSet = additive
                ? IsAdditiveResult(scenesBefore, scenesAfter, request.Scene.Path)
                : scenesAfter.Length == 1 && PathEquals(scenesAfter[0].Path, request.Scene.Path);
            if (!hasExpectedSceneSet)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ExternalSceneChange, "読込完了後のScene数が要求と一致しません。外部のSceneManager操作を確認してください。");
            }

            if (!additive && !_backend.IsActive(request.Scene.Path))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ExternalSceneChange, "Single読込完了後に対象Sceneが有効Sceneではありません。外部のSceneManager操作を確認してください。");
            }

            return SceneFlowResult.Success(request, "Sceneの読込が完了しました。");
        }

        private async Awaitable<SceneFlowResult> ExecuteUnloadAsync(SceneFlowRequest request)
        {
            SetStatus(SceneFlowPhase.Unloading, request, 0f);

            var target = ValidateUnload(request);
            if (!target.IsSuccess) return target;
            var scenesBefore = _backend.SnapshotLoadedScenes();

            var operation = _backend.Unload(request.Scene.Path);
            if (operation == null)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.OperationFailed, "Sceneのアンロード操作を開始できませんでした。");
            }

            await WaitForOperationAsync(request, operation, false);
            var scenesAfter = _backend.SnapshotLoadedScenes();
            if (CountPath(scenesAfter, request.Scene.Path) != 0 || !IsUnloadResult(scenesBefore, scenesAfter, request.Scene.Path))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ExternalSceneChange, "アンロード完了後も対象Sceneが残っています。外部のSceneManager操作を確認してください。");
            }

            return SceneFlowResult.Success(request, "Sceneのアンロードが完了しました。");
        }

        private SceneFlowResult ExecuteSetActive(SceneFlowRequest request)
        {
            SetStatus(SceneFlowPhase.SettingActive, request, 0f);
            var scenesBefore = _backend.SnapshotLoadedScenes();
            var target = ValidateLoadedTarget(request);
            if (!target.IsSuccess) return target;

            if (!_backend.IsActive(request.Scene.Path) && !_backend.SetActive(request.Scene.Path))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ActivationFailed, "対象Sceneを有効Sceneにできませんでした。");
            }

            var scenesAfter = _backend.SnapshotLoadedScenes();
            var matches = CountPath(scenesAfter, request.Scene.Path);
            if (matches > 1)
            {
                return SceneFlowResult.Failure(request, SceneFlowError.AmbiguousScene, "有効化完了時に同じパスのSceneが複数見つかりました。");
            }

            if (matches != 1 || !_backend.IsActive(request.Scene.Path) || !HaveSameIdentities(scenesBefore, scenesAfter))
            {
                return SceneFlowResult.Failure(request, SceneFlowError.ExternalSceneChange, "有効化後のScene状態が要求と一致しません。外部のSceneManager操作を確認してください。");
            }

            return SceneFlowResult.Success(request, "有効Sceneを変更しました。");
        }

        private async Awaitable WaitForOperationAsync(SceneFlowRequest request, ISceneFlowAsyncOperation operation, bool normalizeLoadProgress)
        {
            var progress = 0f;
            while (!operation.IsDone)
            {
                var rawProgress = operation.Progress;
                var observed = float.IsNaN(rawProgress) || float.IsInfinity(rawProgress)
                    ? progress
                    : normalizeLoadProgress ? Mathf.Clamp01(rawProgress / 0.9f) : Mathf.Clamp01(rawProgress);
                if (observed > progress)
                {
                    progress = observed;
                    SetStatus(_status.Phase, request, progress);
                }

                await _backend.NextFrame(_backend.ExitToken);
            }

            _backend.ExitToken.ThrowIfCancellationRequested();
            if (progress < 1f) SetStatus(_status.Phase, request, 1f);
        }

        private void SetStatus(SceneFlowPhase phase, SceneFlowRequest request, float progress)
        {
            _status = new SceneFlowStatus(phase, request, progress);
            _statusVersion = unchecked(_statusVersion + 1);
            InvokeStatusChanged(_status, _statusVersion);
        }

        private void InvokeStatusChanged(SceneFlowStatus status, long statusVersion)
        {
            var observers = _statusChangedObservers;
            _isDispatchingCallback = true;
            try
            {
                for (var i = 0; i < observers.Length; i++)
                {
                    if (_statusVersion != statusVersion) break;

                    try
                    {
                        observers[i].Observer(status);
                        observers[i].IsFailing = false;
                    }
                    catch (Exception exception)
                    {
                        if (observers[i].IsFailing) continue;
                        observers[i].IsFailing = true;
                        LogObserverException(exception);
                    }
                }
            }
            finally
            {
                _isDispatchingCallback = false;
            }
        }

        private void InvokeFinished(SceneFlowResult result)
        {
            var observers = _finishedObservers;
            _isDispatchingCallback = true;
            try
            {
                for (var i = 0; i < observers.Length; i++)
                {
                    try
                    {
                        observers[i].Observer(result);
                        observers[i].IsFailing = false;
                    }
                    catch (Exception exception)
                    {
                        if (observers[i].IsFailing) continue;
                        observers[i].IsFailing = true;
                        LogObserverException(exception);
                    }
                }
            }
            finally
            {
                _isDispatchingCallback = false;
            }
        }

        private static void LogObserverException(Exception exception)
        {
            try
            {
                Debug.LogException(exception);
            }
            catch (Exception)
            {
                // ログ処理が使えない終了局面でも、Scene処理と後続通知は止めない。
            }
        }

        private static string GetExceptionMessage(Exception exception)
        {
            try
            {
                return exception.Message;
            }
            catch (Exception)
            {
                return exception.GetType().Name;
            }
        }

        private static int CountPath(SceneFlowSceneIdentity[] scenes, string path)
        {
            var count = 0;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (PathEquals(scenes[i].Path, path)) count++;
            }

            return count;
        }

        private static bool IsAdditiveResult(SceneFlowSceneIdentity[] before, SceneFlowSceneIdentity[] after, string targetPath)
        {
            if (after.Length != before.Length + 1 || CountPath(after, targetPath) != 1) return false;
            for (var i = 0; i < before.Length; i++)
            {
                if (!Contains(after, before[i])) return false;
            }

            return true;
        }

        private static bool IsUnloadResult(SceneFlowSceneIdentity[] before, SceneFlowSceneIdentity[] after, string targetPath)
        {
            if (after.Length != before.Length - 1 || CountPath(before, targetPath) != 1) return false;
            for (var i = 0; i < before.Length; i++)
            {
                if (PathEquals(before[i].Path, targetPath)) continue;
                if (!Contains(after, before[i])) return false;
            }

            return true;
        }

        private static bool HaveSameIdentities(SceneFlowSceneIdentity[] left, SceneFlowSceneIdentity[] right)
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (!Contains(right, left[i])) return false;
            }

            return true;
        }

        private static bool Contains(SceneFlowSceneIdentity[] scenes, SceneFlowSceneIdentity target)
        {
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].Equals(target)) return true;
            }

            return false;
        }

        private static bool PathEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static ObserverSlot<TObserver>[] AddObserver<TObserver>(ObserverSlot<TObserver>[] observers, TObserver observer) where TObserver : Delegate
        {
            if (observer == null) return observers;

            var additions = observer.GetInvocationList();
            var result = new ObserverSlot<TObserver>[observers.Length + additions.Length];
            Array.Copy(observers, result, observers.Length);
            for (var i = 0; i < additions.Length; i++) result[observers.Length + i] = new ObserverSlot<TObserver>((TObserver)additions[i]);
            return result;
        }

        private static ObserverSlot<TObserver>[] RemoveObserver<TObserver>(ObserverSlot<TObserver>[] observers, TObserver observer) where TObserver : Delegate
        {
            if (observer == null || observers.Length == 0) return observers;

            var removals = observer.GetInvocationList();
            for (var start = observers.Length - removals.Length; start >= 0; start--)
            {
                var matches = true;
                for (var i = 0; i < removals.Length; i++)
                {
                    if (Equals(observers[start + i].Observer, removals[i])) continue;
                    matches = false;
                    break;
                }

                if (!matches) continue;
                if (removals.Length == observers.Length) return Array.Empty<ObserverSlot<TObserver>>();

                var result = new ObserverSlot<TObserver>[observers.Length - removals.Length];
                Array.Copy(observers, 0, result, 0, start);
                Array.Copy(observers, start + removals.Length, result, start, observers.Length - start - removals.Length);
                return result;
            }

            return observers;
        }

        /// <summary>1件の通知先と、連続失敗中かどうかを購読単位で保持する。</summary>
        private sealed class ObserverSlot<TObserver> where TObserver : Delegate
        {
            public ObserverSlot(TObserver observer)
            {
                Observer = observer;
            }

            public TObserver Observer { get; }

            public bool IsFailing { get; set; }
        }
    }
}
