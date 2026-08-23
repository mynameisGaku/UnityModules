using System;
using BuildAssistant.Editor;
using NUnit.Framework;

namespace BuildAssistant.Tests
{
    public sealed class SizeAggregatorTests
    {
        [Test]
        public void Aggregate_SumsEveryOccurrenceAndOrdersByBytesThenOrdinalKey()
        {
            var rows = new[]
            {
                new PackedAssetRow("Assets/B.asset", "Type.Z", 30),
                new PackedAssetRow("Assets/A.asset", "Type.A", 20),
                new PackedAssetRow("Assets/A.asset", "Type.Z", 10),
                new PackedAssetRow("Assets/C.asset", "Type.A", 30)
            };

            var result = SizeAggregator.Aggregate(rows, new ulong[] { 7, 5 });

            Assert.That(result.PackedContentBytes, Is.EqualTo(90));
            Assert.That(result.PackedOverheadBytes, Is.EqualTo(12));
            Assert.That(result.Assets[0].AssetPath, Is.EqualTo("Assets/A.asset"));
            Assert.That(result.Assets[0].PackedBytes, Is.EqualTo(30));
            Assert.That(result.Assets[0].OccurrenceCount, Is.EqualTo(2));
            Assert.That(result.Assets[1].AssetPath, Is.EqualTo("Assets/B.asset"));
            Assert.That(result.Assets[2].AssetPath, Is.EqualTo("Assets/C.asset"));
            Assert.That(result.Types[0].TypeName, Is.EqualTo("Type.A"));
            Assert.That(result.Types[0].PackedBytes, Is.EqualTo(50));
            Assert.That(result.Types[0].AssetCount, Is.EqualTo(2));
            Assert.That(result.Types[1].TypeName, Is.EqualTo("Type.Z"));
        }

        [Test]
        public void Aggregate_ThrowsInsteadOfWrappingPackedContent()
        {
            var rows = new[] { new PackedAssetRow("A", "T", ulong.MaxValue), new PackedAssetRow("B", "T", 1) };

            Assert.Throws<OverflowException>(() => SizeAggregator.Aggregate(rows, Array.Empty<ulong>()));
            Assert.Throws<OverflowException>(() => SizeAggregator.Aggregate(Array.Empty<PackedAssetRow>(), new ulong[] { ulong.MaxValue, 1 }));
        }
    }
}
