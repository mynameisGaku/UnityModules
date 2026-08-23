using System;

namespace SceneWorkspace.Editor
{
    /// <summary>Owns the explicit capture, preview, confirmation, and single apply state shown by the window.</summary>
    internal sealed class SceneWorkspacePresenter
    {
        private readonly Func<SceneWorkspaceCaptureResult> capture;
        private readonly Func<SceneWorkspaceProfile, SceneWorkspacePlan> preview;
        private readonly Func<SceneWorkspacePlan, SceneWorkspaceApplyResult> apply;
        private readonly Func<SceneWorkspaceProfile, SceneWorkspaceCaptureResult, SceneWorkspaceValidation> writeProfile;

        internal SceneWorkspacePresenter(
            Func<SceneWorkspaceCaptureResult> capture = null,
            Func<SceneWorkspaceProfile, SceneWorkspacePlan> preview = null,
            Func<SceneWorkspacePlan, SceneWorkspaceApplyResult> apply = null,
            Func<SceneWorkspaceProfile, SceneWorkspaceCaptureResult, SceneWorkspaceValidation> writeProfile = null)
        {
            this.capture = capture ?? SceneWorkspaceService.CaptureCurrentSetup;
            this.preview = preview ?? SceneWorkspaceService.Preview;
            this.apply = apply ?? SceneWorkspaceService.Apply;
            this.writeProfile = writeProfile ?? SceneWorkspaceProfileWriter.ReplaceFromCapture;
        }

        internal SceneWorkspaceProfile Profile { get; private set; }
        internal SceneWorkspaceCaptureResult Capture { get; private set; }
        internal SceneWorkspacePlan Plan { get; private set; }
        internal SceneWorkspaceApplyResult Result { get; private set; }
        internal bool ConfirmationAccepted { get; private set; }
        internal string Message { get; private set; } = string.Empty;
        internal bool CanPreview => Profile != null;
        internal bool CanApply => Plan != null && Plan.IsReady && ConfirmationAccepted;

        internal void SetProfile(SceneWorkspaceProfile profile)
        {
            if (ReferenceEquals(Profile, profile))
                return;
            Profile = profile;
            Capture = null;
            InvalidatePlan();
        }

        internal void CaptureIntoProfile()
        {
            InvalidatePlan();
            try
            {
                Capture = capture();
                var outcome = writeProfile(Profile, Capture);
                Message = outcome.Succeeded ? "The current scene setup was copied into the profile. Save the profile when ready." : Format(outcome.Error, outcome.Message);
            }
            catch (Exception exception)
            {
                Capture = new SceneWorkspaceCaptureResult(SceneWorkspaceError.CaptureFailed, exception.Message, string.Empty, Array.Empty<SceneWorkspaceSceneState>());
                Message = Format(Capture.Error, Capture.Message);
            }
        }

        internal void NotifyProfileChanged()
        {
            InvalidatePlan();
            Message = "The profile changed. Preview again after finishing the settings.";
        }

        internal void Preview()
        {
            ConfirmationAccepted = false;
            Result = null;
            try
            {
                Plan = preview(Profile);
                Message = Plan == null
                    ? "Preview returned no plan."
                    : Plan.IsReady
                        ? Plan.HasChanges ? "Preview is ready. Review every change before confirming." : "The current setup already matches this profile."
                        : Format(Plan.Error, Plan.Message);
            }
            catch (Exception exception)
            {
                Plan = null;
                Message = Format(SceneWorkspaceError.CaptureFailed, exception.Message);
            }
        }

        internal void SetConfirmation(bool accepted)
        {
            ConfirmationAccepted = accepted && Plan != null && Plan.IsReady;
        }

        internal void Apply()
        {
            if (!CanApply)
            {
                Message = "Preview and confirm a ready plan before switching the workspace.";
                return;
            }

            var consumed = Plan;
            Plan = null;
            ConfirmationAccepted = false;
            try
            {
                Result = apply(consumed);
                Message = Result == null
                    ? "Apply returned no result."
                    : Result.Succeeded
                        ? Result.ApplyMessage
                        : Format(Result.ApplyError, Result.ApplyMessage);
            }
            catch (Exception exception)
            {
                Result = new SceneWorkspaceApplyResult(false, false, SceneWorkspaceError.ApplyFailed, exception.Message, false, false, SceneWorkspaceError.None, string.Empty);
                Message = Format(Result.ApplyError, Result.ApplyMessage);
            }
        }

        private void InvalidatePlan()
        {
            Plan = null;
            Result = null;
            ConfirmationAccepted = false;
        }

        private static string Format(SceneWorkspaceError error, string message)
        {
            return string.IsNullOrEmpty(message) ? error.ToString() : error + ": " + message;
        }
    }
}
