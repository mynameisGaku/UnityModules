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

        [Test]
        public void ImportedSample_TargetHasExactlyOneDirectExampleReference()
        {
            var targetPath = AssetDatabase.GUIDToAssetPath(TargetGuid);
            var ownerPath = AssetDatabase.GUIDToAssetPath(OwnerGuid);
            var rootPath = AssetDatabase.GUIDToAssetPath(RootGuid);
            Assert.That(targetPath, Is.Not.Empty);
            Assert.That(ownerPath, Is.Not.Empty);
            Assert.That(rootPath, Is.Not.Empty);

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
        }
    }
}
