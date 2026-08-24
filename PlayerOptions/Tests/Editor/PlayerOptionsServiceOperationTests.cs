// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>Set/Apply/Save分離、observer隔離、Busy、thread/runtime/storage failureを確認する。</summary>
    internal sealed class PlayerOptionsServiceOperationTests
    {
        [Test]
        public void Constructor_RejectsNullStorageInvalidDefaultsAndNonMainThread()
        {
            Assert.That(
                () => new PlayerOptionsService(
                    PlayerOptionsTestData.CreateDefaultState(),
                    null,
                    new FakePlayerOptionsRuntime(),
                    PlayerOptionsMigrationPipeline.Default),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => PlayerOptionsTestData.CreateService(
                    new FakePlayerOptionsRuntime(),
                    defaults: PlayerOptionsTestData.CreateState(targetFrameRate: 0)),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => PlayerOptionsTestData.CreateService(
                    new FakePlayerOptionsRuntime { IsMainThreadValue = false }),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SetState_NoOpDoesNotNotifyOrTouchExternalBoundaries()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            var notifications = 0;
            service.StateChanged += _ => notifications++;

            var result = service.SetState(service.State);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(notifications, Is.Zero);
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(storage.ReadCount, Is.Zero);
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void SetState_RefreshNormalizationChangesMemoryAndNotifiesOnce()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            var notifications = 0;
            var notified = default(PlayerOptionsState);
            service.StateChanged += state =>
            {
                notifications++;
                notified = state;
            };

            var result = service.SetState(PlayerOptionsTestData.CreateState(
                refreshNumerator: 120,
                refreshDenominator: 2,
                targetFrameRate: 90));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.RefreshRateNormalized));
            Assert.That(result.WasAdjusted, Is.True);
            Assert.That(result.RequiresSave, Is.False);
            Assert.That(result.State.Display.PreferredRefreshRate.numerator, Is.EqualTo(60));
            Assert.That(notified, Is.EqualTo(result.State));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void SetState_InvalidQualityPreservesStateAndDoesNotNotify()
        {
            var service = PlayerOptionsTestData.CreateService(new FakePlayerOptionsRuntime());
            var previous = service.State;
            var notifications = 0;
            service.StateChanged += _ => notifications++;

            var result = service.SetState(
                PlayerOptionsTestData.CreateState(qualityIndex: 1, qualityName: "high"));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.InvalidOptions));
            Assert.That(result.State, Is.EqualTo(previous));
            Assert.That(service.State, Is.EqualTo(previous));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Save_WritesOneCurrentDocumentWithoutApplyOrNotification()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(
                    targetFrameRate: 90,
                    masterVolume: 0.5f)).IsSuccess,
                Is.True);
            var notifications = 0;
            service.StateChanged += _ => notifications++;
            runtime.Calls.Clear();

            var result = service.Save();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(storage.WriteCount, Is.EqualTo(1));
            Assert.That(storage.Exists, Is.True);
            var codec = new PlayerOptionsDocumentCodec(PlayerOptionsMigrationPipeline.Default);
            Assert.That(
                codec.TryDecode(
                    storage.Contents,
                    out var decoded,
                    out _,
                    out _,
                    out var decodeMessage),
                Is.True,
                decodeMessage);
            Assert.That(decoded, Is.EqualTo(service.State));
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Save_StorageWriteExceptionReturnsFailureAndPreservesPreviousRaw()
        {
            const string previousRaw = "preserve-me";
            var storage = new FakePlayerOptionsStorage
            {
                Exists = true,
                Contents = previousRaw,
                WriteException = new InvalidOperationException("write failure"),
            };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);

            var result = service.Save();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.StorageWriteFailed));
            Assert.That(storage.WriteCount, Is.EqualTo(1));
            Assert.That(storage.Contents, Is.EqualTo(previousRaw));
        }

        [Test]
        public void Save_RuntimeQualityDriftFailsStrictValidationWithoutWriting()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            runtime.QualityLevelValue = 0;
            runtime.QualityNameValues = new[] { "Only" };

            var result = service.Save();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.InvalidOptions));
            Assert.That(result.State, Is.EqualTo(service.Defaults));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Apply_ChangesOnlyRuntimeAndDoesNotNotifyOrUseStorage()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            var desired = PlayerOptionsTestData.CreateState(
                targetFrameRate: 90,
                masterVolume: 0.5f,
                qualityIndex: 0,
                qualityName: "Low");
            Assert.That(service.SetState(desired).IsSuccess, Is.True);
            var notifications = 0;
            service.StateChanged += _ => notifications++;
            runtime.Calls.Clear();

            var result = service.Apply();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.State, Is.EqualTo(desired));
            Assert.That(
                runtime.Calls,
                Is.EqualTo(new[] { "quality:0", "target:90", "volume:0.5" }));
            Assert.That(storage.ReadCount, Is.Zero);
            Assert.That(storage.WriteCount, Is.Zero);
            Assert.That(notifications, Is.Zero);
            PlayerOptionsResultAssertions.AssertFields(
                result,
                "AffectedFields",
                "TargetFrameRate",
                "MasterVolume",
                "Quality");
            PlayerOptionsResultAssertions.AssertFields(result, "RollbackFailedFields");
            PlayerOptionsResultAssertions.AssertFields(result, "OutcomeUnknownFields");
        }

        [Test]
        public void ObserverFailure_IsolatedLoggedOnceUntilRecoveryAndThenLoggedAgain()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var service = PlayerOptionsTestData.CreateService(runtime);
            var shouldThrow = true;
            var laterObserverCalls = 0;
            service.StateChanged += _ =>
            {
                if (shouldThrow) throw new InvalidOperationException("observer failure");
            };
            service.StateChanged += _ => laterObserverCalls++;

            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 90)).IsSuccess,
                Is.True);
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 120)).IsSuccess,
                Is.True);
            Assert.That(runtime.ObserverLogCount, Is.EqualTo(1));
            Assert.That(laterObserverCalls, Is.EqualTo(2));

            shouldThrow = false;
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 144)).IsSuccess,
                Is.True);
            shouldThrow = true;
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 165)).IsSuccess,
                Is.True);
            Assert.That(runtime.ObserverLogCount, Is.EqualTo(2));
            Assert.That(laterObserverCalls, Is.EqualTo(4));
        }

        [Test]
        public void ObserverSelfUnsubscribeBeforeThrow_DoesNotRetainFailureState()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var service = PlayerOptionsTestData.CreateService(runtime);
            Action<PlayerOptionsState> observer = null;
            observer = _ =>
            {
                service.StateChanged -= observer;
                throw new InvalidOperationException("observer failure");
            };

            service.StateChanged += observer;
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 90)).IsSuccess,
                Is.True);
            Assert.That(runtime.ObserverLogCount, Is.EqualTo(1));

            service.StateChanged += observer;
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 120)).IsSuccess,
                Is.True);
            Assert.That(runtime.ObserverLogCount, Is.EqualTo(2));
        }

        [Test]
        public void ObserverLoggerFailure_DoesNotFailOperationOrLaterObserver()
        {
            var runtime = new FakePlayerOptionsRuntime { ThrowOnObserverLog = true };
            var service = PlayerOptionsTestData.CreateService(runtime);
            var laterObserverCalls = 0;
            service.StateChanged += _ => throw new InvalidOperationException("observer failure");
            service.StateChanged += _ => laterObserverCalls++;

            var result = service.SetState(
                PlayerOptionsTestData.CreateState(targetFrameRate: 90));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runtime.ObserverLogCount, Is.EqualTo(1));
            Assert.That(laterObserverCalls, Is.EqualTo(1));
        }

        [Test]
        public void ObserverReentrantOperationsReturnBusyWithoutSideEffects()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            var nestedSet = default(PlayerOptionsResult);
            var nestedLoad = default(PlayerOptionsResult);
            var nestedApply = default(PlayerOptionsResult);
            var nestedSave = default(PlayerOptionsResult);
            service.StateChanged += state =>
            {
                nestedSet = service.SetState(state);
                nestedLoad = service.Load();
                nestedApply = service.Apply();
                nestedSave = service.Save();
            };

            var outer = service.SetState(
                PlayerOptionsTestData.CreateState(targetFrameRate: 90));

            Assert.That(outer.IsSuccess, Is.True);
            Assert.That(nestedSet.Error, Is.EqualTo(PlayerOptionsError.Busy));
            Assert.That(nestedLoad.Error, Is.EqualTo(PlayerOptionsError.Busy));
            Assert.That(nestedApply.Error, Is.EqualTo(PlayerOptionsError.Busy));
            Assert.That(nestedSave.Error, Is.EqualTo(PlayerOptionsError.Busy));
            Assert.That(storage.ReadCount, Is.Zero);
            Assert.That(storage.WriteCount, Is.Zero);
            Assert.That(runtime.Calls, Is.Empty);
        }

        [Test]
        public void Operations_MainThreadFalseReturnsMainThreadRequired()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var service = PlayerOptionsTestData.CreateService(runtime);
            runtime.IsMainThreadValue = false;

            Assert.That(service.Load().Error, Is.EqualTo(PlayerOptionsError.MainThreadRequired));
            Assert.That(service.SetState(service.State).Error, Is.EqualTo(PlayerOptionsError.MainThreadRequired));
            Assert.That(service.Apply().Error, Is.EqualTo(PlayerOptionsError.MainThreadRequired));
            Assert.That(service.Save().Error, Is.EqualTo(PlayerOptionsError.MainThreadRequired));
        }

        [Test]
        public void Operations_MainThreadProbeExceptionReturnsRuntimeUnavailable()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var service = PlayerOptionsTestData.CreateService(runtime);
            runtime.MainThreadException = new InvalidOperationException("probe failure");

            Assert.That(service.Load().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.SetState(service.State).Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.Apply().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.Save().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
        }

        [Test]
        public void Operations_RuntimeReadExceptionReturnsRuntimeUnavailableWithoutWrites()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            runtime.RuntimeReadException = new InvalidOperationException("runtime read failure");

            Assert.That(service.Load().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.SetState(service.State).Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.Apply().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(service.Save().Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(storage.WriteCount, Is.Zero);
            Assert.That(runtime.Calls, Is.Empty);
        }
    }
}
