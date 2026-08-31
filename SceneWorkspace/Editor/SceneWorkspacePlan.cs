using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>作業セット切り替えの、変更不能で単回使用の差分確認結果を保持します。</summary>
    public sealed class SceneWorkspacePlan
    {
        /// <summary>検証結果、設定識別情報、指紋値、変更前後の構成、差分から計画を作成します。</summary>
        internal SceneWorkspacePlan(SceneWorkspaceError error, string message, long generation, string profileGuid, string profilePath, string profileName, string profileRevision, string currentFingerprint, IEnumerable<SceneWorkspaceSceneState> currentScenes, IEnumerable<SceneWorkspaceSceneState> targetScenes, IEnumerable<SceneWorkspaceChange> changes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Generation = generation;
            ProfileGuid = profileGuid ?? string.Empty;
            ProfilePath = profilePath ?? string.Empty;
            ProfileName = profileName ?? string.Empty;
            ProfileRevision = profileRevision ?? string.Empty;
            CurrentFingerprint = currentFingerprint ?? string.Empty;
            CurrentScenes = Array.AsReadOnly((currentScenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
            TargetScenes = Array.AsReadOnly((targetScenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
            Changes = Array.AsReadOnly((changes ?? Enumerable.Empty<SceneWorkspaceChange>()).ToArray());
        }

        /// <summary>差分確認に失敗していない場合は<see cref="SceneWorkspaceError.None"/>を返します。</summary>
        public SceneWorkspaceError Error { get; }

        /// <summary>差分確認結果または失敗理由の日本語案内を返します。</summary>
        public string Message { get; }

        /// <summary>エディター領域内で計画を識別する、正の単調増加番号を返します。失敗時は零です。</summary>
        public long Generation { get; }

        /// <summary>差分確認に使った設定アセットのGUIDを返します。</summary>
        public string ProfileGuid { get; }

        /// <summary>差分確認に使った設定アセットのプロジェクト相対パスを返します。</summary>
        public string ProfilePath { get; }

        /// <summary>差分確認に使った設定アセットの名前を返します。</summary>
        public string ProfileName { get; }

        /// <summary>差分確認時の設定内容を表すSHA-256指紋値を返します。</summary>
        public string ProfileRevision { get; }

        /// <summary>差分確認時の現在構成を表すSHA-256指紋値を返します。</summary>
        public string CurrentFingerprint { get; }

        /// <summary>差分確認時の現在構成をシーン順で返します。呼び出し側から一覧を変更できません。</summary>
        public IReadOnlyList<SceneWorkspaceSceneState> CurrentScenes { get; }

        /// <summary>切り替え後の目標構成をシーン順で返します。呼び出し側から一覧を変更できません。</summary>
        public IReadOnlyList<SceneWorkspaceSceneState> TargetScenes { get; }

        /// <summary>現在構成から目標構成までの差分を確定順で返します。呼び出し側から一覧を変更できません。</summary>
        public IReadOnlyList<SceneWorkspaceChange> Changes { get; }

        /// <summary>差分計画を内容確認へ進められるかを返します。</summary>
        public bool IsReady => Error == SceneWorkspaceError.None;

        /// <summary>変更なし以外の差分を一つ以上含むかを返します。</summary>
        public bool HasChanges => Changes.Any(change => change.Kind != SceneWorkspaceChangeKind.Keep);
    }
}
