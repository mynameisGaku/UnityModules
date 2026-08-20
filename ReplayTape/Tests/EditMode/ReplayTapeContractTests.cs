using System;
using System.Linq;
using NUnit.Framework;

namespace ReplayTape.Tests
{
    /// <summary>canonical形式、順序、上限、parse、readerの公開契約を固定する。</summary>
    [TestFixture]
    public sealed class ReplayTapeContractTests
    {
        /// <summary>設定上限の範囲外をconstructorで拒否する。</summary>
        [Test]
        public void Constructor_InvalidLimits_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayTapeBuilder(15));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayTapeBuilder(ReplayTapeBuilder.MaximumAllowedByteCount + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayTapeBuilder(16, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayTapeBuilder(16, ReplayTapeBuilder.MaximumAllowedEntryCount + 1));
        }

        /// <summary>空tapeのmagic、version、count、data長をgolden bytesへ固定する。</summary>
        [Test]
        public void EmptyTape_MatchesGoldenBytes()
        {
            using var builder = new ReplayTapeBuilder();
            var tape = Build(builder);
            Assert.That(ToHex(tape.ToByteArray()), Is.EqualTo("52544150010000000000000000000000"));
            Assert.That(tape.EntryCount, Is.Zero);
            Assert.That(tape.ByteCount, Is.EqualTo(16));
        }

        /// <summary>tick、command id、payloadがlittle-endianのgolden bytesへ固定される。</summary>
        [Test]
        public void Entries_MatchGoldenBytes()
        {
            using var builder = new ReplayTapeBuilder();
            AssertSuccess(builder.TryAppend(1, 7, new byte[] { 0xaa, 0xbb }, out var firstError), firstError);
            AssertSuccess(builder.TryAppend(1, 8, ReadOnlySpan<byte>.Empty, out var secondError), secondError);
            var tape = Build(builder);
            Assert.That(ToHex(tape.ToByteArray()), Is.EqualTo("5254415001000000020000002200000001000000000000000700000002000000aabb01000000000000000800000000000000"));
            Assert.That(ReplayTapeValue.TryParse(tape.ToByteArray(), out var parsed, out var parseError), Is.True);
            Assert.That(parseError, Is.EqualTo(ReplayTapeError.None));
            Assert.That(parsed, Is.EqualTo(tape));
        }

