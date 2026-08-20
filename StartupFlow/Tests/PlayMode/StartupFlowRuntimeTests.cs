using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StartupFlow.PlayMode.Tests
{
    /// <summary>実Unity main thread、frame、background thread、timeScale 0でflow契約を検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class StartupFlowRuntimeTests
    {
        private float _timeScale;

        /// <summary>各test前にmain-thread判定とglobal timeScaleを固定する。</summary>
        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 1f;
            StartupFlowMainThread.BindForTests();
        }

        /// <summary>各test後にglobal timeScaleを必ず復元する。</summary>
        [TearDown]
        public void TearDown() => Time.timeScale = _timeScale;

        /// <summary>timeScale 0でもNextFrame based stepを順番どおり完了する。</summary>
        [UnityTest]
        public IEnumerator RunAsync_TimeScaleZeroCompletesFrameStepsInOrder()
        {
            Time.timeScale = 0f;
            var order = new List<string>();
            var service = new StartupFlowService();
            var operation = service.RunAsync(new IStartupStep[]
            {
                new FrameStep("second", 20, 2, order),
                new FrameStep("first", 10, 1, order)
            });
            var result = default(StartupFlowResult);
            yield return Wait(operation, value => result = value);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(service.Status.Phase, Is.EqualTo(StartupFlowPhase.Idle));
        }

        /// <summary>background threadからのprogressを拒否し、main threadへ戻った後は受理する。</summary>
        [UnityTest]
        public IEnumerator Progress_BackgroundRejectedThenMainAccepted()
        {
            var step = new BackgroundProgressStep();
            var service = new StartupFlowService();
            var operation = service.RunAsync(new IStartupStep[] { step });
            var result = default(StartupFlowResult);
            yield return Wait(operation, value => result = value);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(step.BackgroundError, Is.EqualTo(StartupFlowError.MainThreadRequired));
            Assert.That(step.MainError, Is.EqualTo(StartupFlowError.None));
        }

        /// <summary>外部tokenをframe待機へ渡すとCanceled結果へ収束する。</summary>
        [UnityTest]
        public IEnumerator Cancellation_InterruptsCooperativeStep()
        {
            var source = new CancellationTokenSource();
            var service = new StartupFlowService();
            var operation = service.RunAsync(new IStartupStep[] { new WaitingStep() }, source.Token);
            yield return null;
            source.Cancel();
            var result = default(StartupFlowResult);
            yield return Wait(operation, value => result = value);
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.Canceled));
            Assert.That(result.FailedStepId, Is.EqualTo("waiting"));
            Assert.That(service.IsBusy, Is.False);
        }

        /// <summary>workerからのRunAsyncはUnity APIへ触れずMainThreadRequiredを返す。</summary>
        [UnityTest]
        public IEnumerator RunAsync_FromWorkerReturnsMainThreadRequired()
        {
            var service = new StartupFlowService();
            StartupFlowResult result = default;
            Exception failure = null;
            var task = Task.Run(() =>
            {
                try
                {
                    result = service.RunAsync(Array.Empty<IStartupStep>()).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.That(failure, Is.Null);
            Assert.That(result.Error, Is.EqualTo(StartupFlowError.MainThreadRequired));
            Assert.That(service.Status.Phase, Is.EqualTo(StartupFlowPhase.Idle));
        }

        private static IEnumerator Wait(Awaitable<StartupFlowResult> operation, Action<StartupFlowResult> receiveResult)
        {
            var awaiter = operation.GetAwaiter();
            var deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!awaiter.IsCompleted)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("startup flowが実時間5秒以内に完了しませんでした。");
                yield return null;
            }

            receiveResult(awaiter.GetResult());
        }

        private sealed class FrameStep : IStartupStep
        {
            private readonly int _frames;
            private readonly List<string> _order;

            internal FrameStep(string id, int order, int frames, List<string> executionOrder)
            {
                Id = id;
                Order = order;
                _frames = frames;
                _order = executionOrder;
            }

            public string Id { get; }
            public int Order { get; }

            public async Awaitable ExecuteAsync(StartupStepContext context)
            {
                _order.Add(Id);
                for (var index = 0; index < _frames; index++)
                {
                    await Awaitable.NextFrameAsync(context.CancellationToken);
                    Assert.That(context.ReportProgress((index + 1f) / _frames), Is.EqualTo(StartupFlowError.None));
                }
            }
        }

        private sealed class BackgroundProgressStep : IStartupStep
        {
            public string Id => "background";
            public int Order => 0;
            internal StartupFlowError BackgroundError { get; private set; }
            internal StartupFlowError MainError { get; private set; }

            public async Awaitable ExecuteAsync(StartupStepContext context)
            {
                await Awaitable.BackgroundThreadAsync();
                BackgroundError = context.ReportProgress(0.5f);
                await Awaitable.MainThreadAsync();
                MainError = context.ReportProgress(1f);
            }
        }

        private sealed class WaitingStep : IStartupStep
        {
            public string Id => "waiting";
            public int Order => 0;

            public async Awaitable ExecuteAsync(StartupStepContext context)
            {
                while (true) await Awaitable.NextFrameAsync(context.CancellationToken);
            }
        }
    }
}
