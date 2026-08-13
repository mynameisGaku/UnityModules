using System;
using UnityEditor;
using UnityEngine;

namespace SceneFlow.Tests.PlayMode
{
    /// <summary>PlayMode用Sceneを固定GUIDから現在のAssetsまたはPackages配置へ解決する。</summary>
    internal static class SceneFlowPlayModeSceneAssetPaths
    {
        private static readonly string[] SceneGuids =
        {
            "bc13bc553ce04bef8671b377aa4633f6",
            "683bc1327a454a039c3ae6e684b6de9e",
            "5bfe9a00eb3c4c7585d2b95d51218728",
        };

        /// <summary>Harness、Target A、Target Bの順で現在の完全パスを返す。</summary>
        internal static bool TryResolve(out string[] scenePaths, out string error)
        {
            scenePaths = new string[SceneGuids.Length];
            for (var i = 0; i < SceneGuids.Length; i++)
            {
                var path = NormalizePath(AssetDatabase.GUIDToAssetPath(SceneGuids[i]));
                if (!IsProjectRelativeScenePath(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    error = $"PlayMode用SceneをGUIDから解決できません: {SceneGuids[i]}";
                    return false;
                }

                for (var previous = 0; previous < i; previous++)
                {
                    if (!string.Equals(scenePaths[previous], path, StringComparison.OrdinalIgnoreCase)) continue;
                    error = $"複数のPlayMode用Scene GUIDが同じパスを指しています: {path}";
                    return false;
                }

                scenePaths[i] = path;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>AssetsまたはPackagesから始まるScene Assetの完全パスかを返す。</summary>
        private static bool IsProjectRelativeScenePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return false;
            return path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static string NormalizePath(string path) => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }
}
