using System;
using NUnit.Framework;

namespace StateFingerprint.Tests
{
    /// <summary>canonical形式、failure時不変、値の保存形式を固定する。</summary>
    [TestFixture]
    public sealed class StateFingerprintBuilderTests
    {
        /// <summary>fieldが無い場合もversion headerだけのgolden digestへ固定される。</summary>
        [Test]
        public void EmptyBuilder_MatchesGoldenDigest()
        {
            using var builder = new StateFingerprintBuilder();
            Assert.That(Build(builder).ToString(), Is.EqualTo("c30617f383a4ce85684e18b1a3fa9079c081e9bdca1d410fbd4be3497f994995"));
            Assert.That(builder.ByteCount, Is.EqualTo(4));
            Assert.That(builder.OperationCount, Is.Zero);
        }

        /// <summary>全公開write操作のtag・field id・長さ・little-endian payloadをgolden vectorへ固定する。</summary>
        [Test]
        public void AllTypes_MatchGoldenDigest()
        {
            using var builder = new StateFingerprintBuilder();
            AssertSuccess(builder.WriteNull(0));
            AssertSuccess(builder.WriteBoolean(1, true));
            AssertSuccess(builder.WriteInt32(2, -123456));
            AssertSuccess(builder.WriteUInt32(3, 0x89abcdefU));
            AssertSuccess(builder.WriteInt64(4, -9876543210L));
            AssertSuccess(builder.WriteUInt64(5, 0x0123456789abcdefUL));
            AssertSuccess(builder.WriteSingle(6, -0f));
            AssertSuccess(builder.WriteDouble(7, Math.PI));
            AssertSuccess(builder.WriteString(8, "状態🎮"));
            AssertSuccess(builder.WriteBytes(9, new byte[] { 0, 1, 2, 255 }));

            Assert.That(builder.OperationCount, Is.EqualTo(10));
            Assert.That(builder.ByteCount, Is.EqualTo(145));
            Assert.That(Build(builder).ToString(), Is.EqualTo("a11808eb57cfe544527aa69bcc4de82adccca4fb846ec6f04522e7cda0b8ab4f"));
        }

        /// <summary>別builderでも同じfield操作列なら同じfingerprintになる。</summary>
        [Test]
        public void SameOperations_ReproduceFingerprint()
        {
            using var first = CreateTypicalBuilder();
            using var second = CreateTypicalBuilder();
            Assert.That(Build(first), Is.EqualTo(Build(second)));
        }

        /// <summary>field操作順が違えば同じ値集合でもfingerprintが変わる。</summary>
        [Test]
        public void OperationOrder_ChangesFingerprint()
        {
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteInt32(1, 10));
            AssertSuccess(first.WriteInt32(2, 20));
            AssertSuccess(second.WriteInt32(2, 20));
            AssertSuccess(second.WriteInt32(1, 10));
            Assert.That(Build(first), Is.Not.EqualTo(Build(second)));
        }

