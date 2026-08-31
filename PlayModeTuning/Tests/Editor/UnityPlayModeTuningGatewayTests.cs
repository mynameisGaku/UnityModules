using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayModeTuning.Editor.Tests
{
    public sealed class UnityPlayModeTuningGatewayTests
    {
        private const int OriginalSelectedValue = 10;
        private const int OriginalUnselectedValue = 20;
        private const int OriginalExternalSelectedValue = 30;
        private const int OriginalExternalUnselectedValue = 40;
        private const int AppliedSelectedValue = 71;
        private const int SideEffectUnselectedValue = 92;
        private const string TemporaryFolderPrefix = "Assets/PlayModeTuningTests-";

        private UnityPlayModeTuningGateway gateway;
        private PlayModeTuningGatewayFixtureComponent externalComponent;
        private Scene activeSceneBeforeTest;
        private Scene savedEmptySceneBeforeTest;
        private Scene temporaryScene;
        private string temporaryFolderPath = string.Empty;
        private string temporaryFolderGuid = string.Empty;
        private string temporaryScenePath = string.Empty;
        private bool temporaryFolderCreated;
        private bool restoreEmptyUntitledScene;

        [SetUp]
        public void SetUp()
        {
            gateway = null;
            externalComponent = null;
            activeSceneBeforeTest = SceneManager.GetActiveScene();
            savedEmptySceneBeforeTest = default;
            temporaryScene = default;
            temporaryFolderPath = string.Empty;
            temporaryFolderGuid = string.Empty;
            temporaryScenePath = string.Empty;
            temporaryFolderCreated = false;
            restoreEmptyUntitledScene = false;
        }

        [TearDown]
        public void TearDown()
        {
            var temporarySceneClosed = !temporaryScene.IsValid() || !temporaryScene.isLoaded;
            var savedEmptySceneClosed = !savedEmptySceneBeforeTest.IsValid() || !savedEmptySceneBeforeTest.isLoaded;
            try
            {
                try
                {
                    gateway?.RevertApply();
                }
                finally
                {
                    if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                        temporarySceneClosed = EditorSceneManager.CloseScene(temporaryScene, true);
                    if (restoreEmptyUntitledScene && savedEmptySceneBeforeTest.IsValid() && savedEmptySceneBeforeTest.isLoaded)
                    {
                        var restoredEmptyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        SceneManager.SetActiveScene(restoredEmptyScene);
                        savedEmptySceneClosed = EditorSceneManager.CloseScene(savedEmptySceneBeforeTest, true);
                    }
                    else if (activeSceneBeforeTest.IsValid() && activeSceneBeforeTest.isLoaded)
                    {
                        SceneManager.SetActiveScene(activeSceneBeforeTest);
                    }
                }
            }
            finally
            {
                try
                {
                    if (temporarySceneClosed && savedEmptySceneClosed && OwnsTemporaryFolder())
                    {
                        Assert.That(
                            AssetDatabase.DeleteAsset(temporaryFolderPath),
                            Is.True,
                            "この検査が作成した一時フォルダーを削除できませんでした。");
                    }
                }
                finally
                {
                    gateway = null;
                    externalComponent = null;
                }
            }
            Assert.That(temporarySceneClosed, Is.True, "検査用の一時シーンだけを閉じられませんでした。");
            Assert.That(savedEmptySceneClosed, Is.True, "検査用に一時保存した空シーンを閉じられませんでした。");
        }

        [Test]
        public void CompleteApply_OneUndoRestoresOriginalValue()
        {
            var component = CreateSavedFixtureComponent();
            gateway = new UnityPlayModeTuningGateway();
            var write = ResolveIntegerWrite(component, AppliedSelectedValue);

            var apply = gateway.Apply(new[] { write });
            Assert.That(apply.Succeeded, Is.True, apply.Error + "：" + apply.Message);
            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(AppliedSelectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(SideEffectUnselectedValue));

            var complete = gateway.CompleteApply();
            Assert.That(complete.Succeeded, Is.True, complete.Error + "：" + complete.Message);
            gateway.ReleaseApply();

            Undo.PerformUndo();

            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(OriginalSelectedValue));
            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(OriginalUnselectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(OriginalExternalSelectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(OriginalExternalUnselectedValue));
            Assert.That(temporaryScene.isDirty, Is.False, "一回の取り消しで保存直後の未変更状態へ戻る必要があります。");
        }

        [Test]
        public void CompleteApplyThenRevertApply_RestoresSelectedValueAndUnselectedSideEffect()
        {
            var component = CreateSavedFixtureComponent();
            gateway = new UnityPlayModeTuningGateway();
            var write = ResolveIntegerWrite(component, AppliedSelectedValue);
            var baseline = gateway.Capture(new[] { write.Record });
            Assert.That(baseline.Succeeded, Is.True, baseline.Error + "：" + baseline.Message);

            var apply = gateway.Apply(new[] { write });
            Assert.That(apply.Succeeded, Is.True, apply.Error + "：" + apply.Message);
            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(AppliedSelectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(SideEffectUnselectedValue));
            var changed = gateway.Capture(new[] { write.Record });
            Assert.That(changed.Succeeded, Is.True, changed.Error + "：" + changed.Message);
            Assert.That(changed.Snapshot.Components[0].UnselectedFingerprint, Is.Not.EqualTo(baseline.Snapshot.Components[0].UnselectedFingerprint));

            var complete = gateway.CompleteApply();
            Assert.That(complete.Succeeded, Is.True, complete.Error + "：" + complete.Message);

            var revert = gateway.RevertApply();

            Assert.That(revert.Succeeded, Is.True, revert.Error + "：" + revert.Message);
            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(OriginalSelectedValue));
            Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(OriginalUnselectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(OriginalExternalSelectedValue));
            Assert.That(ReadInteger(externalComponent, nameof(PlayModeTuningGatewayFixtureComponent.unselectedValue)), Is.EqualTo(OriginalExternalUnselectedValue));
            Assert.That(temporaryScene.isDirty, Is.False, "失敗時の復元で保存直後の未変更状態へ戻る必要があります。");
            var restored = gateway.Capture(new[] { write.Record });
            Assert.That(restored.Succeeded, Is.True, restored.Error + "：" + restored.Message);
            Assert.That(restored.Snapshot.Components[0].UnselectedFingerprint, Is.EqualTo(baseline.Snapshot.Components[0].UnselectedFingerprint));
        }

        [Test]
        public void RevertApply_LeavesDetectableAddedComponentOutsideSerializedValueUndo()
        {
            var component = CreateSavedFixtureComponent();
            gateway = new UnityPlayModeTuningGateway();
            var write = ResolveIntegerWrite(component, AppliedSelectedValue);
            var baseline = gateway.Capture(new[] { write.Record });
            Assert.That(baseline.Succeeded, Is.True, baseline.Error + "：" + baseline.Message);
            Component addedComponent = null;
            try
            {
                var apply = gateway.Apply(new[] { write });
                Assert.That(apply.Succeeded, Is.True, apply.Error + "：" + apply.Message);
                addedComponent = externalComponent.gameObject.AddComponent<BoxCollider>();
                var changed = gateway.Capture(new[] { write.Record });
                Assert.That(changed.Succeeded, Is.True, changed.Error + "：" + changed.Message);
                Assert.That(changed.Snapshot.Components[0].UnselectedFingerprint, Is.Not.EqualTo(baseline.Snapshot.Components[0].UnselectedFingerprint));

                var revert = gateway.RevertApply();

                Assert.That(revert.Succeeded, Is.True, revert.Error + "：" + revert.Message);
                Assert.That(ReadInteger(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue)), Is.EqualTo(OriginalSelectedValue));
                Assert.That(addedComponent, Is.Not.Null, "通常の値用取り消し記録では、後から追加されたコンポーネントを自動復元できません。");
                var restored = gateway.Capture(new[] { write.Record });
                Assert.That(restored.Succeeded, Is.True, restored.Error + "：" + restored.Message);
                Assert.That(restored.Snapshot.Components[0].UnselectedFingerprint, Is.Not.EqualTo(baseline.Snapshot.Components[0].UnselectedFingerprint));
            }
            finally
            {
                if (addedComponent != null)
                    UnityEngine.Object.DestroyImmediate(addedComponent);
            }
        }

        private PlayModeTuningGatewayFixtureComponent CreateSavedFixtureComponent()
        {
            CreateTemporaryFolder();
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(temporaryScenePath), Is.Null, "既存のシーン資産は上書きしません。");
            Assert.That(File.Exists(ToAbsoluteProjectPath(temporaryScenePath)), Is.False, "既存のシーンファイルは上書きしません。");

            var scene = CreateTemporaryScene();
            temporaryScene = scene;
            if (SceneManager.GetActiveScene() != scene)
                Assert.That(SceneManager.SetActiveScene(scene), Is.True, "検査用の一時シーンを操作対象にできませんでした。");
            var gameObject = new GameObject("PlayModeTuningGatewayFixture");
            var component = gameObject.AddComponent<PlayModeTuningGatewayFixtureComponent>();
            component.selectedValue = OriginalSelectedValue;
            component.unselectedValue = OriginalUnselectedValue;
            var externalGameObject = new GameObject("PlayModeTuningExternalFixture");
            externalComponent = externalGameObject.AddComponent<PlayModeTuningGatewayFixtureComponent>();
            externalComponent.selectedValue = OriginalExternalSelectedValue;
            externalComponent.unselectedValue = OriginalExternalUnselectedValue;
            component.sideEffectTarget = externalComponent;

            var saved = EditorSceneManager.SaveScene(scene, temporaryScenePath, false);
            Assert.That(saved, Is.True, "検査用の一時シーンを保存できませんでした。");
            Assert.That(scene.path, Is.EqualTo(temporaryScenePath));
            Assert.That(File.Exists(ToAbsoluteProjectPath(temporaryScenePath)), Is.True);
            Assert.That(scene.isDirty, Is.False, "検査用シーンは保存直後の未変更状態から始めます。");
            return component;
        }

        private Scene CreateTemporaryScene()
        {
            if (!Application.isBatchMode)
            {
                Assert.Ignore("既存の取り消し履歴を保護するため、実シーン取り消し検査は隔離した一括実行だけで行います。");
                return default;
            }
            if (string.IsNullOrEmpty(activeSceneBeforeTest.path))
            {
                var canTemporarilySaveEmptyScene = !activeSceneBeforeTest.isDirty && activeSceneBeforeTest.GetRootGameObjects().Length == 0;
                if (!canTemporarilySaveEmptyScene)
                {
                    Assert.Ignore("未保存の作業シーンを保護するため、実シーン取り消し検査を省略しました。");
                    return default;
                }
                var savedEmptyPath = temporaryFolderPath + "/EmptySceneBeforeTest.unity";
                Assert.That(EditorSceneManager.SaveScene(activeSceneBeforeTest, savedEmptyPath, false), Is.True, "空の開始シーンを検査用フォルダーへ一時保存できませんでした。");
                savedEmptySceneBeforeTest = activeSceneBeforeTest;
                restoreEmptyUntitledScene = true;
            }
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        private void CreateTemporaryFolder()
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var folderName = "PlayModeTuningTests-" + Guid.NewGuid().ToString("N");
                var candidatePath = "Assets/" + folderName;
                var candidateAbsolutePath = ToAbsoluteProjectPath(candidatePath);
                if (AssetDatabase.IsValidFolder(candidatePath) || Directory.Exists(candidateAbsolutePath) || File.Exists(candidateAbsolutePath))
                    continue;

                var createdGuid = AssetDatabase.CreateFolder("Assets", folderName);
                if (string.IsNullOrEmpty(createdGuid))
                    continue;
                if (!StringComparer.Ordinal.Equals(AssetDatabase.GUIDToAssetPath(createdGuid), candidatePath))
                    continue;

                temporaryFolderPath = candidatePath;
                temporaryFolderGuid = createdGuid;
                temporaryScenePath = candidatePath + "/GatewayUndo.unity";
                temporaryFolderCreated = true;
                Assert.That(AssetDatabase.IsValidFolder(temporaryFolderPath), Is.True);
                return;
            }

            Assert.Fail("既存物と重ならない検査用一時フォルダーを作成できませんでした。");
        }

        private PlayModeTuningWrite ResolveIntegerWrite(PlayModeTuningGatewayFixtureComponent component, int value)
        {
            var resolved = gateway.ResolveSelections(new[]
            {
                new PlayModeTuningPropertySelection(component, nameof(PlayModeTuningGatewayFixtureComponent.selectedValue))
            });
            Assert.That(resolved.Succeeded, Is.True, resolved.Error + "：" + resolved.Message);
            Assert.That(resolved.Snapshot.Properties, Has.Count.EqualTo(1));
            var encoded = new PlayModeTuningEncodedValue(
                PlayModeTuningValueKind.SignedInteger,
                value.ToString(CultureInfo.InvariantCulture),
                value.ToString(CultureInfo.InvariantCulture));
            return new PlayModeTuningWrite(resolved.Snapshot.Properties[0].Record, encoded);
        }

        private static int ReadInteger(PlayModeTuningGatewayFixtureComponent component, string propertyPath)
        {
            var serialized = new SerializedObject(component);
            serialized.UpdateIfRequiredOrScript();
            var property = serialized.FindProperty(propertyPath);
            Assert.That(property, Is.Not.Null, "検査対象の整数項目が見つかりません：" + propertyPath);
            return property.intValue;
        }

        private bool OwnsTemporaryFolder()
        {
            if (!temporaryFolderCreated || string.IsNullOrEmpty(temporaryFolderGuid))
                return false;
            if (!temporaryFolderPath.StartsWith(TemporaryFolderPrefix, StringComparison.Ordinal))
                return false;
            var suffix = temporaryFolderPath.Substring(TemporaryFolderPrefix.Length);
            if (!Guid.TryParseExact(suffix, "N", out _))
                return false;
            return StringComparer.Ordinal.Equals(AssetDatabase.AssetPathToGUID(temporaryFolderPath), temporaryFolderGuid);
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null, "Unityプロジェクトのルートを解決できませんでした。");
            return Path.GetFullPath(Path.Combine(projectRoot.FullName, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
