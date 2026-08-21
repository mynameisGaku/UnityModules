using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReferenceFinder.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class AssetReferenceFinderTests
    {
        private const string TestRoot = "Assets/ReferenceFinderGeneratedTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "ReferenceFinderGeneratedTests");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void FindDirectReferences_ReturnsOnlyDirectReferencesInOrdinalOrder()
        {
            var target = CreateAsset("Target.asset", null);
            var zReference = CreateAsset("ZReference.asset", target);
            var aReference = CreateAsset("AReference.asset", target);
            CreateAsset("IndirectReference.asset", zReference);
            CreateAsset("Unrelated.asset", null);
            AssetDatabase.SaveAssets();

            var result = AssetReferenceFinder.FindDirectReferences(
                AssetDatabase.GetAssetPath(target),
                new[] { TestRoot });

            Assert.That(result.WasCanceled, Is.False);
            Assert.That(result.SearchMode, Is.EqualTo(AssetReferenceSearchMode.Direct));
            Assert.That(result.FailedAssetPaths, Is.Empty);
            Assert.That(result.ReferenceAssetPaths, Is.EqualTo(new[]
            {
                $"{TestRoot}/AReference.asset",
                $"{TestRoot}/ZReference.asset"
            }));
            Assert.That(result.ScannedAssetCount, Is.EqualTo(result.CandidateAssetCount));
        }

        [Test]
        public void FindReferences_Recursive_IncludesDirectAndTransitiveReferences()
        {
            var target = CreateAsset("Target.asset", null);
            var direct = CreateAsset("Direct.asset", target);
            CreateAsset("Root.asset", direct);
            CreateAsset("Unrelated.asset", null);
            AssetDatabase.SaveAssets();

            var result = AssetReferenceFinder.FindReferences(
                AssetDatabase.GetAssetPath(target),
                AssetReferenceSearchMode.Recursive,
                new[] { TestRoot });

            Assert.That(result.SearchMode, Is.EqualTo(AssetReferenceSearchMode.Recursive));
            Assert.That(result.ReferenceAssetPaths, Is.EqualTo(new[]
            {
                $"{TestRoot}/Direct.asset",
                $"{TestRoot}/Root.asset"
            }));
        }

        [Test]
        public void FindReferences_SearchFolder_ExcludesReferencesOutsideRoot()
        {
            AssetDatabase.CreateFolder(TestRoot, "Included");
            AssetDatabase.CreateFolder(TestRoot, "Excluded");
            var target = CreateAsset("Target.asset", null);
            CreateAssetAtPath($"{TestRoot}/Included/Inside.asset", target);
            CreateAssetAtPath($"{TestRoot}/Excluded/Outside.asset", target);
            AssetDatabase.SaveAssets();

            var result = AssetReferenceFinder.FindReferences(
                AssetDatabase.GetAssetPath(target),
                AssetReferenceSearchMode.Direct,
                new[] { $"{TestRoot}/Included" });

            Assert.That(result.ReferenceAssetPaths, Is.EqualTo(new[]
            {
                $"{TestRoot}/Included/Inside.asset"
            }));
        }

        [Test]
        public void FindDirectReferences_ObjectOverload_UsesPersistentAsset()
        {
            var target = CreateAsset("Target.asset", null);
            CreateAsset("Reference.asset", target);
            AssetDatabase.SaveAssets();

            var result = AssetReferenceFinder.FindDirectReferences(target);

            Assert.That(result.TargetAssetPath, Is.EqualTo($"{TestRoot}/Target.asset"));
            Assert.That(result.ReferenceAssetPaths, Does.Contain($"{TestRoot}/Reference.asset"));
        }

        [Test]
        public void FindDirectReferences_InvalidTarget_ThrowsWithoutScanning()
        {
            Assert.That(
                () => AssetReferenceFinder.FindDirectReferences(string.Empty, new[] { TestRoot }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => AssetReferenceFinder.FindDirectReferences(TestRoot, new[] { TestRoot }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FindReferences_InvalidMode_ThrowsWithoutScanning()
        {
            var target = CreateAsset("Target.asset", null);
            AssetDatabase.SaveAssets();

            Assert.That(
                () => AssetReferenceFinder.FindReferences(
                    AssetDatabase.GetAssetPath(target),
                    (AssetReferenceSearchMode)999,
                    new[] { TestRoot }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void NormalizeSearchFolders_RemovesDuplicatesAndNestedFolders()
        {
            AssetDatabase.CreateFolder(TestRoot, "Nested");

            var result = AssetReferenceFinder.NormalizeSearchFolders(new[]
            {
                $"{TestRoot}/Nested/",
                TestRoot,
                TestRoot.Replace('/', '\\')
            });

            Assert.That(result, Is.EqualTo(new[] { TestRoot }));
        }

        [Test]
        public void NormalizeSearchFolders_RejectsPackageAndMissingFolders()
        {
            Assert.That(
                () => AssetReferenceFinder.NormalizeSearchFolders(new[] { "Packages" }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => AssetReferenceFinder.NormalizeSearchFolders(new[] { "Assets/MissingReferenceFinderFolder" }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FindDirectReferences_CanceledBeforeFirstCandidate_ReturnsEmptyPartialResult()
        {
            var target = CreateAsset("Target.asset", null);
            CreateAsset("Reference.asset", target);
            AssetDatabase.SaveAssets();

            var result = AssetReferenceFinder.FindDirectReferencesInternal(
                AssetDatabase.GetAssetPath(target),
                new[] { TestRoot },
                (_, _, _) => false);

            Assert.That(result.WasCanceled, Is.True);
            Assert.That(result.ScannedAssetCount, Is.Zero);
            Assert.That(result.ReferenceAssetPaths, Is.Empty);
        }

        [Test]
        public void ResolveSelectionFolder_AssetSelection_ReturnsContainingAssetsFolder()
        {
            var target = CreateAsset("Target.asset", null);
            AssetDatabase.SaveAssets();

            var folder = ReferenceFinderWindow.ResolveSelectionFolder(target);

            Assert.That(AssetDatabase.GetAssetPath(folder), Is.EqualTo(TestRoot));
        }

        [Test]
        public void EditorAssembly_ExportsOnlySearchApiTypes()
        {
            var exported = typeof(AssetReferenceFinder).Assembly.GetExportedTypes()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                typeof(AssetBatchRenamer),
                typeof(AssetReferenceFinder),
                typeof(AssetReferenceOccurrence),
                typeof(AssetReferenceReplacer),
                typeof(AssetReferenceReplacementPlan),
                typeof(AssetReferenceReplacementResult),
                typeof(AssetRenameEntry),
                typeof(AssetRenamePlan),
                typeof(AssetRenameResult),
                typeof(AssetReferenceSearchMode),
                typeof(AssetReferenceSearchResult)
            }.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray()));
        }

        [Test]
        public void Preview_ReturnsExactSerializedPropertyWithoutMutation()
        {
            var target = CreateAsset("Target.asset", null);
            var replacement = CreateAsset("Replacement.asset", null);
            var owner = CreateAsset("Owner.asset", target);
            AssetDatabase.SaveAssets();

            var plan = AssetReferenceReplacer.Preview(target, replacement, new[] { TestRoot });

            Assert.That(plan.TargetAssetPath, Is.EqualTo($"{TestRoot}/Target.asset"));
            Assert.That(plan.ReplacementAssetPath, Is.EqualTo($"{TestRoot}/Replacement.asset"));
            Assert.That(plan.FailedAssetPaths, Is.Empty);
            Assert.That(plan.UnsupportedAssetPaths, Is.Empty);
            Assert.That(plan.Occurrences, Has.Count.EqualTo(1));
            Assert.That(plan.Occurrences[0].AssetPath, Is.EqualTo($"{TestRoot}/Owner.asset"));
            Assert.That(plan.Occurrences[0].OwnerName, Is.EqualTo("Owner"));
            Assert.That(plan.Occurrences[0].OwnerTypeName, Does.EndWith("ReferenceFinderTestAsset"));
            Assert.That(plan.Occurrences[0].PropertyPath, Is.EqualTo("_reference"));
            Assert.That(owner.Reference, Is.SameAs(target));
        }

        [Test]
        public void Apply_ReplacesPreviewedReferenceAndSupportsUndo()
        {
            var target = CreateAsset("Target.asset", null);
            var replacement = CreateAsset("Replacement.asset", null);
            var owner = CreateAsset("Owner.asset", target);
            AssetDatabase.SaveAssets();
            var plan = AssetReferenceReplacer.Preview(target, replacement, new[] { TestRoot });

            var result = AssetReferenceReplacer.Apply(plan);

            Assert.That(result.ReplacedReferenceCount, Is.EqualTo(1));
            Assert.That(result.ChangedAssetPaths, Is.EqualTo(new[] { $"{TestRoot}/Owner.asset" }));
            Assert.That(owner.Reference, Is.SameAs(replacement));

            Undo.PerformUndo();
            Assert.That(owner.Reference, Is.SameAs(target));
        }

        [Test]
        public void Apply_StalePreviewThrowsBeforeChangingAnyReference()
        {
            var target = CreateAsset("Target.asset", null);
            var replacement = CreateAsset("Replacement.asset", null);
            var firstOwner = CreateAsset("AOwner.asset", target);
            var staleOwner = CreateAsset("ZOwner.asset", target);
            AssetDatabase.SaveAssets();
            var plan = AssetReferenceReplacer.Preview(target, replacement, new[] { TestRoot });
            staleOwner.Reference = null;
            EditorUtility.SetDirty(staleOwner);
            AssetDatabase.SaveAssets();

            Assert.That(
                () => AssetReferenceReplacer.Apply(plan),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(firstOwner.Reference, Is.SameAs(target));
            Assert.That(staleOwner.Reference, Is.Null);
        }

        [Test]
        public void Preview_DifferentConcreteTypesThrowsWithoutMutation()
        {
            var target = CreateAsset("Target.asset", null);
            var texture = new Texture2D(1, 1);
            AssetDatabase.CreateAsset(texture, $"{TestRoot}/Replacement.asset");
            AssetDatabase.SaveAssets();

            Assert.That(
                () => AssetReferenceReplacer.Preview(target, texture, new[] { TestRoot }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Preview_SceneReferenceIsReportedAsUnsupported()
        {
            var target = CreatePrefab("Target.prefab");
            var replacement = CreatePrefab("Replacement.prefab");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(target, scene);
            var scenePath = $"{TestRoot}/Owner.unity";
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            AssetDatabase.SaveAssets();

            var plan = AssetReferenceReplacer.Preview(target, replacement, new[] { TestRoot });

            Assert.That(plan.Occurrences, Is.Empty);
            Assert.That(plan.FailedAssetPaths, Is.Empty);
            Assert.That(plan.UnsupportedAssetPaths, Is.EqualTo(new[] { scenePath }));
        }

        private static ReferenceFinderTestAsset CreateAsset(string fileName, UnityEngine.Object reference)
        {
            return CreateAssetAtPath($"{TestRoot}/{fileName}", reference);
        }

        private static ReferenceFinderTestAsset CreateAssetAtPath(string path, UnityEngine.Object reference)
        {
            var asset = ScriptableObject.CreateInstance<ReferenceFinderTestAsset>();
            asset.Reference = reference;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject CreatePrefab(string fileName)
        {
            var instance = new GameObject(Path.GetFileNameWithoutExtension(fileName));
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(instance, $"{TestRoot}/{fileName}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

    }
}
