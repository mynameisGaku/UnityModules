// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupSortingLayer : IEquatable<ProjectSetupSortingLayer>
    {
        internal ProjectSetupSortingLayer(string name, int uniqueId, bool locked)
        {
            Name = name ?? string.Empty;
            UniqueId = uniqueId;
            Locked = locked;
        }

        internal string Name { get; }
        internal int UniqueId { get; }
        internal bool Locked { get; }

        public bool Equals(ProjectSetupSortingLayer other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && UniqueId == other.UniqueId
                && Locked == other.Locked;
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupSortingLayer other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 397) ^ UniqueId;
                hash = (hash * 397) ^ Locked.GetHashCode();
                return hash;
            }
        }
    }
}
