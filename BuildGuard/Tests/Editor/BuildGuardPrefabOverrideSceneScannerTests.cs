// SPDX-License-Identifier: MIT

using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 一時作成したUnityプレハブとシーンを使い、変更しない構造差分検査を検証します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabOverrideSceneScannerTests
    {
        private BuildGuardPrefabOverrideTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new BuildGuardPrefabOverrideTestFixture();
            _fixture.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture?.TearDown();
        }

        [Test]
        public void Scan_ValidSceneWithoutPrefabs_ReturnsEmptySuccess()
        {
            var scene = _fixture.CreateSavedScene();
            new GameObject("Plain Root");

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.VisitedGameObjectCount, Is.EqualTo(1));
            Assert.That(result.ScannedPrefabInstanceCount, Is.Zero);
        }

        [Test]
        public void Scan_PropertyAndTransformOverrides_AreExcluded()
        {
            var prefabPath = _fixture.CreatePrefab(
                "PropertyOnly.prefab",
                root => root.AddComponent<BoxCollider>());
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            var collider = instance.GetComponent<BoxCollider>();
            collider.center = new Vector3(1f, 2f, 3f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            instance.transform.localPosition = new Vector3(4f, 5f, 6f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Is.Empty);
        }

        [Test]
        public void Scan_AddedComponent_ReturnsComponentLocationAndSourceStatus()
        {
            var prefabPath = _fixture.CreatePrefab("AddedComponent.prefab");
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            instance.AddComponent<BoxCollider>();

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            var finding = result.Findings[0];
            Assert.That(finding.Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.AddedComponent));
            Assert.That(finding.TargetHierarchyPath, Is.EqualTo("AddedComponent[0]"));
            Assert.That(finding.ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
            Assert.That(finding.ComponentIndex, Is.EqualTo(1));
            Assert.That(finding.PrefabAssetPath, Is.EqualTo(prefabPath));
            Assert.That(finding.PrefabAssetType, Is.EqualTo(PrefabAssetType.Regular));
            Assert.That(finding.NavigationTargetGlobalObjectId, Is.Not.Empty);
        }

        [Test]
        public void Scan_RemovedComponent_ReturnsSourceComponentLocation()
        {
            var prefabPath = _fixture.CreatePrefab(
                "RemovedComponent.prefab",
                root => root.AddComponent<BoxCollider>());
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            Object.DestroyImmediate(instance.GetComponent<BoxCollider>());

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            var finding = result.Findings[0];
            Assert.That(finding.Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedComponent));
            Assert.That(finding.TargetHierarchyPath, Is.EqualTo("RemovedComponent[0]"));
            Assert.That(finding.SourceObjectPath, Is.EqualTo("RemovedComponent[0]"));
            Assert.That(finding.ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
            Assert.That(finding.ComponentIndex, Is.EqualTo(1));
            Assert.That(finding.SourceObjectGlobalObjectId, Is.Not.Empty);
        }

        [Test]
        public void Scan_AddedGameObjectSubtree_SuppressesItsComponentFindings()
        {
            var prefabPath = _fixture.CreatePrefab("AddedSubtree.prefab");
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            var addedRoot = new GameObject("Added Root", typeof(BoxCollider));
            addedRoot.transform.SetParent(instance.transform, false);
            var addedChild = new GameObject("Added Child", typeof(SphereCollider));
            addedChild.transform.SetParent(addedRoot.transform, false);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            Assert.That(result.Findings[0].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.AddedGameObject));
            Assert.That(result.Findings[0].TargetHierarchyPath, Is.EqualTo("AddedSubtree[0]/Added Root[0]"));
            Assert.That(result.Findings[0].ComponentTypeName, Is.Empty);
            Assert.That(result.Findings[0].ComponentIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Scan_RemovedGameObjectSubtree_SuppressesItsComponentFindings()
        {
            var prefabPath = _fixture.CreatePrefab(
                "RemovedSubtree.prefab",
                root =>
                {
                    var child = new GameObject("Removed Root", typeof(BoxCollider));
                    child.transform.SetParent(root.transform, false);
                    var grandchild = new GameObject("Removed Child", typeof(SphereCollider));
                    grandchild.transform.SetParent(child.transform, false);
                });
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            Object.DestroyImmediate(instance.transform.GetChild(0).gameObject);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            Assert.That(result.Findings[0].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedGameObject));
            Assert.That(result.Findings[0].TargetHierarchyPath, Is.EqualTo("RemovedSubtree[0]"));
            Assert.That(result.Findings[0].SourceObjectPath, Does.EndWith("/Removed Root[0]"));
        }

        [Test]
        public void Scan_NestedPrefab_IgnoresAuthoredStateAndReportsSceneDeltaOnce()
        {
            var innerPath = _fixture.CreatePrefab("Inner.prefab");
            var outerPath = _fixture.CreateNestedPrefab(innerPath, "Outer.prefab");
            var scene = _fixture.CreateSavedScene();
            var outerInstance = _fixture.InstantiatePrefab(outerPath, scene);

            var baseline = BuildGuardPrefabOverrideSceneScanner.Scan(scene);
            Assert.That(baseline.Succeeded, Is.True);
            Assert.That(baseline.Findings, Is.Empty);

            var nestedInstance = outerInstance.transform.GetChild(0).gameObject;
            nestedInstance.AddComponent<BoxCollider>();
            var changed = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(changed.Succeeded, Is.True);
            Assert.That(changed.Findings, Has.Count.EqualTo(1));
            Assert.That(changed.Findings[0].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.AddedComponent));
            Assert.That(changed.Findings[0].PrefabAssetPath, Is.EqualTo(outerPath));
            Assert.That(changed.Findings[0].NearestPrefabAssetPath, Is.EqualTo(innerPath));
            Assert.That(changed.Findings[0].IsNestedPrefabObject, Is.True);
        }

        [Test]
        public void Scan_NestedPrefabRemovedComponent_ReportsNestedSourceOnce()
        {
            var innerPath = _fixture.CreatePrefab(
                "InnerRemoved.prefab",
                root => root.AddComponent<BoxCollider>());
            var outerPath = _fixture.CreateNestedPrefab(innerPath, "OuterRemoved.prefab");
            var scene = _fixture.CreateSavedScene();
            var outerInstance = _fixture.InstantiatePrefab(outerPath, scene);
            var nestedInstance = outerInstance.transform.GetChild(0).gameObject;
            Object.DestroyImmediate(nestedInstance.GetComponent<BoxCollider>());

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            var finding = result.Findings[0];
            Assert.That(finding.Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedComponent));
            Assert.That(finding.PrefabAssetPath, Is.EqualTo(outerPath));
            Assert.That(finding.NearestPrefabAssetPath, Is.EqualTo(innerPath));
            Assert.That(finding.IsNestedPrefabObject, Is.True);
            Assert.That(finding.ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
        }

        [Test]
        public void Scan_Variant_IgnoresAuthoredOverrideAndReportsSceneDelta()
        {
            var basePath = _fixture.CreatePrefab("Base.prefab");
            var variantPath = _fixture.CreateVariantPrefab(
                basePath,
                "Variant.prefab",
                root => root.AddComponent<BoxCollider>());
            var scene = _fixture.CreateSavedScene();
            var variantInstance = _fixture.InstantiatePrefab(variantPath, scene);

            var baseline = BuildGuardPrefabOverrideSceneScanner.Scan(scene);
            Assert.That(baseline.Succeeded, Is.True);
            Assert.That(baseline.Findings, Is.Empty);

            variantInstance.AddComponent<SphereCollider>();
            var changed = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(changed.Succeeded, Is.True);
            Assert.That(changed.Findings, Has.Count.EqualTo(1));
            Assert.That(changed.Findings[0].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.AddedComponent));
            Assert.That(changed.Findings[0].PrefabAssetType, Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(changed.Findings[0].PrefabAssetPath, Is.EqualTo(variantPath));
        }

        [Test]
        public void Scan_VariantRemovedComponent_ReportsSceneDeltaAgainstVariant()
        {
            var basePath = _fixture.CreatePrefab(
                "RemovedBase.prefab",
                root => root.AddComponent<BoxCollider>());
            var variantPath = _fixture.CreateVariantPrefab(
                basePath,
                "RemovedVariant.prefab",
                null);
            var scene = _fixture.CreateSavedScene();
            var variantInstance = _fixture.InstantiatePrefab(variantPath, scene);
            Object.DestroyImmediate(variantInstance.GetComponent<BoxCollider>());

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(1));
            var finding = result.Findings[0];
            Assert.That(finding.Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedComponent));
            Assert.That(finding.PrefabAssetPath, Is.EqualTo(variantPath));
            Assert.That(finding.PrefabAssetType, Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(finding.ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
        }

        [Test]
        public void Scan_RemovedGameObjects_SortsBySourcePathInsteadOfRemovalOrder()
        {
            var prefabPath = _fixture.CreatePrefab(
                "RemovedOrdering.prefab",
                root =>
                {
                    new GameObject("Zulu").transform.SetParent(root.transform, false);
                    new GameObject("Alpha").transform.SetParent(root.transform, false);
                });
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            Object.DestroyImmediate(instance.transform.Find("Zulu").gameObject);
            Object.DestroyImmediate(instance.transform.Find("Alpha").gameObject);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(2));
            Assert.That(result.Findings[0].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedGameObject));
            Assert.That(result.Findings[1].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedGameObject));
            Assert.That(result.Findings[0].SourceObjectPath, Does.EndWith("/Alpha[1]"));
            Assert.That(result.Findings[1].SourceObjectPath, Does.EndWith("/Zulu[0]"));
        }

        [Test]
        public void Scan_MissingPrefabSource_FailsInsteadOfReturningClean()
        {
            var prefabPath = _fixture.CreatePrefab("DeletedSource.prefab");
            var scene = _fixture.CreateSavedScene();
            _fixture.InstantiatePrefab(prefabPath, scene);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            Assert.That(AssetDatabase.DeleteAsset(prefabPath), Is.True);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Error,
                Is.EqualTo(BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus)
                    .Or.EqualTo(BuildGuardPrefabOverrideScanError.MissingPrefabSource));
            Assert.That(result.Findings, Is.Empty);
        }

        [Test]
        public void Scan_LimitBoundarySucceedsAndFindingOverflowDiscardsPartialResult()
        {
            var prefabPath = _fixture.CreatePrefab("FindingLimit.prefab");
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            instance.AddComponent<BoxCollider>();
            var oneFindingLimits = new BuildGuardPrefabOverrideScanLimits(1, 1, 1);

            var exact = BuildGuardPrefabOverrideSceneScanner.Scan(scene, oneFindingLimits);
            Assert.That(exact.Succeeded, Is.True);
            Assert.That(exact.Findings, Has.Count.EqualTo(1));

            instance.AddComponent<SphereCollider>();
            var overflow = BuildGuardPrefabOverrideSceneScanner.Scan(scene, oneFindingLimits);

            Assert.That(overflow.Succeeded, Is.False);
            Assert.That(overflow.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.TooManyFindings));
            Assert.That(overflow.Findings, Is.Empty);
        }

        [Test]
        public void Scan_AddedGameObjectOverflow_DiscardsBeforeLaterCategories()
        {
            var prefabPath = _fixture.CreatePrefab(
                "CategoryOverflow.prefab",
                root => new GameObject("Removed Later").transform.SetParent(root.transform, false));
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            new GameObject("Added One").transform.SetParent(instance.transform, false);
            new GameObject("Added Two").transform.SetParent(instance.transform, false);
            Object.DestroyImmediate(instance.transform.Find("Removed Later").gameObject);
            var limits = new BuildGuardPrefabOverrideScanLimits(10, 1, 1);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene, limits);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.TooManyFindings));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.ScannedPrefabInstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void Scan_GameObjectOverflowDiscardsResult()
        {
            var scene = _fixture.CreateSavedScene();
            var root = new GameObject("Root");
            var limits = new BuildGuardPrefabOverrideScanLimits(1, 1, 1);
            Assert.That(BuildGuardPrefabOverrideSceneScanner.Scan(scene, limits).Succeeded, Is.True);
            new GameObject("Child").transform.SetParent(root.transform, false);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene, limits);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.TooManyGameObjects));
            Assert.That(result.Findings, Is.Empty);
        }

        [Test]
        public void Scan_PrefabInstanceOverflowDiscardsEarlierFinding()
        {
            var prefabPath = _fixture.CreatePrefab("InstanceLimit.prefab");
            var scene = _fixture.CreateSavedScene();
            var first = _fixture.InstantiatePrefab(prefabPath, scene);
            first.AddComponent<BoxCollider>();
            _fixture.InstantiatePrefab(prefabPath, scene);
            var limits = new BuildGuardPrefabOverrideScanLimits(2, 1, 10);

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene, limits);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.TooManyPrefabInstances));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.ScannedPrefabInstanceCount, Is.EqualTo(2));
        }

        [Test]
        public void Scan_MultipleComponents_SortsByTypeRegardlessOfAdditionOrder()
        {
            var prefabPath = _fixture.CreatePrefab("Ordering.prefab");
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            instance.AddComponent<SphereCollider>();
            instance.AddComponent<BoxCollider>();

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Findings, Has.Count.EqualTo(2));
            Assert.That(result.Findings[0].ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
            Assert.That(result.Findings[1].ComponentTypeName, Is.EqualTo("UnityEngine.SphereCollider"));
        }

        [Test]
        public void Scan_PreservesCleanAndDirtySceneState()
        {
            var prefabPath = _fixture.CreatePrefab("DirtyState.prefab");
            var scene = _fixture.CreateSavedScene("DirtyState.unity");
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            Assert.That(scene.isDirty, Is.False);

            var cleanResult = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(cleanResult.Succeeded, Is.True);
            Assert.That(scene.isDirty, Is.False);

            instance.AddComponent<BoxCollider>();
            Assert.That(EditorSceneManager.MarkSceneDirty(scene), Is.True);
            Assert.That(scene.isDirty, Is.True);

            var dirtyResult = BuildGuardPrefabOverrideSceneScanner.Scan(scene);

            Assert.That(dirtyResult.Succeeded, Is.True);
            Assert.That(dirtyResult.Findings, Has.Count.EqualTo(1));
            Assert.That(scene.isDirty, Is.True);
        }

        [Test]
        public void Fixture_TearDown_PreservesPreExistingScenesAndActiveScene()
        {
            var preExistingScene = _fixture.CreateSavedScene("PreExisting.unity");
            new GameObject("Pre-existing Dirty Root");
            Assert.That(EditorSceneManager.MarkSceneDirty(preExistingScene), Is.True);
            Assert.That(preExistingScene.isDirty, Is.True);
            Assert.That(
                SceneManager.GetActiveScene() == preExistingScene || SceneManager.SetActiveScene(preExistingScene),
                Is.True);
            var preExistingHandle = preExistingScene.handle;
            var preExistingSceneCount = SceneManager.sceneCount;
            var secondaryFixture = new BuildGuardPrefabOverrideTestFixture();

            try
            {
                secondaryFixture.SetUp();
                secondaryFixture.CreateSavedScene("Secondary.unity");
            }
            finally
            {
                secondaryFixture.TearDown();
            }

            Assert.That(preExistingScene.IsValid(), Is.True);
            Assert.That(preExistingScene.isLoaded, Is.True);
            Assert.That(preExistingScene.handle, Is.EqualTo(preExistingHandle));
            Assert.That(preExistingScene.isDirty, Is.True);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(preExistingSceneCount));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(preExistingScene));
        }

        [Test]
        public void ValidateScene_StructuralOverride_RemainsReviewOnly()
        {
            var prefabPath = _fixture.CreatePrefab("ReviewOnly.prefab");
            var scene = _fixture.CreateSavedScene();
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            instance.AddComponent<BoxCollider>();

            Assert.DoesNotThrow(() => BuildGuardSceneProcessor.ValidateScene(scene));
        }

        [Test]
        public void Scan_InvalidScene_ReturnsFailureWithoutFindings()
        {
            var result = BuildGuardPrefabOverrideSceneScanner.Scan(default);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.InvalidScene));
            Assert.That(result.Findings, Is.Empty);
        }

        [Test]
        public void Scan_InvalidInjectedLimits_ReturnsFailureWithoutTraversal()
        {
            var scene = _fixture.CreateSavedScene();
            new GameObject("Root");

            var result = BuildGuardPrefabOverrideSceneScanner.Scan(
                scene,
                new BuildGuardPrefabOverrideScanLimits(0, 1, 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.InvalidLimits));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.VisitedGameObjectCount, Is.Zero);
        }
    }
}
