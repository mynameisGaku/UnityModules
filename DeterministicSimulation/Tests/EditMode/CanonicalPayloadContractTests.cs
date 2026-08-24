using System;
using System.Linq;
using NUnit.Framework;

namespace CanonicalPayload.Tests
{
    public sealed class CanonicalPayloadContractTests
    {
        [Test]
        public void EmptyBuild_IsValidStableAndReadable()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryBuild(out var first, out var firstError), Is.True);
            Assert.That(writer.TryBuild(out var second, out var secondError), Is.True);
            Assert.That(firstError, Is.EqualTo(CanonicalPayloadError.None));
            Assert.That(secondError, Is.EqualTo(CanonicalPayloadError.None));
            Assert.That(first.IsValid, Is.True);
            Assert.That(first.ByteCount, Is.Zero);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.TryCreateReader(out var reader, out var readerError), Is.True);
            Assert.That(readerError, Is.EqualTo(CanonicalPayloadError.None));
            Assert.That(reader.IsAtEnd, Is.True);
        }

        [Test]
        public void AllPrimitives_ProduceLittleEndianGoldenBytes()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteBoolean(true, out _), Is.True);
            Assert.That(writer.TryWriteBoolean(false, out _), Is.True);
            Assert.That(writer.TryWriteInt32(-1, out _), Is.True);
            Assert.That(writer.TryWriteUInt32(0x12345678u, out _), Is.True);
            Assert.That(writer.TryWriteInt64(-2L, out _), Is.True);
            Assert.That(writer.TryWriteUInt64(0x0123456789ABCDEFul, out _), Is.True);
            Assert.That(writer.TryWriteSingle(1.25f, out _), Is.True);
            Assert.That(writer.TryWriteDouble(-2.5d, out _), Is.True);
            Assert.That(writer.TryBuild(out var value, out _), Is.True);
            Assert.That(Hex(value), Is.EqualTo("0100FFFFFFFF78563412FEFFFFFFFFFFFFFFEFCDAB89674523010000A03F00000000000004C0"));
        }

        [Test]
        public void StringAndBytes_ProduceLengthPrefixedGoldenBytes()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteString("移動🚀", out _), Is.True);
            Assert.That(writer.TryWriteBytes(new byte[] { 0x00, 0xFF, 0x7F }, out _), Is.True);
            Assert.That(writer.TryBuild(out var value, out _), Is.True);
            Assert.That(Hex(value), Is.EqualTo("0A000000E7A7BBE58B95F09F9A800300000000FF7F"));
        }

        [Test]
        public void EmptyStringAndBytes_AreDistinctValidFields()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteString(string.Empty, out _), Is.True);
            Assert.That(writer.TryWriteBytes(ReadOnlySpan<byte>.Empty, out _), Is.True);
            Assert.That(writer.TryBuild(out var value, out _), Is.True);
            Assert.That(Hex(value), Is.EqualTo("0000000000000000"));
        }

        [Test]
        public void InvalidString_DoesNotMutateWriter()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteInt32(7, out _), Is.True);
            var before = writer.ByteCount;
            Assert.That(writer.TryWriteString(null, out var nullError), Is.False);
            Assert.That(nullError, Is.EqualTo(CanonicalPayloadError.InvalidInput));
            Assert.That(writer.TryWriteString("\uD800", out var unicodeError), Is.False);
            Assert.That(unicodeError, Is.EqualTo(CanonicalPayloadError.InvalidUtf8));
            Assert.That(writer.ByteCount, Is.EqualTo(before));
        }

        [Test]
        public void ExactCapacitySucceeds_ThenOverflowIsMutationFree()
        {
            using var writer = new CanonicalPayloadWriter(5);
            Assert.That(writer.TryWriteBoolean(true, out _), Is.True);
            Assert.That(writer.TryWriteInt32(9, out _), Is.True);
            Assert.That(writer.ByteCount, Is.EqualTo(5));
            Assert.That(writer.TryWriteBoolean(false, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.CapacityExceeded));
            Assert.That(writer.ByteCount, Is.EqualTo(5));
        }

        [Test]
        public void LengthPrefixedOverflow_IsMutationFree()
        {
            using var writer = new CanonicalPayloadWriter(8);
            Assert.That(writer.TryWriteUInt32(4, out _), Is.True);
            Assert.That(writer.TryWriteBytes(new byte[] { 1 }, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.CapacityExceeded));
            Assert.That(writer.ByteCount, Is.EqualTo(4));
        }

        [Test]
        public void ConstructorBounds_AreExplicit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CanonicalPayloadWriter(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CanonicalPayloadWriter(CanonicalPayloadWriter.MaximumSupportedByteCount + 1));
            using var writer = new CanonicalPayloadWriter(0);
            Assert.That(writer.TryBuild(out var empty, out _), Is.True);
            Assert.That(empty.ByteCount, Is.Zero);
            Assert.That(writer.TryWriteBoolean(true, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.CapacityExceeded));
        }

        [Test]
        public void InputBytes_AreCopiedWhenWritten()
        {
            var input = new byte[] { 1, 2, 3 };
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteBytes(input, out _), Is.True);
            input[0] = 99;
            Assert.That(writer.TryBuild(out var value, out _), Is.True);
            Assert.That(Hex(value), Is.EqualTo("03000000010203"));
        }

        [Test]
        public void Build_IsNonConsumingAndValuesAreIndependent()
        {
            using var writer = new CanonicalPayloadWriter();
            Assert.That(writer.TryWriteInt32(1, out _), Is.True);
            Assert.That(writer.TryBuild(out var first, out _), Is.True);
            Assert.That(writer.TryWriteBoolean(true, out _), Is.True);
            Assert.That(writer.TryBuild(out var second, out _), Is.True);
            Assert.That(Hex(first), Is.EqualTo("01000000"));
            Assert.That(Hex(second), Is.EqualTo("0100000001"));
        }

        [Test]
        public void Reset_PreservesCapacityAndClearsBytes()
        {
            using var writer = new CanonicalPayloadWriter(12);
            Assert.That(writer.TryWriteUInt64(1, out _), Is.True);
            Assert.That(writer.Reset(), Is.EqualTo(CanonicalPayloadError.None));
            Assert.That(writer.MaximumByteCount, Is.EqualTo(12));
            Assert.That(writer.ByteCount, Is.Zero);
            Assert.That(writer.TryBuild(out var value, out _), Is.True);
            Assert.That(value.ByteCount, Is.Zero);
        }

        [Test]
        public void Dispose_IsIdempotentAndRejectsOperations()
        {
            var writer = new CanonicalPayloadWriter();
            writer.Dispose();
            writer.Dispose();
            Assert.That(writer.IsDisposed, Is.True);
            Assert.That(writer.ByteCount, Is.Zero);
            Assert.That(writer.TryWriteInt32(1, out var writeError), Is.False);
            Assert.That(writeError, Is.EqualTo(CanonicalPayloadError.Disposed));
            Assert.That(writer.TryBuild(out _, out var buildError), Is.False);
            Assert.That(buildError, Is.EqualTo(CanonicalPayloadError.Disposed));
            Assert.That(writer.Reset(), Is.EqualTo(CanonicalPayloadError.Disposed));
        }

        [Test]
        public void ValueTryCreate_CopiesInputAndChecksCapacity()
        {
            var input = new byte[] { 1, 2, 3 };
            Assert.That(CanonicalPayloadValue.TryCreate(input, 3, out var value, out var error), Is.True);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.None));
            input[0] = 99;
            Assert.That(Hex(value), Is.EqualTo("010203"));
            Assert.That(CanonicalPayloadValue.TryCreate(input, 2, out _, out var capacityError), Is.False);
            Assert.That(capacityError, Is.EqualTo(CanonicalPayloadError.CapacityExceeded));
            Assert.That(CanonicalPayloadValue.TryCreate(input, -1, out _, out var inputError), Is.False);
            Assert.That(inputError, Is.EqualTo(CanonicalPayloadError.InvalidInput));
        }

        [Test]
        public void ValueOutput_IsCallerOwnedCopy()
        {
            Assert.That(CanonicalPayloadValue.TryCreate(new byte[] { 1, 2 }, out var value, out _), Is.True);
            var first = value.ToByteArray();
            first[0] = 99;
            Assert.That(value.ToByteArray(), Is.EqualTo(new byte[] { 1, 2 }));
        }

        [Test]
        public void ValueEqualityAndHash_AreContentBased()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 1, 2 }, out var first, out _);
            CanonicalPayloadValue.TryCreate(new byte[] { 1, 2 }, out var second, out _);
            CanonicalPayloadValue.TryCreate(new byte[] { 2, 1 }, out var third, out _);
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != third, Is.True);
            Assert.That(default(CanonicalPayloadValue).IsValid, Is.False);
            Assert.That(default(CanonicalPayloadValue).ToByteArray(), Is.Empty);
        }

        [Test]
        public void Reader_RoundTripsEverySupportedType()
        {
            using var writer = new CanonicalPayloadWriter();
            writer.TryWriteBoolean(true, out _);
            writer.TryWriteInt32(-7, out _);
            writer.TryWriteUInt32(uint.MaxValue, out _);
            writer.TryWriteInt64(long.MinValue, out _);
            writer.TryWriteUInt64(ulong.MaxValue, out _);
            writer.TryWriteSingle(1.25f, out _);
            writer.TryWriteDouble(-2.5d, out _);
            writer.TryWriteString("移動🚀", out _);
            writer.TryWriteBytes(new byte[] { 0, 255 }, out _);
            writer.TryBuild(out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadBoolean(out var boolean, out _), Is.True);
            Assert.That(boolean, Is.True);
            Assert.That(reader.TryReadInt32(out var int32, out _), Is.True);
            Assert.That(int32, Is.EqualTo(-7));
            Assert.That(reader.TryReadUInt32(out var uint32, out _), Is.True);
            Assert.That(uint32, Is.EqualTo(uint.MaxValue));
            Assert.That(reader.TryReadInt64(out var int64, out _), Is.True);
            Assert.That(int64, Is.EqualTo(long.MinValue));
            Assert.That(reader.TryReadUInt64(out var uint64, out _), Is.True);
            Assert.That(uint64, Is.EqualTo(ulong.MaxValue));
            Assert.That(reader.TryReadSingle(out var single, out _), Is.True);
            Assert.That(BitConverter.SingleToInt32Bits(single), Is.EqualTo(BitConverter.SingleToInt32Bits(1.25f)));
            Assert.That(reader.TryReadDouble(out var doubleValue, out _), Is.True);
            Assert.That(BitConverter.DoubleToInt64Bits(doubleValue), Is.EqualTo(BitConverter.DoubleToInt64Bits(-2.5d)));
            Assert.That(reader.TryReadString(out var text, out _), Is.True);
            Assert.That(text, Is.EqualTo("移動🚀"));
            Assert.That(reader.TryReadBytes(out var bytes, out _), Is.True);
            Assert.That(bytes, Is.EqualTo(new byte[] { 0, 255 }));
            Assert.That(reader.IsAtEnd, Is.True);
        }

        [Test]
        public void Readers_HaveIndependentPositions()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 1, 0 }, out var value, out _);
            value.TryCreateReader(out var first, out _);
            value.TryCreateReader(out var second, out _);
            Assert.That(first.TryReadBoolean(out var firstValue, out _), Is.True);
            Assert.That(firstValue, Is.True);
            Assert.That(first.Position, Is.EqualTo(1));
            Assert.That(second.Position, Is.Zero);
            Assert.That(second.TryReadBoolean(out var secondValue, out _), Is.True);
            Assert.That(secondValue, Is.True);
        }

        [Test]
        public void TruncatedPrimitive_DoesNotAdvanceReader()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 1, 2, 3 }, out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadUInt32(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.EndOfPayload));
            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.RemainingByteCount, Is.EqualTo(3));
        }

        [Test]
        public void InvalidBoolean_DoesNotAdvanceReader()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 2 }, out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadBoolean(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.InvalidBoolean));
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void InvalidLength_DoesNotAdvanceReader()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 5, 0, 0, 0, 1 }, out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadBytes(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.InvalidLength));
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void InvalidUtf8_DoesNotAdvanceReader()
        {
            CanonicalPayloadValue.TryCreate(new byte[] { 2, 0, 0, 0, 0xC3, 0x28 }, out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadString(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(CanonicalPayloadError.InvalidUtf8));
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void FloatingPointBits_ArePreservedExactly()
        {
            var single = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC00001));
            var doubleValue = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000001));
            using var writer = new CanonicalPayloadWriter();
            writer.TryWriteSingle(single, out _);
            writer.TryWriteDouble(doubleValue, out _);
            writer.TryBuild(out var value, out _);
            value.TryCreateReader(out var reader, out _);
            Assert.That(reader.TryReadSingle(out var readSingle, out _), Is.True);
            Assert.That(reader.TryReadDouble(out var readDouble, out _), Is.True);
            Assert.That(BitConverter.SingleToInt32Bits(readSingle), Is.EqualTo(BitConverter.SingleToInt32Bits(single)));
            Assert.That(BitConverter.DoubleToInt64Bits(readDouble), Is.EqualTo(BitConverter.DoubleToInt64Bits(doubleValue)));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsExactlyFourTypes()
        {
            var exported = typeof(CanonicalPayloadWriter).Assembly.GetExportedTypes().OrderBy(value => value.FullName).ToArray();
            Assert.That(exported.Select(value => value.FullName), Is.EqualTo(new[]
            {
                "CanonicalPayload.CanonicalPayloadError",
                "CanonicalPayload.CanonicalPayloadReader",
                "CanonicalPayload.CanonicalPayloadValue",
                "CanonicalPayload.CanonicalPayloadWriter"
            }));
        }

        private static string Hex(CanonicalPayloadValue value) => BitConverter.ToString(value.ToByteArray()).Replace("-", string.Empty);
    }
}
