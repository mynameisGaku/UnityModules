using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>一つの接続口を通じて、取得、差分確認、再検証、切り替え、確認、復元を調整します。</summary>
    internal sealed class SceneWorkspaceOperations
    {
        /// <summary>Unityのシーン構成を読み取り、復元する接続口です。</summary>
        private readonly ISceneWorkspaceGateway gateway;

        /// <summary>利用する接続口を指定して処理群を構築します。未指定の場合は失敗します。</summary>
        internal SceneWorkspaceOperations(ISceneWorkspaceGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway), "シーン構成の接続口を指定してください。");
        }

        /// <summary>現在のシーン構成を変更せずに取得・検証します。想定外の問題はコンソールへ記録します。</summary>
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
                Debug.LogException(exception);
                return new SceneWorkspaceCaptureResult(SceneWorkspaceError.CaptureFailed, "現在のシーン構成を取得できませんでした。詳しくはコンソールを確認してください。", string.Empty, Array.Empty<SceneWorkspaceSceneState>());
            }
        }

        /// <summary>現在構成と指定設定の単回使用計画を、シーンを変更せずに作成します。</summary>
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
                Debug.LogException(exception);
                return SceneWorkspacePlanner.Failure(SceneWorkspaceError.CaptureFailed, "差分確認に必要なシーン構成を取得できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        /// <summary>確認済み計画を再検証して一度だけ適用し、失敗時は元構成の復元を試みます。</summary>
        internal SceneWorkspaceApplyResult Apply(SceneWorkspacePlan plan)
        {
            if (plan == null)
                return Failure(false, SceneWorkspaceError.StalePlan, "確認済みの差分計画が必要です。");
            if (!plan.IsReady)
                return Failure(false, plan.Error, plan.Message);
            if (!SceneWorkspaceExecutionGuard.TryEnter(out var lease))
                return Failure(false, SceneWorkspaceError.ApplyInProgress, "別のシーン構成を切り替えています。完了してからやり直してください。");

            using (lease)
            {
                var consumeError = SceneWorkspacePlanRegistry.TryConsume(plan, out var profile);
                if (consumeError != SceneWorkspaceError.None)
                    return Failure(false, consumeError, consumeError == SceneWorkspaceError.PlanAlreadyConsumed ? "この差分確認結果はすでに使用されています。" : "この差分確認結果は使用できません。もう一度差分を確認してください。");

                SceneWorkspaceSnapshot current;
                SceneWorkspaceProfileSnapshot target;
                try
                {
                    current = gateway.CaptureCurrentSetup();
                    target = gateway.CaptureProfile(profile);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return Failure(false, SceneWorkspaceError.CaptureFailed, "切り替え前のシーン構成を取得できませんでした。詳しくはコンソールを確認してください。");
                }

                var currentValidation = SceneWorkspaceValidator.ValidateCurrent(current);
                if (!currentValidation.Succeeded)
                    return Failure(false, currentValidation.Error, currentValidation.Message);
                var profileValidation = SceneWorkspaceValidator.ValidateProfile(target);
                if (!profileValidation.Succeeded)
                    return Failure(false, profileValidation.Error, profileValidation.Message);
                if (!StringComparer.Ordinal.Equals(plan.CurrentFingerprint, SceneWorkspaceFingerprint.ComputeCurrent(current.Scenes)))
                    return Failure(false, SceneWorkspaceError.StalePlan, "差分確認後に現在のシーン構成が変わりました。もう一度差分を確認してください。");
                if (!StringComparer.Ordinal.Equals(plan.ProfileRevision, SceneWorkspaceFingerprint.ComputeProfile(target)))
                    return Failure(false, SceneWorkspaceError.StalePlan, "差分確認後に作業セット設定が変わりました。もう一度差分を確認してください。");

                if (!plan.HasChanges)
                {
                    if (!SceneWorkspaceSnapshotComparer.Matches(plan.TargetScenes, current.Scenes, out var noChangeDifference))
                        return Failure(false, SceneWorkspaceError.VerificationFailed, noChangeDifference);
                    return Success(false, "現在のシーン構成は、この設定と一致しています。");
                }

                var original = Copy(current.Scenes);
                try
                {
                    gateway.RestoreSetup(plan.TargetScenes);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return Rollback(original, SceneWorkspaceError.ApplyFailed, "シーン構成の切り替え中に処理できない問題が発生しました。詳しくはコンソールを確認してください。");
                }

                SceneWorkspaceSnapshot applied;
                try
                {
                    applied = gateway.CaptureCurrentSetup();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, "切り替え後のシーン構成を取得できませんでした。詳しくはコンソールを確認してください。");
                }

                var appliedValidation = SceneWorkspaceValidator.ValidateCurrent(applied);
                if (!appliedValidation.Succeeded)
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, appliedValidation.Message);
                if (!SceneWorkspaceSnapshotComparer.Matches(plan.TargetScenes, applied.Scenes, out var difference))
                    return Rollback(original, SceneWorkspaceError.VerificationFailed, difference);
                return Success(true, "作業セットを切り替え、結果が設定と一致することを確認しました。");
            }
        }

        /// <summary>適用前に取得した構成へ復元し、復元結果を適用失敗とは別に返します。</summary>
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
                return new SceneWorkspaceApplyResult(true, false, applyError, applyMessage, true, true, SceneWorkspaceError.None, "元のシーン構成へ復元し、復元結果が一致することを確認しました。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return RollbackFailure(applyError, applyMessage, "元のシーン構成を復元または確認できませんでした。詳しくはコンソールを確認してください。");
            }
        }

        /// <summary>元構成の復元失敗を、元の適用失敗とは分けて返します。</summary>
        private static SceneWorkspaceApplyResult RollbackFailure(SceneWorkspaceError applyError, string applyMessage, string rollbackMessage)
        {
            return new SceneWorkspaceApplyResult(true, false, applyError, applyMessage, true, false, SceneWorkspaceError.RollbackFailed, rollbackMessage);
        }

        /// <summary>復元を必要としない切り替え成功を返します。</summary>
        private static SceneWorkspaceApplyResult Success(bool attempted, string message)
        {
            return new SceneWorkspaceApplyResult(attempted, true, SceneWorkspaceError.None, message, false, false, SceneWorkspaceError.None, string.Empty);
        }

        /// <summary>シーン変更前に停止した失敗を返します。</summary>
        private static SceneWorkspaceApplyResult Failure(bool attempted, SceneWorkspaceError error, string message)
        {
            return new SceneWorkspaceApplyResult(attempted, false, error, message, false, false, SceneWorkspaceError.None, string.Empty);
        }

        /// <summary>元構成を復元用の独立した配列へ複製し、位置を配列順にそろえます。</summary>
        private static SceneWorkspaceSceneState[] Copy(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            var result = new SceneWorkspaceSceneState[scenes.Count];
            for (var index = 0; index < scenes.Count; index++)
                result[index] = scenes[index].WithIndex(index);
            return result;
        }
    }
}
