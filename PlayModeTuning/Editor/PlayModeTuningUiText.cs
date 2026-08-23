using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>Defines the fixed top-to-bottom workflow used by the editor window and UI tests.</summary>
    internal static class PlayModeTuningUiText
    {
        internal const string Step1 = "\u2460 Targets";
        internal const string Step2 = "\u2461 Capture During Play";
        internal const string Step3 = "\u2462 Preview After Play";
        internal const string Step4 = "\u2463 Review and Confirm";
        internal const string Step5 = "\u2464 Apply Tuning / Result";

        internal static IReadOnlyList<string> OrderedSteps => Array.AsReadOnly(new[] { Step1, Step2, Step3, Step4, Step5 });
    }
}
