using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PlayModeTuning.Editor
{
    /// <summary>長さを付けたUTF-8文字列から、文字列序数に基づく正規のSHA-256値を計算します。</summary>
    internal static class PlayModeTuningFingerprint
    {
        internal static string Compute(IEnumerable<string> tokens)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var token in tokens ?? Array.Empty<string>())
                    AppendToken(stream, token ?? string.Empty);
                using (var sha = SHA256.Create())
                    return ToLowerHex(sha.ComputeHash(stream.ToArray()));
            }
        }

        private static void AppendToken(Stream stream, string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var length = bytes.Length;
            stream.WriteByte((byte)((length >> 24) & 0xff));
            stream.WriteByte((byte)((length >> 16) & 0xff));
            stream.WriteByte((byte)((length >> 8) & 0xff));
            stream.WriteByte((byte)(length & 0xff));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
