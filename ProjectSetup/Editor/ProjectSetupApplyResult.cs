// SPDX-License-Identifier: MIT

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupApplyResult
    {
        internal ProjectSetupApplyResult(bool succeeded, string message, ProjectSetupPlan plan)
        {
            Succeeded = succeeded;
            Message = message;
            Plan = plan;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
        internal ProjectSetupPlan Plan { get; }
    }
}
