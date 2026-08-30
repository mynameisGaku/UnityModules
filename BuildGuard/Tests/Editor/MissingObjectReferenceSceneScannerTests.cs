// SPDX-License-Identifier: MIT

using System;
using System.IO;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 読み込み済みシーンで、欠落した直列化オブジェクト参照の検出を確認します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class MissingObjectReferenceSceneScannerTests
    {
        private string _temporaryFolder;

        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardObjectReferenceTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!string.IsNullOrEmpty(scene.path)
                    && scene.path.StartsWith(_temporaryFolder, StringComparison.Ordinal))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        [Test]
        public void Scan_DeletedCameraTargetTexture_ReturnsHierarchyComponentAndProperty()
        {
            var scene = CreateSceneWithMissingCameraTargetTexture();

            var findings = MissingObjectReferenceSceneScanner.Scan(scene);

            Assert.That(findings.Count, Is.EqualTo(1));
            Assert.That(findings[0].HierarchyPath, Is.EqualTo("Camera Root[0]"));
            Assert.That(findings[0].ComponentTypeName, Is.EqualTo("UnityEngine.Camera"));
            Assert.That(findings[0].ComponentIndex, Is.EqualTo(1));
            Assert.That(findings[0].PropertyPath, Is.EqualTo("m_TargetTexture"));
        }

        [Test]
        public void Scan_ValidInactiveCamera_ReturnsEmpty()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameObject = new GameObject("Inactive Camera", typeof(Camera));
            gameObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var findings = MissingObjectReferenceSceneScanner.Scan(scene);

            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void Scan_InvalidScene_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => MissingObjectReferenceSceneScanner.Scan(default));

            Assert.That(exception.Message, Does.StartWith("検査するシーンが無効です。"));
        }

        internal Scene CreateSceneWithMissingCameraTargetTexture()
        {
            var texturePath = $"{_temporaryFolder}/DeletedTarget.renderTexture";
            var texture = new RenderTexture(16, 16, 0);
            AssetDatabase.CreateAsset(texture, texturePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Camera Root", typeof(Camera));
            cameraObject.GetComponent<Camera>().targetTexture = texture;
            cameraObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var scenePath = $"{_temporaryFolder}/MissingObjectReference.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(AssetDatabase.DeleteAsset(texturePath));
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }
    }
}
