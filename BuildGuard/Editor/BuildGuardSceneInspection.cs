// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Stores all build-blocking findings collected from one loaded Scene.
    /// </summary>
    internal readonly struct BuildGuardSceneInspection
    {
        internal BuildGuardSceneInspection(
            IReadOnlyList<MissingScriptFinding> missingScripts,
            IReadOnlyList<MissingObjectReferenceFinding> missingObjectReferences)
        {
            MissingScripts = missingScripts;
            MissingObjectReferences = missingObjectReferences;
        }

        internal IReadOnlyList<MissingScriptFinding> MissingScripts { get; }

        internal IReadOnlyList<MissingObjectReferenceFinding> MissingObjectReferences { get; }

        internal bool HasFindings => MissingScripts.Count > 0 || MissingObjectReferences.Count > 0;
    }
}
