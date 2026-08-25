using System;
using System.IO;
using System.Text;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// project asset を作らず memory 上だけで監査入力を組み立てます。
    /// </summary>
    internal static class LocalizationKeyAuditTestData
    {
        /// <summary>複数 test で共有する collection GUID です。</summary>
        internal static readonly Guid CollectionGuid =
            Guid.Parse("11111111-2222-3333-4444-555555555555");

        /// <summary>正しい SharedTableData YAML を持つ raw asset を作ります。</summary>
        internal static AuditEditor.LocalizationKeyAuditRawAsset CreateValidRawAsset(
            string assetPath = "Assets/Localization/UI Shared Data.asset",
            Guid? collectionGuid = null)
        {
            var guid = collectionGuid ?? CollectionGuid;
            return CreateRawAsset(assetPath, CreateYamlBytes(guid));
        }

        /// <summary>指定 byte と状態を持つ raw asset を作ります。</summary>
        internal static AuditEditor.LocalizationKeyAuditRawAsset CreateRawAsset(
            string assetPath,
            byte[] bytes,
            string physicalPath = null,
            bool exists = true,
            bool hasReparsePoint = false,
            bool isOversize = false,
            string readError = "")
        {
            return new AuditEditor.LocalizationKeyAuditRawAsset(
                assetPath,
                physicalPath ?? CreatePhysicalPath(assetPath),
                bytes,
                exists,
                hasReparsePoint,
                isOversize,
                readError);
        }

        /// <summary>指定 GUID を標準SharedTableData documentのdirect fieldに一件だけ持つUTF-8 YAMLを作ります。</summary>
        internal static byte[] CreateYamlBytes(
            Guid guid,
            string lineEnding = "\n",
            bool includeBom = false)
        {
            return CreateSharedTableDataYamlBytes(
                new[] { $"{AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {guid:N}" },
                lineEnding,
                includeBom);
        }

        /// <summary>指定direct fieldを持つ標準SharedTableData documentのUTF-8 YAMLを作ります。</summary>
        internal static byte[] CreateSharedTableDataYamlBytes(
            string[] directFields,
            string lineEnding = "\n",
            bool includeBom = false)
        {
            var lines = new[]
            {
                "%YAML 1.1",
                "%TAG !u! tag:unity3d.com,2011:",
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}",
                "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}",
                $"  m_Script: {{fileID: 11500000, guid: {AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid}, type: 3}}",
                "  m_Name: UI Shared Data",
                "  m_EditorClassIdentifier:"
            };
            var builder = new StringBuilder();
            builder.Append(string.Join(lineEnding, lines));
            var fields = directFields ?? Array.Empty<string>();
            for (var index = 0; index < fields.Length; index++)
            {
                builder.Append(lineEnding);
                builder.Append("  ");
                builder.Append(fields[index] ?? string.Empty);
            }

            builder.Append(lineEnding);
            var bytes = new UTF8Encoding(false).GetBytes(builder.ToString());
            if (!includeBom)
            {
                return bytes;
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var withBom = new byte[preamble.Length + bytes.Length];
            Buffer.BlockCopy(preamble, 0, withBom, 0, preamble.Length);
            Buffer.BlockCopy(bytes, 0, withBom, preamble.Length, bytes.Length);
            return withBom;
        }

        /// <summary>任意文字列を raw YAML byte として符号化します。</summary>
        internal static byte[] Utf8(string text)
        {
            return Encoding.UTF8.GetBytes(text ?? string.Empty);
        }

        /// <summary>asset path ごとに衝突しない absolute test path を作ります。</summary>
        internal static string CreatePhysicalPath(string assetPath)
        {
            var fileName = (assetPath ?? "missing")
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_');
            return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LocalizationKeyAuditTests", fileName));
        }
    }
}
