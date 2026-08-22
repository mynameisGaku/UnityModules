// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupPlan
    {
        internal ProjectSetupPlan(IReadOnlyList<ProjectSetupChange> changes, IReadOnlyList<string> errors)
        {
            Changes = changes;
            Errors = errors;
        }

        internal IReadOnlyList<ProjectSetupChange> Changes { get; }
        internal IReadOnlyList<string> Errors { get; }
        internal bool IsValid => Errors.Count == 0;
        internal bool HasChanges => Changes.Count > 0;
    }
}
