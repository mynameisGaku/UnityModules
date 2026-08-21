using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AdaptiveLayout.Tests
{
    public sealed class SafeAreaMathTests
    {
        [Test]
        public void TryCreateSnapshot_ValidArea_ExposesEveryInset()
        {
            var created = SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(100f, 40f, 750f, 420f), out var snapshot);

            Assert.That(created, Is.True);
            Assert.That(snapshot.ScreenSize, Is.EqualTo(new Vector2Int(1000, 500)));
            Assert.That(snapshot.LeftInset, Is.EqualTo(100f));
            Assert.That(snapshot.TopInset, Is.EqualTo(40f));
            Assert.That(snapshot.RightInset, Is.EqualTo(150f));
            Assert.That(snapshot.BottomInset, Is.EqualTo(40f));
            Assert.That(snapshot.IsFullViewport, Is.False);
        }

        [Test]
        public void TryCreateSnapshot_FullViewport_IsFullViewport()
        {
            Assert.That(SafeAreaMath.TryCreateSnapshot(640, 360, new Rect(0f, 0f, 640f, 360f), out var snapshot), Is.True);
            Assert.That(snapshot.IsFullViewport, Is.True);
        }

        [TestCase(0, 500)]
        [TestCase(1000, 0)]
        [TestCase(-1, 500)]
        public void TryCreateSnapshot_InvalidScreenSize_Fails(int width, int height)
        {
            Assert.That(SafeAreaMath.TryCreateSnapshot(width, height, new Rect(0f, 0f, 1f, 1f), out _), Is.False);
        }

        [Test]
        public void TryCreateSnapshot_NonFiniteArea_Fails()
        {
            Assert.That(SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(float.NaN, 0f, 100f, 100f), out _), Is.False);
            Assert.That(SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(0f, 0f, float.PositiveInfinity, 100f), out _), Is.False);
        }

        [TestCase(-1f, 0f, 100f, 100f)]
        [TestCase(0f, -1f, 100f, 100f)]
        [TestCase(0f, 0f, 0f, 100f)]
        [TestCase(0f, 0f, 100f, 0f)]
        [TestCase(900f, 0f, 101f, 100f)]
        [TestCase(0f, 450f, 100f, 51f)]
        public void TryCreateSnapshot_OutOfBoundsOrEmptyArea_Fails(float x, float y, float width, float height)
        {
            Assert.That(SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(x, y, width, height), out _), Is.False);
        }

        [Test]
        public void GetNormalizedRect_AllEdges_UsesSafeArea()
        {
            SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(100f, 50f, 800f, 400f), out var snapshot);

            var result = SafeAreaMath.GetNormalizedRect(snapshot, SafeAreaEdges.All);

            Assert.That(result.min, Is.EqualTo(new Vector2(0.1f, 0.1f)));
            Assert.That(result.max, Is.EqualTo(new Vector2(0.9f, 0.9f)));
        }

        [Test]
        public void GetNormalizedRect_SelectedEdges_LeavesOtherEdgesAtViewport()
        {
            SafeAreaMath.TryCreateSnapshot(1000, 500, new Rect(100f, 50f, 800f, 400f), out var snapshot);

            var result = SafeAreaMath.GetNormalizedRect(snapshot, SafeAreaEdges.Left | SafeAreaEdges.Top);

            Assert.That(result.min, Is.EqualTo(new Vector2(0.1f, 0f)));
            Assert.That(result.max, Is.EqualTo(new Vector2(1f, 0.9f)));
        }

        [Test]
        public void RuntimeAssembly_ExportsOnlyFourContractTypes()
        {
            var exported = typeof(SafeAreaSnapshot).Assembly.GetExportedTypes().OrderBy(type => type.FullName).ToArray();
            var expected = new[]
            {
                typeof(SafeAreaEdges),
                typeof(SafeAreaRectTransform),
                typeof(SafeAreaSnapshot),
                typeof(SafeAreaVisualElement)
            }.OrderBy(type => type.FullName).ToArray();

            Assert.That(exported, Is.EqualTo(expected));
        }
    }
}
