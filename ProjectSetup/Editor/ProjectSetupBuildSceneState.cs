// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal readonly struct ProjectSetupBuildSceneState : IEquatable<ProjectSetupBuildSceneState>
    {
        internal ProjectSetupBuildSceneState(string sceneGuid, string path, bool enabled)
        {
            SceneGuid = sceneGuid ?? string.Empty;
            Path = NormalizePath(path);
            Enabled = enabled;
        }

        internal string SceneGuid { get; }
        internal string Path { get; }
        internal bool Enabled { get; }

        public bool Equals(ProjectSetupBuildSceneState other)
        {
            var sameIdentity = !string.IsNullOrEmpty(SceneGuid) && !string.IsNullOrEmpty(other.SceneGuid)
                ? string.Equals(SceneGuid, other.SceneGuid, StringComparison.Ordinal)
                : string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
            return sameIdentity && Enabled == other.Enabled;
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupBuildSceneState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = !string.IsNullOrEmpty(SceneGuid)
                    ? StringComparer.Ordinal.GetHashCode(SceneGuid)
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
                hash = (hash * 397) ^ Enabled.GetHashCode();
                return hash;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
