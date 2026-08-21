// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// Identifies one GameObject that contains missing MonoBehaviour slots.
    /// </summary>
    internal readonly struct MissingScriptFinding
    {
        /// <summary>
        /// Creates one deterministic missing-script finding.
        /// </summary>
        /// <param name="hierarchyPath">The hierarchy path including sibling indices.</param>
        /// <param name="missingScriptCount">The number of missing MonoBehaviour slots.</param>
        internal MissingScriptFinding(string hierarchyPath, int missingScriptCount)
        {
            HierarchyPath = hierarchyPath;
            MissingScriptCount = missingScriptCount;
        }

        /// <summary>
        /// Gets the hierarchy path including sibling indices.
        /// </summary>
        internal string HierarchyPath { get; }

        /// <summary>
        /// Gets the number of missing MonoBehaviour slots.
        /// </summary>
        internal int MissingScriptCount { get; }
    }
}
