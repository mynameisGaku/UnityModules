using System;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Editor
{
    internal interface IBuildReportView
    {
        BuildResult Result { get; }
        DateTime BuildStartedAt { get; }
        DateTime BuildEndedAt { get; }
        int TotalErrors { get; }
        int TotalWarnings { get; }
        ulong TotalSize { get; }
        PackedAssets[] PackedAssets { get; }
    }
}
