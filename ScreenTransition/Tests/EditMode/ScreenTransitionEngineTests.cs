using System;
using NUnit.Framework;
using UnityEngine;

namespace ScreenTransition.Tests
{
    /// <summary>時間進捗と不透明度計算をUnityの描画状態から分離して検証する。</summary>
    public sealed class ScreenTransitionEngineTests
    {
        /// <summary>全変化曲線が0、0.5、1の既知値を正確に返す。</summary>
        [TestCase(ScreenTransitionEasing.Linear, 0f, 0f)]
        [TestCase(ScreenTransitionEasing.Linear, 0.5f, 0.5f)]
        [TestCase(ScreenTransitionEasing.Linear, 1f, 1f)]
        [TestCase(ScreenTransitionEasing.EaseIn, 0f, 0f)]
        [TestCase(ScreenTransitionEasing.EaseIn, 0.5f, 0.25f)]
        [TestCase(ScreenTransitionEasing.EaseIn, 1f, 1f)]
        [TestCase(ScreenTransitionEasing.EaseOut, 0f, 0f)]
        [TestCase(ScreenTransitionEasing.EaseOut, 0.5f, 0.75f)]
        [TestCase(ScreenTransitionEasing.EaseOut, 1f, 1f)]
        [TestCase(ScreenTransitionEasing.EaseInOut, 0f, 0f)]
        [TestCase(ScreenTransitionEasing.EaseInOut, 0.5f, 0.5f)]
        [TestCase(ScreenTransitionEasing.EaseInOut, 1f, 1f)]
        public void Evaluate_KnownPoint_ReturnsExactValue(ScreenTransitionEasing easing, float progress, float expected)
        {
            Assert.That(ScreenTransitionEasingUtility.Evaluate(easing, progress), Is.EqualTo(expected));
        }

        /// <summary>Coverは時間進捗と不透明度を単調に増やし、要求alphaで完了する。</summary>
        [Test]
        public void Tick_Cover_IncreasesProgressAndOpacityMonotonically()
        {
            var engine = new ScreenTransitionEngine();
            engine.Start(ScreenTransitionRequest.Cover(new Color(0.1f, 0.2f, 0.3f, 0.8f), 1f, ScreenTransitionEasing.Linear));
            var previousProgress = engine.Status.Progress;
            var previousOpacity = engine.Status.Opacity;

            for (var i = 0; i < 4; i++)
            {
                engine.Tick(0.25f);
                Assert.That(engine.Status.Progress, Is.GreaterThanOrEqualTo(previousProgress));
                Assert.That(engine.Status.Opacity, Is.GreaterThanOrEqualTo(previousOpacity));
                previousProgress = engine.Status.Progress;
                previousOpacity = engine.Status.Opacity;
            }

            Assert.That(engine.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Completed));
            Assert.That(engine.Status.Progress, Is.EqualTo(1f));
            Assert.That(engine.Status.Opacity, Is.EqualTo(0.8f).Within(0.00001f));
        }

