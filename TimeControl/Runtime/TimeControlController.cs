using System;
using System.Threading;
using UnityEngine;

namespace TimeControl
{
    /// <summary>
    /// 有効期間中だけTime.timeScaleを所有し、複数の取得権が要求する最小の相対倍率を適用する。
    /// GameObjectが所有し、取得要求と状態参照はUnityメインスレッドからだけ行う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeControlController : MonoBehaviour
    {
        /// <summary>1件の取得権が要求できる相対倍率の上限。</summary>
        public const float MaximumMultiplier = 100f;

        /// <summary>基準値と相対倍率から求めたTime.timeScaleの上限。</summary>
        public const float MaximumEffectiveTimeScale = 100f;

        private static TimeControlController _owner;

        private ObserverSlot<Action<TimeControlStatus>>[] _statusChangedObservers = Array.Empty<ObserverSlot<Action<TimeControlStatus>>>();
        private TimeControlEngine _engine;
        private TimeControlGeneration _generation;
        private TimeControlStatus _status = new TimeControlStatus(false, TimeControlError.ControllerUnavailable, 0f, 1f, 0f, 0);
        private int _ownerThreadId;
        private int _callbackDispatchDepth;
        private int _statusVersion;
        private int _releaseQueued;
        private bool _lifecycleAvailable;
        private bool _ownsStaticReservation;
        private bool _applicationExiting;
        private bool _cleanupComplete;
        private bool _processingReleases;

        /// <summary>現在の所有状態と時間倍率を表す最新スナップショット。Unityメインスレッドから参照する。</summary>
        public TimeControlStatus Status => _status;

        /// <summary>このControllerがTime.timeScaleを正常に管理中ならtrue。Unityメインスレッドから参照する。</summary>
        public bool IsControlling => _status.IsControlling;

        /// <summary>
        /// 所有状態、適用倍率、取得権数のいずれかが変わったときにスナップショットを通知する。
        /// 通知先の例外は他の通知先とControllerへ伝播しない。
        /// </summary>
        public event Action<TimeControlStatus> StatusChanged
        {
            add => _statusChangedObservers = AddObserver(_statusChangedObservers, value);
            remove => _statusChangedObservers = RemoveObserver(_statusChangedObservers, value);
        }

        /// <summary>
        /// 基準値へ掛ける相対倍率を取得し、解放可能な権利を返す。
        /// Unityメインスレッド以外、通知中、Controller利用不可、外部変更、範囲外では取得しない。
        /// </summary>
        /// <param name="multiplier">0以上100以下の有限な相対倍率。</param>
        /// <param name="lease">成功時に返す解放可能な取得権。失敗時はnull。</param>
        /// <param name="error">成功時はNone、失敗時は取得できなかった理由。</param>
        /// <returns>取得して適用後の状態まで確認できた場合はtrue。</returns>
        public bool TryAcquire(float multiplier, out TimeScaleLease lease, out TimeControlError error)
        {
            lease = null;
            error = TimeControlError.None;

            if (_applicationExiting)
            {
                error = TimeControlError.ApplicationExiting;
                return false;
            }

            if (!_lifecycleAvailable)
            {
                error = TimeControlError.ControllerUnavailable;
                return false;
            }

            if (!IsMainThread())
            {
                error = TimeControlError.MainThreadRequired;
                return false;
            }

            if (_callbackDispatchDepth > 0)
            {
                error = TimeControlError.Busy;
                return false;
            }

            if (!_ownsStaticReservation)
            {
                error = TryClaimOwnership();
                if (error != TimeControlError.None) return false;
            }

            if (_engine == null || !_engine.IsControlling)
            {
                error = _status.Error == TimeControlError.None ? TimeControlError.ControllerUnavailable : _status.Error;
                return false;
            }

            error = CheckForExternalChange();
            if (error != TimeControlError.None) return false;

            error = TimeScaleResolver.ValidateMultiplier(_engine.BaselineTimeScale, multiplier, out _);
            if (error != TimeControlError.None) return false;

            var generation = _generation;
            if (generation == null)
            {
                error = TimeControlError.ControllerUnavailable;
                return false;
            }

            var leaseId = generation.Add(multiplier);
            if (leaseId == 0L)
            {
                error = TimeControlError.ControllerUnavailable;
                return false;
            }

            var effectiveMultiplier = TimeScaleResolver.ResolveMinimum(generation.SnapshotMultipliers());
            error = _engine.Apply(effectiveMultiplier, out var actualTimeScale);
            if (error != TimeControlError.None)
            {
                FaultOwnership(error, actualTimeScale);
                return false;
            }

            var createdLease = new TimeScaleLease(generation, leaseId, multiplier);
            SetStatusAndNotify(CreateControllingStatus(effectiveMultiplier, actualTimeScale));

            if (!_lifecycleAvailable || !ReferenceEquals(generation, _generation) || !_engine.IsControlling)
            {
                createdLease.Dispose();
                error = _status.Error == TimeControlError.None ? TimeControlError.ControllerUnavailable : _status.Error;
                return false;
            }

            lease = createdLease;
            error = TimeControlError.None;
            return true;
        }

        private void Awake()
        {
            CaptureMainThread();
            InitializeEngineIfNeeded();
        }

        private void OnEnable()
        {
            CaptureMainThread();
            InitializeEngineIfNeeded();
            _applicationExiting = false;
            _cleanupComplete = false;
            _lifecycleAvailable = true;
            TryClaimOwnership();
        }

        private void Update()
        {
            if (!_lifecycleAvailable || !_ownsStaticReservation || _engine == null || !_engine.IsControlling) return;
            if (CheckForExternalChange() != TimeControlError.None) return;
            ProcessQueuedReleases(_generation);
        }

        private void OnDisable()
        {
            _lifecycleAvailable = false;
            if (_cleanupComplete) return;
            CleanupOwnership(_applicationExiting ? TimeControlError.ApplicationExiting : TimeControlError.ControllerUnavailable);
        }

        private void OnDestroy()
        {
            _lifecycleAvailable = false;
            if (_cleanupComplete) return;
            CleanupOwnership(_applicationExiting ? TimeControlError.ApplicationExiting : TimeControlError.ControllerUnavailable);
        }

        private void OnApplicationQuit()
        {
            _applicationExiting = true;
            _lifecycleAvailable = false;
            if (_cleanupComplete) return;
            CleanupOwnership(TimeControlError.ApplicationExiting);
        }

        /// <summary>任意スレッドから届いた解放要求を、主スレッドなら同期適用し、それ以外なら次のUpdateへ残す。</summary>
        /// <param name="generation">解放された取得権が属していた世代。</param>
        internal void OnLeaseReleaseQueued(TimeControlGeneration generation)
        {
            Interlocked.Exchange(ref _releaseQueued, 1);
            if (!IsMainThread()) return;
            if (_callbackDispatchDepth > 0 || _processingReleases) return;
            ProcessQueuedReleases(generation);
        }

        /// <summary>Play Mode開始時に以前の実行から残った静的所有者を消す。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticOwner()
        {
            _owner = null;
        }

        private void CaptureMainThread()
        {
            if (_ownerThreadId == 0) _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private bool IsMainThread()
        {
            var ownerThreadId = Volatile.Read(ref _ownerThreadId);
            return ownerThreadId != 0 && Thread.CurrentThread.ManagedThreadId == ownerThreadId;
        }

        private void InitializeEngineIfNeeded()
        {
            if (_engine == null) _engine = new TimeControlEngine(UnityTimeScaleBackend.Instance);
        }

        private TimeControlError TryClaimOwnership()
        {
            if (_ownsStaticReservation) return _engine != null && _engine.IsControlling ? TimeControlError.None : _status.Error;
            if (!ReferenceEquals(_owner, null) && !ReferenceEquals(_owner, this))
            {
                SetStatusAndNotify(new TimeControlStatus(false, TimeControlError.OwnerAlreadyExists, 0f, 1f, 0f, 0));
                return TimeControlError.OwnerAlreadyExists;
            }

            _owner = this;
            _ownsStaticReservation = true;
            var startError = _engine.TryStart(out var actualTimeScale);
            if (startError != TimeControlError.None)
            {
                SetStatusAndNotify(new TimeControlStatus(false, startError, actualTimeScale, 1f, actualTimeScale, 0));
                return startError;
            }

            _generation = new TimeControlGeneration(this);
            SetStatusAndNotify(CreateControllingStatus(1f, actualTimeScale));
            if (!_lifecycleAvailable || !_engine.IsControlling)
            {
                return _status.Error == TimeControlError.None ? TimeControlError.ControllerUnavailable : _status.Error;
            }

            return TimeControlError.None;
        }

        private TimeControlError CheckForExternalChange()
        {
            if (_engine == null || !_engine.IsControlling) return _status.Error;
            var error = _engine.CheckExpected(out var actualTimeScale);
            if (error == TimeControlError.None) return TimeControlError.None;
            FaultOwnership(error, actualTimeScale);
            return error;
        }

        private void ProcessQueuedReleases(TimeControlGeneration generation)
        {
            if (_processingReleases || generation == null || !ReferenceEquals(generation, _generation)) return;

            _processingReleases = true;
            try
            {
                while (_lifecycleAvailable && _engine != null && _engine.IsControlling && ReferenceEquals(generation, _generation))
                {
                    Interlocked.Exchange(ref _releaseQueued, 0);
                    if (CheckForExternalChange() != TimeControlError.None) break;
                    if (!generation.DrainPending(out var multipliers)) break;

                    var effectiveMultiplier = TimeScaleResolver.ResolveMinimum(multipliers);
                    var error = _engine.Apply(effectiveMultiplier, out var actualTimeScale);
                    if (error != TimeControlError.None)
                    {
                        FaultOwnership(error, actualTimeScale);
                        break;
                    }

                    SetStatusAndNotify(CreateControllingStatus(effectiveMultiplier, actualTimeScale));
                }
            }
            finally
            {
                _processingReleases = false;
            }
        }

        private void FaultOwnership(TimeControlError error, float actualTimeScale)
        {
            if (_engine == null || !_ownsStaticReservation) return;
            _engine.Fault();
            CloseGeneration();
            SetStatusAndNotify(new TimeControlStatus(false, error, _engine.BaselineTimeScale, 1f, actualTimeScale, 0));
        }

        private void CleanupOwnership(TimeControlError lifecycleError)
        {
            _cleanupComplete = true;
            var terminalError = lifecycleError;
            var previousError = _status.Error;
            var baseline = _engine?.BaselineTimeScale ?? 0f;
            var actualTimeScale = _status.EffectiveTimeScale;

            CloseGeneration();
            if (_engine != null && _engine.HasReservation)
            {
                var stopError = _engine.Stop(out actualTimeScale);
                if (lifecycleError != TimeControlError.ApplicationExiting &&
                    stopError != TimeControlError.None && stopError != TimeControlError.ControllerUnavailable)
                {
                    terminalError = stopError;
                }
                else if (lifecycleError != TimeControlError.ApplicationExiting && previousError != TimeControlError.None)
                {
                    terminalError = previousError;
                }
            }

            if (ReferenceEquals(_owner, this)) _owner = null;
            _ownsStaticReservation = false;
            Interlocked.Exchange(ref _releaseQueued, 0);
            SetStatusAndNotify(new TimeControlStatus(false, terminalError, baseline, 1f, actualTimeScale, 0));
        }

        private void CloseGeneration()
        {
            var generation = _generation;
            _generation = null;
            generation?.Close();
        }

        private TimeControlStatus CreateControllingStatus(float effectiveMultiplier, float actualTimeScale)
        {
            return new TimeControlStatus(
                true,
                TimeControlError.None,
                _engine.BaselineTimeScale,
                effectiveMultiplier,
                actualTimeScale,
                _generation?.ActiveLeaseCount ?? 0);
        }

        private void SetStatusAndNotify(TimeControlStatus status)
        {
            _status = status;
            var version = ++_statusVersion;
            InvokeStatusChanged(status, version);
            if (_callbackDispatchDepth == 0 && !_processingReleases && Volatile.Read(ref _releaseQueued) != 0)
            {
                ProcessQueuedReleases(_generation);
            }
        }

        private void InvokeStatusChanged(TimeControlStatus status, int version)
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
                    if (_engine != null && _engine.IsControlling && CheckForExternalChange() != TimeControlError.None) break;
                }
            }
            finally
            {
                _callbackDispatchDepth--;
            }
        }

        private static void InvokeObserver(ObserverSlot<Action<TimeControlStatus>> observer, TimeControlStatus status)
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
                try
                {
                    Debug.LogException(exception);
                }
                catch (Exception)
                {
                    // 終了中にログ機構を利用できなくても、残りの通知と所有終了を続ける。
                }
            }
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
                    if (!Equals(observers[start + i].Observer, removals[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches) continue;
                var result = new ObserverSlot<TObserver>[observers.Length - removals.Length];
                Array.Copy(observers, 0, result, 0, start);
                Array.Copy(observers, start + removals.Length, result, start, observers.Length - start - removals.Length);
                return result;
            }

            return observers;
        }

        private sealed class ObserverSlot<TObserver> where TObserver : Delegate
        {
            /// <summary>個別に呼び出す通知先を保持する。</summary>
            /// <param name="observer">保持する単一の通知先。</param>
            internal ObserverSlot(TObserver observer)
            {
                Observer = observer;
            }

            /// <summary>呼び出す単一の通知先。</summary>
            internal TObserver Observer { get; }

            /// <summary>前回の呼出しが失敗し、同じ連続失敗を記録済みならtrue。</summary>
            internal bool IsFailing { get; set; }
        }
    }
}
