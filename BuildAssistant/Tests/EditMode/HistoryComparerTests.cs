using System;
using BuildAssistant.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BuildAssistant.Tests
{
    public sealed class HistoryComparerTests
    {
        [Test]
        public void FindLatestComparable_UsesOnlyStableCompatibilityKeyAndNewestSuccess()
        {
            var oldScene = new[] { new BuildAssistantScene(0, "old", "Assets/Old.unity", true, "old-hash") };
            var newScene = new[] { new BuildAssistantScene(0, "new", "Assets/New.unity", true, "new-hash") };
            var oldMatch = BuildAssistantTestData.Entry("old-match", completedAtUtc: new DateTime(2026, 8, 23, 2, 0, 0, DateTimeKind.Utc), scenes: oldScene);
            var newestMatch = BuildAssistantTestData.Entry("newest-match", completedAtUtc: new DateTime(2026, 8, 23, 3, 0, 0, DateTimeKind.Utc), scenes: newScene);
            var failedNewer = BuildAssistantTestData.Entry("failed", BuildAssistantHistoryStatus.Failed, new DateTime(2026, 8, 23, 4, 0, 0, DateTimeKind.Utc));
            var wrongProfile = BuildAssistantTestData.Entry("profile", completedAtUtc: new DateTime(2026, 8, 23, 5, 0, 0, DateTimeKind.Utc), profileStableId: "other");
            var wrongTarget = BuildAssistantTestData.Entry("target", completedAtUtc: new DateTime(2026, 8, 23, 5, 0, 0, DateTimeKind.Utc), target: BuildTarget.StandaloneWindows);
            var wrongBackend = BuildAssistantTestData.Entry("backend", completedAtUtc: new DateTime(2026, 8, 23, 5, 0, 0, DateTimeKind.Utc), backend: ScriptingImplementation.IL2CPP);
            var wrongOptions = BuildAssistantTestData.Entry("options", completedAtUtc: new DateTime(2026, 8, 23, 5, 0, 0, DateTimeKind.Utc), options: BuildOptions.Development | BuildOptions.DetailedBuildReport);
            var snapshot = BuildAssistantTestData.Environment(scenes: oldScene);

            var result = HistoryComparer.FindLatestComparable(new[] { oldMatch, newestMatch, failedNewer, wrongProfile, wrongTarget, wrongBackend, wrongOptions }, snapshot);

            Assert.That(result.RunId, Is.EqualTo("newest-match"), "Scene identity is intentionally not part of the comparison compatibility key.");
        }

        [Test]
        public void Difference_UsesCheckedSignedArithmetic()
        {
            Assert.That(HistoryComparer.Difference(120, 100), Is.EqualTo(20));
            Assert.That(HistoryComparer.Difference(80, 100), Is.EqualTo(-20));
            Assert.Throws<OverflowException>(() => HistoryComparer.Difference(ulong.MaxValue, 0));
        }
    }
}

