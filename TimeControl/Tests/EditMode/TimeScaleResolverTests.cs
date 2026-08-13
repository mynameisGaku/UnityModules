using NUnit.Framework;

namespace TimeControl.Tests
{
    /// <summary>要求倍率の値域、積の上限、最小倍率の決定をUnityのglobal状態から分離して検証する。</summary>
    public sealed class TimeScaleResolverTests
    {
        /// <summary>空の要求は1、複数要求は順序や重複に関係なく最小値を返す。</summary>
        [Test]
        public void ResolveMinimum_EmptyOrderedAndDuplicateInputs_ReturnDeterministicMinimum()
        {
            Assert.That(TimeScaleResolver.ResolveMinimum(System.Array.Empty<float>()), Is.EqualTo(1f));
            Assert.That(TimeScaleResolver.ResolveMinimum(new[] { 2f, 0.5f, 0.5f, 1.5f }), Is.EqualTo(0.5f));
            Assert.That(TimeScaleResolver.ResolveMinimum(new[] { 1.5f, 0.5f, 2f, 0.5f }), Is.EqualTo(0.5f));
        }

        /// <summary>停止倍率0と高速倍率100を境界値として受理する。</summary>
        [Test]
        public void ValidateMultiplier_PauseAndMaximumAtUnitBaseline_AreAccepted()
        {
            var pause = TimeScaleResolver.ValidateMultiplier(1f, 0f, out var pausedScale);
            var maximum = TimeScaleResolver.ValidateMultiplier(1f, 100f, out var maximumScale);

            Assert.That(pause, Is.EqualTo(TimeControlError.None));
            Assert.That(pausedScale, Is.Zero);
            Assert.That(maximum, Is.EqualTo(TimeControlError.None));
            Assert.That(maximumScale, Is.EqualTo(100f));
        }

        /// <summary>負値、NaN、無限大、要求上限超過をInvalidMultiplierとして拒否する。</summary>
        [TestCase(-0.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(100.001f)]
        public void ValidateMultiplier_InvalidRequest_ReturnsInvalidMultiplier(float multiplier)
        {
            var error = TimeScaleResolver.ValidateMultiplier(1f, multiplier, out _);

            Assert.That(error, Is.EqualTo(TimeControlError.InvalidMultiplier));
        }

        /// <summary>別要求の最小倍率で隠れる場合でも、単独適用時に100を超える要求を拒否する。</summary>
        [Test]
        public void ValidateMultiplier_UnmaskedEffectiveScaleExceedsMaximum_ReturnsRangeError()
        {
            var error = TimeScaleResolver.ValidateMultiplier(2f, 50.0001f, out _);

            Assert.That(error, Is.EqualTo(TimeControlError.EffectiveTimeScaleOutOfRange));
        }

        /// <summary>基準値は有限な0以上100以下だけを受理する。</summary>
        [TestCase(0f, TimeControlError.None)]
        [TestCase(100f, TimeControlError.None)]
        [TestCase(-0.01f, TimeControlError.EffectiveTimeScaleOutOfRange)]
        [TestCase(100.01f, TimeControlError.EffectiveTimeScaleOutOfRange)]
        [TestCase(float.NaN, TimeControlError.EffectiveTimeScaleOutOfRange)]
        [TestCase(float.PositiveInfinity, TimeControlError.EffectiveTimeScaleOutOfRange)]
        public void ValidateBaseline_Boundaries_ReturnExpectedError(float baseline, TimeControlError expected)
        {
            Assert.That(TimeScaleResolver.ValidateBaseline(baseline), Is.EqualTo(expected));
        }
    }
}
