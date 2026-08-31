// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 独立した構造差分画面のメニュー、結果保持、古い結果への案内を検証します。
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardPrefabOverrideReviewWindowTests
    {
        private BuildGuardPrefabOverrideTestFixture _fixture;
        private EditorBuildSettingsScene[] _originalBuildScenes;
        private BuildGuardPrefabOverrideReviewWindow _window;

        [SetUp]
        public void SetUp()
        {
            _fixture = new BuildGuardPrefabOverrideTestFixture();
            _fixture.SetUp();
            _originalBuildScenes = EditorBuildSettings.scenes;
            _window = ScriptableObject.CreateInstance<BuildGuardPrefabOverrideReviewWindow>();
            Selection.activeObject = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                UnityEngine.Object.DestroyImmediate(_window);
            }

            EditorBuildSettings.scenes = _originalBuildScenes;
            Selection.activeObject = null;
            _fixture?.TearDown();
        }

        [Test]
        public void Menu_UsesDedicatedReviewPathAndPriority()
        {
            var method = typeof(BuildGuardPrefabOverrideReviewWindow).GetMethod(
                "ShowWindow",
                BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = method?.GetCustomAttribute<MenuItem>();

            Assert.That(BuildGuardPrefabOverrideReviewWindow.MenuPath, Is.EqualTo(
                "Tools/ビルドガード/プレハブ構造差分を確認"));
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.menuItem, Is.EqualTo(BuildGuardPrefabOverrideReviewWindow.MenuPath));
            Assert.That(attribute.priority, Is.EqualTo(2002));
            Assert.That(BuildGuardPrefabOverrideReviewWindow.MaximumDisplayedFindings, Is.EqualTo(1000));
        }

        [Test]
        public void RunScan_NoEnabledScenes_ShowsConfigurationGuidance()
        {
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

            _window.RunScan();

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "現在のビルドプロファイルに、有効なビルド対象シーンがありません。"));
        }

        [Test]
        public void RunScan_EnabledScene_CapturesReviewOnlySnapshot()
        {
            var prefabPath = _fixture.CreatePrefab("WindowReview.prefab");
            var scene = _fixture.CreateSavedScene("WindowReview.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };

            _window.RunScan();

            Assert.That(_window.FindingCount, Is.EqualTo(1));
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "シーンを1件検査し、1件のプレハブ構造差分を見つけました。"));
            Assert.That(
                _window.GetFinding(0).Kind,
                Is.EqualTo(BuildGuardPrefabOverrideKind.AddedComponent));
        }

        [Test]
        public void RunScan_CancelledAfterFirstScene_DiscardsPartialWindowState()
        {
            var prefabPath = _fixture.CreatePrefab("WindowCancel.prefab");
            var firstScene = _fixture.CreateSavedScene("WindowFirst.unity");
            _fixture.InstantiatePrefab(prefabPath, firstScene).AddComponent<BoxCollider>();
            var secondScene = _fixture.CreateSavedScene("WindowSecond.unity");
            _fixture.InstantiatePrefab(prefabPath, secondScene).AddComponent<SphereCollider>();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(firstScene.path, true),
                new EditorBuildSettingsScene(secondScene.path, true),
            };

            _window.RunScan((index, _, _) => index == 1);

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "シーンを1件検査した時点で中止しました。途中結果は破棄しました。"));
        }

        [Test]
        public void LocateFinding_ChangedSnapshot_ShowsStaleGuidanceWithoutRemovingSnapshot()
        {
            var prefabPath = _fixture.CreatePrefab("WindowStale.prefab");
            var scene = _fixture.CreateSavedScene("WindowStale.unity");
            var instance = _fixture.InstantiatePrefab(prefabPath, scene);
            var addedComponent = instance.AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            _window.RunScan();
            Assert.That(_window.FindingCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(addedComponent);
            var wasDirty = scene.isDirty;

            var outcome = _window.LocateFinding(0);

            Assert.That(outcome, Is.EqualTo(BuildGuardPrefabOverrideNavigationOutcome.Stale));
            Assert.That(_window.FindingCount, Is.EqualTo(1));
            Assert.That(_window.StatusText, Is.EqualTo(
                "プレハブ構造差分が変化したため、この結果は古くなっています。「更新して検査」を押してください。"));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void ClearResults_AfterSnapshot_RemovesWindowStateOnly()
        {
            var prefabPath = _fixture.CreatePrefab("WindowClear.prefab");
            var scene = _fixture.CreateSavedScene("WindowClear.unity");
            _fixture.InstantiatePrefab(prefabPath, scene).AddComponent<BoxCollider>();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
            _window.RunScan();
            var wasDirty = scene.isDirty;

            _window.ClearResults();

            Assert.That(_window.FindingCount, Is.Zero);
            Assert.That(_window.FailureCount, Is.Zero);
            Assert.That(_window.StatusText, Is.EqualTo(
                "結果を消去しました。「更新して検査」を押すと、最新の結果を作成できます。"));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void JapaneseMenuAndVisibleLabels_AreConfiguredExactly()
        {
            Assert.That(_window.StatusText, Is.EqualTo(
                "「更新して検査」を押すと、ビルド対象として有効なシーンのプレハブ構造差分を確認できます。"));
            Assert.That(GetPrivateStaticField<string>("WindowTitleText"), Is.EqualTo(
                "ビルドガード - プレハブ構造差分"));
            Assert.That(GetPrivateStaticField<string>("HeadingText"), Is.EqualTo(
                "ビルド対象シーンのプレハブ構造差分"));
            Assert.That(GetPrivateStaticField<string>("DescriptionText"), Is.EqualTo(
                "追加または削除されたプレハブ内のゲームオブジェクトとコンポーネントを表示します。プロパティ値の変更は対象外で、結果がプレイヤービルドを停止することはありません。"));
            Assert.That(GetPrivateStaticField<GUIContent>("ScanButtonContent").text, Is.EqualTo(
                "更新して検査"));
            Assert.That(GetPrivateStaticField<GUIContent>("ClearButtonContent").text, Is.EqualTo(
                "結果を消去"));
            Assert.That(GetPrivateStaticField<string>("LocateButtonText"), Is.EqualTo(
                "開いて選択"));
            Assert.That(GetPrivateStaticField<string>("CopyButtonText"), Is.EqualTo(
                "内容をコピー"));
            Assert.That(GetPrivateStaticField<string>("ProgressTitleText"), Is.EqualTo(
                "ビルドガード - プレハブ構造差分"));
            Assert.That(GetPrivateStaticField<Vector2>("MinimumWindowSize"), Is.EqualTo(
                new Vector2(760f, 360f)));
        }

        private static T GetPrivateStaticField<T>(string fieldName)
        {
            var field = typeof(BuildGuardPrefabOverrideReviewWindow).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"対象の内部項目が見つかりません: {fieldName}");
            return (T)field.GetValue(null);
        }
    }
}
