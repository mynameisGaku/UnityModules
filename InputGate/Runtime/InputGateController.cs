using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputGate
{
    /// <summary>
    /// PlayerInputが実行時に所有する指定Action Mapを、取得権が1件以上ある間だけ停止する。
    /// GameObjectが所有し、取得要求と状態参照はUnityメインスレッドからだけ行う。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class InputGateController : MonoBehaviour
    {
        private static readonly Dictionary<InputActionMap, InputGateController> Owners =
            new Dictionary<InputActionMap, InputGateController>(ReferenceEqualityComparer<InputActionMap>.Instance);
        private static bool ApplicationIsExiting;

        [SerializeField]
        private PlayerInput _playerInput;

        [SerializeField]
        private string[] _blockedActionMapNames = { "Gameplay" };

        private ObserverSlot<Action<InputGateStatus>>[] _statusChangedObservers =
            Array.Empty<ObserverSlot<Action<InputGateStatus>>>();
        private ControlledActionMapSet _controlledMaps;
        private InputGateGeneration _generation;
        private string[] _ownedMapNames = Array.Empty<string>();
        private InputGateStatus _status = new InputGateStatus(false, false, InputGateError.ControllerUnavailable, 0, 0);
        private int _ownerThreadId;
        private int _callbackDispatchDepth;
        private int _operationDepth;
        private int _statusVersion;
        private int _releaseQueued;
        private bool _lifecycleAvailable;
        private bool _ownsReservations;
        private bool _applicationExiting;
        private bool _cleanupComplete;
        private bool _cleanupInProgress;
        private bool _processingReleases;
        private bool _restartRequested;

        /// <summary>現在の所有準備、停止状態、取得権数を表す最新スナップショット。</summary>
        public InputGateStatus Status => _status;

        /// <summary>1件以上の取得権により対象Action Mapを停止中ならtrue。</summary>
        public bool IsBlocking => _status.IsBlocking;

        /// <summary>
        /// 所有準備、停止状態、取得権数のいずれかが変わったときにスナップショットを通知する。
        /// 通知先の例外は他の通知先とControllerへ伝播しない。
        /// </summary>
        public event Action<InputGateStatus> StatusChanged
        {
            add => _statusChangedObservers = AddObserver(_statusChangedObservers, value);
            remove => _statusChangedObservers = RemoveObserver(_statusChangedObservers, value);
        }

        /// <summary>
        /// 設定済みAction Mapの停止権を取得し、解放可能なleaseを返す。
        /// Unityメインスレッド以外、通知中、Controller利用不可、外部変更時は取得しない。
        /// </summary>
        /// <param name="lease">成功時に返す解放可能な取得権。失敗時はnull。</param>
        /// <param name="error">成功時はNone、失敗時は取得できなかった理由。</param>
        /// <returns>取得とAction停止後の確認まで完了した場合はtrue。</returns>
        public bool TryAcquire(out InputGateLease lease, out InputGateError error)
        {
            lease = null;
            error = InputGateError.None;

            if (_applicationExiting)
            {
                error = InputGateError.ApplicationExiting;
                return false;
            }

            if (!_lifecycleAvailable)
            {
                error = InputGateError.ControllerUnavailable;
                return false;
            }

            if (!IsMainThread())
            {
                error = InputGateError.MainThreadRequired;
                return false;
            }

            if (_callbackDispatchDepth > 0 || _operationDepth > 0 || _processingReleases)
            {
                error = InputGateError.Busy;
                return false;
            }

            if (!_ownsReservations)
            {
                error = TryClaimOwnership();
                if (error != InputGateError.None) return false;
            }

            if (!_status.IsReady || _generation == null || _controlledMaps == null)
            {
                error = _status.Error == InputGateError.None ? InputGateError.ControllerUnavailable : _status.Error;
                return false;
            }

            error = CheckForExternalChange();
            if (error != InputGateError.None) return false;

            var generation = _generation;
            var wasEmpty = generation.ActiveLeaseCount == 0;
            var leaseId = generation.Add();
            if (leaseId == 0L)
            {
                error = InputGateError.ControllerUnavailable;
                return false;
            }

            if (wasEmpty)
            {
                error = RunMapOperation(() => _controlledMaps.BeginBlocking(ConfigurationStillMatches, () => _lifecycleAvailable));
                if (!_lifecycleAvailable)
                {
                    CleanupOwnership(_applicationExiting ? InputGateError.ApplicationExiting : InputGateError.ControllerUnavailable);
                    error = _status.Error == InputGateError.None
                        ? InputGateError.ControllerUnavailable
                        : _status.Error;
                    return false;
                }

                if (error != InputGateError.None)
                {
                    FaultOwnership(error);
                    return false;
                }

                error = CheckForExternalChange();
                if (error != InputGateError.None) return false;
            }

            var createdLease = new InputGateLease(generation, leaseId);
            SetStatusAndNotify(CreateReadyStatus(true, generation.ActiveLeaseCount));

            if (!_lifecycleAvailable || !ReferenceEquals(generation, _generation) || !_status.IsReady || !createdLease.IsActive)
            {
                createdLease.Dispose();
                error = _status.Error == InputGateError.None ? InputGateError.ControllerUnavailable : _status.Error;
                return false;
            }

            lease = createdLease;
            error = InputGateError.None;
            return true;
        }

        private void Awake()
        {
            CaptureMainThread();
            if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            CaptureMainThread();
            if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
            if (ApplicationIsExiting)
            {
                _applicationExiting = true;
                _lifecycleAvailable = false;
                _restartRequested = false;
                return;
            }

            _applicationExiting = false;
            if (_cleanupInProgress || _operationDepth > 0)
            {
                _restartRequested = true;
                return;
            }

            BeginLifecycle();
        }

        private void BeginLifecycle()
        {
            _restartRequested = false;
            _cleanupComplete = false;
            _cleanupInProgress = false;
            _lifecycleAvailable = true;
            TryClaimOwnership();
        }

        private void Update()
        {
            if (!_lifecycleAvailable || !_ownsReservations || !_status.IsReady) return;
            if (CheckForExternalChange() != InputGateError.None) return;
            ProcessQueuedReleases(_generation);
        }

        private void OnDisable()
        {
            InterruptLifecycle(false);
        }

        private void OnDestroy()
        {
            InterruptLifecycle(false);
        }

        private void OnApplicationQuit()
        {
            ApplicationIsExiting = true;
            InterruptLifecycle(true);
        }

        /// <summary>任意スレッドから届いた解放要求を、主スレッドなら同期適用し、それ以外なら次のUpdateへ残す。</summary>
        /// <param name="generation">解放された取得権が属していた世代。</param>
        internal void OnLeaseReleaseQueued(InputGateGeneration generation)
        {
            Interlocked.Exchange(ref _releaseQueued, 1);
            if (!IsMainThread()) return;
            if (_callbackDispatchDepth > 0 || _operationDepth > 0 || _processingReleases) return;
            ProcessQueuedReleases(generation);
        }

        /// <summary>テストfixtureからOnEnable前のPlayerInputとMap名を設定する。</summary>
        /// <param name="playerInput">実行中Action Assetを所有するPlayerInput。</param>
        /// <param name="mapNames">停止対象のAction Map名。</param>
        internal void ConfigureForTests(PlayerInput playerInput, string[] mapNames)
        {
            _playerInput = playerInput;
            _blockedActionMapNames = mapNames;
        }

        /// <summary>Play Mode開始時に以前の実行から残った静的所有者を消す。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticOwners()
        {
            Owners.Clear();
            ApplicationIsExiting = false;
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

        private InputGateError TryClaimOwnership()
        {
            if (_ownsReservations) return _status.IsReady ? InputGateError.None : _status.Error;
            var resolveError = TryResolveConfiguredMaps(out var maps, out var names);
            if (resolveError != InputGateError.None)
            {
                SetStatusAndNotify(new InputGateStatus(false, false, resolveError, 0, 0));
                return resolveError;
            }

            for (var i = 0; i < maps.Length; i++)
            {
                if (Owners.TryGetValue(maps[i], out var owner) && !ReferenceEquals(owner, this))
                {
                    SetStatusAndNotify(new InputGateStatus(false, false, InputGateError.OwnerAlreadyExists, 0, 0));
                    return InputGateError.OwnerAlreadyExists;
                }
            }

            for (var i = 0; i < maps.Length; i++) Owners[maps[i]] = this;
            _controlledMaps = new ControlledActionMapSet(maps);
            _ownedMapNames = names;
            _generation = new InputGateGeneration(this);
            _ownsReservations = true;
            SetStatusAndNotify(CreateReadyStatus(false, 0));

            if (!_lifecycleAvailable || !_ownsReservations)
            {
                return _status.Error == InputGateError.None ? InputGateError.ControllerUnavailable : _status.Error;
            }

            return InputGateError.None;
        }

        private InputGateError TryResolveConfiguredMaps(out InputActionMap[] maps, out string[] names)
        {
            maps = Array.Empty<InputActionMap>();
            names = Array.Empty<string>();
            var actions = _playerInput == null ? null : _playerInput.actions;
            if (actions == null || _blockedActionMapNames == null || _blockedActionMapNames.Length == 0)
            {
                return InputGateError.InvalidConfiguration;
            }

            maps = new InputActionMap[_blockedActionMapNames.Length];
            names = new string[_blockedActionMapNames.Length];
            var seen = new HashSet<InputActionMap>(ReferenceEqualityComparer<InputActionMap>.Instance);
            for (var i = 0; i < _blockedActionMapNames.Length; i++)
            {
                var name = _blockedActionMapNames[i];
                if (string.IsNullOrWhiteSpace(name)) return InputGateError.InvalidConfiguration;
                var map = actions.FindActionMap(name, false);
                if (map == null) return InputGateError.ActionMapNotFound;
                if (!seen.Add(map)) return InputGateError.DuplicateActionMap;
                maps[i] = map;
                names[i] = name;
            }

            return InputGateError.None;
        }

        private bool ConfigurationStillMatches()
        {
            if (_playerInput == null || _playerInput.actions == null || _controlledMaps == null ||
                _ownedMapNames.Length != _controlledMaps.Count)
            {
                return false;
            }

            for (var i = 0; i < _ownedMapNames.Length; i++)
            {
                var current = _playerInput.actions.FindActionMap(_ownedMapNames[i], false);
                if (!ReferenceEquals(current, _controlledMaps.GetMap(i))) return false;
            }

            return true;
        }

        private InputGateError CheckForExternalChange()
        {
            if (!_ownsReservations || !_status.IsReady) return _status.Error;
            if (!ConfigurationStillMatches())
            {
                FaultOwnership(InputGateError.ExternalActionStateChanged);
                return InputGateError.ExternalActionStateChanged;
            }

            var error = _controlledMaps.CheckBlocked();
            if (error != InputGateError.None) FaultOwnership(error);
            return error;
        }

        private void ProcessQueuedReleases(InputGateGeneration generation)
        {
            if (_processingReleases || generation == null || !ReferenceEquals(generation, _generation)) return;

            _processingReleases = true;
            try
            {
                while (_lifecycleAvailable && _ownsReservations && _status.IsReady && ReferenceEquals(generation, _generation))
                {
                    Interlocked.Exchange(ref _releaseQueued, 0);
                    if (CheckForExternalChange() != InputGateError.None) break;
                    if (!generation.DrainPending(out var activeLeaseCount)) break;

                    if (activeLeaseCount > 0)
                    {
                        SetStatusAndNotify(CreateReadyStatus(true, activeLeaseCount));
                        continue;
                    }

                    var restoreError = RunMapOperation(() => _controlledMaps.Restore(ConfigurationStillMatches));
                    if (!_lifecycleAvailable)
                    {
                        CleanupOwnership(_applicationExiting ? InputGateError.ApplicationExiting : InputGateError.ControllerUnavailable);
                        break;
                    }

                    if (restoreError != InputGateError.None)
                    {
                        FaultOwnership(restoreError);
                        break;
                    }

                    if (!ConfigurationStillMatches())
                    {
                        FaultOwnership(InputGateError.ExternalActionStateChanged);
                        break;
                    }

                    CloseGeneration();
                    _generation = new InputGateGeneration(this);
                    generation = _generation;
                    SetStatusAndNotify(CreateReadyStatus(false, 0));
                }
            }
            finally
            {
                _processingReleases = false;
            }
        }

        private InputGateError RunMapOperation(Func<InputGateError> operation)
        {
            _operationDepth++;
            try
            {
                return operation();
            }
            finally
            {
                _operationDepth--;
            }
        }

        private void InterruptLifecycle(bool applicationExiting)
        {
            if (applicationExiting) _applicationExiting = true;
            _lifecycleAvailable = false;
            _restartRequested = false;
            if (_cleanupComplete || _cleanupInProgress) return;
            if (_operationDepth > 0)
            {
                return;
            }

            CleanupOwnership(_applicationExiting ? InputGateError.ApplicationExiting : InputGateError.ControllerUnavailable);
        }

        private void CleanupOwnership(InputGateError lifecycleError)
        {
            if (_cleanupComplete || _cleanupInProgress) return;
            _cleanupInProgress = true;
            try
            {
                _cleanupComplete = true;
                var terminalError = lifecycleError;
                var previousError = _status.Error;

                CloseGeneration();
                if (_controlledMaps != null && _controlledMaps.IsBlocking)
                {
                    var restoreError = RunMapOperation(() => _controlledMaps.Restore(ConfigurationStillMatches));
                    if (lifecycleError != InputGateError.ApplicationExiting && restoreError != InputGateError.None)
                    {
                        terminalError = restoreError;
                    }
                }
                else if (lifecycleError != InputGateError.ApplicationExiting && previousError != InputGateError.None)
                {
                    terminalError = previousError;
                }

                ReleaseReservations();
                Interlocked.Exchange(ref _releaseQueued, 0);
                SetStatusAndNotify(new InputGateStatus(false, false, terminalError, 0, 0));
            }
            finally
            {
                _cleanupInProgress = false;
                if (_restartRequested && !_applicationExiting && isActiveAndEnabled)
                {
                    BeginLifecycle();
                }
            }
        }

        private void FaultOwnership(InputGateError error)
        {
            if (!_ownsReservations) return;
            _controlledMaps?.Abandon();
            CloseGeneration();
            SetStatusAndNotify(new InputGateStatus(false, false, error, _controlledMaps?.Count ?? 0, 0));
        }

        private void ReleaseReservations()
        {
            if (_controlledMaps != null)
            {
                for (var i = 0; i < _controlledMaps.Count; i++)
                {
                    var map = _controlledMaps.GetMap(i);
                    if (Owners.TryGetValue(map, out var owner) && ReferenceEquals(owner, this)) Owners.Remove(map);
                }
            }

            _ownsReservations = false;
            _controlledMaps = null;
            _ownedMapNames = Array.Empty<string>();
        }

        private void CloseGeneration()
        {
            var generation = _generation;
            _generation = null;
            generation?.Close();
        }

        private InputGateStatus CreateReadyStatus(bool isBlocking, int activeLeaseCount)
        {
            return new InputGateStatus(true, isBlocking, InputGateError.None, _controlledMaps?.Count ?? 0, activeLeaseCount);
        }

        private void SetStatusAndNotify(InputGateStatus status)
        {
            _status = status;
            var version = ++_statusVersion;
            InvokeStatusChanged(status, version);
            if (_callbackDispatchDepth == 0 && _operationDepth == 0 && !_processingReleases &&
                Volatile.Read(ref _releaseQueued) != 0)
            {
                ProcessQueuedReleases(_generation);
            }
        }

        private void InvokeStatusChanged(InputGateStatus status, int version)
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
                    if (_ownsReservations && _status.IsReady && CheckForExternalChange() != InputGateError.None) break;
                }
            }
            finally
            {
                _callbackDispatchDepth--;
            }
        }

        private static void InvokeObserver(ObserverSlot<Action<InputGateStatus>> observer, InputGateStatus status)
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

        private static ObserverSlot<TObserver>[] AddObserver<TObserver>(ObserverSlot<TObserver>[] observers, TObserver observer)
            where TObserver : Delegate
        {
            if (observer == null) return observers;
            var additions = observer.GetInvocationList();
            var result = new ObserverSlot<TObserver>[observers.Length + additions.Length];
            Array.Copy(observers, result, observers.Length);
            for (var i = 0; i < additions.Length; i++)
            {
                result[observers.Length + i] = new ObserverSlot<TObserver>((TObserver)additions[i]);
            }

            return result;
        }

        private static ObserverSlot<TObserver>[] RemoveObserver<TObserver>(ObserverSlot<TObserver>[] observers, TObserver observer)
            where TObserver : Delegate
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
