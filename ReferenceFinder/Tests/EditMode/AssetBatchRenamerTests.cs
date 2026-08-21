using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReferenceFinder.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class AssetBatchRenamerTests
    {
        private const string TestRoot = "Assets/ReferenceFinderBatchRenameTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "ReferenceFinderBatchRenameTests");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Preview_ComposesRulesInOrdinalSourceOrderWithoutMutation()
        {
            var second = CreateAsset("ZIcon.asset", null);
            var first = CreateAsset("AIcon.asset", null);
            AssetDatabase.SaveAssets();

            var plan = AssetBatchRenamer.Preview(
                new UnityEngine.Object[] { second, first },
                "Icon",
                "Texture",
                "UI_",
                "_v2");

            Assert.That(plan.Entries, Has.Count.EqualTo(2));
            Assert.That(plan.Entries[0].OriginalPath, Is.EqualTo($"{TestRoot}/AIcon.asset"));
            Assert.That(plan.Entries[0].NewPath, Is.EqualTo($"{TestRoot}/UI_ATexture_v2.asset"));
            Assert.That(plan.Entries[1].OriginalPath, Is.EqualTo($"{TestRoot}/ZIcon.asset"));
            Assert.That(plan.Entries[1].NewPath, Is.EqualTo($"{TestRoot}/UI_ZTexture_v2.asset"));
            Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo($"{TestRoot}/AIcon.asset"));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo($"{TestRoot}/ZIcon.asset"));
        }

        [Test]
        public void Preview_DuplicateDestinationsThrowsWithoutMutation()
        {
            var first = CreateAsset("AOld.asset", null);
            var second = CreateAsset("OldA.asset", null);
            AssetDatabase.SaveAssets();

            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new UnityEngine.Object[] { first, second },
                    "Old",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo($"{TestRoot}/AOld.asset"));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo($"{TestRoot}/OldA.asset"));
        }

        [Test]
        public void Preview_ExistingDestinationThrowsWithoutMutation()
        {
            var source = CreateAsset("Source.asset", null);
            CreateAsset("Existing.asset", null);
            AssetDatabase.SaveAssets();

            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new UnityEngine.Object[] { source },
                    "Source",
                    "Existing",
                    string.Empty,
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo($"{TestRoot}/Source.asset"));
        }

        [Test]
        public void Apply_PreservesGuidAndSerializedReference()
        {
            var target = CreateAsset("Target.asset", null);
            var owner = CreateAsset("Owner.asset", target);
            AssetDatabase.SaveAssets();
            var originalGuid = AssetDatabase.AssetPathToGUID($"{TestRoot}/Target.asset");
            var plan = AssetBatchRenamer.Preview(
                new UnityEngine.Object[] { target },
                "Target",
                "Renamed",
                string.Empty,
                string.Empty);

            var result = AssetBatchRenamer.Apply(plan);

            Assert.That(result.RenamedAssetCount, Is.EqualTo(1));
            Assert.That(result.RenamedAssetPaths, Is.EqualTo(new[] { $"{TestRoot}/Renamed.asset" }));
            Assert.That(AssetDatabase.AssetPathToGUID($"{TestRoot}/Renamed.asset"), Is.EqualTo(originalGuid));
            Assert.That(AssetDatabase.GetAssetPath(owner.Reference), Is.EqualTo($"{TestRoot}/Renamed.asset"));
        }

        [Test]
        public void Apply_StalePlanThrowsBeforeRenamingAnyAsset()
        {
            var first = CreateAsset("AFirst.asset", null);
            var stale = CreateAsset("ZStale.asset", null);
            AssetDatabase.SaveAssets();
            var plan = AssetBatchRenamer.Preview(
                new UnityEngine.Object[] { first, stale },
                string.Empty,
                string.Empty,
                "New_",
                string.Empty);
            Assert.That(AssetDatabase.RenameAsset($"{TestRoot}/ZStale.asset", "Moved"), Is.Empty);

            Assert.That(
                () => AssetBatchRenamer.Apply(plan),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo($"{TestRoot}/AFirst.asset"));
            Assert.That(AssetDatabase.AssetPathToGUID($"{TestRoot}/New_AFirst.asset"), Is.Empty);
        }

        [Test]
        public void Preview_RejectsFoldersSubAssetsScriptsAndCaseOnlyChanges()
        {
            var mainAsset = CreateAsset("Main.asset", null);
            var subAsset = ScriptableObject.CreateInstance<ReferenceFinderTestAsset>();
            AssetDatabase.AddObjectToAsset(subAsset, mainAsset);
            AssetDatabase.SaveAssets();
            var folder = AssetDatabase.LoadMainAssetAtPath(TestRoot);
            var temporaryInstance = ScriptableObject.CreateInstance<ReferenceFinderTestAsset>();
            var script = MonoScript.FromScriptableObject(temporaryInstance);
            UnityEngine.Object.DestroyImmediate(temporaryInstance);

            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new[] { folder },
                    string.Empty,
                    string.Empty,
                    "New_",
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(script, Is.TypeOf<MonoScript>());
            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new[] { script },
                    string.Empty,
                    string.Empty,
                    "New_",
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new UnityEngine.Object[] { subAsset },
                    string.Empty,
                    string.Empty,
                    "New_",
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => AssetBatchRenamer.Preview(
                    new UnityEngine.Object[] { mainAsset },
                    "M",
                    "m",
                    string.Empty,
                    string.Empty),
                Throws.TypeOf<ArgumentException>());
        }

        private static ReferenceFinderTestAsset CreateAsset(string fileName, UnityEngine.Object reference)
        {
            var asset = ScriptableObject.CreateInstance<ReferenceFinderTestAsset>();
            asset.Reference = reference;
            AssetDatabase.CreateAsset(asset, $"{TestRoot}/{fileName}");
            return asset;
        }
    }
}
