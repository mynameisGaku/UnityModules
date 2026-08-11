using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>設定ファイルへ書き出す形式。</summary>
    public enum DebugMenuSettingsFormat
    {
        /// <summary>UnityのJsonUtilityで読めるJSON。</summary>
        Json,

        /// <summary>1行1項目で手編集できるテキスト。</summary>
        Text,

        /// <summary>型と値を長さ付きで格納するバイナリ。</summary>
        Binary,
    }

    /// <summary>設定データをJSON、テキスト、バイナリへ相互変換する。</summary>
    public static class DebugMenuSettingsSerializer
    {
        private const string TextHeader = "DEBUGMENU-TEXT\t1";
        private const string BinaryEnvelope = "DEBUGMENU-BINARY\t1\n";

        private static readonly byte[] BinaryMagic = { (byte)'D', (byte)'M', (byte)'N', (byte)'U' };

        /// <summary>保存ストレージへ渡せる文字列に変換する。</summary>
        public static string Serialize(DebugMenuSettingsData data, DebugMenuSettingsFormat format)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            switch (format)
            {
                case DebugMenuSettingsFormat.Text:
                    return SerializeText(data);
                case DebugMenuSettingsFormat.Binary:
                    return BinaryEnvelope + Convert.ToBase64String(SerializeBinary(data));
                default:
                    return JsonUtility.ToJson(data, true);
            }
        }

        /// <summary>保存文字列の中身から形式を判別して復元する。</summary>
        public static bool TryDeserialize(string serialized, out DebugMenuSettingsData data, out DebugMenuSettingsFormat format)
        {
            data = null;
            format = DebugMenuSettingsFormat.Json;
            if (string.IsNullOrEmpty(serialized)) return false;

            try
            {
                if (serialized.StartsWith(BinaryEnvelope, StringComparison.Ordinal))
                {
                    format = DebugMenuSettingsFormat.Binary;
                    var bytes = Convert.FromBase64String(serialized.Substring(BinaryEnvelope.Length).Trim());
                    return TryDeserializeBinary(bytes, out data);
                }

                if (serialized.StartsWith(TextHeader, StringComparison.Ordinal))
                {
                    format = DebugMenuSettingsFormat.Text;
                    return TryDeserializeText(serialized, out data);
                }

                data = JsonUtility.FromJson<DebugMenuSettingsData>(serialized);
                return IsUsable(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] 設定形式を解釈できなかった。\n{exception.Message}");
                data = null;
                return false;
            }
        }

        /// <summary>ファイルへ直接書けるバイト列へ変換する。</summary>
        public static byte[] SerializeFile(DebugMenuSettingsData data, DebugMenuSettingsFormat format)
        {
            if (format == DebugMenuSettingsFormat.Binary) return SerializeBinary(data);
            return new UTF8Encoding(false).GetBytes(Serialize(data, format));
        }

        /// <summary>ファイルの中身から形式を判別して復元する。</summary>
        public static bool TryDeserializeFile(byte[] bytes, out DebugMenuSettingsData data, out DebugMenuSettingsFormat format)
        {
            data = null;
            format = DebugMenuSettingsFormat.Json;
            if (bytes == null || bytes.Length == 0) return false;

            if (HasBinaryMagic(bytes))
            {
                format = DebugMenuSettingsFormat.Binary;
                return TryDeserializeBinary(bytes, out data);
            }

            try
            {
                return TryDeserialize(new UTF8Encoding(false, true).GetString(bytes), out data, out format);
            }
            catch (DecoderFallbackException)
            {
                data = null;
                format = DebugMenuSettingsFormat.Json;
                return false;
            }
        }

        /// <summary>形式に合う標準拡張子を返す。</summary>
        public static string GetExtension(DebugMenuSettingsFormat format) => format switch
        {
            DebugMenuSettingsFormat.Text => ".txt",
            DebugMenuSettingsFormat.Binary => ".bin",
            _ => ".json",
        };

        private static string SerializeText(DebugMenuSettingsData data)
        {
            var builder = new StringBuilder(TextHeader);
            var count = SafeCount(data);
            for (var i = 0; i < count; i++)
            {
                builder.Append('\n');
                builder.Append(data.Kinds[i]);
                builder.Append('\t');
                builder.Append(Escape(data.Keys[i]));
                builder.Append('\t');
                builder.Append(Escape(data.Values[i]));
            }

            return builder.ToString();
        }

        private static bool TryDeserializeText(string text, out DebugMenuSettingsData data)
        {
            data = new DebugMenuSettingsData();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || !string.Equals(lines[0], TextHeader, StringComparison.Ordinal)) return false;

            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                var parts = lines[i].Split(new[] { '\t' }, 3);
                if (parts.Length != 3 || !int.TryParse(parts[0], out var kind)) return false;

                data.Kinds.Add(kind);
                data.Keys.Add(Unescape(parts[1]));
                data.Values.Add(Unescape(parts[2]));
            }

            return true;
        }

        private static byte[] SerializeBinary(DebugMenuSettingsData data)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(BinaryMagic);
                writer.Write(1);
                var count = SafeCount(data);
                writer.Write(count);
                for (var i = 0; i < count; i++)
                {
                    writer.Write(data.Kinds[i]);
                    writer.Write(data.Keys[i] ?? string.Empty);
                    writer.Write(data.Values[i] ?? string.Empty);
                }
            }

            return stream.ToArray();
        }

        private static bool TryDeserializeBinary(byte[] bytes, out DebugMenuSettingsData data)
        {
            data = null;
            if (!HasBinaryMagic(bytes)) return false;

            try
            {
                using var stream = new MemoryStream(bytes, false);
                using var reader = new BinaryReader(stream, new UTF8Encoding(false), false);
                reader.ReadBytes(BinaryMagic.Length);
                var version = reader.ReadInt32();
                var count = reader.ReadInt32();
                if (version != 1 || count < 0 || count > 1_000_000) return false;

                var result = new DebugMenuSettingsData { Version = version };
                for (var i = 0; i < count; i++)
                {
                    result.Kinds.Add(reader.ReadInt32());
                    result.Keys.Add(reader.ReadString());
                    result.Values.Add(reader.ReadString());
                }

                if (stream.Position != stream.Length) return false;
                data = result;
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        private static int SafeCount(DebugMenuSettingsData data)
        {
            if (data.Keys == null || data.Values == null || data.Kinds == null) return 0;
            return Math.Min(data.Keys.Count, Math.Min(data.Values.Count, data.Kinds.Count));
        }

        private static bool IsUsable(DebugMenuSettingsData data) =>
            data != null && data.Keys != null && data.Values != null && data.Kinds != null;

        private static bool HasBinaryMagic(byte[] bytes)
        {
            if (bytes == null || bytes.Length < BinaryMagic.Length + sizeof(int) * 2) return false;
            for (var i = 0; i < BinaryMagic.Length; i++)
            {
                if (bytes[i] != BinaryMagic[i]) return false;
            }

            return true;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(value[i]);
                    continue;
                }

                var escaped = value[++i];
                builder.Append(escaped switch
                {
                    't' => '\t',
                    'r' => '\r',
                    'n' => '\n',
                    '\\' => '\\',
                    _ => escaped,
                });
            }

            return builder.ToString();
        }
    }
}
