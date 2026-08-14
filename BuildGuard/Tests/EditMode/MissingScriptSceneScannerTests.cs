// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Missing Script走査の階層・inactive・Prefab契約を検証します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class MissingScriptSceneScannerTests
    {
        /// <summary>
        /// Missing Script Scene fixtureを示すGUIDです。
        /// </summary>
        internal const string BrokenSceneFixtureGuid = "62568305b48f4bfb8de5c5786171f370";

        /// <summary>
        /// Missing Script Prefab fixtureを示すGUIDです。
        /// </summary>
        internal const string BrokenPrefabFixtureGuid = "1288dc4ed86b4939a6b9be1a70cf5ef5";

        /// <summary>
        /// testごとの一時asset folderです。
        /// </summary>
        private string _temporaryFolder;

        /// <summary>
        /// processor testから利用する一時asset folderを取得します。
        /// </summary>
        internal string TemporaryFolder => _temporaryFolder;

        /// <summary>
        /// 各test専用の一時asset folderを作成します。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        /// <summary>
        /// 開いた一時Sceneとasset folderを必ず削除します。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!string.IsNullOrEmpty(scene.path) && scene.path.StartsWith(_temporaryFolder, StringComparison.Ordinal))
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        /// <summary>
        /// inactive childを含む全Missing Scriptと件数を収集できることを検証します。
        /// </summary>
        [Test]
        public void Scan_BrokenScene_IncludesInactiveHierarchy()
        {
            var scene = OpenSceneFixture();

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings.Count, Is.EqualTo(2));
            Assert.That(findings[0].HierarchyPath, Is.EqualTo("Broken\\/Root[0]"));
            Assert.That(findings[0].MissingScriptCount, Is.EqualTo(1));
            Assert.That(findings[1].HierarchyPath, Is.EqualTo("Broken\\/Root[0]/Inactive Child[0]"));
            Assert.That(findings[1].MissingScriptCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Missing Scriptを持たないinactive階層が検出されないことを検証します。
        /// </summary>
        [Test]
        public void Scan_ValidInactiveHierarchy_ReturnsEmpty()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Valid Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Inactive Child");
            child.transform.SetParent(root.transform, false);
            child.SetActive(false);

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings, Is.Empty);
        }

        /// <summary>
        /// Prefab instance内のMissing ScriptをScene階層として検出できることを検証します。
        /// </summary>
        [Test]
        public void Scan_BrokenPrefabInstance_FindsNestedObject()
        {
            var prefabPath = CopyFixture(BrokenPrefabFixtureGuid, "BrokenPrefab.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Assert.That(instance, Is.Not.Null);

            var findings = MissingScriptSceneScanner.Scan(scene);

            Assert.That(findings.Count, Is.EqualTo(1));
            Assert.That(findings[0].HierarchyPath, Is.EqualTo("BrokenPrefab[0]"));
            Assert.That(findings[0].MissingScriptCount, Is.EqualTo(1));
        }

        /// <summary>
        /// path区切りと制御文字が一行の表現へ変換されることを検証します。
        /// </summary>
        [Test]
        public void EscapePathText_ControlCharacters_AreEscaped()
        {
            Assert.That(MissingScriptSceneScanner.EscapePathText("A/B\\C\r\n\t"), Is.EqualTo("A\\/B\\\\C\\r\\n\\t"));
        }

        /// <summary>
        /// 無効なSceneを誤って空Sceneとして扱わないことを検証します。
        /// </summary>
        [Test]
        public void Scan_InvalidScene_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MissingScriptSceneScanner.Scan(default));
        }

        /// <summary>
        /// Missing Script Scene fixtureを一時assetとして開きます。
        /// </summary>
        internal Scene OpenSceneFixture()
        {
            var scenePath = CopyFixture(BrokenSceneFixtureGuid, "BrokenScene.unity");
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        /// <summary>
        /// GUIDで解決したtext fixtureを指定拡張子の一時assetへ複製します。
        /// </summary>
        private string CopyFixture(string fixtureGuid, string destinationName)
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(fixtureGuid);
            Assert.That(sourcePath, Is.Not.Empty);
            var destinationPath = $"{_temporaryFolder}/{destinationName}";
            File.WriteAllText(destinationPath, File.ReadAllText(sourcePath), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return destinationPath;
        }
    }
}
