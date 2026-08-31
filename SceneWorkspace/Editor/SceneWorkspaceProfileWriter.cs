using System;
using UnityEditor;

namespace SceneWorkspace.Editor
{
    /// <summary>画面から明示的に要求された現在構成の取り込みだけを実行します。</summary>
    internal static class SceneWorkspaceProfileWriter
    {
        /// <summary>取得済みの現在構成で設定を置き換え、取り消し履歴へ記録します。入力不備では設定を変更しません。</summary>
        internal static SceneWorkspaceValidation ReplaceFromCapture(SceneWorkspaceProfile profile, SceneWorkspaceCaptureResult capture)
        {
            if (profile == null)
                return new SceneWorkspaceValidation(SceneWorkspaceError.InvalidProfile, "現在構成を取り込む前に、作業セット設定を選択してください。");
            var profilePath = AssetDatabase.GetAssetPath(profile) ?? string.Empty;
            if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", StringComparison.Ordinal))
                return new SceneWorkspaceValidation(SceneWorkspaceError.ProfileNotSaved, "現在構成を取り込む前に、作業セット設定をAssetsフォルダー以下へ保存してください。");
            if (capture == null || !capture.Succeeded)
                return new SceneWorkspaceValidation(capture?.Error ?? SceneWorkspaceError.CaptureFailed, capture?.Message ?? "現在のシーン構成を取得できませんでした。");

            var entries = new SceneWorkspaceProfileEntry[capture.Scenes.Count];
            for (var index = 0; index < capture.Scenes.Count; index++)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(capture.Scenes[index].Path);
                if (scene == null)
                    return new SceneWorkspaceValidation(SceneWorkspaceError.MissingScene, "取得したシーンアセットを参照できません。");
                entries[index] = new SceneWorkspaceProfileEntry(scene, capture.Scenes[index].Loaded, capture.Scenes[index].Active);
            }

            Undo.RecordObject(profile, "現在のシーン構成を設定へ取り込む");
            profile.ReplaceEntries(entries);
            EditorUtility.SetDirty(profile);
            return SceneWorkspaceValidation.Success;
        }
    }
}
