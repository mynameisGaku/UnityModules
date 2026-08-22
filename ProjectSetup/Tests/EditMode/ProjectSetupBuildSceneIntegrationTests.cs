// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSetup.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class ProjectSetupBuildSceneIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_ReplacesBuildScenesThenRestoresExactOrderAndState()
        {
            var environment = new UnityProjectSetupEnvironment();
            var original = environment.Capture();
            var testFolder = "Assets/ProjectSetupBuildSceneTests-" + Guid.NewGuid().ToString("N");
            var backupDirectory = Path.Combine(Path.GetTempPath(), "ProjectSetupBuildSceneTests", Guid.NewGuid().ToString("N"));
            var backupPath = Path.Combine(backupDirectory, "backup.json");
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Directory.CreateDirectory(testFolder);
                var bootstrapPath = CreateScene(testFolder, "Bootstrap.unity");
                var gameplayPath = CreateScene(testFolder, "Gameplay.unity");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                profile.SetRecommendedDefaults();
                profile.ConfigureAssetSerialization = false;
                profile.ConfigureVersionControl = false;
                profile.ConfigureBuildScenes = true;
                profile.BuildScenes = new[]
                {
                    new ProjectSetupBuildScene(AssetDatabase.AssetPathToGUID(bootstrapPath), bootstrapPath, true),
                    new ProjectSetupBuildScene(AssetDatabase.AssetPathToGUID(gameplayPath), gameplayPath, false)
                };
                profile.ConfigurePlayModeStartScene = true;
                profile.PlayModeStartScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapPath);
                var service = new ProjectSetupService(environment, new ProjectSetupBackupStore(backupPath));

                var preview = service.Preview(profile);
                var applied = service.Apply(profile);
                var changed = environment.Capture();

                Assert.That(preview.IsValid, Is.True, string.Join(" | ", preview.Errors));
                Assert.That(preview.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.BuildScenes));
                Assert.That(preview.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.PlayModeStartScene));
                Assert.That(applied.Succeeded, Is.True, applied.Message);
                Assert.That(changed.BuildScenes.Select(scene => scene.Path), Is.EqualTo(new[] { bootstrapPath, gameplayPath }));
                Assert.That(changed.BuildScenes.Select(scene => scene.Enabled), Is.EqualTo(new[] { true, false }));
                Assert.That(changed.PlayModeStartSceneGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(bootstrapPath)));
                Assert.That(changed.PlayModeStartScenePath, Is.EqualTo(bootstrapPath));

                var restored = service.RestoreLast();

                Assert.That(restored.Succeeded, Is.True, restored.Message);
                Assert.That(original.Matches(environment.Capture()), Is.True);
                Assert.That(File.Exists(backupPath + ".tmp"), Is.False);
            }
            finally
            {
                try
                {
                    if (original.HasBuildSceneData)
                    {
                        environment.Apply(original);
                    }
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(testFolder);
                    if (Directory.Exists(backupDirectory))
                    {
                        Directory.Delete(backupDirectory, true);
                    }

                    UnityEngine.Object.DestroyImmediate(profile);
                }
            }
        }

        private static string CreateScene(string folder, string fileName)
        {
            var path = folder + "/" + fileName;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            return path;
        }
    }
}
