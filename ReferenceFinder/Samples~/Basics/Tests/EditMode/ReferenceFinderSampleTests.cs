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

        [Test]
        public void ImportedSample_TargetHasExactlyOneDirectExampleReference()
        {
            var targetPath = AssetDatabase.GUIDToAssetPath(TargetGuid);
            var ownerPath = AssetDatabase.GUIDToAssetPath(OwnerGuid);
            Assert.That(targetPath, Is.Not.Empty);
            Assert.That(ownerPath, Is.Not.Empty);

            var folder = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            var result = AssetReferenceFinder.FindDirectReferences(targetPath, new[] { folder });

            Assert.That(result.FailedAssetPaths, Is.Empty);
            Assert.That(result.ReferenceAssetPaths, Is.EqualTo(new[] { ownerPath }));
        }
    }
}
