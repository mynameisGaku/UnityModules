using System;
using System.Collections.Generic;
using System.IO;
using BuildAssistant.Editor;
using UnityEditor;

namespace BuildAssistant.Tests
{
    internal static class BuildAssistantTestData
    {
        internal static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Path.GetPathRoot(System.Environment.CurrentDirectory), "BuildAssistantTestProject"));
        internal static readonly string OutputRoot = Path.GetFullPath(Path.Combine(Path.GetPathRoot(System.Environment.CurrentDirectory), "BuildAssistantTestOutput"));

        internal static EnvironmentSnapshot Environment(string profileHash = "profile-hash", BuildTarget target = BuildTarget.StandaloneWindows64, ScriptingImplementation backend = ScriptingImplementation.Mono2x, IEnumerable<BuildAssistantScene> scenes = null, BuildOptions options = BuildOptions.DetailedBuildReport, string profileStableId = "platform:Standalone")
        {
            var profile = new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, "Standalone Platform", string.Empty, profileHash, profileStableId);
            var sceneList = scenes ?? new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "scene-hash") };
            return new EnvironmentSnapshot(profile, target, BuildTargetGroup.Standalone, "Standalone", (int)StandaloneBuildSubtarget.Player, backend, options, string.Empty, new[] { "EXTRA" }, new[] { "GLOBAL", "EXTRA" }, sceneList);
        }

        internal static BuildAssistantPlan Plan(EnvironmentSnapshot environment = null, string entropy = "1234abcd", DateTime? createdAtUtc = null, bool runPathBusy = false, BuildAssistantHistoryEntry previous = null)
        {
            var context = new PlanningContext(environment ?? Environment(), OutputRoot, OutputRootMode.ExistingDirectory, createdAtUtc ?? new DateTime(2026, 8, 23, 1, 2, 3, DateTimeKind.Utc), entropy, runPathBusy, previous);
            return PlanFactory.Create(context);
        }

        internal static BuildAssistantHistoryEntry Entry(string runId = "BA-20260823-010203-1234abcd", BuildAssistantHistoryStatus status = BuildAssistantHistoryStatus.Succeeded, DateTime? completedAtUtc = null, ulong totalBytes = 100, ulong packedBytes = 80, string profileStableId = "platform:Standalone", BuildTarget target = BuildTarget.StandaloneWindows64, int subtarget = (int)StandaloneBuildSubtarget.Player, ScriptingImplementation backend = ScriptingImplementation.Mono2x, BuildOptions options = BuildOptions.DetailedBuildReport, IEnumerable<BuildAssistantScene> scenes = null)
        {
            var created = new DateTime(2026, 8, 23, 1, 2, 3, DateTimeKind.Utc);
            var completed = completedAtUtc ?? created.AddMinutes(1);
            return new BuildAssistantHistoryEntry(runId, created, created.AddSeconds(1), completed, status, status == BuildAssistantHistoryStatus.Succeeded ? BuildAssistantError.None : BuildAssistantError.BuildInvocationFailed, string.Empty, OutputRoot, Path.Combine(OutputRoot, runId), Path.Combine(OutputRoot, runId, "Player.exe"), BuildAssistantProfileKind.Platform, string.Empty, "Standalone Platform", string.Empty, "profile-hash", profileStableId, target, BuildTargetGroup.Standalone, "Standalone", subtarget, backend, options, new[] { "GLOBAL" }, scenes ?? new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "scene-hash") }, 0, 0, totalBytes, packedBytes, 5, new[] { new BuildAssistantAssetSize("Assets/A.asset", packedBytes, 1) }, new[] { new BuildAssistantTypeSize("Type.A", packedBytes, 1, 1) }, string.Empty, 0, 0);
        }
    }
}