        /// <summary>field idが違えば型と値が同じでもfingerprintが変わる。</summary>
        [Test]
        public void FieldId_ChangesFingerprint()
        {
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteUInt64(1, 42));
            AssertSuccess(second.WriteUInt64(2, 42));
            Assert.That(Build(first), Is.Not.EqualTo(Build(second)));
        }

        /// <summary>型tagが違えば同じraw数値でもfingerprintが変わる。</summary>
        [Test]
        public void TypeTag_ChangesFingerprint()
        {
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteInt32(1, 42));
            AssertSuccess(second.WriteUInt32(1, 42));
            Assert.That(Build(first), Is.Not.EqualTo(Build(second)));
        }

        /// <summary>明示nullと空文字列を別のfield状態として扱う。</summary>
        [Test]
        public void NullAndEmptyString_AreDistinct()
        {
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteNull(1));
            AssertSuccess(second.WriteString(1, string.Empty));
            Assert.That(Build(first), Is.Not.EqualTo(Build(second)));
        }

        /// <summary>日本語と補助平面文字をBOMなしUTF-8として再現する。</summary>
        [Test]
        public void UnicodeString_ReproducesAcrossBuilders()
        {
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteString(7, "保存🎮\n再現"));
            AssertSuccess(second.WriteString(7, "保存🎮\n再現"));
            Assert.That(Build(first), Is.EqualTo(Build(second)));
        }

        /// <summary>単独high surrogateを拒否し、byte数・操作数・fingerprintを変えない。</summary>
        [Test]
        public void InvalidSurrogate_DoesNotMutateBuilder()
        {
            using var builder = CreateTypicalBuilder();
            var before = Build(builder);
            var bytes = builder.ByteCount;
            var operations = builder.OperationCount;
            Assert.That(builder.WriteString(99, "\ud800"), Is.EqualTo(StateFingerprintError.InvalidInput));
            Assert.That(builder.ByteCount, Is.EqualTo(bytes));
            Assert.That(builder.OperationCount, Is.EqualTo(operations));
            Assert.That(Build(builder), Is.EqualTo(before));
        }

        /// <summary>上限ちょうどは成功し、1 byteでも超える操作は状態を変えずに拒否する。</summary>
        [Test]
        public void CapacityBoundary_FailureDoesNotMutateBuilder()
        {
            using var builder = new StateFingerprintBuilder(13);
            AssertSuccess(builder.WriteNull(1));
            var before = Build(builder);
            Assert.That(builder.ByteCount, Is.EqualTo(13));
            Assert.That(builder.WriteNull(2), Is.EqualTo(StateFingerprintError.CapacityExceeded));
            Assert.That(builder.ByteCount, Is.EqualTo(13));
            Assert.That(builder.OperationCount, Is.EqualTo(1));
            Assert.That(Build(builder), Is.EqualTo(before));

            using var stringBuilder = new StateFingerprintBuilder(13);
            Assert.That(stringBuilder.WriteString(1, "a"), Is.EqualTo(StateFingerprintError.CapacityExceeded));
            Assert.That(stringBuilder.ByteCount, Is.EqualTo(4));
            Assert.That(stringBuilder.OperationCount, Is.Zero);
        }

        /// <summary>null byte列を拒否し、空builderのままにする。</summary>
        [Test]
        public void NullBytes_ReturnInvalidInputWithoutMutation()
        {
            using var builder = new StateFingerprintBuilder();
            Assert.That(builder.WriteBytes(1, null), Is.EqualTo(StateFingerprintError.InvalidInput));
            Assert.That(builder.ByteCount, Is.EqualTo(4));
            Assert.That(builder.OperationCount, Is.Zero);
        }

        /// <summary>呼出後の配列変更が既に追加したcanonical payloadへ影響しない。</summary>
        [Test]
        public void WriteBytes_CopiesInput()
        {
            var source = new byte[] { 1, 2, 3 };
            using var first = new StateFingerprintBuilder();
            using var second = new StateFingerprintBuilder();
            AssertSuccess(first.WriteBytes(1, source));
            source[0] = 9;
            AssertSuccess(second.WriteBytes(1, new byte[] { 1, 2, 3 }));
            Assert.That(Build(first), Is.EqualTo(Build(second)));
        }

        /// <summary>singleは数値比較ではなくraw bit列を使うため+0と-0を区別する。</summary>
        [Test]
        public void SingleRawBits_DistinguishPositiveAndNegativeZero()
        {
            using var positive = new StateFingerprintBuilder();
            using var negative = new StateFingerprintBuilder();
            AssertSuccess(positive.WriteSingle(1, 0f));
            AssertSuccess(negative.WriteSingle(1, -0f));
            Assert.That(Build(positive), Is.Not.EqualTo(Build(negative)));
        }

        /// <summary>Buildを複数回呼んでも位置を消費せず、追加入力だけが次の値を変える。</summary>
        [Test]
        public void Build_DoesNotConsumeBuilder()
        {
            using var builder = CreateTypicalBuilder();
            var first = Build(builder);
            Assert.That(Build(builder), Is.EqualTo(first));
            AssertSuccess(builder.WriteBoolean(3, true));
            Assert.That(Build(builder), Is.Not.EqualTo(first));
        }

        /// <summary>Reset後に同じ操作を入れると同じfingerprintへ戻る。</summary>
        [Test]
        public void Reset_ReplaysFingerprint()
        {
            using var builder = CreateTypicalBuilder();
            var expected = Build(builder);
            AssertSuccess(builder.Reset());
            Assert.That(builder.ByteCount, Is.EqualTo(4));
            Assert.That(builder.OperationCount, Is.Zero);
            WriteTypical(builder);
            Assert.That(Build(builder), Is.EqualTo(expected));
        }

        /// <summary>Disposeは冪等で、その後のwrite・build・resetをDisposedとして拒否する。</summary>
        [Test]
        public void Dispose_IsIdempotentAndRejectsFurtherOperations()
        {
            var builder = CreateTypicalBuilder();
            builder.Dispose();
            builder.Dispose();
            Assert.That(builder.IsDisposed, Is.True);
            Assert.That(builder.ByteCount, Is.Zero);
            Assert.That(builder.OperationCount, Is.Zero);
            Assert.That(builder.WriteNull(1), Is.EqualTo(StateFingerprintError.Disposed));
            Assert.That(builder.WriteString(1, null), Is.EqualTo(StateFingerprintError.Disposed));
            Assert.That(builder.Reset(), Is.EqualTo(StateFingerprintError.Disposed));
            Assert.That(builder.TryBuild(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(StateFingerprintError.Disposed));
        }

        /// <summary>小文字出力と大文字入力を同じ32-byte値へ往復する。</summary>
        [Test]
        public void Value_ParseAndBytesRoundTrip()
        {
            using var builder = CreateTypicalBuilder();
            var expected = Build(builder);
            Assert.That(StateFingerprintValue.TryParse(expected.ToString().ToUpperInvariant(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
            CollectionAssert.AreEqual(expected.ToByteArray(), parsed.ToByteArray());
            Assert.That(parsed.ToString(), Is.EqualTo(expected.ToString()));
        }

        /// <summary>長さ違いとhex以外を拒否し、default値を返す。</summary>
        [Test]
        public void Value_ParseRejectsInvalidText()
        {
            Assert.That(StateFingerprintValue.TryParse(null, out var nullValue), Is.False);
            Assert.That(nullValue, Is.EqualTo(default(StateFingerprintValue)));
            Assert.That(StateFingerprintValue.TryParse("00", out _), Is.False);
            Assert.That(StateFingerprintValue.TryParse(new string('g', 64), out _), Is.False);
        }

        /// <summary>constructorがheader未満と公開上限超過を拒否する。</summary>
        [Test]
        public void Constructor_RejectsUnsupportedCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StateFingerprintBuilder(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StateFingerprintBuilder(StateFingerprintBuilder.MaximumAllowedByteCount + 1));
        }

        private static StateFingerprintBuilder CreateTypicalBuilder()
        {
            var builder = new StateFingerprintBuilder();
            WriteTypical(builder);
            return builder;
        }

        private static void WriteTypical(StateFingerprintBuilder builder)
        {
            AssertSuccess(builder.WriteUInt64(1, 123456789UL));
            AssertSuccess(builder.WriteString(2, "player-one"));
        }

        private static StateFingerprintValue Build(StateFingerprintBuilder builder)
        {
            Assert.That(builder.TryBuild(out var value, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(StateFingerprintError.None));
            return value;
        }

        private static void AssertSuccess(StateFingerprintError error) => Assert.That(error, Is.EqualTo(StateFingerprintError.None));
    }
}
