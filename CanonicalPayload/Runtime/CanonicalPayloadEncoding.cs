using System;
using System.Text;

namespace CanonicalPayload
{
    internal static class CanonicalPayloadEncoding
    {
        internal static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        internal static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            WriteUInt32(buffer, offset, (uint)value);
            WriteUInt32(buffer, offset + 4, (uint)(value >> 32));
        }

        internal static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
        }

        internal static ulong ReadUInt64(byte[] buffer, int offset)
        {
            return ReadUInt32(buffer, offset) | ((ulong)ReadUInt32(buffer, offset + 4) << 32);
        }

        internal static bool TryGetUtf8Bytes(string value, out byte[] bytes)
        {
            bytes = null;
            if (value == null) return false;
            try
            {
                bytes = StrictUtf8.GetBytes(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        internal static bool TryGetString(byte[] buffer, int offset, int length, out string value)
        {
            value = null;
            try
            {
                value = StrictUtf8.GetString(buffer, offset, length);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}