        /// <summary>Revealは時間進捗を単調に増やしながら不透明度を単調に減らす。</summary>
        [Test]
        public void Tick_Reveal_DecreasesOpacityMonotonically()
        {
            var engine = new ScreenTransitionEngine();
            engine.Start(ScreenTransitionRequest.Reveal(Color.black, 1f, ScreenTransitionEasing.EaseInOut));
            var previousProgress = engine.Status.Progress;
            var previousOpacity = engine.Status.Opacity;

            for (var i = 0; i < 4; i++)
            {
                engine.Tick(0.25f);
                Assert.That(engine.Status.Progress, Is.GreaterThanOrEqualTo(previousProgress));
                Assert.That(engine.Status.Opacity, Is.LessThanOrEqualTo(previousOpacity));
                previousProgress = engine.Status.Progress;
                previousOpacity = engine.Status.Opacity;
            }

            Assert.That(engine.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Completed));
            Assert.That(engine.Status.Opacity, Is.Zero);
        }

        /// <summary>durationが0なら開始時に終端不透明度へ到達し、追加tickを必要としない。</summary>
        [Test]
        public void Start_ZeroDuration_CompletesImmediately()
        {
            var cover = new ScreenTransitionEngine();
            cover.Start(ScreenTransitionRequest.Cover(new Color(1f, 0f, 0f, 0.6f), 0f));
            var reveal = new ScreenTransitionEngine();
            reveal.Start(ScreenTransitionRequest.Reveal(Color.black, 0f));

            Assert.That(cover.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Completed));
            Assert.That(cover.Status.Opacity, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(reveal.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Completed));
            Assert.That(reveal.Status.Opacity, Is.Zero);
        }

        /// <summary>渡されたdeltaだけで進み、timeScale相当の外部値へ依存しない。</summary>
        [Test]
        public void Tick_ExplicitUnscaledDelta_UsesOnlyPassedTime()
        {
            var engine = new ScreenTransitionEngine();
            engine.Start(ScreenTransitionRequest.Cover(Color.black, 2f, ScreenTransitionEasing.Linear));

            engine.Tick(0.5f);

            Assert.That(engine.Status.Progress, Is.EqualTo(0.25f));
            Assert.That(engine.Status.Opacity, Is.EqualTo(0.25f));
        }

        /// <summary>負値、NaN、無限大のdurationをInvalidRequestとして拒否する。</summary>
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(3600.01f)]
        [TestCase(1000000f)]
        public void Validate_InvalidDuration_ReturnsInvalidRequest(float duration)
        {
            var result = ScreenTransitionEngine.Validate(ScreenTransitionRequest.Cover(Color.black, duration));

            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
        }

        /// <summary>最大所要時間を受理し、通常のframe刻みをdoubleで蓄積して完了できる。</summary>
        [Test]
        public void Tick_MaximumDuration_CompletesWithoutFloatAccumulationStall()
        {
            var engine = new ScreenTransitionEngine();
            var request = ScreenTransitionRequest.Cover(Color.black, ScreenTransitionEngine.MaximumDuration, ScreenTransitionEasing.Linear);
            Assert.That(ScreenTransitionEngine.Validate(request).IsSuccess, Is.True);
            engine.Start(request);

            const float delta = 1f / 60f;
            for (var i = 0; i < 216001 && engine.IsActive; i++) engine.Tick(delta);

            Assert.That(engine.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Completed));
            Assert.That(engine.Status.Progress, Is.EqualTo(1f));
            Assert.That(engine.Status.Opacity, Is.EqualTo(1f));
        }

        /// <summary>未定義の操作と変化曲線をInvalidRequestとして拒否する。</summary>
        [Test]
        public void Validate_UndefinedEnums_ReturnsInvalidRequest()
        {
            var operation = ScreenTransitionEngine.Validate(new ScreenTransitionRequest((ScreenTransitionOperation)99, Color.black, 1f));
            var easing = ScreenTransitionEngine.Validate(new ScreenTransitionRequest(ScreenTransitionOperation.Cover, Color.black, 1f, (ScreenTransitionEasing)99));

            Assert.That(operation.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
            Assert.That(easing.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
        }

        /// <summary>非有限色成分と範囲外alphaをInvalidRequestとして拒否する。</summary>
        [Test]
        public void Validate_InvalidColor_ReturnsInvalidRequest()
        {
            var nonFinite = ScreenTransitionEngine.Validate(ScreenTransitionRequest.Cover(new Color(float.NaN, 0f, 0f, 1f), 1f));
            var alphaBelow = ScreenTransitionEngine.Validate(ScreenTransitionRequest.Cover(new Color(0f, 0f, 0f, -0.1f), 1f));
            var alphaAbove = ScreenTransitionEngine.Validate(ScreenTransitionRequest.Cover(new Color(0f, 0f, 0f, 1.1f), 1f));

            Assert.That(nonFinite.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
            Assert.That(alphaBelow.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
            Assert.That(alphaAbove.Error, Is.EqualTo(ScreenTransitionError.InvalidRequest));
        }

        /// <summary>不正なdeltaは状態を進めず例外で呼出側へ知らせる。</summary>
        [Test]
        public void Tick_InvalidDelta_ThrowsWithoutChangingStatus()
        {
            var engine = new ScreenTransitionEngine();
            engine.Start(ScreenTransitionRequest.Cover(Color.black, 1f));
            var before = engine.Status;

            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Tick(float.NaN));
            Assert.That(engine.Status.Progress, Is.EqualTo(before.Progress));
            Assert.That(engine.Status.Opacity, Is.EqualTo(before.Opacity));
        }
    }
}
