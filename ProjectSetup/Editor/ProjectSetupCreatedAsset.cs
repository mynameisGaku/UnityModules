// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal readonly struct ProjectSetupCreatedAsset : IEquatable<ProjectSetupCreatedAsset>
    {
        internal ProjectSetupCreatedAsset(string path, string contentHash)
        {
            Path = NormalizePath(path);
            ContentHash = contentHash ?? string.Empty;
        }

        internal string Path { get; }

        internal string ContentHash { get; }

        public bool Equals(ProjectSetupCreatedAsset other)
        {
            return string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupCreatedAsset other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(Path) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(ContentHash);
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
