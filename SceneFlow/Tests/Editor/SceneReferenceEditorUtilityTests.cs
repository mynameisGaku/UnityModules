using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneFlow.Editor.Tests
{
    /// <summary>SceneReferenceのGUID修復とBuild Profile登録判定を検証する。</summary>
    public sealed class SceneReferenceEditorUtilityTests
    {
        private string _temporaryFolder;
        private string _scenePath;

        /// <summary>テスト用Sceneを保存する一時フォルダーを作る。</summary>
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _temporaryFolder = AssetDatabase.GenerateUniqueAssetPath("Assets/SceneFlowEditorTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryFolder));
            _scenePath = _temporaryFolder + "/Target.unity";
        }

        /// <summary>一時Sceneとフォルダーを削除する。</summary>
        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_temporaryFolder)) AssetDatabase.DeleteAsset(_temporaryFolder);
        }

        /// <summary>GUIDが有効なら、古いパスよりGUIDから得た現在パスを優先する。</summary>
        [Test]
        public void TryResolve_PrefersGuidAndRepairsMovedPath()
        {
            CreateSceneAsset(_scenePath);
            var guid = AssetDatabase.AssetPathToGUID(_scenePath);

            var resolved = SceneReferenceEditorUtility.TryResolve(guid, "Assets/Old/Target.unity", out var asset, out var resolvedGuid, out var resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(asset, Is.Not.Null);
            Assert.That(resolvedGuid, Is.EqualTo(guid));
            Assert.That(resolvedPath, Is.EqualTo(_scenePath));
        }

        /// <summary>GUIDが欠けていても有効なパスから現在GUIDを補う。</summary>
        [Test]
        public void TryResolve_RepairsGuidFromExistingPath()
        {
            CreateSceneAsset(_scenePath);
            var expectedGuid = AssetDatabase.AssetPathToGUID(_scenePath);

            var resolved = SceneReferenceEditorUtility.TryResolve("missing-guid", _scenePath, out _, out var resolvedGuid, out var resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedGuid, Is.EqualTo(expectedGuid));
            Assert.That(resolvedPath, Is.EqualTo(_scenePath));
        }

        /// <summary>大文字小文字だけが異なる保存パスから、AssetDatabaseが返す正規pathを復元する。</summary>
        [Test]
        public void TryResolve_RepairsPathLetterCaseFromGuid()
        {
            CreateSceneAsset(_scenePath);
            var expectedGuid = AssetDatabase.AssetPathToGUID(_scenePath);

            var resolved = SceneReferenceEditorUtility.TryResolve(string.Empty, _scenePath.ToLowerInvariant(), out _, out var resolvedGuid, out var resolvedPath);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedGuid, Is.EqualTo(expectedGuid));
            Assert.That(resolvedPath, Is.EqualTo(_scenePath));
        }

        /// <summary>同名Sceneが別フォルダーにあっても、完全なパスが異なれば登録済みにしない。</summary>
        [Test]
        public void Validate_DoesNotMatchDuplicateShortName()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Other/Target.unity", true),
            };

            var status = SceneReferenceEditorUtility.Validate("Assets/Feature/Target.unity", scenes);

            Assert.That(status, Is.EqualTo(SceneReferenceEditorUtility.ValidationStatus.NotInBuild));
        }

        /// <summary>完全なパスが一致する有効Sceneを利用可能と判定する。</summary>
        [Test]
        public void Validate_AcceptsEnabledExactPath()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(_scenePath, true),
            };

            var status = SceneReferenceEditorUtility.Validate(_scenePath, scenes);

            Assert.That(status, Is.EqualTo(SceneReferenceEditorUtility.ValidationStatus.Valid));
        }

        /// <summary>AssetDatabaseと同様に、完全パスの大文字小文字だけが異なる登録を同じSceneとして扱う。</summary>
        [Test]
        public void Validate_AcceptsPathWithDifferentLetterCase()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("assets/feature/target.unity", true),
            };

            var status = SceneReferenceEditorUtility.Validate("Assets/Feature/Target.unity", scenes);

            Assert.That(status, Is.EqualTo(SceneReferenceEditorUtility.ValidationStatus.Valid));
        }

        /// <summary>完全なパスが一致しても無効なSceneは警告対象にする。</summary>
        [Test]
        public void Validate_ReportsDisabledExactPath()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(_scenePath, false),
            };

            var status = SceneReferenceEditorUtility.Validate(_scenePath, scenes);

            Assert.That(status, Is.EqualTo(SceneReferenceEditorUtility.ValidationStatus.Disabled));
        }

        /// <summary>platform profileでは、明示されたfallback Scene一覧をそのまま使う。</summary>
        [Test]
        public void GetEffectiveScenes_PlatformProfileUsesFallback()
        {
            var fallback = new[]
            {
                new EditorBuildSettingsScene(_scenePath, true),
            };

            var scenes = SceneReferenceEditorUtility.GetEffectiveScenes(null, fallback);

            Assert.That(scenes, Is.SameAs(fallback));
        }

        /// <summary>overrideするBuild Profileでは、profile固有のScene一覧を返す。</summary>
        [Test]
        public void GetEffectiveScenes_OverrideProfileUsesOwnScenes()
        {
            CreateSceneAsset(_scenePath);
            var profile = CreateProfile(true, new EditorBuildSettingsScene(_scenePath, false));

            var profileScenes = profile.GetScenesForBuild();
            var scenes = SceneReferenceEditorUtility.GetEffectiveScenes(profile, Array.Empty<EditorBuildSettingsScene>());

            Assert.That(profileScenes, Has.Length.EqualTo(1));
            Assert.That(profileScenes[0].path, Is.EqualTo(_scenePath));
            Assert.That(profileScenes[0].enabled, Is.False);
            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].path, Is.EqualTo(_scenePath));
            Assert.That(scenes[0].enabled, Is.False);
        }

        /// <summary>overrideしないBuild Profileでは、現在のglobal Scene一覧を返す。</summary>
        [Test]
        public void GetEffectiveScenes_InheritedProfileUsesGlobalScenes()
        {
            var originalGlobalScenes = EditorBuildSettings.globalScenes;
            try
            {
                var globalScene = new EditorBuildSettingsScene(_scenePath, true);
                EditorBuildSettings.globalScenes = new[] { globalScene };
                var profile = CreateProfile(false);

                var profileScenes = profile.GetScenesForBuild();
                var scenes = SceneReferenceEditorUtility.GetEffectiveScenes(profile, Array.Empty<EditorBuildSettingsScene>());

                Assert.That(profileScenes, Has.Length.EqualTo(1));
                Assert.That(profileScenes[0].path, Is.EqualTo(_scenePath));
                Assert.That(profileScenes[0].enabled, Is.True);
                Assert.That(scenes, Has.Length.EqualTo(1));
                Assert.That(scenes[0].path, Is.EqualTo(_scenePath));
                Assert.That(scenes[0].enabled, Is.True);
            }
            finally
            {
                EditorBuildSettings.globalScenes = originalGlobalScenes;
            }
        }

        /// <summary>GUIDとパスのどちらも解決できなければ参照修復を行わない。</summary>
        [Test]
        public void TryResolve_RejectsMissingScene()
        {
            var resolved = SceneReferenceEditorUtility.TryResolve("missing-guid", "Assets/Missing/Target.unity", out var asset, out _, out _);

            Assert.That(resolved, Is.False);
            Assert.That(asset, Is.Null);
        }

        /// <summary>未登録状態で最初にBackground threadから生成しても、そのthreadをメインとして登録しない。</summary>
        [Test]
        public async Task MainThreadBinding_BackgroundFirstConstructionDoesNotBind()
        {
            ResetMainThreadBinding();
            try
            {
                var exception = await Task.Run(() =>
                {
                    try
                    {
                        _ = new SceneFlowService();
                        return null;
                    }
                    catch (System.Exception caught)
                    {
                        return caught;
                    }
                });

                Assert.That(exception, Is.TypeOf<System.InvalidOperationException>());
                Assert.That(GetBoundThreadId(), Is.Zero);
            }
            finally
            {
                SceneFlowEditorMainThread.Bind();
            }
        }

        /// <summary>Editorのmain-thread callbackから登録した後は標準サービスを生成できる。</summary>
        [Test]
        public void MainThreadBinding_EditorCallbackAllowsConstruction()
        {
            ResetMainThreadBinding();
            try
            {
                SceneFlowEditorMainThread.Bind();

                Assert.That(GetBoundThreadId(), Is.Not.Zero);
                Assert.DoesNotThrow(() => _ = new SceneFlowService());
            }
            finally
            {
                SceneFlowEditorMainThread.Bind();
            }
        }

        /// <summary>Editor実装型を利用者向けの互換対象として公開しない。</summary>
        [Test]
        public void EditorAssembly_DoesNotExportImplementationTypes()
        {
            Assert.That(typeof(SceneReferenceEditorUtility).Assembly.GetExportedTypes(), Is.Empty);
        }

        private static void CreateSceneAsset(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private BuildProfile CreateProfile(bool overrideGlobalScenes, params EditorBuildSettingsScene[] scenes)
        {
            var profile = ScriptableObject.CreateInstance<BuildProfile>();
            profile.overrideGlobalScenes = overrideGlobalScenes;
            profile.scenes = scenes ?? Array.Empty<EditorBuildSettingsScene>();
            AssetDatabase.CreateAsset(profile, _temporaryFolder + "/Profile.asset");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void ResetMainThreadBinding()
        {
            GetMainThreadField("_threadId").SetValue(null, 0);
        }

        private static int GetBoundThreadId()
        {
            return (int)GetMainThreadField("_threadId").GetValue(null);
        }

        private static FieldInfo GetMainThreadField(string name)
        {
            var mainThreadType = typeof(SceneFlowService).Assembly.GetType("SceneFlow.SceneFlowMainThread", true);
            return mainThreadType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        }
    }
}
