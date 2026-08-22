// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal sealed class ProjectSetupBuildScene
    {
        [SerializeField] private string sceneGuid = string.Empty;
        [SerializeField] private string fallbackPath = string.Empty;
        [SerializeField] private bool enabled = true;

        internal ProjectSetupBuildScene()
        {
        }

        internal ProjectSetupBuildScene(string sceneGuid, string fallbackPath, bool enabled)
        {
            this.sceneGuid = sceneGuid ?? string.Empty;
            this.fallbackPath = NormalizePath(fallbackPath);
            this.enabled = enabled;
        }

        internal string SceneGuid => sceneGuid ?? string.Empty;
        internal string FallbackPath => fallbackPath ?? string.Empty;
        internal bool Enabled { get => enabled; set => enabled = value; }

        internal SceneAsset SceneAsset
        {
            get
            {
                return TryResolve(out var path) ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;
            }
            set
            {
                fallbackPath = value == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(value));
                sceneGuid = string.IsNullOrEmpty(fallbackPath) ? string.Empty : AssetDatabase.AssetPathToGUID(fallbackPath);
            }
        }

        internal bool TryResolve(out string path)
        {
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                var guidPath = NormalizePath(AssetDatabase.GUIDToAssetPath(sceneGuid));
                if (IsSceneAsset(guidPath))
                {
                    path = guidPath;
                    return true;
                }
            }

            var storedPath = NormalizePath(fallbackPath);
            if (IsSceneAsset(storedPath))
            {
                path = storedPath;
                return true;
            }

            path = storedPath;
            return false;
        }

        internal ProjectSetupBuildScene Clone()
        {
            return new ProjectSetupBuildScene(SceneGuid, FallbackPath, Enabled);
        }

        private static bool IsSceneAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
