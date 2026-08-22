// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal sealed class ProjectSetupSceneReference
    {
        [SerializeField] private string sceneGuid = string.Empty;
        [SerializeField] private string fallbackPath = string.Empty;

        internal ProjectSetupSceneReference()
        {
        }

        internal ProjectSetupSceneReference(string sceneGuid, string fallbackPath)
        {
            this.sceneGuid = sceneGuid ?? string.Empty;
            this.fallbackPath = NormalizePath(fallbackPath);
        }

        internal string SceneGuid => sceneGuid ?? string.Empty;
        internal string FallbackPath => fallbackPath ?? string.Empty;
        internal bool IsEmpty => string.IsNullOrEmpty(SceneGuid) && string.IsNullOrEmpty(FallbackPath);

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
            if (!string.IsNullOrEmpty(SceneGuid))
            {
                var guidPath = NormalizePath(AssetDatabase.GUIDToAssetPath(SceneGuid));
                if (IsSceneAsset(guidPath))
                {
                    path = guidPath;
                    return true;
                }
            }

            var storedPath = NormalizePath(FallbackPath);
            if (IsSceneAsset(storedPath))
            {
                path = storedPath;
                return true;
            }

            path = storedPath;
            return false;
        }

        internal ProjectSetupSceneReference Clone()
        {
            return new ProjectSetupSceneReference(SceneGuid, FallbackPath);
        }

        internal static bool SameIdentity(string leftGuid, string leftPath, string rightGuid, string rightPath)
        {
            return !string.IsNullOrEmpty(leftGuid) && !string.IsNullOrEmpty(rightGuid)
                ? string.Equals(leftGuid, rightGuid, StringComparison.Ordinal)
                : string.Equals(NormalizePath(leftPath), NormalizePath(rightPath), StringComparison.OrdinalIgnoreCase);
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