        /// <summary>同tickのentryを追加順のままreaderが返す。</summary>
        [Test]
        public void SameTick_PreservesAppendOrder()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 10, 3, 30);
            Append(builder, 10, 1, 10);
            Append(builder, 10, 2, 20);
            var reader = Reader(Build(builder));
            Assert.That(Read(reader).CommandId, Is.EqualTo(3));
            Assert.That(Read(reader).CommandId, Is.EqualTo(1));
            Assert.That(Read(reader).CommandId, Is.EqualTo(2));
        }

        /// <summary>tick逆行を拒否し、既存bytesとcountを変更しない。</summary>
        [Test]
        public void DecreasingTick_IsRejectedWithoutMutation()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 5, 1, 1);
            var before = Build(builder);
            Assert.That(builder.TryAppend(4, 2, ReadOnlySpan<byte>.Empty, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ReplayTapeError.TickOrderViolation));
            Assert.That(Build(builder), Is.EqualTo(before));
            Assert.That(builder.EntryCount, Is.EqualTo(1));
        }

        /// <summary>予約command id 0を拒否し、既存tapeを変更しない。</summary>
        [Test]
        public void ZeroCommandId_IsRejectedWithoutMutation()
        {
            using var builder = new ReplayTapeBuilder();
            var before = Build(builder);
            Assert.That(builder.TryAppend(0, 0, new byte[] { 1 }, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ReplayTapeError.InvalidInput));
            Assert.That(Build(builder), Is.EqualTo(before));
        }

        /// <summary>caller payloadの後変更がbuilderへ影響しない。</summary>
        [Test]
        public void Append_CopiesCallerPayload()
        {
            using var builder = new ReplayTapeBuilder();
            var payload = new byte[] { 1, 2, 3 };
            AssertSuccess(builder.TryAppend(2, 9, payload, out var error), error);
            payload[0] = 99;
            Assert.That(Read(Reader(Build(builder))).ToPayloadArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        /// <summary>byte上限を満たすentryは受理し、次のentryをmutationなしで拒否する。</summary>
        [Test]
        public void ByteCapacity_ExactBoundaryThenRejects()
        {
            using var builder = new ReplayTapeBuilder(34, 2);
            AssertSuccess(builder.TryAppend(0, 1, new byte[] { 7, 8 }, out var error), error);
            var before = Build(builder);
            Assert.That(builder.ByteCount, Is.EqualTo(34));
            Assert.That(builder.TryAppend(0, 2, ReadOnlySpan<byte>.Empty, out error), Is.False);
            Assert.That(error, Is.EqualTo(ReplayTapeError.CapacityExceeded));
            Assert.That(Build(builder), Is.EqualTo(before));
        }

        /// <summary>entry上限を満たした後の追加をmutationなしで拒否する。</summary>
        [Test]
        public void EntryCapacity_RejectsAdditionalEntry()
        {
            using var builder = new ReplayTapeBuilder(64, 1);
            Append(builder, 0, 1, 0);
            var before = Build(builder);
            Assert.That(builder.TryAppend(0, 2, ReadOnlySpan<byte>.Empty, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ReplayTapeError.CapacityExceeded));
            Assert.That(Build(builder), Is.EqualTo(before));
        }

        /// <summary>Build結果が後続appendから独立し、Build自体はbuilderを消費しない。</summary>
        [Test]
        public void Build_IsNonConsumingAndReturnsIndependentValue()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 1, 1, 10);
            var first = Build(builder);
            var again = Build(builder);
            Append(builder, 2, 2, 20);
            Assert.That(again, Is.EqualTo(first));
            Assert.That(Build(builder), Is.Not.EqualTo(first));
            Assert.That(first.EntryCount, Is.EqualTo(1));
        }

        /// <summary>Resetが上限を保ち、空headerへ戻す。</summary>
        [Test]
        public void Reset_ClearsEntriesAndAllowsEarlierTick()
        {
            using var builder = new ReplayTapeBuilder(128, 4);
            Append(builder, 99, 1, 1);
            Assert.That(builder.Reset(), Is.EqualTo(ReplayTapeError.None));
            Append(builder, 1, 2, 2);
            Assert.That(builder.EntryCount, Is.EqualTo(1));
            Assert.That(builder.MaximumByteCount, Is.EqualTo(128));
            Assert.That(builder.MaximumEntryCount, Is.EqualTo(4));
        }

        /// <summary>Disposeを複数回呼べ、以後の変更操作を拒否する。</summary>
        [Test]
        public void Dispose_IsIdempotentAndRejectsOperations()
        {
            var builder = new ReplayTapeBuilder();
            builder.Dispose();
            builder.Dispose();
            Assert.That(builder.IsDisposed, Is.True);
            Assert.That(builder.TryAppend(0, 1, ReadOnlySpan<byte>.Empty, out var appendError), Is.False);
            Assert.That(appendError, Is.EqualTo(ReplayTapeError.Disposed));
            Assert.That(builder.TryBuild(out _, out var buildError), Is.False);
            Assert.That(buildError, Is.EqualTo(ReplayTapeError.Disposed));
            Assert.That(builder.Reset(), Is.EqualTo(ReplayTapeError.Disposed));
        }

        /// <summary>Parse結果が入力配列の後変更から独立する。</summary>
        [Test]
        public void Parse_CopiesInputBytes()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 1, 4, 44);
            var bytes = Build(builder).ToByteArray();
            Assert.That(ReplayTapeValue.TryParse(bytes, out var parsed, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ReplayTapeError.None));
            bytes[0] = 0;
            Assert.That(Read(Reader(parsed)).CommandId, Is.EqualTo(4));
        }

        /// <summary>magic、version、reserved、declared lengthの破損を区別して拒否する。</summary>
        [Test]
        public void Parse_HeaderFailures_AreRejected()
        {
            var empty = EmptyBytes();
            var invalidMagic = (byte[])empty.Clone();
            invalidMagic[0] = 0;
            AssertParseFailure(invalidMagic, ReplayTapeError.InvalidHeader);
            var unsupported = (byte[])empty.Clone();
            unsupported[4] = 2;
            AssertParseFailure(unsupported, ReplayTapeError.UnsupportedVersion);
            var reserved = (byte[])empty.Clone();
            reserved[6] = 1;
            AssertParseFailure(reserved, ReplayTapeError.CorruptedData);
            var length = (byte[])empty.Clone();
            length[12] = 1;
            AssertParseFailure(length, ReplayTapeError.CorruptedData);
        }

        /// <summary>record count、payload長、trailing byteの不整合を拒否する。</summary>
        [Test]
        public void Parse_RecordShapeFailures_AreRejected()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 1, 1, 10);
            var valid = Build(builder).ToByteArray();
            var missingRecord = (byte[])valid.Clone();
            missingRecord[8] = 2;
            AssertParseFailure(missingRecord, ReplayTapeError.CorruptedData);
            var oversizedPayload = (byte[])valid.Clone();
            oversizedPayload[28] = 255;
            AssertParseFailure(oversizedPayload, ReplayTapeError.CorruptedData);
            var trailing = new byte[valid.Length + 1];
            Buffer.BlockCopy(valid, 0, trailing, 0, valid.Length);
            AssertParseFailure(trailing, ReplayTapeError.CorruptedData);
        }

        /// <summary>record内のcommand id 0とtick逆行を拒否する。</summary>
        [Test]
        public void Parse_RecordContractFailures_AreRejected()
        {
            using var builder = new ReplayTapeBuilder();
            AssertSuccess(builder.TryAppend(5, 1, ReadOnlySpan<byte>.Empty, out var firstError), firstError);
            AssertSuccess(builder.TryAppend(6, 2, ReadOnlySpan<byte>.Empty, out var secondError), secondError);
            var invalidId = Build(builder).ToByteArray();
            Array.Clear(invalidId, 24, 4);
            AssertParseFailure(invalidId, ReplayTapeError.CorruptedData);
            var decreasing = Build(builder).ToByteArray();
            decreasing[32] = 4;
            for (var index = 33; index < 40; index++) decreasing[index] = 0;
            AssertParseFailure(decreasing, ReplayTapeError.CorruptedData);
        }

        /// <summary>readerがPositionとRemainingCountを更新し、末尾とResetを明示する。</summary>
        [Test]
        public void Reader_ReadsToEndAndResets()
        {
            using var builder = new ReplayTapeBuilder();
            Append(builder, 1, 1, 11);
            Append(builder, 2, 2, 22);
            var reader = Reader(Build(builder));
            Assert.That(reader.EntryCount, Is.EqualTo(2));
            Assert.That(Read(reader).Tick, Is.EqualTo(1));
            Assert.That(reader.Position, Is.EqualTo(1));
            Assert.That(reader.RemainingCount, Is.EqualTo(1));
            Assert.That(Read(reader).Tick, Is.EqualTo(2));
            Assert.That(reader.TryRead(out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(ReplayTapeError.EndOfTape));
            reader.Reset();
            Assert.That(reader.Position, Is.Zero);
            Assert.That(Read(reader).CommandId, Is.EqualTo(1));
        }

        /// <summary>entry payload copyが不足領域を変更せず拒否し、十分な領域へcopyする。</summary>
        [Test]
        public void Entry_CopyPayload_ValidatesDestination()
        {
            using var builder = new ReplayTapeBuilder();
            AssertSuccess(builder.TryAppend(0, 1, new byte[] { 4, 5, 6 }, out var appendError), appendError);
            var entry = Read(Reader(Build(builder)));
            var small = new byte[] { 9, 9 };
            Assert.That(entry.TryCopyPayload(small, out var smallError), Is.False);
            Assert.That(smallError, Is.EqualTo(ReplayTapeError.DestinationTooSmall));
            Assert.That(small, Is.EqualTo(new byte[] { 9, 9 }));
            var exact = new byte[3];
            Assert.That(entry.TryCopyPayload(exact, out var exactError), Is.True);
            Assert.That(exactError, Is.EqualTo(ReplayTapeError.None));
            Assert.That(exact, Is.EqualTo(new byte[] { 4, 5, 6 }));
        }

        /// <summary>default valueとentryが無効で、readerやcopyを作らない。</summary>
        [Test]
        public void DefaultValues_AreExplicitlyInvalid()
        {
            var value = default(ReplayTapeValue);
            Assert.That(value.IsValid, Is.False);
            Assert.That(value.ToByteArray(), Is.Empty);
            Assert.That(value.TryCreateReader(out var reader, out var valueError), Is.False);
            Assert.That(reader, Is.Null);
            Assert.That(valueError, Is.EqualTo(ReplayTapeError.InvalidHeader));
            var entry = default(ReplayTapeEntry);
            Assert.That(entry.IsValid, Is.False);
            Assert.That(entry.TryCopyPayload(Span<byte>.Empty, out var entryError), Is.False);
            Assert.That(entryError, Is.EqualTo(ReplayTapeError.InvalidInput));
        }

        /// <summary>public Runtime型をbuilder、value、reader、entry、errorの5つへ限定する。</summary>
        [Test]
        public void RuntimeAssembly_ExportsExactlyFiveContractTypes()
        {
            var names = typeof(ReplayTapeBuilder).Assembly.GetExportedTypes().Select(type => type.FullName).OrderBy(name => name).ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "ReplayTape.ReplayTapeBuilder",
                "ReplayTape.ReplayTapeEntry",
                "ReplayTape.ReplayTapeError",
                "ReplayTape.ReplayTapeReader",
                "ReplayTape.ReplayTapeValue"
            }));
        }

        private static byte[] EmptyBytes()
        {
            using var builder = new ReplayTapeBuilder();
            return Build(builder).ToByteArray();
        }

        private static void Append(ReplayTapeBuilder builder, ulong tick, uint commandId, int payloadValue)
        {
            var payload = BitConverter.GetBytes(payloadValue);
            if (!BitConverter.IsLittleEndian) Array.Reverse(payload);
            AssertSuccess(builder.TryAppend(tick, commandId, payload, out var error), error);
        }

        private static ReplayTapeValue Build(ReplayTapeBuilder builder)
        {
            Assert.That(builder.TryBuild(out var value, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(ReplayTapeError.None));
            return value;
        }

        private static ReplayTapeReader Reader(ReplayTapeValue value)
        {
            Assert.That(value.TryCreateReader(out var reader, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(ReplayTapeError.None));
            return reader;
        }

        private static ReplayTapeEntry Read(ReplayTapeReader reader)
        {
            Assert.That(reader.TryRead(out var entry, out var error), Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(ReplayTapeError.None));
            return entry;
        }

        private static void AssertSuccess(bool succeeded, ReplayTapeError error)
        {
            Assert.That(succeeded, Is.True, error.ToString());
            Assert.That(error, Is.EqualTo(ReplayTapeError.None));
        }

        private static void AssertParseFailure(byte[] bytes, ReplayTapeError expected)
        {
            Assert.That(ReplayTapeValue.TryParse(bytes, out var value, out var error), Is.False);
            Assert.That(value.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static string ToHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = hex[bytes[index] >> 4];
                characters[(index * 2) + 1] = hex[bytes[index] & 0x0f];
            }

            return new string(characters);
        }
    }
}
