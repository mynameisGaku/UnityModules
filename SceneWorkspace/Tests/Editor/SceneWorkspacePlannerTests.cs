using System;
using System.Linq;
using NUnit.Framework;
using SceneWorkspace.Editor;

namespace SceneWorkspace.Editor.Tests
{
    [TestFixture]
    internal sealed class SceneWorkspacePlannerTests
    {
        [Test]
        public void IdenticalSetupProducesOnlyKeepRows()
        {
            var scenes = new[]
            {
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, false, false)
            };

            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(scenes), SceneWorkspaceTestData.Profile(scenes), 10L);

            Assert.That(plan.IsReady, Is.True);
            Assert.That(plan.HasChanges, Is.False);
            Assert.That(plan.Changes.Select(change => change.Kind), Is.All.EqualTo(SceneWorkspaceChangeKind.Keep));
        }

        [Test]
        public void ChangeRowsHaveDeterministicCloseThenTargetOrder()
        {
            var current = SceneWorkspaceTestData.Current(
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Gameplay", 1, false, false));
            var target = SceneWorkspaceTestData.Profile(
                SceneWorkspaceTestData.Scene("Gameplay", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, false, false));

            var plan = SceneWorkspacePlanner.Create(current, target, 11L);

            Assert.That(plan.Changes.Select(change => change.Kind), Is.EqualTo(new[]
            {
                SceneWorkspaceChangeKind.Close,
                SceneWorkspaceChangeKind.Load,
                SceneWorkspaceChangeKind.Reorder,
                SceneWorkspaceChangeKind.SetActive,
                SceneWorkspaceChangeKind.Open
            }));
            Assert.That(plan.Changes.Select(change => change.Path), Is.EqualTo(new[]
            {
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Gameplay.unity",
                "Assets/Scenes/Gameplay.unity",
                "Assets/Scenes/Gameplay.unity",
                "Assets/Scenes/Lighting.unity"
            }));
        }

        [Test]
        public void ActiveSceneTransferReportsClearAndSetWithoutKeepRows()
        {
            var current = SceneWorkspaceTestData.Current(
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, true, false));
            var target = SceneWorkspaceTestData.Profile(
                SceneWorkspaceTestData.Scene("Main", 0, true, false),
                SceneWorkspaceTestData.Scene("Lighting", 1, true, true));

            var plan = SceneWorkspacePlanner.Create(current, target, 12L);

            Assert.That(plan.IsReady, Is.True);
            Assert.That(plan.Changes.Select(change => change.Kind), Is.EqualTo(new[]
            {
                SceneWorkspaceChangeKind.ClearActive,
                SceneWorkspaceChangeKind.SetActive
            }));
            Assert.That(plan.Changes, Has.None.Matches<SceneWorkspaceChange>(change => change.Kind == SceneWorkspaceChangeKind.Keep));
        }

        [TestCase(SceneWorkspaceError.PlayModeActive)]
        [TestCase(SceneWorkspaceError.EditorBusy)]
        [TestCase(SceneWorkspaceError.PrefabStageOpen)]
        public void EditorGuardsRejectBeforePlanning(SceneWorkspaceError expected)
        {
            var current = expected == SceneWorkspaceError.PlayModeActive
                ? SceneWorkspaceTestData.WithFlags(play: true)
                : expected == SceneWorkspaceError.PrefabStageOpen
                    ? SceneWorkspaceTestData.WithFlags(prefab: true)
                    : SceneWorkspaceTestData.WithFlags(compiling: true);

            var plan = SceneWorkspacePlanner.Create(current, SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, true, true)), 13L);

            Assert.That(plan.Error, Is.EqualTo(expected));
            Assert.That(plan.Generation, Is.Zero);
        }

        [Test]
        public void UpdatingRejectsAsEditorBusy()
        {
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.WithFlags(updating: true), SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, true, true)), 14L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.EditorBusy));
        }

        [Test]
        public void DirtyCurrentSceneIsRejected()
        {
            var current = SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true, dirty: true));
            var plan = SceneWorkspacePlanner.Create(current, SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, true, true)), 14L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.DirtyScene));
        }

        [Test]
        public void UntitledCurrentSceneIsRejected()
        {
            var scene = new SceneWorkspaceSceneState(0, string.Empty, string.Empty, true, true, true, false);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(scene), SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, true, true)), 15L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.UntitledScene));
        }

        [Test]
        public void MissingProfileSceneIsRejected()
        {
            var missing = SceneWorkspaceTestData.Scene("Missing", 0, true, true, exists: false);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true)), SceneWorkspaceTestData.Profile(missing), 16L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.MissingScene));
        }

        [Test]
        public void DuplicateSceneIsRejected()
        {
            var first = SceneWorkspaceTestData.Scene("Main", 0, true, true);
            var duplicate = SceneWorkspaceTestData.Scene("Main", 1, false, false);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(first), SceneWorkspaceTestData.Profile(first, duplicate), 17L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.DuplicateScene));
        }

        [Test]
        public void UnloadedActiveSceneIsRejected()
        {
            var invalid = SceneWorkspaceTestData.Scene("Main", 0, false, true);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true)), SceneWorkspaceTestData.Profile(invalid), 18L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.InvalidActiveScene));
        }

        [Test]
        public void SetupWithoutLoadedSceneIsRejected()
        {
            var invalid = SceneWorkspaceTestData.Scene("Main", 0, false, false);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true)), SceneWorkspaceTestData.Profile(invalid), 19L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.NoLoadedScene));
        }

        [Test]
        public void SetupWithoutExactlyOneActiveSceneIsRejected()
        {
            var invalid = SceneWorkspaceTestData.Scene("Main", 0, true, false);
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true)), SceneWorkspaceTestData.Profile(invalid), 20L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.InvalidActiveScene));
        }

        [Test]
        public void UnsupportedScenePathIsRejected()
        {
            var invalid = SceneWorkspaceTestData.Scene("Main", 0, true, true, path: "Packages/Scenes/Main.unity");
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true)), SceneWorkspaceTestData.Profile(invalid), 21L);
            Assert.That(plan.Error, Is.EqualTo(SceneWorkspaceError.UnsupportedScenePath));
        }

        [Test]
        public void FingerprintsChangeWithOrderLoadActiveAndProfileIdentity()
        {
            var first = SceneWorkspaceTestData.Profile(
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, false, false));
            var second = new SceneWorkspaceProfileSnapshot(true, "profile-guid-2", first.Path, first.Name, new[]
            {
                SceneWorkspaceTestData.Scene("Lighting", 0, true, true),
                SceneWorkspaceTestData.Scene("Main", 1, false, false)
            });

            Assert.That(SceneWorkspaceFingerprint.ComputeProfile(first), Is.Not.EqualTo(SceneWorkspaceFingerprint.ComputeProfile(second)));
        }

        [Test]
        public void PublicCollectionsRejectCallerMutation()
        {
            var scenes = new[] { SceneWorkspaceTestData.Scene("Main", 0, true, true) };
            var plan = SceneWorkspacePlanner.Create(SceneWorkspaceTestData.Current(scenes), SceneWorkspaceTestData.Profile(scenes), 22L);

            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<SceneWorkspaceSceneState>)plan.TargetScenes).Add(scenes[0]));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<SceneWorkspaceChange>)plan.Changes).Clear());
        }
    }
}
