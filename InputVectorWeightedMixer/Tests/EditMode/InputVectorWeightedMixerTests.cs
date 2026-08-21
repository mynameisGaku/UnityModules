using NUnit.Framework;

namespace InputMixing.Tests
{
    public sealed class InputVectorWeightedMixerTests
    {
        [Test]
        public void Mix_NullInput_ReturnsExplicitError()
        {
            var result = InputVectorWeightedMixer.Mix(null);
            AssertFailure(result, InputVectorWeightedMixerError.NullInput, 0, -1);
        }

        [Test]
        public void Mix_TooManyContributions_ReturnsExplicitError()
        {
            var input = new InputVectorContribution[InputVectorWeightedMixer.MaximumContributionCount + 1];
            var result = InputVectorWeightedMixer.Mix(input);
            AssertFailure(result, InputVectorWeightedMixerError.TooManyContributions, input.Length, -1);
        }

        [Test]
        public void Mix_EmptyInput_ReturnsNeutralSuccess()
        {
            AssertSuccess(InputVectorWeightedMixer.Mix(System.Array.Empty<InputVectorContribution>()), 0d, 0d, 0d, 0, 0, false);
        }

        [Test]
        public void Mix_ZeroWeights_ReturnsNeutralSuccessAndPreservesInputCount()
        {
            var input = new[]
            {
                new InputVectorContribution(1d, -1d, 0d),
                new InputVectorContribution(-0.5d, 0.25d, 0d)
            };
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0d, 0d, 0d, 2, 0, false);
        }

        [Test]
        public void Mix_EqualWeights_ReturnsArithmeticAverage()
        {
            var input = new[]
            {
                new InputVectorContribution(1d, 0d, 1d),
                new InputVectorContribution(0d, 1d, 1d)
            };
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0.5d, 0.5d, 2d, 2, 2, false);
        }

        [Test]
        public void Mix_UnequalWeights_ReturnsNormalizedWeightedAverage()
        {
            var input = new[]
            {
                new InputVectorContribution(1d, 0d, 0.75d),
                new InputVectorContribution(0d, 1d, 0.25d)
            };
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0.75d, 0.25d, 1d, 2, 2, false);
        }

        [Test]
        public void Mix_ZeroWeightContribution_DoesNotAffectAverage()
        {
            var input = new[]
            {
                new InputVectorContribution(0.4d, -0.2d, 1d),
                new InputVectorContribution(-1d, 1d, 0d)
            };
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0.4d, -0.2d, 1d, 2, 1, false);
        }

        [Test]
        public void Mix_SubnormalWeights_PreservesRelativeRatio()
        {
            var input = new[]
            {
                new InputVectorContribution(1d, 0d, double.Epsilon),
                new InputVectorContribution(0d, 1d, double.Epsilon)
            };
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0.5d, 0.5d, double.Epsilon * 2d, 2, 2, false);
        }

        [Test]
        public void Mix_MaximumCount_AllowsBoundary()
        {
            var input = new InputVectorContribution[InputVectorWeightedMixer.MaximumContributionCount];
            for (var index = 0; index < input.Length; index++) input[index] = new InputVectorContribution(0.25d, -0.5d, 1d);
            AssertSuccess(InputVectorWeightedMixer.Mix(input), 0.25d, -0.5d, InputVectorWeightedMixer.MaximumContributionCount, input.Length, input.Length, false);
        }

        [TestCase(double.NaN, 0d, 1d, InputVectorWeightedMixerError.NonFiniteInput)]
        [TestCase(0d, double.PositiveInfinity, 1d, InputVectorWeightedMixerError.NonFiniteInput)]
        [TestCase(-1.000001d, 0d, 1d, InputVectorWeightedMixerError.InputOutOfRange)]
        [TestCase(0d, 1.000001d, 1d, InputVectorWeightedMixerError.InputOutOfRange)]
        [TestCase(0d, 0d, double.NaN, InputVectorWeightedMixerError.NonFiniteWeight)]
        [TestCase(0d, 0d, double.NegativeInfinity, InputVectorWeightedMixerError.NonFiniteWeight)]
        [TestCase(0d, 0d, -0.000001d, InputVectorWeightedMixerError.WeightOutOfRange)]
        [TestCase(0d, 0d, 1.000001d, InputVectorWeightedMixerError.WeightOutOfRange)]
        public void Mix_InvalidContribution_ReportsExactIndex(double horizontal, double vertical, double weight, InputVectorWeightedMixerError expected)
        {
            var input = new[]
            {
                new InputVectorContribution(0.25d, 0.5d, 1d),
                new InputVectorContribution(horizontal, vertical, weight),
                new InputVectorContribution(-0.25d, -0.5d, 1d)
            };
            AssertFailure(InputVectorWeightedMixer.Mix(input), expected, 3, 1);
        }

        [Test]
        public void Mix_InvalidZeroWeightContribution_IsNotSilentlyIgnored()
        {
            var input = new[]
            {
                new InputVectorContribution(1d, 0d, 1d),
                new InputVectorContribution(2d, 0d, 0d)
            };
            AssertFailure(InputVectorWeightedMixer.Mix(input), InputVectorWeightedMixerError.InputOutOfRange, 2, 1);
        }

        [Test]
        public void Mix_DoesNotMutateCallerInput()
        {
            var input = new[]
            {
                new InputVectorContribution(0.5d, 0.25d, 0.2d),
                new InputVectorContribution(-0.5d, -0.25d, 0.8d)
            };
            var before = (InputVectorContribution[])input.Clone();
            InputVectorWeightedMixer.Mix(input);
            Assert.That(input, Is.EqualTo(before));
        }

        [Test]
        public void Mix_RepeatedOrderedInput_ReturnsEqualResult()
        {
            var input = new[]
            {
                new InputVectorContribution(0.8d, -0.1d, 0.4d),
                new InputVectorContribution(-0.2d, 0.7d, 0.6d)
            };
            var first = InputVectorWeightedMixer.Mix(input);
            var second = InputVectorWeightedMixer.Mix(input);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(default(InputVectorMixResult)));
        }

        [Test]
        public void ContributionEquality_IncludesAllInputFields()
        {
            var first = new InputVectorContribution(0.25d, -0.5d, 0.75d);
            var second = new InputVectorContribution(0.25d, -0.5d, 0.75d);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != new InputVectorContribution(0.25d, -0.5d, 0.5d), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        private static void AssertSuccess(InputVectorMixResult result, double horizontal, double vertical, double totalWeight, int contributionCount, int activeContributionCount, bool clamped)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(InputVectorWeightedMixerError.None));
            Assert.That(result.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(result.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(result.TotalWeight, Is.EqualTo(totalWeight).Within(double.Epsilon));
            Assert.That(result.ContributionCount, Is.EqualTo(contributionCount));
            Assert.That(result.ActiveContributionCount, Is.EqualTo(activeContributionCount));
            Assert.That(result.WasNumericallyClamped, Is.EqualTo(clamped));
            Assert.That(result.InvalidContributionIndex, Is.EqualTo(-1));
        }

        private static void AssertFailure(InputVectorMixResult result, InputVectorWeightedMixerError error, int contributionCount, int invalidIndex)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.ContributionCount, Is.EqualTo(contributionCount));
            Assert.That(result.ActiveContributionCount, Is.Zero);
            Assert.That(result.InvalidContributionIndex, Is.EqualTo(invalidIndex));
        }
    }
}
