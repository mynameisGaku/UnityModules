// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal readonly struct ProjectSetupCreatedRootFile : IEquatable<ProjectSetupCreatedRootFile>
    {
        internal ProjectSetupCreatedRootFile(string path, string contentHash)
        {
            Path = path ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
        }

        internal string Path { get; }

        internal string ContentHash { get; }

        public bool Equals(ProjectSetupCreatedRootFile other)
        {
            return string.Equals(Path, other.Path, StringComparison.Ordinal)
                && string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupCreatedRootFile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Path) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(ContentHash);
            }
        }
    }
}
