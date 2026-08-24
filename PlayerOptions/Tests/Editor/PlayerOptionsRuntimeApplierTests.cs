// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>同期適用順、readback、screen last、warnings、逆順rollbackを確認する。</summary>
    internal sealed class PlayerOptionsRuntimeApplierTests
    {
        [Test]
        public void Apply_UsesQualityTargetVolumeResolutionOrderAndReturnsWarnings()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                VSyncCountValue = 1,
            };
            var state = PlayerOptionsTestData.CreateState(
                width: 1280,
                height: 720,
                targetFrameRate: 90,
                masterVolume: 0.5f,
                qualityIndex: 0,
                qualityName: "Low");

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out var warnings,
                out var message,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(
                runtime.Calls,
                Is.EqualTo(new[]
                {
                    "quality:0",
                    "target:90",
                    "volume:0.5",
                    "resolution:1280x720:True",
                }));
            Assert.That(
                warnings,
                Is.EqualTo(
                    PlayerOptionsWarning.ResolutionChangeDeferred |
                    PlayerOptionsWarning.TargetFrameRateMayBeOverridden));
            Assert.That(runtime.LastResolutionRequest, Is.EqualTo(state.Display));
            Assert.That(runtime.LastResolutionSpecifiedRefreshRate, Is.True);
            Assert.That(
                affectedFields,
                Is.EqualTo(
                    PlayerOptionsField.Display |
                    PlayerOptionsField.TargetFrameRate |
                    PlayerOptionsField.MasterVolume |
                    PlayerOptionsField.Quality));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_ExactRuntimeValuesAreNoOpWithoutDeferredWarning()
        {
            var runtime = new FakePlayerOptionsRuntime();

            var success = PlayerOptionsRuntimeApplier.TryApply(
                PlayerOptionsTestData.CreateDefaultState(),
                runtime,
                out var error,
                out var warnings,
                out var message,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(affectedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_SmallButExactVolumeDifferenceInvokesSetter()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = PlayerOptionsTestData.CreateState(masterVolume: 0.75005f);

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out var warnings,
                out var message,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(runtime.MasterVolumeValue, Is.EqualTo(0.75005f));
            Assert.That(runtime.Calls, Has.Count.EqualTo(1));
            Assert.That(runtime.Calls[0], Does.StartWith("volume:"));
            Assert.That(affectedFields, Is.EqualTo(PlayerOptionsField.MasterVolume));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_VolumeFailureRollsBackTouchedFieldsInReverseOrder()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                VolumeSetFailuresRemaining = 1,
            };
            var state = PlayerOptionsTestData.CreateState(
                width: 1280,
                height: 720,
                targetFrameRate: 90,
                masterVolume: 0.5f,
                qualityIndex: 0,
                qualityName: "Low");

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out var warnings,
                out _,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.ApplyFailed));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(
                runtime.Calls,
                Is.EqualTo(new[]
                {
                    "quality:0",
                    "target:90",
                    "volume:0.5",
                    "volume:0.75",
                    "target:60",
                    "quality:1",
                }));
            Assert.That(runtime.QualityLevelValue, Is.EqualTo(1));
            Assert.That(runtime.TargetFrameRateValue, Is.EqualTo(60));
            Assert.That(runtime.MasterVolumeValue, Is.EqualTo(0.75f));
            Assert.That(
                affectedFields,
                Is.EqualTo(
                    PlayerOptionsField.TargetFrameRate |
                    PlayerOptionsField.MasterVolume |
                    PlayerOptionsField.Quality));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_ReadbackMismatchIsFailureAndRollbackIsVerified()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                IgnoreTargetSet = true,
            };
            var state = PlayerOptionsTestData.CreateState(targetFrameRate: 90);

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out _,
                out _,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.ApplyFailed));
            Assert.That(runtime.Calls, Is.EqualTo(new[] { "target:90", "target:60" }));
            Assert.That(runtime.TargetFrameRateValue, Is.EqualTo(60));
            Assert.That(affectedFields, Is.EqualTo(PlayerOptionsField.TargetFrameRate));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_RollbackSetterFailureReturnsRollbackFailed()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                FailEveryQualitySet = true,
            };
            var state = PlayerOptionsTestData.CreateState(
                qualityIndex: 0,
                qualityName: "Low");

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out _,
                out _,
                out var affectedFields,
                out var rollbackFailedFields,
                out var outcomeUnknownFields);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.RollbackFailed));
            Assert.That(runtime.Calls, Is.EqualTo(new[] { "quality:0", "quality:1" }));
            Assert.That(affectedFields, Is.EqualTo(PlayerOptionsField.Quality));
            Assert.That(rollbackFailedFields, Is.EqualTo(PlayerOptionsField.Quality));
            Assert.That(outcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [Test]
        public void Apply_ResolutionThrowReportsUnknownOutcomeAndRollsBackSyncFields()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                ResolutionValues = new[]
                {
                    PlayerOptionsTestData.CreateResolution(1280, 720, 60, 1),
                },
            };
            var service = PlayerOptionsTestData.CreateService(runtime);
            var state = PlayerOptionsTestData.CreateState(
                width: 1280,
                height: 720,
                targetFrameRate: 90,
                masterVolume: 0.5f,
                qualityIndex: 0,
                qualityName: "Low");
            Assert.That(service.SetState(state).IsSuccess, Is.True);
            runtime.Calls.Clear();
            runtime.ResolutionSetFailuresRemaining = 1;

            var result = service.Apply();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.ApplyFailed));
            Assert.That(
                (result.Warnings & PlayerOptionsWarning.ResolutionOutcomeUnknown) != 0,
                Is.True);
            Assert.That(
                runtime.Calls,
                Is.EqualTo(new[]
                {
                    "quality:0",
                    "target:90",
                    "volume:0.5",
                    "resolution:1280x720:True",
                    "volume:0.75",
                    "target:60",
                    "quality:1",
                }));
            Assert.That(runtime.QualityLevelValue, Is.EqualTo(1));
            Assert.That(runtime.TargetFrameRateValue, Is.EqualTo(60));
            Assert.That(runtime.MasterVolumeValue, Is.EqualTo(0.75f));
            Assert.That(
                result.AffectedFields,
                Is.EqualTo(
                    PlayerOptionsField.Display |
                    PlayerOptionsField.TargetFrameRate |
                    PlayerOptionsField.MasterVolume |
                    PlayerOptionsField.Quality));
            Assert.That(result.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(result.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.Display));
        }

        [TestCase("Quality")]
        [TestCase("TargetFrameRate")]
        [TestCase("MasterVolume")]
        public void Apply_SetterThrowReportsExactAttemptedField(string fieldName)
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = ConfigureSingleFieldChange(runtime, fieldName, setterThrows: true, readbackMismatch: false);
            var service = PlayerOptionsTestData.CreateService(runtime);
            Assert.That(service.SetState(state).IsSuccess, Is.True);

            var result = service.Apply();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.ApplyFailed));
            Assert.That(result.AffectedFields, Is.EqualTo(ParseField(fieldName)));
            Assert.That(result.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(result.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [TestCase("Quality")]
        [TestCase("TargetFrameRate")]
        [TestCase("MasterVolume")]
        public void Apply_ReadbackMismatchReportsExactAttemptedField(string fieldName)
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = ConfigureSingleFieldChange(runtime, fieldName, setterThrows: false, readbackMismatch: true);
            var service = PlayerOptionsTestData.CreateService(runtime);
            Assert.That(service.SetState(state).IsSuccess, Is.True);

            var result = service.Apply();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.ApplyFailed));
            Assert.That(result.AffectedFields, Is.EqualTo(ParseField(fieldName)));
            Assert.That(result.RollbackFailedFields, Is.EqualTo(PlayerOptionsField.None));
            Assert.That(result.OutcomeUnknownFields, Is.EqualTo(PlayerOptionsField.None));
        }

        [TestCase("Quality", true)]
        [TestCase("Quality", false)]
        [TestCase("TargetFrameRate", true)]
        [TestCase("TargetFrameRate", false)]
        [TestCase("MasterVolume", true)]
        [TestCase("MasterVolume", false)]
        public void Apply_RollbackFailureReportsExactField(
            string fieldName,
            bool rollbackSetterThrows)
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = ConfigureRollbackFailure(runtime, fieldName, rollbackSetterThrows);
            var service = PlayerOptionsTestData.CreateService(runtime);
            Assert.That(service.SetState(state).IsSuccess, Is.True);

            var result = service.Apply();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.RollbackFailed));
            Assert.That(result.RollbackFailedFields, Is.EqualTo(ParseField(fieldName)));
            Assert.That(
                result.AffectedFields,
                Is.EqualTo(ExpectedAffectedFieldsForRollbackFailure(fieldName)));
            Assert.That(
                result.OutcomeUnknownFields,
                Is.EqualTo(
                    fieldName == "MasterVolume"
                        ? PlayerOptionsField.Display
                        : PlayerOptionsField.None));
        }

        [Test]
        public void Apply_UnspecifiedRefreshUsesThreeArgumentResolutionContract()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var state = PlayerOptionsTestData.CreateState(
                width: 1280,
                height: 720,
                refreshNumerator: 0,
                refreshDenominator: 0);

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out _,
                out var warnings,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(runtime.Calls, Is.EqualTo(new[] { "resolution:1280x720:False" }));
            Assert.That(runtime.LastResolutionSpecifiedRefreshRate, Is.False);
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.ResolutionChangeDeferred));
        }

        [Test]
        public void Apply_BaselineReadFailureReturnsRuntimeUnavailableWithoutSetters()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                RuntimeReadException = new InvalidOperationException("runtime read failure"),
            };

            var success = PlayerOptionsRuntimeApplier.TryApply(
                PlayerOptionsTestData.CreateDefaultState(),
                runtime,
                out var error,
                out _,
                out _);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(runtime.Calls, Is.Empty);
        }

        [Test]
        public void Apply_DiagnosticFrameOverrideReadFailureDoesNotUndoSuccessfulApply()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                ThrowOnVSyncRead = true,
                ThrowOnRenderFrameIntervalRead = true,
            };
            var state = PlayerOptionsTestData.CreateState(targetFrameRate: 90);

            var success = PlayerOptionsRuntimeApplier.TryApply(
                state,
                runtime,
                out var error,
                out var warnings,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(runtime.TargetFrameRateValue, Is.EqualTo(90));
        }

        private static PlayerOptionsState ConfigureSingleFieldChange(
            FakePlayerOptionsRuntime runtime,
            string fieldName,
            bool setterThrows,
            bool readbackMismatch)
        {
            switch (fieldName)
            {
                case "Quality":
                    runtime.QualitySetFailuresRemaining = setterThrows ? 1 : 0;
                    runtime.IgnoreQualitySet = readbackMismatch;
                    return PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "Low");
                case "TargetFrameRate":
                    runtime.TargetSetFailuresRemaining = setterThrows ? 1 : 0;
                    runtime.IgnoreTargetSet = readbackMismatch;
                    return PlayerOptionsTestData.CreateState(targetFrameRate: 90);
                case "MasterVolume":
                    runtime.VolumeSetFailuresRemaining = setterThrows ? 1 : 0;
                    runtime.IgnoreVolumeSet = readbackMismatch;
                    return PlayerOptionsTestData.CreateState(masterVolume: 0.5f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null);
            }
        }

        private static PlayerOptionsState ConfigureRollbackFailure(
            FakePlayerOptionsRuntime runtime,
            string fieldName,
            bool rollbackSetterThrows)
        {
            switch (fieldName)
            {
                case "Quality":
                    runtime.TargetSetFailuresRemaining = 1;
                    runtime.QualitySetFailureOnCall = rollbackSetterThrows ? 2 : 0;
                    runtime.IgnoreQualitySetOnCall = rollbackSetterThrows ? 0 : 2;
                    return PlayerOptionsTestData.CreateState(
                        targetFrameRate: 90,
                        qualityIndex: 0,
                        qualityName: "Low");
                case "TargetFrameRate":
                    runtime.VolumeSetFailuresRemaining = 1;
                    runtime.TargetSetFailureOnCall = rollbackSetterThrows ? 2 : 0;
                    runtime.IgnoreTargetSetOnCall = rollbackSetterThrows ? 0 : 2;
                    return PlayerOptionsTestData.CreateState(
                        targetFrameRate: 90,
                        masterVolume: 0.5f);
                case "MasterVolume":
                    runtime.ResolutionSetFailuresRemaining = 1;
                    runtime.VolumeSetFailureOnCall = rollbackSetterThrows ? 2 : 0;
                    runtime.IgnoreVolumeSetOnCall = rollbackSetterThrows ? 0 : 2;
                    return PlayerOptionsTestData.CreateState(
                        width: 1280,
                        height: 720,
                        masterVolume: 0.5f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null);
            }
        }

        private static PlayerOptionsField ExpectedAffectedFieldsForRollbackFailure(string fieldName)
        {
            switch (fieldName)
            {
                case "Quality":
                    return PlayerOptionsField.Quality | PlayerOptionsField.TargetFrameRate;
                case "TargetFrameRate":
                    return PlayerOptionsField.TargetFrameRate | PlayerOptionsField.MasterVolume;
                case "MasterVolume":
                    return PlayerOptionsField.MasterVolume | PlayerOptionsField.Display;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, null);
            }
        }

        private static PlayerOptionsField ParseField(string fieldName)
        {
            return (PlayerOptionsField)Enum.Parse(typeof(PlayerOptionsField), fieldName);
        }
    }
}
