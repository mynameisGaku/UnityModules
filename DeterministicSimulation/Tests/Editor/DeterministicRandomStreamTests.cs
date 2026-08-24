using System;
using System.Linq;
using NUnit.Framework;

namespace DeterministicRandom.Tests
{
    /// <summary>version固定乱数列、状態復元、範囲と失敗時不変条件を検証する。</summary>
    public sealed class DeterministicRandomStreamTests
    {
        private static readonly DeterministicRandomState ReferenceState = new DeterministicRandomState(1, 1UL, 2UL, 3UL, 4UL);

        /// <summary>xoshiro256** reference stateから既知の64-bit列を返す。</summary>
        [Test]
        public void NextUInt64_ReferenceState_MatchesGoldenVector()
        {
            var stream = Restore(ReferenceState);
            var expected = new[]
            {
                11520UL,
                0UL,
                1509978240UL,
                1215971899390074240UL,
                1216172134540287360UL,
                607988272756665600UL,
                16172922978634559625UL,
                8476171486693032832UL,
                10595114339597558777UL,
                2904607092377533576UL
            };
            Assert.That(expected.Select(_ => stream.NextUInt64()).ToArray(), Is.EqualTo(expected));
        }

        /// <summary>seed 0を決められたSplitMix64状態へ展開する。</summary>
        [Test]
        public void Create_ZeroSeed_MatchesGoldenState()
        {
            var state = DeterministicRandomStream.Create(0UL).State;
            Assert.That(state, Is.EqualTo(new DeterministicRandomState(
                1,
                16294208416658607535UL,
                7960286522194355700UL,
                487617019471545679UL,
                17909611376780542444UL)));
        }

        /// <summary>同じseedと操作列は値と状態を完全再現する。</summary>
        [Test]
        public void SameSeedAndOperations_ReproduceValuesAndState()
        {
            var first = DeterministicRandomStream.Create(0xC0FFEEUL);
            var second = DeterministicRandomStream.Create(0xC0FFEEUL);
            for (var index = 0; index < 64; index++) Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
            Assert.That(first.State, Is.EqualTo(second.State));
        }

        /// <summary>異なるseedは同じ先頭列にならない。</summary>
        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var first = DeterministicRandomStream.Create(1UL);
            var second = DeterministicRandomStream.Create(2UL);
            Assert.That(Enumerable.Range(0, 8).Select(_ => first.NextUInt64()), Is.Not.EqualTo(Enumerable.Range(0, 8).Select(_ => second.NextUInt64())));
        }

        /// <summary>保存状態へ戻すと後続列を完全再生する。</summary>
        [Test]
        public void Reset_SavedState_ReplaysFollowingSequence()
        {
            var stream = DeterministicRandomStream.Create(42UL);
            stream.NextUInt64();
            var saved = stream.State;
            var expected = Enumerable.Range(0, 12).Select(_ => stream.NextUInt64()).ToArray();
            Assert.That(stream.Reset(saved), Is.EqualTo(DeterministicRandomError.None));
            Assert.That(Enumerable.Range(0, 12).Select(_ => stream.NextUInt64()).ToArray(), Is.EqualTo(expected));
        }

        /// <summary>保存状態から作った別streamが同じ後続列を返す。</summary>
        [Test]
        public void TryCreate_SavedState_ClonesPosition()
        {
            var first = DeterministicRandomStream.Create(77UL);
            first.NextUInt64();
            var second = Restore(first.State);
            for (var index = 0; index < 16; index++) Assert.That(second.NextUInt64(), Is.EqualTo(first.NextUInt64()));
        }

        /// <summary>version不一致と全word 0を拒否する。</summary>
        [TestCase(0, 1UL, 2UL, 3UL, 4UL)]
        [TestCase(2, 1UL, 2UL, 3UL, 4UL)]
        [TestCase(1, 0UL, 0UL, 0UL, 0UL)]
        public void TryCreate_InvalidState_ReturnsError(int version, ulong word0, ulong word1, ulong word2, ulong word3)
        {
            var state = new DeterministicRandomState(version, word0, word1, word2, word3);
            Assert.That(DeterministicRandomStream.TryCreate(state, out var stream, out var error), Is.False);
            Assert.That(stream, Is.Null);
            Assert.That(error, Is.EqualTo(DeterministicRandomError.InvalidState));
        }

