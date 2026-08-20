using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace StartupFlow.Tests
{
    /// <summary>step検証、順序、進捗、失敗、cancel、callback境界を検証する。</summary>
    public sealed class StartupFlowServiceTests
    {
        private FakeBackend _backend;
        private StartupFlowService _service;

        /// <summary>各testへmain-thread扱いの終了していないbackendを用意する。</summary>
        [SetUp]
        public void SetUp()
        {
            _backend = new FakeBackend();
            _service = new StartupFlowService(_backend);
        }

        /// <summary>null一覧はstepを呼ばずInvalidStepsを返す。</summary>
        [Test]
        public async Task RunAsync_NullListIsRejected()
        {
            var result = await _service.RunAsync(null);
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.InvalidSteps));
            Assert.That(_service.IsBusy, Is.False);
        }

        /// <summary>空一覧は成功し、CompletedからIdleへ戻る。</summary>
        [Test]
        public async Task RunAsync_EmptyListCompletes()
        {
            var phases = new List<StartupFlowPhase>();
            _service.StatusChanged += status => phases.Add(status.Phase);
            var result = await _service.RunAsync(Array.Empty<IStartupStep>());
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.TotalStepCount, Is.Zero);
            Assert.That(phases, Is.EqualTo(new[] { StartupFlowPhase.Validating, StartupFlowPhase.Completed, StartupFlowPhase.Idle }));
        }

        /// <summary>上限超過、null step、不正Id、重複Idを個別に拒否する。</summary>
        [Test]
        public async Task RunAsync_InvalidCollectionsAreRejected()
        {
            var tooMany = new IStartupStep[StartupFlowService.MaximumStepCount + 1];
            var tooManyResult = await _service.RunAsync(tooMany);
            Assert.That(tooManyResult.Error, Is.EqualTo(StartupFlowError.TooManySteps));

            var nullResult = await _service.RunAsync(new IStartupStep[] { null });
            Assert.That(nullResult.Error, Is.EqualTo(StartupFlowError.InvalidSteps));

            var invalidId = await _service.RunAsync(new[] { Step(" ", 0) });
            Assert.That(invalidId.Error, Is.EqualTo(StartupFlowError.InvalidSteps));

            var duplicate = await _service.RunAsync(new[] { Step("same", 0), Step("same", 1) });
            Assert.That(duplicate.Error, Is.EqualTo(StartupFlowError.DuplicateStepId));
            Assert.That(duplicate.FailedStepId, Is.EqualTo("same"));
        }

        /// <summary>Order昇順、同値ならId ordinal昇順で1件ずつ実行する。</summary>
        [Test]
        public async Task RunAsync_SortsByOrderThenOrdinalId()
        {
            var order = new List<string>();
            var result = await _service.RunAsync(new[]
            {
                Step("zeta", 20, context => { order.Add(context.StepId); return Completed(); }),
                Step("beta", 10, context => { order.Add(context.StepId); return Completed(); }),
                Step("alpha", 10, context => { order.Add(context.StepId); return Completed(); })
            });
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "alpha", "beta", "zeta" }));
        }

        /// <summary>保留中のstepがある間は次stepを開始せず、別要求をBusyで返す。</summary>
        [Test]
        public async Task RunAsync_IsSequentialAndRejectsConcurrentRun()
        {
            var firstCompletion = new AwaitableCompletionSource();
            var order = new List<string>();
            var first = Step("first", 0, context => { order.Add(context.StepId); return firstCompletion.Awaitable; });
            var second = Step("second", 1, context => { order.Add(context.StepId); return Completed(); });
            var running = _service.RunAsync(new[] { first, second });
            Assert.That(order, Is.EqualTo(new[] { "first" }));

            var busy = await _service.RunAsync(Array.Empty<IStartupStep>());
            Assert.That(busy.Error, Is.EqualTo(StartupFlowError.Busy));
            firstCompletion.SetResult();
            var result = await running;
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        }

        /// <summary>有効な単調進捗だけを通知し、全体進捗へ反映する。</summary>
        [Test]
        public async Task ReportProgress_RequiresFiniteMonotonicRange()
        {
            StartupFlowError[] errors = null;
            var statuses = new List<StartupFlowStatus>();
            _service.StatusChanged += status => statuses.Add(status);
            var result = await _service.RunAsync(new[]
            {
                Step("progress", 0, context =>
                {
                    errors = new[]
                    {
                        context.ReportProgress(0.25f),
                        context.ReportProgress(0.75f),
                        context.ReportProgress(0.5f),
                        context.ReportProgress(float.NaN),
                        context.ReportProgress(1.1f)
                    };
                    return Completed();
                }),
                Step("last", 1)
            });
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(errors, Is.EqualTo(new[] { StartupFlowError.None, StartupFlowError.None, StartupFlowError.InvalidProgress, StartupFlowError.InvalidProgress, StartupFlowError.InvalidProgress }));
            Assert.That(statuses.Exists(status => status.StepId == "progress" && status.StepProgress == 0.75f && status.OverallProgress == 0.375f), Is.True);
        }

        /// <summary>完了後contextの進捗通知は状態を変えずStepNotActiveを返す。</summary>
        [Test]
        public async Task Context_AfterCompletionIsInactive()
        {
            StartupStepContext captured = null;
            var result = await _service.RunAsync(new[] { Step("capture", 0, context => { captured = context; return Completed(); }) });
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(captured.ReportProgress(1f), Is.EqualTo(StartupFlowError.StepNotActive));
            Assert.That(_service.Status.Phase, Is.EqualTo(StartupFlowPhase.Idle));
        }

        /// <summary>step例外はそのstepで停止し、後続を呼ばない。</summary>
        [Test]
        public async Task StepException_StopsFollowingSteps()
        {
            var followingCalled = false;
            var result = await _service.RunAsync(new[]
            {
                Step("ready", 0),
                Step("broken", 1, _ => throw new InvalidOperationException("expected failure")),
                Step("following", 2, _ => { followingCalled = true; return Completed(); })
            });
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.StepFailed));
            Assert.That(result.FailedStepId, Is.EqualTo("broken"));
            Assert.That(result.CompletedStepCount, Is.EqualTo(1));
            Assert.That(followingCalled, Is.False);
        }

        /// <summary>外部cancelは現在step完了後にCanceledへ収束し、後続を呼ばない。</summary>
        [Test]
        public async Task Cancellation_StopsBeforeFollowingStep()
        {
            var source = new CancellationTokenSource();
            var pause = new AwaitableCompletionSource();
            var followingCalled = false;
            var running = _service.RunAsync(new[]
            {
                Step("waiting", 0, _ => pause.Awaitable),
                Step("following", 1, _ => { followingCalled = true; return Completed(); })
            }, source.Token);
            source.Cancel();
            pause.SetResult();
            var result = await running;
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.Canceled));
            Assert.That(result.FailedStepId, Is.EqualTo("waiting"));
            Assert.That(followingCalled, Is.False);
        }

        /// <summary>終了tokenのcancelはApplicationExitingとして区別する。</summary>
        [Test]
        public async Task ExitCancellation_ReturnsApplicationExiting()
        {
            var pause = new AwaitableCompletionSource();
            var running = _service.RunAsync(new[] { Step("waiting", 0, _ => pause.Awaitable) });
            _backend.ExitSource.Cancel();
            pause.SetResult();
            var result = await running;
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.ApplicationExiting));
        }

        /// <summary>main thread以外ではstep情報にも触れず明示エラーを返す。</summary>
        [Test]
        public async Task NonMainThread_IsRejectedBeforeValidation()
        {
            _backend.IsMainThreadValue = false;
            var result = await _service.RunAsync(new ThrowingList());
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.MainThreadRequired));
        }

        /// <summary>Status callback中の再入はBusyで、progressの入れ子通知もBusyになる。</summary>
        [Test]
        public async Task StatusCallback_ReentryAndNestedProgressAreBusy()
        {
            Awaitable<StartupFlowResult> reentry = null;
            StartupFlowError nested = StartupFlowError.None;
            StartupStepContext context = null;
            _service.StatusChanged += status =>
            {
                if (status.Phase != StartupFlowPhase.Running || status.StepProgress <= 0f || reentry != null) return;
                reentry = _service.RunAsync(Array.Empty<IStartupStep>());
                nested = context.ReportProgress(0.5f);
            };
            var result = await _service.RunAsync(new[] { Step("callback", 0, value =>
            {
                context = value;
                Assert.That(context.ReportProgress(0.25f), Is.EqualTo(StartupFlowError.None));
                return Completed();
            }) });
            var reentryResult = await reentry;
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(reentryResult.Error, Is.EqualTo(StartupFlowError.Busy));
            Assert.That(nested, Is.EqualTo(StartupFlowError.Busy));
        }

        /// <summary>observer例外を隔離し、後続observerとflow完了を維持する。</summary>
        [Test]
        public async Task ObserverException_IsLoggedOnceAndDoesNotStopFlow()
        {
            var healthyCalls = 0;
            _service.StatusChanged += _ => throw new InvalidOperationException("observer failure");
            _service.StatusChanged += _ => healthyCalls++;
            var result = await _service.RunAsync(new[] { Step("one", 0) });
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(healthyCalls, Is.GreaterThanOrEqualTo(3));
            Assert.That(_backend.Logged.Count, Is.EqualTo(1));
        }

        /// <summary>完了callbackとIdle callbackでは新しいflowをまだ開始できない。</summary>
        [Test]
        public async Task CompletionCallbacks_ReentryIsBusy()
        {
            Awaitable<StartupFlowResult> finishedReentry = null;
            Awaitable<StartupFlowResult> idleReentry = null;
            _service.Finished += _ => finishedReentry = _service.RunAsync(Array.Empty<IStartupStep>());
            _service.StatusChanged += status =>
            {
                if (status.Phase == StartupFlowPhase.Idle) idleReentry = _service.RunAsync(Array.Empty<IStartupStep>());
            };
            var result = await _service.RunAsync(new[] { Step("one", 0) });
            Assert.That(result.IsSuccess, Is.True);
            Assert.That((await finishedReentry).Error, Is.EqualTo(StartupFlowError.Busy));
            Assert.That((await idleReentry).Error, Is.EqualTo(StartupFlowError.Busy));
        }

        /// <summary>Awaitable continuationが次flowを開始後にthrowしても、その次flowを破損しない。</summary>
        [Test]
        public async Task CompletionContinuation_StartsNextThenThrowsWithoutCorruption()
        {
            var pause = new AwaitableCompletionSource();
            var first = _service.RunAsync(new[] { Step("first", 0, _ => pause.Awaitable) });
            Awaitable<StartupFlowResult> second = null;
            var awaiter = first.GetAwaiter();
            awaiter.OnCompleted(() =>
            {
                Assert.That(awaiter.GetResult().IsSuccess, Is.True);
                second = _service.RunAsync(new[] { Step("second", 0) });
                throw new InvalidOperationException("continuation failure");
            });
            pause.SetResult();
            Assert.That(second, Is.Not.Null);
            var secondResult = await second;
            Assert.That(secondResult.IsSuccess, Is.True);
            Assert.That(_backend.Logged.Count, Is.EqualTo(1));
            Assert.That(_service.IsBusy, Is.False);
        }

        private static DelegateStep Step(string id, int order, Func<StartupStepContext, Awaitable> execute = null) => new DelegateStep(id, order, execute ?? (_ => Completed()));

        private static Awaitable Completed()
        {
            var completion = new AwaitableCompletionSource();
            completion.SetResult();
            return completion.Awaitable;
        }

        private sealed class DelegateStep : IStartupStep
        {
            private readonly Func<StartupStepContext, Awaitable> _execute;

            internal DelegateStep(string id, int order, Func<StartupStepContext, Awaitable> execute)
            {
                Id = id;
                Order = order;
                _execute = execute;
            }

            public string Id { get; }
            public int Order { get; }
            public Awaitable ExecuteAsync(StartupStepContext context) => _execute(context);
        }

        private sealed class ThrowingList : IReadOnlyList<IStartupStep>
        {
            public int Count => throw new InvalidOperationException("must not read");
            public IStartupStep this[int index] => throw new InvalidOperationException("must not read");
            public IEnumerator<IStartupStep> GetEnumerator() => throw new InvalidOperationException("must not enumerate");
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class FakeBackend : IStartupFlowBackend
        {
            internal readonly CancellationTokenSource ExitSource = new CancellationTokenSource();
            internal readonly List<Exception> Logged = new List<Exception>();
            internal bool IsMainThreadValue = true;

            public bool IsMainThread => IsMainThreadValue;
            public CancellationToken ExitToken => ExitSource.Token;
            public void LogObserverException(Exception exception) => Logged.Add(exception);
        }
    }
}
