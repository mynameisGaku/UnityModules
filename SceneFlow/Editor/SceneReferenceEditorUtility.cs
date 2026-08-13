using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build.Profile;

[assembly: InternalsVisibleTo("SceneFlow.Editor.Tests")]

namespace SceneFlow.Editor
{
    /// <summary>SceneReference の参照解決と、現在の Build Profile に対する登録状態を調べる。</summary>
    internal static class SceneReferenceEditorUtility
    {
        /// <summary>SceneReference を Inspector に表示するための検証状態。</summary>
        internal enum ValidationStatus
        {
            Empty,
            Missing,
            NotInBuild,
            Disabled,
            Valid,
        }

        /// <summary>
        /// GUID を優先して Scene Asset を解決し、移動後のパスまたは欠けた GUID を補う。
        /// GUID とパスの両方から Scene Asset を見つけられない場合は false を返す。
        /// </summary>
        /// <param name="guid">保存されている Scene Asset の GUID。</param>
        /// <param name="path">保存されているプロジェクト相対パス。</param>
        /// <param name="sceneAsset">解決できた Scene Asset。</param>
        /// <param name="resolvedGuid">現在の Scene Asset の GUID。</param>
        /// <param name="resolvedPath">現在の Scene Asset のプロジェクト相対パス。</param>
        /// <returns>Scene Asset を一意に解決できた場合は true。</returns>
        internal static bool TryResolve(string guid, string path, out SceneAsset sceneAsset, out string resolvedGuid, out string resolvedPath)
        {
            var normalizedGuid = guid ?? string.Empty;
            var normalizedPath = NormalizePath(path);

            if (!string.IsNullOrEmpty(normalizedGuid))
            {
                var guidPath = NormalizePath(AssetDatabase.GUIDToAssetPath(normalizedGuid));
                if (TryLoadScene(guidPath, out sceneAsset))
                {
                    resolvedGuid = normalizedGuid;
                    resolvedPath = guidPath;
                    return true;
                }
            }

            if (TryLoadScene(normalizedPath, out sceneAsset))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(normalizedPath);
                if (!string.IsNullOrEmpty(pathGuid))
                {
                    var canonicalPath = NormalizePath(AssetDatabase.GUIDToAssetPath(pathGuid));
                    if (!TryLoadScene(canonicalPath, out sceneAsset))
                    {
                        resolvedGuid = normalizedGuid;
                        resolvedPath = normalizedPath;
                        return false;
                    }

                    resolvedGuid = pathGuid;
                    resolvedPath = canonicalPath;
                    return true;
                }
            }

            sceneAsset = null;
            resolvedGuid = normalizedGuid;
            resolvedPath = normalizedPath;
            return false;
        }

        /// <summary>現在の Build Profile で、指定Sceneが有効に含まれるかを調べる。</summary>
        /// <param name="path">GUIDから解決済みのプロジェクト相対パス。</param>
        /// <returns>現在の実効Scene一覧に対する検証状態。</returns>
        internal static ValidationStatus Validate(string path)
        {
            return Validate(path, GetEffectiveScenes());
        }

        /// <summary>与えられた実効Scene一覧で、指定Sceneが有効に含まれるかを調べる。</summary>
        /// <param name="path">GUIDから解決済みのプロジェクト相対パス。</param>
        /// <param name="scenes">Build Profileから得た実効Scene一覧。</param>
        /// <returns>実効Scene一覧に対する検証状態。</returns>
        internal static ValidationStatus Validate(string path, EditorBuildSettingsScene[] scenes)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath)) return ValidationStatus.Empty;

            var foundDisabled = false;
            var effectiveScenes = scenes ?? Array.Empty<EditorBuildSettingsScene>();
            for (var i = 0; i < effectiveScenes.Length; i++)
            {
                var scene = effectiveScenes[i];
                if (!string.Equals(NormalizePath(scene.path), normalizedPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (scene.enabled) return ValidationStatus.Valid;
                foundDisabled = true;
            }

            return foundDisabled ? ValidationStatus.Disabled : ValidationStatus.NotInBuild;
        }

        /// <summary>現在のBuild Profileが実際のPlayer buildに使うScene一覧を返す。</summary>
        /// <returns>有効・無効の状態を含む実効Scene一覧。</returns>
        internal static EditorBuildSettingsScene[] GetEffectiveScenes()
        {
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            return GetEffectiveScenes(activeProfile, EditorBuildSettings.scenes);
        }

        /// <summary>Build Profileの実効一覧を返し、platform profileでは指定fallbackを使う。</summary>
        /// <param name="activeProfile">現在のBuild Profile。platform profileではnull。</param>
        /// <param name="fallbackScenes">platform profileで使うScene一覧。</param>
        /// <returns>overrideまたはglobal継承を解決した実効Scene一覧。</returns>
        internal static EditorBuildSettingsScene[] GetEffectiveScenes(
            BuildProfile activeProfile,
            EditorBuildSettingsScene[] fallbackScenes)
        {
            if (activeProfile == null) return fallbackScenes ?? Array.Empty<EditorBuildSettingsScene>();

            var profileScenes = activeProfile.GetScenesForBuild();
            return profileScenes ?? Array.Empty<EditorBuildSettingsScene>();
        }

        /// <summary>Scene Asset の現在の GUID とパスを返す。</summary>
        /// <param name="sceneAsset">保存対象に選ばれた Scene Asset。</param>
        /// <param name="guid">Scene Asset の GUID。</param>
        /// <param name="path">Scene Asset のプロジェクト相対パス。</param>
        /// <returns>保存できるScene Assetである場合は true。</returns>
        internal static bool TryGetIdentity(SceneAsset sceneAsset, out string guid, out string path)
        {
            path = sceneAsset == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(sceneAsset));
            guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return sceneAsset != null && IsScenePath(path) && !string.IsNullOrEmpty(guid);
        }

        private static bool TryLoadScene(string path, out SceneAsset sceneAsset)
        {
            sceneAsset = IsScenePath(path) ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;
            return sceneAsset != null;
        }

        private static bool IsScenePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
