// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Owns temporary Prefab assets and Scenes used by structural override tests.
    /// </summary>
    internal sealed class BuildGuardPrefabOverrideTestFixture
    {
        private const string FreshUntitledSceneName = "Untitled";
        private const string BootstrapSceneFileName = "FreshUntitledBootstrap.unity";
        private const string AssetAuthoringSceneFileName = "AssetAuthoring.unity";

        private readonly HashSet<ulong> _ownedSceneHandles = new HashSet<ulong>();
        private Scene _originalActiveScene;
        private Scene _assetAuthoringScene;
        private bool _usesFreshUntitledBootstrap;
        private bool _setUpCompleted;
        private string _bootstrapScenePath;

        internal string TemporaryFolder { get; private set; }

        internal void SetUp()
        {
            _setUpCompleted = false;
            _originalActiveScene = SceneManager.GetActiveScene();
            _usesFreshUntitledBootstrap = IsFreshUntitledSceneSetup();
            EnsureNoUnsupportedUnsavedScenes();
            TemporaryFolder = $"Assets/__BuildGuardPrefabOverrideTests_{Guid.NewGuid():N}";
            try
            {
                Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(TemporaryFolder)));
                if (_usesFreshUntitledBootstrap)
                {
                    _bootstrapScenePath = $"{TemporaryFolder}/{BootstrapSceneFileName}";
                    Assert.That(
                        EditorSceneManager.SaveScene(_originalActiveScene, _bootstrapScenePath),
                        Is.True,
                        "Failed to move the empty fresh Untitled Scene into the test-owned folder.");
                }

                _assetAuthoringScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                OwnScene(_assetAuthoringScene);
                Assert.That(
                    EditorSceneManager.SaveScene(
                        _assetAuthoringScene,
                        $"{TemporaryFolder}/{AssetAuthoringSceneFileName}"),
                    Is.True,
                    "Failed to save the test-owned asset-authoring Scene.");
                Assert.That(IsActiveOrSetActive(_assetAuthoringScene), Is.True);
                _setUpCompleted = true;
            }
            catch
            {
                CleanupFailedSetUp();
                throw;
            }
        }

        internal void TearDown()
        {
            if (!_setUpCompleted) return;
            _setUpCompleted = false;

            var folderDeleted = true;
            var scenesClosed = true;
            var originalSetupRestored = TryRestoreOriginalActiveScene();
            var mayDeleteTemporaryFolder = true;
            try
            {
                for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (scene.isLoaded && _ownedSceneHandles.Contains(scene.handle.GetRawData()))
                    {
                        scenesClosed &= EditorSceneManager.CloseScene(scene, true);
                    }
                }

                if (_usesFreshUntitledBootstrap)
                {
                    var freshUntitledRestored = TryRestoreFreshUntitledScene();
                    originalSetupRestored &= freshUntitledRestored;
                    mayDeleteTemporaryFolder = freshUntitledRestored;
                }
                else
                {
                    originalSetupRestored &= TryRestoreOriginalActiveScene();
                }
            }
            finally
            {
                _ownedSceneHandles.Clear();
                if (mayDeleteTemporaryFolder
                    && !string.IsNullOrEmpty(TemporaryFolder)
                    && AssetDatabase.IsValidFolder(TemporaryFolder))
                {
                    folderDeleted = AssetDatabase.DeleteAsset(TemporaryFolder);
                }
                else if (!mayDeleteTemporaryFolder)
                {
                    folderDeleted = false;
                }
            }

            Assert.That(scenesClosed, Is.True, "Failed to close an owned test Scene.");
            Assert.That(folderDeleted, Is.True, $"Failed to delete test folder {TemporaryFolder}.");
            Assert.That(
                originalSetupRestored,
                Is.True,
                "Failed to restore the pre-existing Scene setup safely.");
        }

        internal Scene CreateSavedScene(string fileName = "OverrideScene.unity")
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            OwnScene(scene);
            Assert.That(EditorSceneManager.SaveScene(scene, $"{TemporaryFolder}/{fileName}"), Is.True);
            Assert.That(IsActiveOrSetActive(scene), Is.True);
            return scene;
        }

        internal string CreatePrefab(
            string fileName,
            Action<GameObject> configure = null)
        {
            var previousActiveScene = ActivateAuthoringScene();
            GameObject root = null;
            try
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
                configure?.Invoke(root);
                var path = $"{TemporaryFolder}/{fileName}";
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
                return path;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                RestoreActiveScene(previousActiveScene);
            }
        }

        internal string CreateNestedPrefab(string innerPrefabPath, string fileName)
        {
            var previousActiveScene = ActivateAuthoringScene();
            GameObject outerRoot = null;
            try
            {
                outerRoot = new GameObject(Path.GetFileNameWithoutExtension(fileName));
                var innerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(innerPrefabPath);
                Assert.That(innerAsset, Is.Not.Null);
                var nestedInstance = PrefabUtility.InstantiatePrefab(innerAsset) as GameObject;
                Assert.That(nestedInstance, Is.Not.Null);
                nestedInstance.transform.SetParent(outerRoot.transform, false);

                var path = $"{TemporaryFolder}/{fileName}";
                Assert.That(PrefabUtility.SaveAsPrefabAsset(outerRoot, path), Is.Not.Null);
                return path;
            }
            finally
            {
                if (outerRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(outerRoot);
                }

                RestoreActiveScene(previousActiveScene);
            }
        }

        internal string CreateVariantPrefab(
            string basePrefabPath,
            string fileName,
            Action<GameObject> configure)
        {
            var previousActiveScene = ActivateAuthoringScene();
            GameObject variantRoot = null;
            try
            {
                var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
                Assert.That(baseAsset, Is.Not.Null);
                variantRoot = PrefabUtility.InstantiatePrefab(baseAsset) as GameObject;
                Assert.That(variantRoot, Is.Not.Null);
                configure?.Invoke(variantRoot);
                var path = $"{TemporaryFolder}/{fileName}";
                Assert.That(PrefabUtility.SaveAsPrefabAsset(variantRoot, path), Is.Not.Null);
                return path;
            }
            finally
            {
                if (variantRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(variantRoot);
                }

                RestoreActiveScene(previousActiveScene);
            }
        }

        internal GameObject InstantiatePrefab(string prefabPath, Scene scene)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(asset, Is.Not.Null);
            var instance = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
            Assert.That(instance, Is.Not.Null);
            return instance;
        }

        private void OwnScene(Scene scene)
        {
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            _ownedSceneHandles.Add(scene.handle.GetRawData());
        }

        private Scene ActivateAuthoringScene()
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            Assert.That(_assetAuthoringScene.IsValid(), Is.True);
            Assert.That(_assetAuthoringScene.isLoaded, Is.True);
            Assert.That(IsActiveOrSetActive(_assetAuthoringScene), Is.True);
            return previousActiveScene;
        }

        private static void RestoreActiveScene(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                Assert.That(IsActiveOrSetActive(scene), Is.True);
            }
        }

        private bool TryRestoreOriginalActiveScene()
        {
            return IsActiveOrSetActive(_originalActiveScene);
        }

        private void EnsureNoUnsupportedUnsavedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!string.IsNullOrEmpty(scene.path)) continue;
                if (_usesFreshUntitledBootstrap && scene == _originalActiveScene) continue;

                Assert.Inconclusive(
                    "Prefab override tests did not run because an unsaved Scene is already open. "
                    + "Save or close it explicitly; the fixture will not save or discard arbitrary user Scenes.");
            }
        }

        private static bool IsFreshUntitledSceneSetup()
        {
            if (SceneManager.sceneCount != 1) return false;

            var scene = SceneManager.GetSceneAt(0);
            return scene.IsValid()
                && scene.isLoaded
                && scene == SceneManager.GetActiveScene()
                && string.IsNullOrEmpty(scene.path)
                && (string.IsNullOrEmpty(scene.name)
                    || string.Equals(scene.name, FreshUntitledSceneName, StringComparison.Ordinal))
                && scene.rootCount == 0
                && !scene.isDirty;
        }

        private bool TryRestoreFreshUntitledScene()
        {
            if (IsFreshUntitledSceneSetup()) return true;
            if (!_originalActiveScene.IsValid() || !_originalActiveScene.isLoaded) return false;
            if (SceneManager.sceneCount != 1 || SceneManager.GetSceneAt(0) != _originalActiveScene)
            {
                return false;
            }

            if (_originalActiveScene.isDirty
                || _originalActiveScene.rootCount != 0
                || !string.Equals(
                    _originalActiveScene.path,
                    _bootstrapScenePath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!IsActiveOrSetActive(_originalActiveScene))
            {
                return false;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return IsFreshUntitledSceneSetup();
        }

        private static bool IsActiveOrSetActive(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            return SceneManager.GetActiveScene() == scene
                || SceneManager.SetActiveScene(scene);
        }

        private void CleanupFailedSetUp()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && _ownedSceneHandles.Contains(scene.handle.GetRawData()))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            _ownedSceneHandles.Clear();
            var setupRestored = !_usesFreshUntitledBootstrap || TryRestoreFreshUntitledScene();
            if (setupRestored
                && !string.IsNullOrEmpty(TemporaryFolder)
                && AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.DeleteAsset(TemporaryFolder);
            }
        }
    }
}
