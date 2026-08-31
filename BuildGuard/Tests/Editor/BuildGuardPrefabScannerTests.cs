// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 選択プレハブアセットを一定の順序で検査することを確認します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabScannerTests
    {
        private string _temporaryFolder;

        [SetUp]
        public void SetUp()
        {
            _temporaryFolder = $"Assets/__BuildGuardPrefabScannerTests_{Guid.NewGuid():N}";
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder)));
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_temporaryFolder))
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(_temporaryFolder));
            }
        }

        [Test]
        public void Scan_BrokenPrefab_ReturnsMissingScriptWithExactPath()
        {
            var prefabPath = CopyBrokenPrefab("Broken.prefab");
            var previewSceneCount = EditorSceneManager.previewSceneCount;

            var result = BuildGuardPrefabScanner.Scan(new[] { prefabPath });
            var repeatedResult = BuildGuardPrefabScanner.Scan(new[] { prefabPath });

            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.ScannedPrefabCount, Is.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingScript));
            Assert.That(result.Issues[0].PrefabPath, Is.EqualTo(prefabPath));
            Assert.That(result.Issues[0].HierarchyPath, Is.EqualTo("Broken[0]"));
            Assert.That(result.Issues[0].TargetGlobalObjectId, Is.Not.Empty);
            Assert.That(repeatedResult.Issues, Has.Count.EqualTo(1));
            Assert.That(repeatedResult.Issues[0].TargetGlobalObjectId, Is.EqualTo(
                result.Issues[0].TargetGlobalObjectId));
            Assert.That(result.Issues[0].Details, Is.EqualTo("欠落スクリプト: 1"));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewSceneCount));
        }

        [Test]
        public void Scan_DeletedPrefabReference_ReturnsComponentAndProperty()
        {
            var prefabPath = CreatePrefabWithDeletedCameraTarget();

            var result = BuildGuardPrefabScanner.Scan(new[] { prefabPath });

            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingObjectReference));
            Assert.That(result.Issues[0].HierarchyPath, Is.EqualTo("MissingObjectReference[0]"));
            Assert.That(result.Issues[0].TargetGlobalObjectId, Is.Not.Empty);
            Assert.That(result.Issues[0].Details, Is.EqualTo("UnityEngine.Camera[1].m_TargetTexture"));
        }

        [Test]
        public void Scan_DuplicatePaths_UsesOrdinalOrderOnceAndSupportsCancellation()
        {
            var first = CopyBrokenPrefab("A_Broken.prefab");
            var second = CreateValidPrefab("Z_Valid.prefab");
            var visited = string.Empty;
            var previewSceneCount = EditorSceneManager.previewSceneCount;

            var result = BuildGuardPrefabScanner.Scan(
                new[] { second, first, first.Replace('/', '\\') },
                (index, total, path) =>
                {
                    visited += $"{index}/{total}:{path}|";
                    return index == 1;
                });

            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.ScannedPrefabCount, Is.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(BuildGuardIssueKind.MissingScript));
            Assert.That(result.Issues[0].PrefabPath, Is.EqualTo(first));
            Assert.That(result.Issues[0].Details, Is.EqualTo("欠落スクリプト: 1"));
            Assert.That(visited, Is.EqualTo($"0/2:{first}|1/2:{second}|"));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewSceneCount));
        }

        [Test]
        public void Scan_FailureAfterFirstPrefab_ReturnsNoPartialResultAndUnloadsContents()
        {
            var first = CopyBrokenPrefab("A_Broken.prefab");
            var second = CreateValidPrefab("Z_Valid.prefab");
            var previewSceneCount = EditorSceneManager.previewSceneCount;
            var returnedResult = default(BuildGuardPrefabScanResult);
            var returned = false;

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                returnedResult = BuildGuardPrefabScanner.Scan(
                    new[] { second, first },
                    (index, _, _) => index == 1
                        ? throw new InvalidOperationException("プレハブ検査中の試験例外です。")
                        : false);
                returned = true;
            });

            Assert.That(exception.Message, Is.EqualTo("プレハブ検査中の試験例外です。"));
            Assert.That(returned, Is.False);
            Assert.That(returnedResult.Issues, Is.Null);
            Assert.That(returnedResult.ScannedPrefabCount, Is.Zero);
            Assert.That(returnedResult.Cancelled, Is.False);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewSceneCount));
        }

        [Test]
        public void NormalizePrefabPaths_NullList_ReturnsExactJapaneseReason()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => BuildGuardPrefabScanner.NormalizePrefabPaths(null));

            Assert.That(exception.ParamName, Is.EqualTo("prefabPaths"));
            AssertPrimaryExceptionMessage(
                exception,
                "プレハブのパス一覧を指定してください。");
        }

        [Test]
        public void NormalizePrefabPaths_RejectsFoldersScenesAndPackageFixturesWithExactJapaneseReason()
        {
            AssertInvalidPrefabPath(_temporaryFolder);
            AssertInvalidPrefabPath("Assets/Missing.prefab");
            AssertInvalidPrefabPath(
                AssetDatabase.GUIDToAssetPath(MissingScriptSceneScannerTests.BrokenPrefabFixtureGuid));
        }

        /// <summary>利用できないプレハブパスの失敗理由を完全一致で確認します。</summary>
        private static void AssertInvalidPrefabPath(string path)
        {
            var normalizedPath = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            var exception = Assert.Throws<ArgumentException>(
                () => BuildGuardPrefabScanner.NormalizePrefabPaths(new[]
                {
                    path
                }));

            Assert.That(exception.ParamName, Is.EqualTo("prefabPaths"));
            AssertPrimaryExceptionMessage(
                exception,
                $"指定されたパスは「Assets」配下のプレハブアセットではありません: {normalizedPath}");
        }

        /// <summary>実行環境が付加する引数名を除き、固有の失敗理由を完全一致で確認します。</summary>
        private static void AssertPrimaryExceptionMessage(
            ArgumentException exception,
            string expectedMessage)
        {
            Assert.That(exception.Message.Length, Is.GreaterThanOrEqualTo(expectedMessage.Length));
            Assert.That(
                exception.Message.Substring(0, expectedMessage.Length),
                Is.EqualTo(expectedMessage));
        }

        internal string CopyBrokenPrefab(string fileName)
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(MissingScriptSceneScannerTests.BrokenPrefabFixtureGuid);
            Assert.That(sourcePath, Is.Not.Empty);
            var destinationPath = $"{_temporaryFolder}/{fileName}";
            File.WriteAllText(destinationPath, File.ReadAllText(sourcePath), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return destinationPath;
        }

        private string CreateValidPrefab(string fileName)
        {
            var instance = new GameObject(Path.GetFileNameWithoutExtension(fileName));
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(instance, $"{_temporaryFolder}/{fileName}") != null
                    ? $"{_temporaryFolder}/{fileName}"
                    : throw new InvalidOperationException("試験用プレハブを作成できませんでした。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private string CreatePrefabWithDeletedCameraTarget()
        {
            var texturePath = $"{_temporaryFolder}/DeletedTarget.renderTexture";
            var texture = new RenderTexture(16, 16, 0);
            AssetDatabase.CreateAsset(texture, texturePath);
            var instance = new GameObject("Camera Root", typeof(Camera));
            instance.GetComponent<Camera>().targetTexture = texture;
            var prefabPath = $"{_temporaryFolder}/MissingObjectReference.prefab";
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(instance, prefabPath), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            Assert.IsTrue(AssetDatabase.DeleteAsset(texturePath));
            return prefabPath;
        }
    }
}