        /// <summary>不正Resetは現在状態を変更しない。</summary>
        [Test]
        public void Reset_InvalidState_DoesNotMutate()
        {
            var stream = DeterministicRandomStream.Create(5UL);
            var before = stream.State;
            Assert.That(stream.Reset(default), Is.EqualTo(DeterministicRandomError.InvalidState));
            Assert.That(stream.State, Is.EqualTo(before));
        }

        /// <summary>uint上端1では必ず0を返し、1 draw進む。</summary>
        [Test]
        public void TryNextUInt64_ExclusiveOne_ReturnsZeroAndAdvances()
        {
            var stream = DeterministicRandomStream.Create(8UL);
            var before = stream.State;
            Assert.That(stream.TryNextUInt64(1UL, out var value, out var error), Is.True);
            Assert.That(value, Is.Zero);
            Assert.That(error, Is.EqualTo(DeterministicRandomError.None));
            Assert.That(stream.State, Is.Not.EqualTo(before));
        }

        /// <summary>上端0を拒否し、streamを進めない。</summary>
        [Test]
        public void TryNextUInt64_ZeroBound_DoesNotMutate()
        {
            var stream = DeterministicRandomStream.Create(9UL);
            var before = stream.State;
            Assert.That(stream.TryNextUInt64(0UL, out var value, out var error), Is.False);
            Assert.That(value, Is.Zero);
            Assert.That(error, Is.EqualTo(DeterministicRandomError.InvalidRange));
            Assert.That(stream.State, Is.EqualTo(before));
        }

        /// <summary>負の下端と正の上端を含むint範囲内だけを返す。</summary>
        [Test]
        public void TryNextInt32_SignedRange_StaysWithinBounds()
        {
            var stream = DeterministicRandomStream.Create(10UL);
            for (var index = 0; index < 10000; index++)
            {
                Assert.That(stream.TryNextInt32(-5, 7, out var value, out var error), Is.True);
                Assert.That(error, Is.EqualTo(DeterministicRandomError.None));
                Assert.That(value, Is.InRange(-5, 6));
            }
        }

        /// <summary>int全域に近い範囲もoverflowせず値を返す。</summary>
        [Test]
        public void TryNextInt32_WideRange_StaysWithinBounds()
        {
            var stream = DeterministicRandomStream.Create(11UL);
            for (var index = 0; index < 1000; index++)
            {
                Assert.That(stream.TryNextInt32(int.MinValue, int.MaxValue, out var value, out _), Is.True);
                Assert.That(value, Is.GreaterThanOrEqualTo(int.MinValue));
                Assert.That(value, Is.LessThan(int.MaxValue));
            }
        }

        /// <summary>空または逆順int範囲を拒否し、streamを進めない。</summary>
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        public void TryNextInt32_InvalidRange_DoesNotMutate(int min, int max)
        {
            var stream = DeterministicRandomStream.Create(12UL);
            var before = stream.State;
            Assert.That(stream.TryNextInt32(min, max, out var value, out var error), Is.False);
            Assert.That(value, Is.Zero);
            Assert.That(error, Is.EqualTo(DeterministicRandomError.InvalidRange));
            Assert.That(stream.State, Is.EqualTo(before));
        }

        /// <summary>floatとdoubleを0以上1未満で返す。</summary>
        [Test]
        public void FloatingPointOutputs_StayInHalfOpenUnitRange()
        {
            var stream = DeterministicRandomStream.Create(13UL);
            for (var index = 0; index < 10000; index++)
            {
                var single = stream.NextSingle();
                var value = stream.NextDouble();
                Assert.That(single, Is.GreaterThanOrEqualTo(0f));
                Assert.That(single, Is.LessThan(1f));
                Assert.That(value, Is.GreaterThanOrEqualTo(0d));
                Assert.That(value, Is.LessThan(1d));
            }
        }

        /// <summary>32-bit値とboolが同じseedで再現する。</summary>
        [Test]
        public void ConvenienceOutputs_ReproduceExactly()
        {
            var first = DeterministicRandomStream.Create(14UL);
            var second = DeterministicRandomStream.Create(14UL);
            for (var index = 0; index < 100; index++)
            {
                Assert.That(first.NextUInt32(), Is.EqualTo(second.NextUInt32()));
                Assert.That(first.NextBoolean(), Is.EqualTo(second.NextBoolean()));
            }
        }

        private static DeterministicRandomStream Restore(DeterministicRandomState state)
        {
            Assert.That(DeterministicRandomStream.TryCreate(state, out var stream, out var error), Is.True, error.ToString());
            return stream;
        }
    }
}
