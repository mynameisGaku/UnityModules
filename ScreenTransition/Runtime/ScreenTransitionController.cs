using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScreenTransition
{
    /// <summary>
    /// 1つのUIDocumentへ全画面表示を追加し、timeScaleに依存しない時間で画面を覆う。
    /// GameObjectが所有し、Unityメインスレッドからだけ使う。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScreenTransitionController : MonoBehaviour
    {
        private UIDocument _uiDocument;

        private ObserverSlot<Action<ScreenTransitionStatus>>[] _statusChangedObservers = Array.Empty<ObserverSlot<Action<ScreenTransitionStatus>>>();
        private ObserverSlot<Action<ScreenTransitionResult>>[] _finishedObservers = Array.Empty<ObserverSlot<Action<ScreenTransitionResult>>>();
        private ScreenTransitionEngine _engine;
        private ScreenTransitionSurface _surface;
        private ScreenTransitionStatus _status;
        private AwaitableCompletionSource<ScreenTransitionResult> _completion;
        private int _ownerThreadId;
        private int _callbackDispatchDepth;
        private int _statusVersion;
        private bool _busy;
        private bool _isCompleting;
        private bool _surfaceInterruptedDuringCompletion;
        private bool _applicationExiting;

        /// <summary>現在の処理段階、時間進捗、表示不透明度。</summary>
        public ScreenTransitionStatus Status => _status;

        /// <summary>別の要求を処理中または通知中ならtrue。</summary>
        public bool IsBusy => _busy || _callbackDispatchDepth > 0 || _isCompleting;

        /// <summary>受理した要求の段階、進捗、不透明度が変わったときに通知する。通知先の例外は他の通知先と処理本体へ伝播しない。</summary>
        public event Action<ScreenTransitionStatus> StatusChanged
        {
            add => _statusChangedObservers = AddObserver(_statusChangedObservers, value);
            remove => _statusChangedObservers = RemoveObserver(_statusChangedObservers, value);
        }

        /// <summary>受理した要求が成功または失敗で確定したときに通知する。受理前の早期失敗は通知しない。</summary>
        public event Action<ScreenTransitionResult> Finished
        {
            add => _finishedObservers = AddObserver(_finishedObservers, value);
            remove => _finishedObservers = RemoveObserver(_finishedObservers, value);
        }

        /// <summary>透明な状態から指定色で画面を覆う。</summary>
        /// <param name="color">表示する色。</param>
        /// <param name="duration">完了までの秒数。0以上3600以下。0なら直ちに完了する。</param>
        /// <param name="easing">進捗へ適用する変化曲線。</param>
        /// <returns>要求どおりの不透明度へ到達したかを表す結果。</returns>
        public Awaitable<ScreenTransitionResult> CoverAsync(Color color, float duration, ScreenTransitionEasing easing = ScreenTransitionEasing.EaseInOut) =>
            ExecuteAsync(ScreenTransitionRequest.Cover(color, duration, easing));

        /// <summary>指定色で覆われた状態から画面を見せる。</summary>
        /// <param name="color">開始時に表示する色。</param>
        /// <param name="duration">完了までの秒数。0以上3600以下。0なら直ちに完了する。</param>
        /// <param name="easing">進捗へ適用する変化曲線。</param>
        /// <returns>透明な状態へ到達したかを表す結果。</returns>
        public Awaitable<ScreenTransitionResult> RevealAsync(Color color, float duration, ScreenTransitionEasing easing = ScreenTransitionEasing.EaseInOut) =>
            ExecuteAsync(ScreenTransitionRequest.Reveal(color, duration, easing));

        /// <summary>1件の画面遷移要求を実行する。処理中または通知中の要求は描画先へ触れずBusyで返す。</summary>
        /// <param name="request">実行する操作、色、時間、変化曲線。</param>
        /// <returns>要求どおりの不透明度へ到達したかを表す結果。</returns>
        public Awaitable<ScreenTransitionResult> ExecuteAsync(ScreenTransitionRequest request)
        {
            if (IsBusy)
            {
                return FromResult(ScreenTransitionResult.Failure(request, ScreenTransitionError.Busy, "別の画面遷移要求を処理中、または状態通知中です。"));
            }

            if (_ownerThreadId == 0 || Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                return FromResult(ScreenTransitionResult.Failure(request, ScreenTransitionError.MainThreadRequired, "ScreenTransitionControllerはUnityメインスレッドから呼んでください。"));
            }

            if (_applicationExiting || !isActiveAndEnabled)
            {
                return FromResult(ScreenTransitionResult.Failure(request, ScreenTransitionError.ApplicationExiting, "Controllerが無効、破棄中、またはアプリケーションが終了しています。"));
            }

            var validation = ScreenTransitionEngine.Validate(request);
            if (!validation.IsSuccess) return FromResult(validation);

            if (!EnsureSurface())
            {
                return FromResult(ScreenTransitionResult.Failure(request, ScreenTransitionError.SurfaceUnavailable, "PanelSettingsを設定したUIDocumentを使用できません。"));
            }

            var completion = new AwaitableCompletionSource<ScreenTransitionResult>();
            _completion = completion;
            _busy = true;
            try
            {
                _engine.Start(request);
                ApplyEngineStatus();
                PublishEngineStatus();
                if (_engine.Status.Phase == ScreenTransitionPhase.Completed) CompleteAcceptedRequest(ScreenTransitionResult.Success(request));
            }
            catch (Exception exception)
            {
                if (!_surface.IsAvailable)
                {
                    CompleteSurfaceUnavailable(request);
                    return completion.Awaitable;
                }

                _engine.Fail();
                CompleteAcceptedRequest(ScreenTransitionResult.Failure(request, ScreenTransitionError.OperationFailed, $"画面遷移を開始できませんでした: {GetExceptionMessage(exception)}"));
            }

            return completion.Awaitable;
        }

        private void Awake()
        {
            InitializeIfNeeded();
            EnsureSurface();
        }

        private void OnEnable()
        {
            _applicationExiting = false;
            InitializeIfNeeded();
            EnsureSurface();
        }

        private void Update()
        {
            if (!_busy || _isCompleting) return;

            var request = _engine.Status.Request;
            try
            {
                if (!_surface.IsAvailable)
                {
                    CompleteSurfaceUnavailable(request);
                    return;
                }

                _engine.Tick(Time.unscaledDeltaTime);
                ApplyEngineStatus();
                PublishEngineStatus();
                if (_engine.Status.Phase == ScreenTransitionPhase.Completed) CompleteAcceptedRequest(ScreenTransitionResult.Success(request));
            }
            catch (Exception exception)
            {
                if (!_surface.IsAvailable)
                {
                    CompleteSurfaceUnavailable(request);
                    return;
                }

                _engine.Fail();
                CompleteAcceptedRequest(ScreenTransitionResult.Failure(request, ScreenTransitionError.OperationFailed, $"画面遷移に失敗しました: {GetExceptionMessage(exception)}"));
            }
        }

        private void LateUpdate()
        {
            if (_status.Opacity <= 0f) return;
            if (_surface.IsAvailable && _surface.EnsureFront()) return;

            if (_busy && !_isCompleting)
            {
                CompleteSurfaceUnavailable(_engine.Status.Request);
                return;
            }

            _surface.Detach();
            ClearDetachedSurfaceStatus();
        }

        private void OnDisable()
        {
            if (_isCompleting) _surfaceInterruptedDuringCompletion = true;
            _surface?.Detach();
            AbortAcceptedRequest("Controllerが無効になったため画面遷移を中断しました。");
            ClearDetachedSurfaceStatus();
        }

        private void OnDestroy()
        {
            if (_isCompleting) _surfaceInterruptedDuringCompletion = true;
            _surface?.Detach();
            AbortAcceptedRequest("Controllerが破棄されたため画面遷移を中断しました。");
            ClearDetachedSurfaceStatus();
        }

        private void OnApplicationQuit()
        {
            _applicationExiting = true;
            if (_isCompleting) _surfaceInterruptedDuringCompletion = true;
            _surface?.Detach();
            AbortAcceptedRequest("アプリケーション終了のため画面遷移を中断しました。");
            ClearDetachedSurfaceStatus();
        }

        private void InitializeIfNeeded()
        {
            if (_ownerThreadId == 0) _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            if (_engine == null) _engine = new ScreenTransitionEngine();
            if (_surface == null) _surface = new ScreenTransitionSurface();
            if (_status.Phase == ScreenTransitionPhase.Idle) _status = _engine.Status;
            var ownerDocument = GetComponent<UIDocument>();
            if (!ReferenceEquals(_uiDocument, ownerDocument)) _uiDocument = ownerDocument;
        }

        private bool EnsureSurface()
        {
            InitializeIfNeeded();
            if (_surface.IsAvailable) return true;
            return _surface.TryAttach(_uiDocument);
        }

        private void ApplyEngineStatus()
        {
            var engineStatus = _engine.Status;
            _surface.Apply(engineStatus.Request.Color, engineStatus.Opacity);
        }

        private void PublishEngineStatus()
        {
            var next = _engine.Status;
            if (_status.Phase == next.Phase && _status.Progress == next.Progress && _status.Opacity == next.Opacity) return;
            SetStatusAndNotify(next);
        }

        private void AbortAcceptedRequest(string message)
        {
            if (!_busy || _isCompleting) return;
            var request = _engine.Status.Request;
            _engine.Fail();
            CompleteAcceptedRequest(ScreenTransitionResult.Failure(request, ScreenTransitionError.ApplicationExiting, message));
        }

        private void CompleteSurfaceUnavailable(ScreenTransitionRequest request)
        {
            if (!_busy || _isCompleting) return;
            _engine.Fail();
            _surface.Detach();
            CompleteAcceptedRequest(ScreenTransitionResult.Failure(request, ScreenTransitionError.SurfaceUnavailable, "画面遷移中にUIDocumentの表示先を使用できなくなりました。"));
        }

        private void CompleteAcceptedRequest(ScreenTransitionResult result)
        {
            if (!_busy || _isCompleting) return;

            _isCompleting = true;
            _surfaceInterruptedDuringCompletion = false;
            PublishEngineStatus();
            InvokeFinished(result);

            _busy = false;
            var surfaceAvailable = !_surfaceInterruptedDuringCompletion && _surface != null && _surface.IsAvailable;
            if (!surfaceAvailable) _surface?.Detach();
            var idleOpacity = result.Error == ScreenTransitionError.ApplicationExiting ||
                              result.Error == ScreenTransitionError.SurfaceUnavailable ||
                              !surfaceAvailable
                ? 0f
                : _engine.Status.Opacity;
            _engine.Reset(idleOpacity);
            SetStatusAndNotify(_engine.Status);
            NormalizeDetachedSurfaceDuringCompletion();

            var completion = _completion;
            _completion = null;
            _surfaceInterruptedDuringCompletion = false;
            _isCompleting = false;
            DeliverCompletion(completion, result);
        }

        private void ClearDetachedSurfaceStatus()
        {
            if (_engine == null || _busy) return;
            if (_status.Phase == ScreenTransitionPhase.Idle && _status.Opacity == 0f) return;

            _engine.Reset(0f);
            SetStatusAndNotify(_engine.Status);
        }

        private void SetStatusAndNotify(ScreenTransitionStatus status)
        {
            _status = status;
            var version = ++_statusVersion;
            InvokeStatusChanged(status, version);
            if (version == _statusVersion) NormalizeDetachedSurfaceDuringCompletion();
        }

        private void InvokeStatusChanged(ScreenTransitionStatus status, int version)
        {
            var observers = _statusChangedObservers;
            _callbackDispatchDepth++;
            try
            {
                for (var i = 0; i < observers.Length; i++)
                {
                    if (version != _statusVersion) break;
                    InvokeObserver(observers[i], status);
                    if (version != _statusVersion) break;
                    if (NormalizeDetachedSurfaceDuringCompletion()) break;
                }
            }
            finally
            {
                _callbackDispatchDepth--;
            }
        }

        private bool NormalizeDetachedSurfaceDuringCompletion()
        {
            if (!_isCompleting || _status.Phase != ScreenTransitionPhase.Idle || _status.Opacity <= 0f) return false;
            if (_surface != null && _surface.IsAvailable) return false;

            _surfaceInterruptedDuringCompletion = true;
            _surface?.Detach();
            _engine.Reset(0f);
            SetStatusAndNotify(_engine.Status);
            return true;
        }

        private void InvokeFinished(ScreenTransitionResult result)
        {
            var observers = _finishedObservers;
            _callbackDispatchDepth++;
            try
            {
                for (var i = 0; i < observers.Length; i++) InvokeObserver(observers[i], result);
            }
            finally
            {
                _callbackDispatchDepth--;
            }
        }

        private static void InvokeObserver(ObserverSlot<Action<ScreenTransitionStatus>> observer, ScreenTransitionStatus status)
        {
            try
            {
                observer.Observer(status);
                observer.IsFailing = false;
            }
            catch (Exception exception)
            {
                if (observer.IsFailing) return;
                observer.IsFailing = true;
                LogCallbackException(exception);
            }
        }

        private static void InvokeObserver(ObserverSlot<Action<ScreenTransitionResult>> observer, ScreenTransitionResult result)
        {
            try
            {
                observer.Observer(result);
                observer.IsFailing = false;
            }
            catch (Exception exception)
            {
                if (observer.IsFailing) return;
                observer.IsFailing = true;
                LogCallbackException(exception);
            }
        }

        private static void DeliverCompletion(AwaitableCompletionSource<ScreenTransitionResult> completion, ScreenTransitionResult result)
        {
            if (completion == null) return;

            try
            {
                completion.SetResult(result);
            }
            catch (Exception exception)
            {
                LogCallbackException(exception);
            }
        }

        private static void LogCallbackException(Exception exception)
        {
            try
            {
                Debug.LogException(exception);
            }
            catch (Exception)
            {
                // 終了中にログ機能が使えなくても、後続通知と完了処理は続ける。
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

        private static Awaitable<ScreenTransitionResult> FromResult(ScreenTransitionResult result)
        {
            var completion = new AwaitableCompletionSource<ScreenTransitionResult>();
            completion.SetResult(result);
            return completion.Awaitable;
        }

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
            internal ObserverSlot(TObserver observer)
            {
                Observer = observer;
            }

            internal TObserver Observer { get; }

            internal bool IsFailing { get; set; }
        }
    }
}
