using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace ReferenceFinder.Samples.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ReferenceFinderSampleTests
    {
        private const string TargetGuid = "b7f10000000000000000000000000031";
        private const string OwnerGuid = "b7f10000000000000000000000000032";
        private const string RootGuid = "b7f10000000000000000000000000033";
        private const string ReplacementGuid = "b7f10000000000000000000000000034";

        [Test]
        public void ImportedSample_TargetHasExactlyOneDirectExampleReference()
        {
            var targetPath = AssetDatabase.GUIDToAssetPath(TargetGuid);
            var ownerPath = AssetDatabase.GUIDToAssetPath(OwnerGuid);
            var rootPath = AssetDatabase.GUIDToAssetPath(RootGuid);
            var replacementPath = AssetDatabase.GUIDToAssetPath(ReplacementGuid);
            Assert.That(targetPath, Is.Not.Empty);
            Assert.That(ownerPath, Is.Not.Empty);
            Assert.That(rootPath, Is.Not.Empty);
            Assert.That(replacementPath, Is.Not.Empty);

            var folder = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            var result = AssetReferenceFinder.FindDirectReferences(targetPath, new[] { folder });

            Assert.That(result.FailedAssetPaths, Is.Empty);
            Assert.That(result.ReferenceAssetPaths, Is.EqualTo(new[] { ownerPath }));

            var recursiveResult = AssetReferenceFinder.FindReferences(
                targetPath,
                AssetReferenceSearchMode.Recursive,
                new[] { folder });
            Assert.That(recursiveResult.FailedAssetPaths, Is.Empty);
            Assert.That(recursiveResult.ReferenceAssetPaths, Is.EqualTo(new[]
            {
                ownerPath,
                rootPath
            }));

            var target = AssetDatabase.LoadMainAssetAtPath(targetPath);
            var replacement = AssetDatabase.LoadMainAssetAtPath(replacementPath);
            var plan = AssetReferenceReplacer.Preview(target, replacement, new[] { folder });
            Assert.That(plan.FailedAssetPaths, Is.Empty);
            Assert.That(plan.UnsupportedAssetPaths, Is.Empty);
            Assert.That(plan.Occurrences, Has.Count.EqualTo(1));
            Assert.That(plan.Occurrences[0].AssetPath, Is.EqualTo(ownerPath));
            Assert.That(plan.Occurrences[0].PropertyPath, Is.EqualTo("_reference"));

            var renamePlan = AssetBatchRenamer.Preview(
                new[] { target, replacement },
                "ReferenceFinderExample",
                "Demo",
                string.Empty,
                string.Empty);
            Assert.That(renamePlan.Entries, Has.Count.EqualTo(2));
            Assert.That(renamePlan.Entries[0].OriginalPath, Is.EqualTo(replacementPath));
            Assert.That(renamePlan.Entries[0].NewPath, Does.EndWith("/DemoReplacement.asset"));
            Assert.That(renamePlan.Entries[1].OriginalPath, Is.EqualTo(targetPath));
            Assert.That(renamePlan.Entries[1].NewPath, Does.EndWith("/DemoTarget.asset"));
            Assert.That(AssetDatabase.GUIDToAssetPath(TargetGuid), Is.EqualTo(targetPath));
            Assert.That(AssetDatabase.GUIDToAssetPath(ReplacementGuid), Is.EqualTo(replacementPath));
        }
    }
}
