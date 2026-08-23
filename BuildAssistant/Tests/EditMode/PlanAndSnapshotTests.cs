using System;
using System.Collections.Generic;
using System.Text;
using BuildAssistant.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BuildAssistant.Tests
{
    public sealed class PlanAndSnapshotTests
    {
        [Test]
        public void PlanFactory_IsDeterministicForTheSameExplicitContext()
        {
            var createdAt = new DateTime(2026, 8, 23, 1, 2, 3, DateTimeKind.Utc);
            var first = BuildAssistantTestData.Plan(entropy: "A1B2C3D4", createdAtUtc: createdAt);
            var second = BuildAssistantTestData.Plan(entropy: "A1B2C3D4", createdAtUtc: createdAt);

            Assert.That(first.RunId, Is.EqualTo("BA-20260823-010203-a1b2c3d4"));
            Assert.That(second.RunId, Is.EqualTo(first.RunId));
            Assert.That(second.RunDirectory, Is.EqualTo(first.RunDirectory));
            Assert.That(second.ArtifactPath, Is.EqualTo(first.ArtifactPath));
        }

        [Test]
        public void PlanFactory_RejectsAnAlreadyPlannedRunPathWithoutMutation()
        {
            var plan = BuildAssistantTestData.Plan(runPathBusy: true);

            Assert.That(plan.IsReady, Is.False);
            Assert.That(plan.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
        }

        [Test]
        public void PlanAndSnapshot_DefensivelyCopyMutableCollections()
        {
            var extra = new List<string> { "EXTRA" };
            var effective = new List<string> { "GLOBAL", "EXTRA" };
            var scenes = new List<BuildAssistantScene> { new BuildAssistantScene(0, "guid", "Assets/Main.unity", true, "hash") };
            var profile = new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, "Platform", string.Empty, string.Empty, "platform:Standalone");
            var environment = new EnvironmentSnapshot(profile, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Standalone", (int)StandaloneBuildSubtarget.Player, ScriptingImplementation.Mono2x, BuildOptions.DetailedBuildReport, string.Empty, extra, effective, scenes);
            var plan = BuildAssistantTestData.Plan(environment);

            extra.Clear();
            effective.Clear();
            scenes.Clear();

            Assert.That(environment.ExtraScriptingDefines, Is.EqualTo(new[] { "EXTRA" }));
            Assert.That(environment.EffectiveDefines, Is.EqualTo(new[] { "GLOBAL", "EXTRA" }));
            Assert.That(environment.Scenes.Count, Is.EqualTo(1));
            Assert.That(plan.ExtraScriptingDefines, Is.EqualTo(new[] { "EXTRA" }));
            Assert.That(plan.EffectiveDefines, Is.EqualTo(new[] { "GLOBAL", "EXTRA" }));
            Assert.That(plan.Scenes.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)plan.EffectiveDefines).Add("MUTATION"));
        }

        [Test]
        public void SnapshotComparer_AcceptsAnExactRecapture()
        {
            var environment = BuildAssistantTestData.Environment();
            var plan = BuildAssistantTestData.Plan(environment);

            Assert.That(SnapshotComparer.AreEquivalent(plan, environment, out var difference), Is.True);
            Assert.That(difference, Is.Empty);
        }

        [TestCase("profile")]
        [TestCase("target")]
        [TestCase("backend")]
        [TestCase("scene")]
        public void SnapshotComparer_RejectsEveryCapturedBuildInputChange(string changedInput)
        {
            var plan = BuildAssistantTestData.Plan();
            EnvironmentSnapshot changed;
            switch (changedInput)
            {
                case "profile":
                    changed = BuildAssistantTestData.Environment(profileHash: "changed-profile-hash");
                    break;
                case "target":
                    changed = BuildAssistantTestData.Environment(target: BuildTarget.StandaloneWindows);
                    break;
                case "backend":
                    changed = BuildAssistantTestData.Environment(backend: ScriptingImplementation.IL2CPP);
                    break;
                default:
                    changed = BuildAssistantTestData.Environment(scenes: new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "changed-scene-hash") });
                    break;
            }

            Assert.That(SnapshotComparer.AreEquivalent(plan, changed, out var difference), Is.False);
            Assert.That(difference, Is.Not.Empty);
        }

        [Test]
        public void PublicHistoryDtos_DefensivelyCopyMutableCollections()
        {
            var entries = new List<BuildAssistantHistoryEntry> { BuildAssistantTestData.Entry() };
            var history = new BuildAssistantHistory(entries, false, string.Empty);

            entries.Clear();

            Assert.That(history.Entries.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<BuildAssistantHistoryEntry>)history.Entries).Add(BuildAssistantTestData.Entry("other")));
        }

        [Test]
        public void CompressionOption_ResolvesUnsetPlatformValueThroughUnityDefault()
        {
            Assert.That(UnityBuildEnvironment.ResolveCompressionOption("-1", "Lz4"), Is.EqualTo(BuildOptions.CompressWithLz4));
            Assert.That(UnityBuildEnvironment.ResolveCompressionOption("Lz4HC"), Is.EqualTo(BuildOptions.CompressWithLz4HC));
            Assert.That(UnityBuildEnvironment.ResolveCompressionOption("None"), Is.EqualTo(BuildOptions.None));
        }

        [Test]
        public void ProjectSettingsFingerprint_FramesFileNamesAndContentsWithoutStructuralAmbiguity()
        {
            var splitFiles = new[]
            {
                new KeyValuePair<string, byte[]>("A", Array.Empty<byte>()),
                new KeyValuePair<string, byte[]>("B", Encoding.UTF8.GetBytes("x"))
            };
            var mergedFile = new[]
            {
                new KeyValuePair<string, byte[]>("A", Encoding.UTF8.GetBytes("B\0x"))
            };

            Assert.That(UnityBuildEnvironment.ComputeProjectSettingsFingerprint(splitFiles), Is.Not.EqualTo(UnityBuildEnvironment.ComputeProjectSettingsFingerprint(mergedFile)));
        }

        [Test]
        public void SnapshotComparer_RejectsAPlatformProfileLibraryFingerprintChange()
        {
            var before = UnityBuildEnvironment.ComputePlatformProfilesFingerprint(new[] { new KeyValuePair<string, byte[]>("EditorUserBuildSettings.asset", Encoding.UTF8.GetBytes("before")) });
            var after = UnityBuildEnvironment.ComputePlatformProfilesFingerprint(new[] { new KeyValuePair<string, byte[]>("EditorUserBuildSettings.asset", Encoding.UTF8.GetBytes("after")) });
            var plan = BuildAssistantTestData.Plan(BuildAssistantTestData.Environment(profileHash: before));

            Assert.That(SnapshotComparer.AreEquivalent(plan, BuildAssistantTestData.Environment(profileHash: after), out var difference), Is.False);
            Assert.That(difference, Does.Contain("profile"));
        }

        [Test]
        public void SnapshotComparer_RejectsImportedRevisionOrRawStreamingAssetsChanges()
        {
            var before = UnityBuildEnvironment.ComputeProjectContentFingerprint(10, 20, new[] { new KeyValuePair<string, byte[]>("Assets/StreamingAssets/data.bin", new byte[] { 1 }) });
            var importedChange = UnityBuildEnvironment.ComputeProjectContentFingerprint(11, 20, new[] { new KeyValuePair<string, byte[]>("Assets/StreamingAssets/data.bin", new byte[] { 1 }) });
            var rawChange = UnityBuildEnvironment.ComputeProjectContentFingerprint(10, 20, new[] { new KeyValuePair<string, byte[]>("Assets/StreamingAssets/data.bin", new byte[] { 2 }) });
            var plan = BuildAssistantTestData.Plan(BuildAssistantTestData.Environment(profileHash: before));

            Assert.That(SnapshotComparer.AreEquivalent(plan, BuildAssistantTestData.Environment(profileHash: importedChange), out _), Is.False);
            Assert.That(SnapshotComparer.AreEquivalent(plan, BuildAssistantTestData.Environment(profileHash: rawChange), out _), Is.False);
        }

        [Test]
        public void CustomProfileOptions_AreReportedEffectivelyWithoutBeingRepassedAsInvocationOverrides()
        {
            var profileFlags = UnityBuildEnvironment.ComposeProfileOptions("Lz4HC", string.Empty, true, true, true, true, true, true, false, ScriptingImplementation.Mono2x);
            var gatedFlags = UnityBuildEnvironment.ComposeProfileOptions("Lz4", string.Empty, false, true, true, true, true, true, false, ScriptingImplementation.Mono2x);
            var invocationFlags = BuildOptions.DetailedBuildReport;
            var effectiveFlags = invocationFlags | profileFlags;
            var profile = new ProfileSnapshot(BuildAssistantProfileKind.Custom, "profile-guid", "Custom", "Assets/Settings/Custom.asset", "dependency", "custom:profile-guid");
            var scenes = new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "scene-hash") };
            var environment = new EnvironmentSnapshot(profile, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Standalone", (int)StandaloneBuildSubtarget.Player, ScriptingImplementation.IL2CPP, effectiveFlags, string.Empty, Array.Empty<string>(), new[] { "CUSTOM" }, scenes, invocationFlags);
            var plan = BuildAssistantTestData.Plan(environment);

            Assert.That(plan.Options, Is.EqualTo(effectiveFlags));
            Assert.That(plan.InvocationOptions, Is.EqualTo(invocationFlags));
            Assert.That((gatedFlags & BuildOptions.Development), Is.EqualTo(BuildOptions.None));
            Assert.That((gatedFlags & BuildOptions.ConnectWithProfiler), Is.EqualTo(BuildOptions.None));
            Assert.That((profileFlags & BuildOptions.EnableCodeCoverage), Is.EqualTo(BuildOptions.EnableCodeCoverage));
            Assert.DoesNotThrow(() => UnityBuildEnvironment.ValidateGlobalInstallInBuildFolder(false, true));
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateGlobalInstallInBuildFolder(true, true));
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ComposeProfileOptions("Lz4", string.Empty, false, false, false, false, false, false, true, ScriptingImplementation.Mono2x));
        }

        [Test]
        public void DevelopmentOptions_IncludeCoverageForMonoAndOmitItForIl2Cpp()
        {
            var mono = UnityBuildEnvironment.ComposeDevelopmentOptions(true, false, false, false, true, false, ScriptingImplementation.Mono2x);
            var il2Cpp = UnityBuildEnvironment.ComposeDevelopmentOptions(true, false, false, false, true, false, ScriptingImplementation.IL2CPP);

            Assert.That((mono & BuildOptions.EnableCodeCoverage), Is.EqualTo(BuildOptions.EnableCodeCoverage));
            Assert.That((il2Cpp & BuildOptions.EnableCodeCoverage), Is.EqualTo(BuildOptions.None));
        }

        [Test]
        public void TargetValidation_RejectsServerBeforeAnyBuildInvocation()
        {
            var exception = Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateTarget(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, StandaloneBuildSubtarget.Server));

            Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.UnsupportedBuildTarget));
        }

        [Test]
        public void TargetValidation_RejectsWindows32BitBeforeAnyBuildInvocation()
        {
            var exception = Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateTarget(BuildTarget.StandaloneWindows, BuildTargetGroup.Standalone, StandaloneBuildSubtarget.Player));

            Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.UnsupportedBuildTarget));
        }

        [Test]
        public void TargetValidation_RejectsAnUnavailablePlaybackModuleBeforePlanning()
        {
            var exception = Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateBuildTargetSupport(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, false));

            Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.UnsupportedBuildTarget));
        }

        [Test]
        public void BuildAuthority_UsesTheCustomProfileTargetInsteadOfDivergentGlobals()
        {
            UnityBuildEnvironment.ResolveBuildAuthority(true, BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, BuildTarget.StandaloneOSX, StandaloneBuildSubtarget.Default, out var target, out var subtarget);

            Assert.That(target, Is.EqualTo(BuildTarget.StandaloneOSX));
            Assert.That(subtarget, Is.EqualTo(StandaloneBuildSubtarget.Default));
        }

        [Test]
        public void CustomProfileIdentity_RejectsUnsavedOrUnhashedProfilesWithoutMutation()
        {
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateCustomProfileIdentity(string.Empty, string.Empty, string.Empty));
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateCustomProfileIdentity("Assets/Profile.asset", new string('0', 32), new string('0', 32)));
            Assert.DoesNotThrow(() => UnityBuildEnvironment.ValidateCustomProfileIdentity("Assets/Profile.asset", "1234567890abcdef1234567890abcdef", "abcdef1234567890abcdef1234567890"));
        }

        [Test]
        public void ProfilerConnectionValidation_RejectsConnectToHostBeforePlanning()
        {
            Assert.DoesNotThrow(() => UnityBuildEnvironment.ValidateProfilerConnectionId(string.Empty));
            var exception = Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateProfilerConnectionId("ProfilerHost"));

            Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.UnsupportedBuildTarget));
        }

        [Test]
        public void EnabledSceneValidation_RejectsMissingOrNonSceneAssetsBeforePlanning()
        {
            const string guid = "1234567890abcdef1234567890abcdef";
            const string hash = "abcdef1234567890abcdef1234567890";

            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateEnabledScene(string.Empty, string.Empty, string.Empty, false));
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateEnabledScene("Assets/Data.asset", guid, hash, false));
            Assert.Throws<EnvironmentCaptureException>(() => UnityBuildEnvironment.ValidateEnabledScene("Packages/com.example/../Scenes/Main.unity", guid, hash, true));
            Assert.DoesNotThrow(() => UnityBuildEnvironment.ValidateEnabledScene("Assets/Scenes/Main.unity", guid, hash, true));
            Assert.DoesNotThrow(() => UnityBuildEnvironment.ValidateEnabledScene("Packages/com.example/Scenes/Main.unity", guid, hash, true));
        }
    }
}
