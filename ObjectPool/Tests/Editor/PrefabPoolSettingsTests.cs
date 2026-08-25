// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;

namespace ObjectPool.Editor.Tests
{
    /// <summary>既定値、全field比較による等価性、不正値の検証を確認する。</summary>
    internal sealed class PrefabPoolSettingsTests
    {
        [Test]
        public void Default_HasDocumentedValues()
        {
            var settings = PrefabPoolSettings.Default;

            Assert.That(settings.MaximumActiveCount, Is.EqualTo(0), "0はアクティブ無制限。");
            Assert.That(settings.MaximumIdleCount, Is.EqualTo(128));
            Assert.That(settings.InitialPreloadCount, Is.EqualTo(0));
            Assert.That(settings.ReuseOrder, Is.EqualTo(PoolReuseOrder.Lifo));

            Assert.That(new PrefabPoolSettings(), Is.EqualTo(settings), "引数なしconstructorもDefaultと一致する。");
        }

        [Test]
        public void EqualSettings_AreEqualAcrossEqualsOperatorAndHash()
        {
            var left = new PrefabPoolSettings(10, 20, 3, PoolReuseOrder.Fifo);
            var right = new PrefabPoolSettings(10, 20, 3, PoolReuseOrder.Fifo);

            Assert.That(left == right, Is.True);
            Assert.That(left != right, Is.False);
            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.Equals((object)right), Is.True);
            Assert.That(right.Equals(left), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left, Is.EqualTo(right));
        }

        [Test]
        public void DifferentField_BreaksEquality()
        {
            var baseSettings = new PrefabPoolSettings(10, 20, 3, PoolReuseOrder.Lifo);

            Assert.That(baseSettings == new PrefabPoolSettings(11, 20, 3, PoolReuseOrder.Lifo), Is.False);
            Assert.That(baseSettings == new PrefabPoolSettings(10, 21, 3, PoolReuseOrder.Lifo), Is.False);
            Assert.That(baseSettings == new PrefabPoolSettings(10, 20, 4, PoolReuseOrder.Lifo), Is.False);
            Assert.That(baseSettings == new PrefabPoolSettings(10, 20, 3, PoolReuseOrder.Fifo), Is.False);
            Assert.That(baseSettings != new PrefabPoolSettings(11, 20, 3, PoolReuseOrder.Lifo), Is.True);
        }

        [Test]
        public void NullComparison_FollowsValueSemantics()
        {
            var settings = new PrefabPoolSettings();

            Assert.That(settings == null, Is.False);
            Assert.That(null == settings, Is.False);
            Assert.That(settings != null, Is.True);
            Assert.That(settings.Equals(null), Is.False);

            PrefabPoolSettings nothing = null;
            Assert.That(nothing == null, Is.True);
            Assert.That(nothing == settings, Is.False);
        }

        [TestCase(-1, 0, 0)]
        [TestCase(-100, 8, 0)]
        public void Constructor_RejectsNegativeMaximumActiveCount(int maximumActiveCount, int maximumIdleCount, int initialPreloadCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrefabPoolSettings(maximumActiveCount, maximumIdleCount, initialPreloadCount, PoolReuseOrder.Lifo));
        }

        [Test]
        public void Constructor_RejectsNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrefabPoolSettings(0, -1, 0, PoolReuseOrder.Lifo));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrefabPoolSettings(0, 8, -1, PoolReuseOrder.Lifo));
        }

        [Test]
        public void Constructor_RejectsUndefinedReuseOrder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PrefabPoolSettings(0, 8, 0, (PoolReuseOrder)999));
        }

        [Test]
        public void Constructor_AcceptsZeroIdleAndZeroActive()
        {
            var settings = new PrefabPoolSettings(0, 0, 0, PoolReuseOrder.Fifo);

            Assert.That(settings.MaximumActiveCount, Is.EqualTo(0));
            Assert.That(settings.MaximumIdleCount, Is.EqualTo(0));
            Assert.That(settings.ReuseOrder, Is.EqualTo(PoolReuseOrder.Fifo));
            Assert.That(
                settings.ToString(),
                Does.Contain("MaximumActiveCount=0").And.Contain("ReuseOrder=Fifo"));
        }
    }
}
