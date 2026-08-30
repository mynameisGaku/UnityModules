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
    /// 階層、無効なオブジェクト、プレハブの実体を含む欠落スクリプト検査を確認します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class MissingScriptSceneScannerTests
    {
        /// <summary>
        /// 欠落スクリプトを含む試験用シーンを識別します。
        /// </summary>
        internal const string BrokenSceneFixtureGuid = "62568305b48f4bfb8de5c5786171f370";

        /// <summary>
        /// 欠落スクリプトを含む試験用プレハブを識別します。
        /// </summary>
        internal const string BrokenPrefabFixtureGuid = "1288dc4ed86b4939a6b9be1a70cf5ef5";

        /// <summary>
        /// 現在の試験で使う一時アセットフォルダーを保持します。
        /// </summary>
        private string _temporaryFolder;

        /// <summary>
        /// 処理試験で使う一時アセットフォルダーを取得します。
        /// </summary>
        internal string TemporaryFolder => _temporaryFolder;

        /// <summary>
        /// 各試験専用の一時アセットフォルダーを作成します。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        /// <summary>
        /// 各試験後に一時シーンとアセットを除去します。
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
        /// 有効・無効を問わず、階層内の欠落スクリプトをすべて数えることを確認します。
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
        /// 問題のない無効な階層から検出結果が生じないことを確認します。
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
        /// プレハブの実体内にある欠落スクリプトを、シーン階層のパスとして報告することを確認します。
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
        /// パス区切りと制御文字を1行へ収まる表記に置き換えることを確認します。
        /// </summary>
        [Test]
        public void EscapePathText_ControlCharacters_AreEscaped()
        {
            Assert.That(MissingScriptSceneScanner.EscapePathText("A/B\\C\r\n\t"), Is.EqualTo("A\\/B\\\\C\\r\\n\\t"));
        }

        /// <summary>
        /// 無効なシーンを空のシーンとして扱わず、拒否することを確認します。
        /// </summary>
        [Test]
        public void Scan_InvalidScene_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => MissingScriptSceneScanner.Scan(default));

            Assert.That(exception.Message, Does.StartWith("検査するシーンが無効です。"));
        }

        /// <summary>
        /// 欠落スクリプトを含む試験用シーンを、一時アセットとして開きます。
        /// </summary>
        internal Scene OpenSceneFixture()
        {
            var scenePath = CopyFixture(BrokenSceneFixtureGuid, "BrokenScene.unity");
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        /// <summary>
        /// GUIDから解決した試験用テキストを、指定された拡張子の一時アセットへ複製します。
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
