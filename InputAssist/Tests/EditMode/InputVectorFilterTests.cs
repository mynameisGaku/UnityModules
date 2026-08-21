using NUnit.Framework;
using UnityEngine;

namespace InputAssist.Tests
{
    public sealed class InputVectorFilterTests
    {
        [Test]
        public void Process_LinearWithoutRateLimit_AppliesRadialDeadZone()
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryConfigure(0.2f, 1f, InputResponseMode.Linear, 0f, 0f, 0.25f, InputDirectionMode.EightWay, out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputAssistError.None));

            var neutral = filter.Process(new Vector2(0.2f, 0f), 0.016f);
            var middle = filter.Process(new Vector2(0.6f, 0f), 0.016f);
            var outer = filter.Process(new Vector2(2f, 0f), 0.016f);

            Assert.That(neutral.Succeeded, Is.True);
            Assert.That(neutral.Value, Is.EqualTo(Vector2.zero));
            Assert.That(middle.Value.x, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(middle.Direction, Is.EqualTo(InputDirection.Right));
            Assert.That(outer.Value, Is.EqualTo(Vector2.right));
        }

        [TestCase(InputResponseMode.Linear, 0.5f)]
        [TestCase(InputResponseMode.Squared, 0.25f)]
        [TestCase(InputResponseMode.Cubic, 0.125f)]
        [TestCase(InputResponseMode.SmoothStep, 0.5f)]
        public void Process_ResponseMode_UsesExpectedMagnitude(InputResponseMode mode, float expected)
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryConfigure(0f, 1f, mode, 0f, 0f, 0.1f, InputDirectionMode.EightWay, out _), Is.True);

            var result = filter.Process(new Vector2(0f, 0.5f), 1f);

            Assert.That(result.Value.y, Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void Process_RateLimit_UsesExplicitDeltaTime()
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryConfigure(0f, 1f, InputResponseMode.Linear, 2f, 1f, 0.1f, InputDirectionMode.FourWay, out _), Is.True);

            var rising = filter.Process(Vector2.right, 0.25f);
            var falling = filter.Process(Vector2.zero, 0.25f);

            Assert.That(rising.Value.x, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(falling.Value.x, Is.EqualTo(0.25f).Within(0.00001f));
        }

        [TestCase(0.9f, 0.2f, InputDirection.Right)]
        [TestCase(0.2f, 0.9f, InputDirection.Up)]
        [TestCase(-0.8f, 0.7f, InputDirection.UpLeft)]
        [TestCase(0.8f, -0.7f, InputDirection.DownRight)]
        public void Process_EightWay_ClassifiesDirection(float x, float y, InputDirection expected)
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryConfigure(0f, 1f, InputResponseMode.Linear, 0f, 0f, 0.1f, InputDirectionMode.EightWay, out _), Is.True);

            var result = filter.Process(new Vector2(x, y), 0f);

            Assert.That(result.Direction, Is.EqualTo(expected));
        }

        [Test]
        public void Process_InvalidInput_PreservesState()
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryConfigure(0f, 1f, InputResponseMode.Linear, 0f, 0f, 0.1f, InputDirectionMode.EightWay, out _), Is.True);
            Assert.That(filter.Process(new Vector2(0.5f, 0f), 0f).Succeeded, Is.True);

            var nonFinite = filter.Process(new Vector2(float.NaN, 0f), 0.1f);
            var negativeTime = filter.Process(Vector2.one, -0.1f);

            Assert.That(nonFinite.Error, Is.EqualTo(InputAssistError.NonFiniteInput));
            Assert.That(negativeTime.Error, Is.EqualTo(InputAssistError.NegativeDeltaTime));
            Assert.That(filter.Current, Is.EqualTo(new Vector2(0.5f, 0f)));
        }

        [Test]
        public void TryConfigure_InvalidSettings_DoesNotReplaceConfiguration()
        {
            var filter = new InputVectorFilter();

            var succeeded = filter.TryConfigure(0.8f, 0.2f, InputResponseMode.Linear, 0f, 0f, 0.1f, InputDirectionMode.EightWay, out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(error, Is.EqualTo(InputAssistError.InvalidConfiguration));
            Assert.That(filter.InnerDeadZone, Is.EqualTo(0.15f));
            Assert.That(filter.OuterDeadZone, Is.EqualTo(1f));
        }

        [Test]
        public void Reset_AndTryReset_RebuildStateExplicitly()
        {
            var filter = new InputVectorFilter();
            Assert.That(filter.TryReset(new Vector2(0.3f, -0.4f), out var error), Is.True);
            Assert.That(error, Is.EqualTo(InputAssistError.None));
            Assert.That(filter.Current, Is.EqualTo(new Vector2(0.3f, -0.4f)));
            Assert.That(filter.TryReset(new Vector2(2f, 0f), out error), Is.False);
            Assert.That(error, Is.EqualTo(InputAssistError.ResetValueOutOfRange));
            Assert.That(filter.Current, Is.EqualTo(new Vector2(0.3f, -0.4f)));

            filter.Reset();

            Assert.That(filter.Current, Is.EqualTo(Vector2.zero));
        }
    }
}
