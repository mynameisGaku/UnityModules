// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies one serialized object property whose referenced object is missing.
    /// </summary>
    internal readonly struct MissingObjectReferenceFinding
    {
        /// <summary>Creates one deterministic missing-reference location.</summary>
        internal MissingObjectReferenceFinding(
            string hierarchyPath,
            string componentTypeName,
            int componentIndex,
            string propertyPath)
        {
            HierarchyPath = hierarchyPath;
            ComponentTypeName = componentTypeName;
            ComponentIndex = componentIndex;
            PropertyPath = propertyPath;
        }

        /// <summary>Gets the hierarchy path including sibling indices.</summary>
        internal string HierarchyPath { get; }

        /// <summary>Gets the full component type name.</summary>
        internal string ComponentTypeName { get; }

        /// <summary>Gets the component index on the GameObject.</summary>
        internal int ComponentIndex { get; }

        /// <summary>Gets the serialized property path.</summary>
        internal string PropertyPath { get; }
    }
}
