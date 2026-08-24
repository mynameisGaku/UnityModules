// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies all-or-nothing review snapshots, formatting and safe refreshed navigation.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabOverrideReviewServiceTests
    {
        private BuildGuardPrefabOverrideTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new BuildGuardPrefabOverrideTestFixture();
            _fixture.SetUp();
            Selection.activeObject = null;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            _fixture?.TearDown();
        }

        [Test]
        public void Scan_AggregatesSortsAndCapsDetachedSnapshot()
        {
            var prefabPath = _fixture.CreatePrefab("ReviewCap.prefab");
            var zuluScene = _fixture.CreateSavedScene("Zulu.unity");
            _fixture.InstantiatePrefab(prefabPath, zuluScene).AddComponent<SphereCollider>();
            var alphaScene = _fixture.CreateSavedScene("Alpha.unity");
            _fixture.InstantiatePrefab(prefabPath, alphaScene).AddComponent<BoxCollider>();
            var activeScene = SceneManager.GetActiveScene();
            var sceneCount = SceneManager.sceneCount;
            var zuluWasDirty = zuluScene.isDirty;
            var alphaWasDirty = alphaScene.isDirty;

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                new[] { zuluScene.path, alphaScene.path },
                1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.ScannedSceneCount, Is.EqualTo(2));
            Assert.That(result.TotalFindingCount, Is.EqualTo(2));
            Assert.That(result.WasTruncated, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            Assert.That(result.Findings[0].ScenePath, Is.EqualTo(alphaScene.path));
            Assert.That(result.Findings[0].ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(zuluScene.isDirty, Is.EqualTo(zuluWasDirty));
            Assert.That(alphaScene.isDirty, Is.EqualTo(alphaWasDirty));
        }

        [Test]
        public void Scan_CancelledAfterPartialVisit_DiscardsAllFindings()
        {
            var prefabPath = _fixture.CreatePrefab("ReviewCancel.prefab");
            var firstScene = _fixture.CreateSavedScene("First.unity");
            _fixture.InstantiatePrefab(prefabPath, firstScene).AddComponent<BoxCollider>();
            var secondScene = _fixture.CreateSavedScene("Second.unity");
            _fixture.InstantiatePrefab(prefabPath, secondScene).AddComponent<SphereCollider>();

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                new[] { firstScene.path, secondScene.path },
                1000,
                (index, _, _) => index == 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.Failures, Is.Empty);
            Assert.That(result.TotalFindingCount, Is.Zero);
        }

        [Test]
        public void Scan_LaterSceneFailure_DiscardsEarlierFindings()
        {
            var validPrefabPath = _fixture.CreatePrefab("ReviewValid.prefab");
            var firstScene = _fixture.CreateSavedScene("ReviewValid.unity");
            _fixture.InstantiatePrefab(validPrefabPath, firstScene).AddComponent<BoxCollider>();

            var deletedPrefabPath = _fixture.CreatePrefab("ReviewDeleted.prefab");
            var secondScene = _fixture.CreateSavedScene("ReviewDeleted.unity");
            _fixture.InstantiatePrefab(deletedPrefabPath, secondScene);
            Assert.That(EditorSceneManager.SaveScene(secondScene), Is.True);
            Assert.That(AssetDatabase.DeleteAsset(deletedPrefabPath), Is.True);
            var stableState = CreateSceneState(
                11,
                "Assets/Host.unity",
                false,
                22,
                "Assets/Secondary.unity",
                false,
                11,
                "Assets/Host.unity");
            var captureCount = 0;

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                new[] { firstScene.path, secondScene.path },
                1000,
                null,
                () =>
                {
                    captureCount++;
                    return stableState;
                });

            Assert.That(captureCount, Is.EqualTo(2));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].ScenePath, Is.EqualTo(secondScene.path));
            Assert.That(
                result.Failures[0].Error,
                Is.EqualTo(BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus)
                    .Or.EqualTo(BuildGuardPrefabOverrideScanError.MissingPrefabSource));
        }

        [TestCase(SceneStateMismatch.ExtraLoadedScene, "Loaded Scene count changed")]
        [TestCase(SceneStateMismatch.ActiveScene, "Active Scene changed")]
        [TestCase(SceneStateMismatch.DirtyScene, "dirty state changed")]
        [TestCase(SceneStateMismatch.ScenePath, "Loaded Scene path changed")]
        public void Scan_InjectedSceneStateMismatch_FailsAndDiscardsSuccessfulFindings(
            SceneStateMismatch mismatch,
            string expectedMessage)
        {
            var prefabPath = _fixture.CreatePrefab("StateMismatch.prefab");
            var scene = _fixture.CreateSavedScene("StateMismatch.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            var expectedState = CreateSceneState(
                11,
                "Assets/Host.unity",
                false,
                22,
                "Assets/Secondary.unity",
                false,
                11,
                "Assets/Host.unity");
            var currentState = CreateMismatchedSceneState(expectedState, mismatch);
            var captureCount = 0;

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                new[] { scene.path },
                1000,
                null,
                () => captureCount++ == 0 ? expectedState : currentState);

            Assert.That(captureCount, Is.EqualTo(2));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.ScannedSceneCount, Is.EqualTo(1));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].Message, Does.Contain(expectedMessage));
        }

        [Test]
        public void SceneStateValidator_ExactStateSucceedsAndHandleOrderMismatchFails()
        {
            var expected = CreateSceneState(
                11,
                "Assets/First.unity",
                false,
                22,
                "Assets/Second.unity",
                true,
                11,
                "Assets/First.unity");
            var reordered = new BuildGuardPrefabOverrideReviewSceneState(
                new[]
                {
                    expected.Scenes[1],
                    expected.Scenes[0],
                },
                expected.ActiveSceneHandle,
                expected.ActiveScenePath);

            Assert.That(
                BuildGuardPrefabOverrideReviewSceneState.TryValidate(
                    expected,
                    expected,
                    out var successMessage),
                Is.True);
            Assert.That(successMessage, Is.Empty);
            Assert.That(
                BuildGuardPrefabOverrideReviewSceneState.TryValidate(
                    expected,
                    reordered,
                    out var failureMessage),
                Is.False);
            Assert.That(failureMessage, Does.Contain("handle or order changed"));
        }

        [Test]
        public void Scan_CancelledVisitWithStateMismatch_BecomesExplicitFailure()
        {
            var scene = _fixture.CreateSavedScene("CancelledStateMismatch.unity");
            var expectedState = CreateSceneState(
                11,
                "Assets/Host.unity",
                false,
                22,
                "Assets/Secondary.unity",
                false,
                11,
                "Assets/Host.unity");
            var currentState = CreateMismatchedSceneState(
                expectedState,
                SceneStateMismatch.ActiveScene);
            var captureCount = 0;

            var result = BuildGuardPrefabOverrideReviewService.Scan(
                new[] { scene.path },
                1000,
                (_, _, _) => true,
                () => captureCount++ == 0 ? expectedState : currentState);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].Message, Does.Contain("Active Scene changed"));
        }

        [Test]
        public void ResultFactories_DetachSourceCollectionsAndDiscardPartialFailures()
        {
            var findingSource = new List<BuildGuardPrefabOverrideFinding>
            {
                CreateFinding(BuildGuardPrefabOverrideKind.AddedComponent, "Target[0]", string.Empty, 1),
            };
            var success = BuildGuardPrefabOverrideReviewScanResult.Success(findingSource, 1, 1);
            findingSource.Clear();

            var failureSource = new List<BuildGuardPrefabOverrideReviewFailure>
            {
                new BuildGuardPrefabOverrideReviewFailure(
                    "Assets/Broken.unity",
                    BuildGuardPrefabOverrideScanError.UnityApiFailure,
                    "failure"),
            };
            var failure = BuildGuardPrefabOverrideReviewScanResult.Failure(failureSource, 0);
            failureSource.Clear();

            Assert.That(success.Findings, Has.Count.EqualTo(1));
            Assert.That(success.Failures, Is.Empty);
            Assert.That(failure.Findings, Is.Empty);
            Assert.That(failure.Failures, Has.Count.EqualTo(1));
        }

        [Test]
        public void MatchesSnapshot_RequiresEveryIdentityField()
        {
            var snapshot = CreateFinding(
                BuildGuardPrefabOverrideKind.RemovedComponent,
                "Root[0]/Target[0]",
                "Root[0]/Target[0]",
                2);
            var changedIndex = WithComponentIndex(snapshot, snapshot.ComponentIndex + 1);

            Assert.That(BuildGuardPrefabOverrideReviewService.MatchesSnapshot(snapshot, snapshot), Is.True);
            Assert.That(BuildGuardPrefabOverrideReviewService.MatchesSnapshot(snapshot, changedIndex), Is.False);
        }

        [TestCase(BuildGuardPrefabOverrideKind.AddedGameObject, "Added GameObject")]
        [TestCase(BuildGuardPrefabOverrideKind.RemovedGameObject, "Removed GameObject")]
        [TestCase(BuildGuardPrefabOverrideKind.AddedComponent, "Added Component")]
        [TestCase(BuildGuardPrefabOverrideKind.RemovedComponent, "Removed Component")]
        public void Presentation_FormatsEverySupportedKind(
            BuildGuardPrefabOverrideKind kind,
            string expected)
        {
            Assert.That(BuildGuardPrefabOverrideReviewPresentation.FormatKind(kind), Is.EqualTo(expected));
        }

        [Test]
        public void Presentation_FormatsStableComponentSourceAndClipboardFields()
        {
            var finding = CreateFinding(
                BuildGuardPrefabOverrideKind.RemovedComponent,
                "Root[0]/Target[0]",
                "Root[0]/Source[0]",
                2);

            Assert.That(
                BuildGuardPrefabOverrideReviewPresentation.FormatComponent(finding),
                Is.EqualTo("UnityEngine.BoxCollider[2]"));
            Assert.That(
                BuildGuardPrefabOverrideReviewPresentation.FormatSource(finding),
                Is.EqualTo("Assets/Inner.prefab :: Root[0]/Source[0]"));
            Assert.That(
                BuildGuardPrefabOverrideReviewPresentation.FormatClipboardText(finding),
                Is.EqualTo(
                    "Removed Component | Assets/Scene.unity | Root[0]/Target[0] | "
                    + "UnityEngine.BoxCollider[2] | Assets/Inner.prefab :: Root[0]/Source[0]"));
        }

        [Test]
        public void Locate_CurrentLoadedScene_SelectsTargetWithoutChangingDirtyState()
        {
            var hostScene = SceneManager.GetActiveScene();
            var prefabPath = _fixture.CreatePrefab("LocateLoaded.prefab");
            var scene = _fixture.CreateSavedScene("LocateLoaded.unity");
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            instance.AddComponent<BoxCollider>();
            var snapshot = BuildGuardPrefabOverrideSceneScanner.Scan(scene).Findings[0];
            Assert.That(SceneManager.SetActiveScene(hostScene), Is.True);
            var sceneCount = SceneManager.sceneCount;
            var wasDirty = scene.isDirty;

            var outcome = BuildGuardPrefabOverrideReviewService.Locate(snapshot, out var message);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.SelectedSceneObject));
            Assert.That(message, Does.Contain("Selected current override target"));
            Assert.That(Selection.activeGameObject, Is.EqualTo(instance));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(hostScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void Locate_CurrentClosedScene_ClosesTemporarySceneAndPingsAsset()
        {
            var hostScene = SceneManager.GetActiveScene();
            var prefabPath = _fixture.CreatePrefab("LocateClosed.prefab");
            var scene = _fixture.CreateSavedScene("LocateClosed.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            var snapshot = BuildGuardPrefabOverrideSceneScanner.Scan(scene).Findings[0];
            Assert.That(SceneManager.SetActiveScene(hostScene), Is.True);
            Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
            var sceneCount = SceneManager.sceneCount;

            var outcome = BuildGuardPrefabOverrideReviewService.Locate(snapshot, out var message);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.PingedSceneAsset));
            Assert.That(message, Does.Contain("Scene was kept closed"));
            Assert.That(SceneManager.GetSceneByPath(snapshot.ScenePath).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(hostScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(
                Selection.activeObject,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.ScenePath)));
        }

        [Test]
        public void Locate_StaleClosedFinding_ClosesTemporarySceneAndPreservesSelection()
        {
            var hostScene = SceneManager.GetActiveScene();
            var prefabPath = _fixture.CreatePrefab("LocateClosedStale.prefab");
            var scene = _fixture.CreateSavedScene("LocateClosedStale.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            var current = BuildGuardPrefabOverrideSceneScanner.Scan(scene).Findings[0];
            var staleSnapshot = WithComponentIndex(current, current.ComponentIndex + 1);
            var sentinel = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = sentinel;
            Assert.That(SceneManager.SetActiveScene(hostScene), Is.True);
            Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
            var sceneCount = SceneManager.sceneCount;

            var outcome = BuildGuardPrefabOverrideReviewService.Locate(
                staleSnapshot,
                out var message);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.Stale));
            Assert.That(message, Does.Contain("stale"));
            Assert.That(SceneManager.GetSceneByPath(staleSnapshot.ScenePath).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(hostScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(Selection.activeObject, Is.EqualTo(sentinel));
        }

        [Test]
        public void Locate_ChangedLoadedOverride_ReportsStaleWithoutChangingSceneState()
        {
            var prefabPath = _fixture.CreatePrefab("LocateStale.prefab");
            var scene = _fixture.CreateSavedScene("LocateStale.unity");
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            var addedComponent = instance.AddComponent<BoxCollider>();
            var snapshot = BuildGuardPrefabOverrideSceneScanner.Scan(scene).Findings[0];
            var sentinel = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = sentinel;
            UnityEngine.Object.DestroyImmediate(addedComponent);
            var activeScene = SceneManager.GetActiveScene();
            var sceneCount = SceneManager.sceneCount;
            var wasDirty = scene.isDirty;

            var outcome = BuildGuardPrefabOverrideReviewService.Locate(snapshot, out var message);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.Stale));
            Assert.That(message, Does.Contain("stale"));
            Assert.That(Selection.activeObject, Is.EqualTo(sentinel));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        private static BuildGuardPrefabOverrideFinding CreateFinding(
            BuildGuardPrefabOverrideKind kind,
            string targetPath,
            string sourcePath,
            int componentIndex)
        {
            return new BuildGuardPrefabOverrideFinding(
                kind,
                "Assets/Scene.unity",
                "scene-guid",
                "Assets/Outer.prefab",
                "outer-guid",
                PrefabAssetType.Regular,
                "Assets/Inner.prefab",
                PrefabAssetType.Regular,
                true,
                "Root[0]",
                targetPath,
                sourcePath,
                kind == BuildGuardPrefabOverrideKind.AddedGameObject
                    || kind == BuildGuardPrefabOverrideKind.RemovedGameObject
                    ? string.Empty
                    : "UnityEngine.BoxCollider",
                componentIndex,
                "root-id",
                "target-id",
                "source-id");
        }

        private static BuildGuardPrefabOverrideFinding WithComponentIndex(
            BuildGuardPrefabOverrideFinding source,
            int componentIndex)
        {
            return new BuildGuardPrefabOverrideFinding(
                source.Kind,
                source.ScenePath,
                source.SceneGuid,
                source.PrefabAssetPath,
                source.PrefabAssetGuid,
                source.PrefabAssetType,
                source.NearestPrefabAssetPath,
                source.NearestPrefabAssetType,
                source.IsNestedPrefabObject,
                source.InstanceRootHierarchyPath,
                source.TargetHierarchyPath,
                source.SourceObjectPath,
                source.ComponentTypeName,
                componentIndex,
                source.InstanceRootGlobalObjectId,
                source.NavigationTargetGlobalObjectId,
                source.SourceObjectGlobalObjectId);
        }

        private static BuildGuardPrefabOverrideReviewSceneState CreateSceneState(
            ulong firstHandle,
            string firstPath,
            bool firstDirty,
            ulong secondHandle,
            string secondPath,
            bool secondDirty,
            ulong activeHandle,
            string activePath)
        {
            return new BuildGuardPrefabOverrideReviewSceneState(
                new[]
                {
                    new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                        firstHandle,
                        firstPath,
                        firstDirty),
                    new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                        secondHandle,
                        secondPath,
                        secondDirty),
                },
                activeHandle,
                activePath);
        }

        private static BuildGuardPrefabOverrideReviewSceneState CreateMismatchedSceneState(
            BuildGuardPrefabOverrideReviewSceneState expected,
            SceneStateMismatch mismatch)
        {
            switch (mismatch)
            {
                case SceneStateMismatch.ExtraLoadedScene:
                    return new BuildGuardPrefabOverrideReviewSceneState(
                        new[]
                        {
                            expected.Scenes[0],
                            expected.Scenes[1],
                            new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                                33,
                                "Assets/Unexpected.unity",
                                false),
                        },
                        expected.ActiveSceneHandle,
                        expected.ActiveScenePath);
                case SceneStateMismatch.ActiveScene:
                    return new BuildGuardPrefabOverrideReviewSceneState(
                        expected.Scenes,
                        expected.Scenes[1].Handle,
                        expected.Scenes[1].Path);
                case SceneStateMismatch.DirtyScene:
                    return new BuildGuardPrefabOverrideReviewSceneState(
                        new[]
                        {
                            new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                                expected.Scenes[0].Handle,
                                expected.Scenes[0].Path,
                                !expected.Scenes[0].IsDirty),
                            expected.Scenes[1],
                        },
                        expected.ActiveSceneHandle,
                        expected.ActiveScenePath);
                case SceneStateMismatch.ScenePath:
                    return new BuildGuardPrefabOverrideReviewSceneState(
                        new[]
                        {
                            new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                                expected.Scenes[0].Handle,
                                "Assets/Renamed.unity",
                                expected.Scenes[0].IsDirty),
                            expected.Scenes[1],
                        },
                        expected.ActiveSceneHandle,
                        expected.ActiveScenePath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mismatch));
            }
        }

        public enum SceneStateMismatch
        {
            ExtraLoadedScene = 0,
            ActiveScene = 1,
            DirtyScene = 2,
            ScenePath = 3
        }
    }
}
