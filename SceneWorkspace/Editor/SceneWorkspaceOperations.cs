using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Coordinates capture, preview, exact revalidation, restore, verification, and recovery through one gateway.</summary>
    internal sealed class SceneWorkspaceOperations
    {
        private readonly ISceneWorkspaceGateway gateway;

        internal SceneWorkspaceOperations(ISceneWorkspaceGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        internal SceneWorkspaceCaptureResult CaptureCurrentSetup()
        {
            try
            {
                var snapshot = gateway.CaptureCurrentSetup();
                var validation = SceneWorkspaceValidator.ValidateCurrent(snapshot);
                return validation.Succeeded
                    ? new SceneWorkspaceCaptureResult(SceneWorkspaceError.None, string.Empty, SceneWorkspaceFingerprint.ComputeCurrent(snapshot.Scenes), snapshot.Scenes)
                    : new SceneWorkspaceCaptureResult(validation.Error, validation.Message, string.Empty, snapshot?.Scenes);
            }
            catch (Exception exception)
            {
                return new SceneWorkspaceCaptureResult(SceneWorkspaceError.CaptureFailed, exception.Message, string.Empty, Array.Empty<SceneWorkspaceSceneState>());
            }
        }

        internal SceneWorkspacePlan Preview(SceneWorkspaceProfile profile)
        {
            try
            {
                var current = gateway.CaptureCurrentSetup();
                var target = gateway.CaptureProfile(profile);
                var plan = SceneWorkspacePlanner.Create(current, target, SceneWorkspacePlanRegistry.NextGeneration());
                if (plan.IsReady)
                    SceneWorkspacePlanRegistry.Register(plan, profile);
                return plan;
            }
            catch (Exception exception)
            {
                return SceneWorkspacePlanner.Failure(SceneWorkspaceError.CaptureFailed, exception.Message);
            }
        }

        internal SceneWorkspaceApplyResult Apply(SceneWorkspacePlan plan)
        {
            if (plan == null)
                return Failure(false, SceneWorkspaceError.StalePlan, "A confirmed plan is required.");
            if (!plan.IsReady)
                return Failure(false, plan.Error, plan.Message);
            if (!SceneWorkspaceExecutionGuard.TryEnter(out var lease))
                return Failure(false, SceneWorkspaceError.ApplyInProgress, "Scene Workspace is already switching a setup.");

            using (lease)
            {
                var consumeError = SceneWorkspacePlanRegistry.TryConsume(plan, out var profile);
                if (consumeError != SceneWorkspaceError.None)
                    return Failure(false, consumeError, consumeError == SceneWorkspaceError.PlanAlreadyConsumed ? "This plan has already been used." : "This plan is no longer available. Preview again.");

                SceneWorkspaceSnapshot current;
                SceneWorkspaceProfileSnapshot target;
                try
                {
                    current = gateway.CaptureCurrentSetup();
                    target = gateway.CaptureProfile(profile);
                }
                catch (Exception exception)
                {
                    return Failure(false, SceneWorkspaceError.CaptureFailed, exception.Message);
                }

                var currentValidation = SceneWorkspaceValidator.ValidateCurrent(current);
                if (!currentValidation.Succeeded)
                    return Failure(false, currentValidation.Error, currentValidation.Message);
                var profileValidation = SceneWorkspaceValidator.ValidateProfile(target);
                if (!profileValidation.Succeeded)
                    return Failure(false, profileValidation.Error, profileValidation.Message);
                if (!StringComparer.Ordinal.Equals(plan.CurrentFingerprint, SceneWorkspaceFingerprint.ComputeCurrent(current.Scenes)))
                    return Failure(false, SceneWorkspaceError.StalePlan, "The current scene setup changed after Preview.");
                if (!StringComparer.Ordinal.Equals(plan.ProfileRevision, SceneWorkspaceFingerprint.ComputeProfile(target)))
                    return Failure(false, SceneWorkspaceError.StalePlan, "The workspace profile changed after Preview.");

                if (!plan.HasChanges)
                {
                    if (!SceneWorkspaceSnapshotComparer.Matches(plan.TargetScenes, current.Scenes, out var noChangeDifference))
                        return Failure(false, SceneWorkspaceError.VerificationFailed, noChangeDifference);
                    return Success(false, "The current setup already matches the profile.");
                }

                var original = Copy(current.Scenes);
                try
                {
                    gateway.RestoreSetup(plan.TargetScenes);
                }
                catch (Exception exception)
                {
                    return Rollback(original, SceneWorkspaceError.ApplyFailed, exception.Message);
                }

                SceneWorkspaceSnapshot applied;
                try
                {
                    applied = gateway.CaptureCurrentSetup();
                }
                catch (Exception exception)
                {
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, "The applied setup could not be captured: " + exception.Message);
                }

                var appliedValidation = SceneWorkspaceValidator.ValidateCurrent(applied);
                if (!appliedValidation.Succeeded)
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, appliedValidation.Message);
                if (!SceneWorkspaceSnapshotComparer.Matches(plan.TargetScenes, applied.Scenes, out var difference))
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, difference);
                return Success(true, "The workspace was switched and verified.");
            }
        }

        private SceneWorkspaceApplyResult Rollback(IReadOnlyList<SceneWorkspaceSceneState> original, SceneWorkspaceError applyError, string applyMessage)
        {
            try
            {
                gateway.RestoreSetup(original);
                var rolledBack = gateway.CaptureCurrentSetup();
                var validation = SceneWorkspaceValidator.ValidateCurrent(rolledBack);
                if (!validation.Succeeded)
                    return RollbackFailure(applyError, applyMessage, validation.Message);
                if (!SceneWorkspaceSnapshotComparer.Matches(original, rolledBack.Scenes, out var difference))
                    return RollbackFailure(applyError, applyMessage, difference);
                return new SceneWorkspaceApplyResult(true, false, applyError, applyMessage, true, true, SceneWorkspaceError.None, "The original setup was restored and verified.");
            }
            catch (Exception exception)
            {
                return RollbackFailure(applyError, applyMessage, exception.Message);
            }
        }

        private static SceneWorkspaceApplyResult RollbackFailure(SceneWorkspaceError applyError, string applyMessage, string rollbackMessage)
        {
            return new SceneWorkspaceApplyResult(true, false, applyError, applyMessage, true, false, SceneWorkspaceError.RollbackFailed, rollbackMessage);
        }

        private static SceneWorkspaceApplyResult Success(bool attempted, string message)
        {
            return new SceneWorkspaceApplyResult(attempted, true, SceneWorkspaceError.None, message, false, false, SceneWorkspaceError.None, string.Empty);
        }

        private static SceneWorkspaceApplyResult Failure(bool attempted, SceneWorkspaceError error, string message)
        {
            return new SceneWorkspaceApplyResult(attempted, false, error, message, false, false, SceneWorkspaceError.None, string.Empty);
        }

        private static SceneWorkspaceSceneState[] Copy(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            var result = new SceneWorkspaceSceneState[scenes.Count];
            for (var index = 0; index < scenes.Count; index++)
                result[index] = scenes[index].WithIndex(index);
            return result;
        }
    }
}
