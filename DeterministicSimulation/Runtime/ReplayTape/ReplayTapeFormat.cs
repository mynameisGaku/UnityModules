using System;

namespace ReplayTape
{
    /// <summary>version 1 canonical byte列の読書きを共有する。</summary>
    internal static class ReplayTapeFormat
    {
        internal const int HeaderByteCount = 16;
        internal const int RecordHeaderByteCount = 16;
        internal const ushort Version = 1;

        internal static void WriteEmptyHeader(Span<byte> destination)
        {
            destination[0] = (byte)'R';
            destination[1] = (byte)'T';
            destination[2] = (byte)'A';
            destination[3] = (byte)'P';
            WriteUInt16(destination, 4, Version);
            WriteUInt16(destination, 6, 0);
            WriteUInt32(destination, 8, 0);
            WriteUInt32(destination, 12, 0);
        }

        internal static void WriteCounts(Span<byte> destination, int entryCount, int byteCount)
        {
            WriteUInt32(destination, 8, (uint)entryCount);
            WriteUInt32(destination, 12, (uint)(byteCount - HeaderByteCount));
        }

        internal static ReplayTapeError Validate(ReadOnlySpan<byte> bytes, out int entryCount)
        {
            entryCount = 0;
            if (bytes.Length < HeaderByteCount) return ReplayTapeError.InvalidHeader;
            if (bytes[0] != (byte)'R' || bytes[1] != (byte)'T' || bytes[2] != (byte)'A' || bytes[3] != (byte)'P') return ReplayTapeError.InvalidHeader;
            if (ReadUInt16(bytes, 4) != Version) return ReplayTapeError.UnsupportedVersion;
            if (ReadUInt16(bytes, 6) != 0) return ReplayTapeError.CorruptedData;
            if (bytes.Length > ReplayTapeBuilder.MaximumAllowedByteCount) return ReplayTapeError.CapacityExceeded;

            var declaredEntryCount = ReadUInt32(bytes, 8);
            if (declaredEntryCount > ReplayTapeBuilder.MaximumAllowedEntryCount) return ReplayTapeError.CapacityExceeded;
            if (ReadUInt32(bytes, 12) != (uint)(bytes.Length - HeaderByteCount)) return ReplayTapeError.CorruptedData;

            var offset = HeaderByteCount;
            var hasPreviousTick = false;
            var previousTick = 0UL;
            for (var index = 0U; index < declaredEntryCount; index++)
            {
                if (bytes.Length - offset < RecordHeaderByteCount) return ReplayTapeError.CorruptedData;
                var tick = ReadUInt64(bytes, offset);
                var commandId = ReadUInt32(bytes, offset + 8);
                var payloadByteCount = ReadUInt32(bytes, offset + 12);
                if (commandId == 0) return ReplayTapeError.CorruptedData;
                if (hasPreviousTick && tick < previousTick) return ReplayTapeError.CorruptedData;
                if (payloadByteCount > int.MaxValue || payloadByteCount > (uint)(bytes.Length - offset - RecordHeaderByteCount)) return ReplayTapeError.CorruptedData;
                offset += RecordHeaderByteCount + (int)payloadByteCount;
                previousTick = tick;
                hasPreviousTick = true;
            }

            if (offset != bytes.Length) return ReplayTapeError.CorruptedData;
            entryCount = (int)declaredEntryCount;
            return ReplayTapeError.None;
        }

        internal static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        internal static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
        {
            return (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        internal static ulong ReadUInt64(ReadOnlySpan<byte> bytes, int offset)
        {
            return ReadUInt32(bytes, offset) | ((ulong)ReadUInt32(bytes, offset + 4) << 32);
        }

        internal static void WriteUInt16(Span<byte> bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        internal static void WriteUInt32(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        internal static void WriteUInt64(Span<byte> bytes, int offset, ulong value)
        {
            WriteUInt32(bytes, offset, (uint)value);
            WriteUInt32(bytes, offset + 4, (uint)(value >> 32));
        }
    }
}
