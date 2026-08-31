using System;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>画面に表示する取り込み、差分確認、同意、単回切り替えの状態を所有します。</summary>
    internal sealed class SceneWorkspacePresenter
    {
        /// <summary>現在のシーン構成を取得する処理です。</summary>
        private readonly Func<SceneWorkspaceCaptureResult> capture;

        /// <summary>選択中の設定との差分を作る処理です。</summary>
        private readonly Func<SceneWorkspaceProfile, SceneWorkspacePlan> preview;

        /// <summary>確認済み計画を一度だけ適用する処理です。</summary>
        private readonly Func<SceneWorkspacePlan, SceneWorkspaceApplyResult> apply;

        /// <summary>取得した現在構成を設定へ書き込む処理です。</summary>
        private readonly Func<SceneWorkspaceProfile, SceneWorkspaceCaptureResult, SceneWorkspaceValidation> writeProfile;

        /// <summary>画面処理を構築します。未指定の処理には実際のエディター処理を使います。</summary>
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

        /// <summary>現在選択している設定アセットです。</summary>
        internal SceneWorkspaceProfile Profile { get; private set; }

        /// <summary>最後に取得した現在のシーン構成です。</summary>
        internal SceneWorkspaceCaptureResult Capture { get; private set; }

        /// <summary>最後に作成した単回使用の差分計画です。</summary>
        internal SceneWorkspacePlan Plan { get; private set; }

        /// <summary>最後の切り替え結果です。</summary>
        internal SceneWorkspaceApplyResult Result { get; private set; }

        /// <summary>利用者が差分内容を確認済みかを表します。</summary>
        internal bool ConfirmationAccepted { get; private set; }

        /// <summary>画面下部へ表示する日本語案内です。</summary>
        internal string Message { get; private set; } = string.Empty;

        /// <summary>設定が選択され、差分を確認できるかを表します。</summary>
        internal bool CanPreview => Profile != null;

        /// <summary>準備済み計画へ利用者が同意し、切り替えられるかを表します。</summary>
        internal bool CanApply => Plan != null && Plan.IsReady && ConfirmationAccepted;

        /// <summary>選択中の設定を変更し、以前の差分計画と結果を破棄します。</summary>
        internal void SetProfile(SceneWorkspaceProfile profile)
        {
            if (ReferenceEquals(Profile, profile))
                return;
            Profile = profile;
            Capture = null;
            InvalidatePlan();
        }

        /// <summary>現在構成を設定へ取り込みます。想定外の問題は詳細をコンソールだけへ記録します。</summary>
        internal void CaptureIntoProfile()
        {
            InvalidatePlan();
            try
            {
                Capture = capture();
                var outcome = writeProfile(Profile, Capture);
                Message = outcome.Succeeded ? "現在のシーン構成を設定へ取り込みました。内容を確認してから設定アセットを保存してください。" : SceneWorkspaceDisplayText.FormatOutcome(outcome.Error, outcome.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Capture = new SceneWorkspaceCaptureResult(SceneWorkspaceError.CaptureFailed, "現在構成の取り込み中に処理できない問題が発生しました。詳しくはコンソールを確認してください。", string.Empty, Array.Empty<SceneWorkspaceSceneState>());
                Message = SceneWorkspaceDisplayText.FormatOutcome(Capture.Error, Capture.Message);
            }
        }

        /// <summary>設定内容の変更後に、古い差分計画と同意を破棄します。</summary>
        internal void NotifyProfileChanged()
        {
            InvalidatePlan();
            Message = "設定内容が変わりました。編集を終えてから、もう一度差分を確認してください。";
        }

        /// <summary>現在構成と設定の差分を作成し、切り替え前の同意を未確認へ戻します。</summary>
        internal void Preview()
        {
            ConfirmationAccepted = false;
            Result = null;
            try
            {
                Plan = preview(Profile);
                Message = Plan == null
                    ? "差分確認結果を取得できませんでした。"
                    : Plan.IsReady
                        ? Plan.HasChanges ? "差分を確認できました。同意する前に、すべての変更を確認してください。" : "現在のシーン構成は、この設定と一致しています。"
                        : SceneWorkspaceDisplayText.FormatOutcome(Plan.Error, Plan.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Plan = null;
                Message = SceneWorkspaceDisplayText.FormatOutcome(SceneWorkspaceError.CaptureFailed, "差分確認中に処理できない問題が発生しました。詳しくはコンソールを確認してください。");
            }
        }

        /// <summary>準備済み計画がある場合だけ、利用者の同意状態を更新します。</summary>
        internal void SetConfirmation(bool accepted)
        {
            ConfirmationAccepted = accepted && Plan != null && Plan.IsReady;
        }

        /// <summary>同意済み計画を一度だけ切り替え処理へ渡し、計画を画面から破棄します。</summary>
        internal void Apply()
        {
            if (!CanApply)
            {
                Message = "作業セットを切り替える前に、最新の差分を確認して内容確認欄を有効にしてください。";
                return;
            }

            var consumed = Plan;
            Plan = null;
            ConfirmationAccepted = false;
            try
            {
                Result = apply(consumed);
                Message = Result == null
                    ? "切り替え結果を取得できませんでした。"
                    : Result.Succeeded
                        ? Result.ApplyMessage
                        : SceneWorkspaceDisplayText.FormatOutcome(Result.ApplyError, Result.ApplyMessage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Result = new SceneWorkspaceApplyResult(false, false, SceneWorkspaceError.ApplyFailed, "切り替え中に処理できない問題が発生しました。詳しくはコンソールを確認してください。", false, false, SceneWorkspaceError.None, string.Empty);
                Message = SceneWorkspaceDisplayText.FormatOutcome(Result.ApplyError, Result.ApplyMessage);
            }
        }

        /// <summary>表示中の計画、結果、同意状態をまとめて破棄します。</summary>
        private void InvalidatePlan()
        {
            Plan = null;
            Result = null;
            ConfirmationAccepted = false;
        }

    }
}
