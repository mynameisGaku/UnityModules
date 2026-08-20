using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace StartupFlow
{
    /// <summary>
    /// 明示されたstartup stepを決定論的な順序で1件ずつ実行し、進捗と停止位置を結果として返す。
    /// 利用側が所有し、Unityメインスレッドから使う。
    /// </summary>
    public sealed class StartupFlowService
    {
        /// <summary>1回のflowで受理できるstep数。</summary>
        public const int MaximumStepCount = 128;

        /// <summary>step識別子に使えるUTF-16文字数。</summary>
        public const int MaximumStepIdLength = 128;

        private readonly IStartupFlowBackend _backend;
        private ObserverSlot<Action<StartupFlowStatus>>[] _statusObservers = Array.Empty<ObserverSlot<Action<StartupFlowStatus>>>();
        private ObserverSlot<Action<StartupFlowResult>>[] _finishedObservers = Array.Empty<ObserverSlot<Action<StartupFlowResult>>>();
        private StartupFlowStatus _status = IdleStatus();
        private StartupStepContext _activeContext;
        private CancellationTokenSource _runCancellation;
        private CancellationToken _runExitToken;
        private AwaitableCompletionSource<StartupFlowResult> _completion;
        private bool _busy;
        private bool _isDispatching;
        private bool _isCompleting;

        /// <summary>Unity runtimeへ接続するstartup flowを作る。実行はUnityメインスレッドから開始する。</summary>
        public StartupFlowService() : this(new UnityStartupFlowBackend())
        {
        }

        /// <summary>threadと終了状態を置き換えたテスト可能なstartup flowを作る。</summary>
        /// <param name="backend">thread、終了token、observer例外記録を提供する内部境界。</param>
        internal StartupFlowService(IStartupFlowBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>現在の段階、step、件数、進捗。</summary>
        public StartupFlowStatus Status => _status;

        /// <summary>flow実行中、完了処理中、またはcallback通知中ならtrue。</summary>
        public bool IsBusy => _busy || _isCompleting || _isDispatching;

        /// <summary>受理したflowの段階または進捗が変化したときにUnityメインスレッドで通知する。</summary>
        public event Action<StartupFlowStatus> StatusChanged
        {
            add => _statusObservers = AddObserver(_statusObservers, value);
            remove => _statusObservers = RemoveObserver(_statusObservers, value);
        }

        /// <summary>受理したflowが成功、失敗、または中止で確定したときにUnityメインスレッドで通知する。</summary>
        public event Action<StartupFlowResult> Finished
        {
            add => _finishedObservers = AddObserver(_finishedObservers, value);
            remove => _finishedObservers = RemoveObserver(_finishedObservers, value);
        }

        /// <summary>
        /// stepをOrder昇順、同値ならIdのordinal昇順で直列実行する。
        /// Busy、thread違反、終了済み、cancel済み、または不正一覧はstepを呼ばず早期結果として返す。
        /// </summary>
        /// <param name="steps">実行するstep一覧。実行開始時にIdとOrderをsnapshotする。</param>
        /// <param name="cancellationToken">利用側が中止を要求するtoken。stepへApplication終了tokenと結合して渡す。</param>
        /// <returns>停止位置と完了件数を含む結果。</returns>
        public Awaitable<StartupFlowResult> RunAsync(IReadOnlyList<IStartupStep> steps, CancellationToken cancellationToken = default)
        {
            if (IsBusy)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.Busy, string.Empty, 0, 0, "別のstartup flowを実行中、または通知中です。"));
            }

            bool isMainThread;
            try
            {
                isMainThread = _backend.IsMainThread;
            }
            catch (Exception exception)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.OperationFailed, string.Empty, 0, 0, $"実行環境を確認できませんでした: {GetExceptionMessage(exception)}"));
            }

            if (!isMainThread)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.MainThreadRequired, string.Empty, 0, 0, "StartupFlowServiceはUnityメインスレッドから実行してください。"));
            }

            CancellationToken exitToken;
            try
            {
                exitToken = _backend.ExitToken;
            }
            catch (Exception exception)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.OperationFailed, string.Empty, 0, SafeCount(steps), $"終了状態を確認できませんでした: {GetExceptionMessage(exception)}"));
            }

            if (exitToken.IsCancellationRequested)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.ApplicationExiting, string.Empty, 0, SafeCount(steps), "Play Modeまたはアプリケーションが終了しています。"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return FromResult(StartupFlowResult.Failure(StartupFlowError.Canceled, string.Empty, 0, SafeCount(steps), "startup flowは開始前に中止されました。"));
            }

            if (!TrySnapshot(steps, out var entries, out var validationFailure)) return FromResult(validationFailure);

            var completion = new AwaitableCompletionSource<StartupFlowResult>();
            _completion = completion;
            _runExitToken = exitToken;
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, exitToken);
            _busy = true;
            SetStatus(new StartupFlowStatus(StartupFlowPhase.Validating, string.Empty, -1, entries.Length, 0f, entries.Length == 0 ? 1f : 0f));
            ExecuteAcceptedAsync(entries, _runCancellation.Token);
            return completion.Awaitable;
        }

        private async void ExecuteAcceptedAsync(Entry[] entries, CancellationToken cancellationToken)
        {
            var completedCount = 0;
            var currentStepId = string.Empty;
            try
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    currentStepId = entries[index].Id;
                    if (TryCompleteCancellation(currentStepId, completedCount, entries.Length, cancellationToken)) return;

                    var context = new StartupStepContext(currentStepId, cancellationToken, ReportProgress);
                    _activeContext = context;
                    SetStatus(CreateRunningStatus(currentStepId, index, entries.Length, completedCount, 0f));
                    if (TryCompleteCancellation(currentStepId, completedCount, entries.Length, cancellationToken)) return;

                    Exception stepFailure = null;
                    var canceledByStep = false;
                    try
                    {
                        var operation = entries[index].Step.ExecuteAsync(context);
                        if (operation == null) throw new InvalidOperationException("stepがnull Awaitableを返しました。");
                        await operation;
                    }
                    catch (OperationCanceledException)
                    {
                        canceledByStep = true;
                    }
                    catch (Exception exception)
                    {
                        stepFailure = exception;
                    }

                    await Awaitable.MainThreadAsync();
                    context.Deactivate();
                    if (ReferenceEquals(_activeContext, context)) _activeContext = null;

                    if (TryCompleteCancellation(currentStepId, completedCount, entries.Length, cancellationToken)) return;
                    if (canceledByStep)
                    {
                        CompleteAcceptedFlow(StartupFlowResult.Failure(StartupFlowError.StepFailed, currentStepId, completedCount, entries.Length, $"step '{currentStepId}' がCancellationToken未要求のまま中止されました。"));
                        return;
                    }

                    if (stepFailure != null)
                    {
                        CompleteAcceptedFlow(StartupFlowResult.Failure(StartupFlowError.StepFailed, currentStepId, completedCount, entries.Length, $"step '{currentStepId}' が失敗しました: {GetExceptionMessage(stepFailure)}"));
                        return;
                    }

                    completedCount++;
                    SetStatus(CreateRunningStatus(currentStepId, index, entries.Length, completedCount - 1, 1f));
                }

                if (TryCompleteCancellation(currentStepId, completedCount, entries.Length, cancellationToken)) return;
                CompleteAcceptedFlow(StartupFlowResult.Success(entries.Length));
            }
            catch (Exception exception)
            {
                try
                {
                    await Awaitable.MainThreadAsync();
                }
                catch
                {
                    return;
                }

                if (_busy)
                {
                    CompleteAcceptedFlow(StartupFlowResult.Failure(StartupFlowError.OperationFailed, currentStepId, completedCount, entries.Length, $"startup flowの実行に失敗しました: {GetExceptionMessage(exception)}"));
                }
            }
        }

        private StartupFlowError ReportProgress(StartupStepContext context, float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f || progress > 1f) return StartupFlowError.InvalidProgress;

            bool isMainThread;
            try
            {
                isMainThread = _backend.IsMainThread;
            }
            catch
            {
                return StartupFlowError.OperationFailed;
            }

            if (!isMainThread) return StartupFlowError.MainThreadRequired;
            if (_isDispatching || _isCompleting) return StartupFlowError.Busy;
            if (!ReferenceEquals(context, _activeContext) || _status.Phase != StartupFlowPhase.Running) return StartupFlowError.StepNotActive;
            if (_runCancellation == null || _runCancellation.IsCancellationRequested) return _runExitToken.IsCancellationRequested ? StartupFlowError.ApplicationExiting : StartupFlowError.Canceled;
            if (progress < _status.StepProgress) return StartupFlowError.InvalidProgress;

            SetStatus(CreateRunningStatus(_status.StepId, _status.StepIndex, _status.TotalStepCount, _status.StepIndex, progress));
            return StartupFlowError.None;
        }

        private bool TryCompleteCancellation(string stepId, int completedCount, int totalCount, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested) return false;

            var exiting = _runExitToken.IsCancellationRequested;
            CompleteAcceptedFlow(StartupFlowResult.Failure(
                exiting ? StartupFlowError.ApplicationExiting : StartupFlowError.Canceled,
                stepId,
                completedCount,
                totalCount,
                exiting ? "Play Modeまたはアプリケーションの終了によりstartup flowを中止しました。" : "利用側の要求によりstartup flowを中止しました。"));
            return true;
        }

        private void CompleteAcceptedFlow(StartupFlowResult result)
        {
            if (!_busy || _isCompleting) return;

            _isCompleting = true;
            _activeContext?.Deactivate();
            _activeContext = null;
            var terminalProgress = result.IsSuccess ? 1f : _status.OverallProgress;
            SetStatus(new StartupFlowStatus(result.IsSuccess ? StartupFlowPhase.Completed : StartupFlowPhase.Failed, result.FailedStepId, _status.StepIndex, result.TotalStepCount, _status.StepProgress, terminalProgress));
            InvokeFinished(result);

            var completion = _completion;
            _completion = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
            _runExitToken = default;
            _busy = false;
            SetStatus(IdleStatus());
            _isCompleting = false;
            DeliverCompletion(completion, result);
        }

        private void DeliverCompletion(AwaitableCompletionSource<StartupFlowResult> completion, StartupFlowResult result)
        {
            if (completion == null) return;
            try
            {
                completion.SetResult(result);
            }
            catch (Exception exception)
            {
                TryLogObserverException(exception);
            }
        }

        private void SetStatus(StartupFlowStatus status)
        {
            _status = status;
            var snapshot = _statusObservers;
            var previousDispatch = _isDispatching;
            _isDispatching = true;
            try
            {
                for (var index = 0; index < snapshot.Length; index++) InvokeObserver(snapshot[index], observer => observer(status));
            }
            finally
            {
                _isDispatching = previousDispatch;
            }
        }

        private void InvokeFinished(StartupFlowResult result)
        {
            var snapshot = _finishedObservers;
            var previousDispatch = _isDispatching;
            _isDispatching = true;
            try
            {
                for (var index = 0; index < snapshot.Length; index++) InvokeObserver(snapshot[index], observer => observer(result));
            }
            finally
            {
                _isDispatching = previousDispatch;
            }
        }

        private void InvokeObserver<TObserver>(ObserverSlot<TObserver> slot, Action<TObserver> invoke) where TObserver : Delegate
        {
            try
            {
                invoke(slot.Observer);
                slot.HasConsecutiveFailure = false;
            }
            catch (Exception exception)
            {
                if (!slot.HasConsecutiveFailure) TryLogObserverException(exception);
                slot.HasConsecutiveFailure = true;
            }
        }

        private void TryLogObserverException(Exception exception)
        {
            try
            {
                _backend.LogObserverException(exception);
            }
            catch
            {
            }
        }

        private static bool TrySnapshot(IReadOnlyList<IStartupStep> steps, out Entry[] entries, out StartupFlowResult failure)
        {
            entries = Array.Empty<Entry>();
            if (steps == null)
            {
                failure = StartupFlowResult.Failure(StartupFlowError.InvalidSteps, string.Empty, 0, 0, "step一覧を指定してください。");
                return false;
            }

            int count;
            try
            {
                count = steps.Count;
            }
            catch (Exception exception)
            {
                failure = StartupFlowResult.Failure(StartupFlowError.InvalidSteps, string.Empty, 0, 0, $"step数を取得できませんでした: {GetExceptionMessage(exception)}");
                return false;
            }

            if (count > MaximumStepCount)
            {
                failure = StartupFlowResult.Failure(StartupFlowError.TooManySteps, string.Empty, 0, count, $"step数は{MaximumStepCount}件以下にしてください。");
                return false;
            }

            entries = new Entry[count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                IStartupStep step;
                string id;
                int order;
                try
                {
                    step = steps[index];
                    if (step == null || step is UnityEngine.Object unityObject && unityObject == null)
                    {
                        failure = StartupFlowResult.Failure(StartupFlowError.InvalidSteps, string.Empty, 0, count, $"位置{index}のstepがnullです。");
                        return false;
                    }

                    id = step.Id;
                    order = step.Order;
                }
                catch (Exception exception)
                {
                    failure = StartupFlowResult.Failure(StartupFlowError.InvalidSteps, string.Empty, 0, count, $"位置{index}のstep情報を取得できませんでした: {GetExceptionMessage(exception)}");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(id) || id.Length > MaximumStepIdLength)
                {
                    failure = StartupFlowResult.Failure(StartupFlowError.InvalidSteps, id, 0, count, $"位置{index}のstep識別子は空白以外で{MaximumStepIdLength}文字以内にしてください。");
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = StartupFlowResult.Failure(StartupFlowError.DuplicateStepId, id, 0, count, $"step識別子'{id}'が重複しています。");
                    return false;
                }

                entries[index] = new Entry(step, id, order);
            }

            Array.Sort(entries, EntryComparer.Instance);
            failure = default;
            return true;
        }

        private static StartupFlowStatus CreateRunningStatus(string stepId, int stepIndex, int totalCount, int completedCount, float stepProgress)
        {
            var overall = totalCount == 0 ? 1f : (completedCount + stepProgress) / totalCount;
            return new StartupFlowStatus(StartupFlowPhase.Running, stepId, stepIndex, totalCount, stepProgress, overall);
        }

        private static StartupFlowStatus IdleStatus() => new StartupFlowStatus(StartupFlowPhase.Idle, string.Empty, -1, 0, 0f, 0f);

        private static int SafeCount(IReadOnlyList<IStartupStep> steps)
        {
            try
            {
                return steps?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static Awaitable<StartupFlowResult> FromResult(StartupFlowResult result)
        {
            var completion = new AwaitableCompletionSource<StartupFlowResult>();
            completion.SetResult(result);
            return completion.Awaitable;
        }

        private static string GetExceptionMessage(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception?.Message) ? exception?.GetType().Name ?? "Unknown error" : exception.Message;
            return message.Length <= 1024 ? message : message.Substring(0, 1024);
        }

        private static ObserverSlot<TObserver>[] AddObserver<TObserver>(ObserverSlot<TObserver>[] current, TObserver observer) where TObserver : Delegate
        {
            if (observer == null) return current;
            var next = new ObserverSlot<TObserver>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = new ObserverSlot<TObserver>(observer);
            return next;
        }

        private static ObserverSlot<TObserver>[] RemoveObserver<TObserver>(ObserverSlot<TObserver>[] current, TObserver observer) where TObserver : Delegate
        {
            if (observer == null) return current;
            for (var index = current.Length - 1; index >= 0; index--)
            {
                if (!Equals(current[index].Observer, observer)) continue;
                var next = new ObserverSlot<TObserver>[current.Length - 1];
                if (index > 0) Array.Copy(current, 0, next, 0, index);
                if (index < current.Length - 1) Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                return next;
            }

            return current;
        }

        private readonly struct Entry
        {
            internal Entry(IStartupStep step, string id, int order)
            {
                Step = step;
                Id = id;
                Order = order;
            }

            internal IStartupStep Step { get; }
            internal string Id { get; }
            internal int Order { get; }
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            internal static readonly EntryComparer Instance = new EntryComparer();

            public int Compare(Entry left, Entry right)
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
            }
        }

        private sealed class ObserverSlot<TObserver> where TObserver : Delegate
        {
            internal ObserverSlot(TObserver observer) => Observer = observer;

            internal TObserver Observer { get; }
            internal bool HasConsecutiveFailure { get; set; }
        }
    }
}
