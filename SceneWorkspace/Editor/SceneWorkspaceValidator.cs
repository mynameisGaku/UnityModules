using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>差分確認または変更の前に、エディター、シーン、設定を安全側で検証します。</summary>
    internal static class SceneWorkspaceValidator
    {
        internal static SceneWorkspaceValidation ValidateCurrent(SceneWorkspaceSnapshot snapshot)
        {
            if (snapshot == null)
                return Failure(SceneWorkspaceError.CaptureFailed, "現在のシーン構成を取得できませんでした。");
            if (snapshot.PlayModeActive)
                return Failure(SceneWorkspaceError.PlayModeActive, "再生モードを終了してから、シーン作業セットを使用してください。");
            if (snapshot.Compiling || snapshot.Updating)
                return Failure(SceneWorkspaceError.EditorBusy, "コンパイルまたはアセット更新が終わるまで待ってください。");
            if (snapshot.PrefabStageOpen)
                return Failure(SceneWorkspaceError.PrefabStageOpen, "プレハブ編集画面を閉じてから、シーン作業セットを使用してください。");
            return ValidateScenes(snapshot.Scenes, true);
        }

        internal static SceneWorkspaceValidation ValidateProfile(SceneWorkspaceProfileSnapshot profile)
        {
            if (profile == null || !profile.Exists)
                return Failure(SceneWorkspaceError.InvalidProfile, "作業セット設定を選択してください。");
            if (!IsSupportedAssetPath(profile.Path) || string.IsNullOrEmpty(profile.Guid))
                return Failure(SceneWorkspaceError.ProfileNotSaved, "差分を確認する前に、作業セット設定をAssetsフォルダー以下へ保存してください。");
            return ValidateScenes(profile.Scenes, false);
        }

        private static SceneWorkspaceValidation ValidateScenes(IReadOnlyList<SceneWorkspaceSceneState> scenes, bool current)
        {
            if (scenes == null || scenes.Count == 0)
                return Failure(SceneWorkspaceError.NoScenes, current ? "保存済みのシーンを一つ以上開いてください。" : "設定へ保存済みのシーンを一つ以上追加してください。");

            var guids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var loadedCount = 0;
            var activeCount = 0;
            for (var index = 0; index < scenes.Count; index++)
            {
                var scene = scenes[index];
                if (scene == null || !scene.Exists)
                    return Failure(SceneWorkspaceError.MissingScene, "参照先が未指定か、存在しないシーンがあります。");
                if (string.IsNullOrEmpty(scene.Path))
                    return Failure(SceneWorkspaceError.UntitledScene, "無題のシーンをすべて保存してから、シーン作業セットを使用してください。");
                if (!IsSupportedScenePath(scene.Path))
                    return Failure(SceneWorkspaceError.UnsupportedScenePath, "すべてのシーンをAssetsフォルダー以下の.unityアセットとして保存してください。");
                if (string.IsNullOrEmpty(scene.Guid))
                    return Failure(SceneWorkspaceError.MissingScene, "有効なアセットGUIDを取得できないシーンがあります。");
                if (!guids.Add(scene.Guid) || !paths.Add(scene.Path))
                    return Failure(SceneWorkspaceError.DuplicateScene, "シーン構成に同じシーンが重複しています。");
                if (current && scene.Dirty)
                    return Failure(SceneWorkspaceError.DirtyScene, "作業セットを切り替える前に、変更済みのシーンをすべて保存するか変更を元へ戻してください。");
                if (scene.Loaded)
                    loadedCount++;
                if (scene.Active)
                {
                    activeCount++;
                    if (!scene.Loaded)
                        return Failure(SceneWorkspaceError.InvalidActiveScene, "使用中にするシーンは、読み込む設定も有効にしてください。");
                }
            }

            if (loadedCount == 0)
                return Failure(SceneWorkspaceError.NoLoadedScene, "読み込むシーンを一つ以上指定してください。");
            if (activeCount != 1)
                return Failure(SceneWorkspaceError.InvalidActiveScene, "読み込むシーンのうち、使用中にするシーンを一つだけ指定してください。");
            return SceneWorkspaceValidation.Success;
        }

        private static bool IsSupportedScenePath(string path)
        {
            return IsSupportedAssetPath(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static SceneWorkspaceValidation Failure(SceneWorkspaceError error, string message)
        {
            return new SceneWorkspaceValidation(error, message);
        }
    }
}
