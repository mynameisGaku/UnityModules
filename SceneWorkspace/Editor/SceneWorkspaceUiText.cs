using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Defines the fixed top-to-bottom setup sequence used by the editor window and UI tests.</summary>
    internal static class SceneWorkspaceUiText
    {
        internal const string Step1 = "\u2460 Workspace Profile";
        internal const string Step2 = "\u2461 Scene Setup/Capture";
        internal const string Step3 = "\u2462 Preview Changes";
        internal const string Step4 = "\u2463 Review and Confirm";
        internal const string Step5 = "\u2464 Switch Workspace/Result";

        internal static IReadOnlyList<string> OrderedSteps => Array.AsReadOnly(new[] { Step1, Step2, Step3, Step4, Step5 });
    }
}
