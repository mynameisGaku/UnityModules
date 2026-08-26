// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// SharedTableData の physical bytes を検証し、typed load 前の read-only 保証を確立します。
    /// </summary>
    internal static class LocalizationKeyAuditRawPreflight
    {
        /// <summary>Unity YAML で厳密に探す serialized field 名です。</summary>
        internal const string CollectionGuidFieldName = "m_TableCollectionNameGuidString";

        /// <summary>Unity YAML で SharedTableData の型を識別する direct field 名です。</summary>
        private const string ScriptFieldName = "m_Script";

        /// <summary>Unity YAML の MonoBehaviour root 直下で使われる空白数です。</summary>
        private const int DirectFieldIndentation = 2;

        /// <summary>
        /// 全 raw asset の収集と検証が完了した場合だけ identity 一覧を返します。
        /// </summary>
        internal static bool TryRun(
            ILocalizationKeyAuditRawSource source,
            out IReadOnlyList<LocalizationKeyAuditRawIdentity> identities,
            out string failureAssetPath,
            out string failureMessage)
        {
            identities = Array.Empty<LocalizationKeyAuditRawIdentity>();
            failureAssetPath = string.Empty;
            failureMessage = string.Empty;

            if (source == null)
            {
                failureMessage = "raw SharedTableData source がありません。";
                return false;
            }

            List<LocalizationKeyAuditRawAsset> assets;
            try
            {
                var sourceAssets = source.ReadSharedTableDataAssets();
                if (sourceAssets == null)
                {
                    failureMessage = "raw SharedTableData source が null を返しました。";
                    return false;
                }

                if (sourceAssets.Count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
                {
                    failureMessage = $"SharedTableData 数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。";
                    return false;
                }

                assets = new List<LocalizationKeyAuditRawAsset>(sourceAssets.Count);
                for (var index = 0; index < sourceAssets.Count; index++)
                {
                    assets.Add(sourceAssets[index]);
                }
            }
            catch (Exception exception)
            {
                failureMessage = $"raw SharedTableData の全件収集に失敗しました: {exception.GetType().Name}";
                return false;
            }

            assets.Sort(CompareRawAssets);
            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            var physicalPathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var physicalPaths = new HashSet<string>(physicalPathComparer);
            var validated = new List<LocalizationKeyAuditRawIdentity>(assets.Count);
            long totalBytes = 0;

            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];
                if (asset == null)
                {
                    failureMessage = "raw SharedTableData 一覧に null が含まれています。";
                    return false;
                }

                if (!TryValidatePath(asset.AssetPath, false, out failureMessage))
                {
                    return false;
                }

                failureAssetPath = asset.AssetPath;
                if (!TryValidatePath(asset.PhysicalPath, true, out failureMessage))
                {
                    return false;
                }

                if (!assetPaths.Add(asset.AssetPath))
                {
                    failureMessage = "同じ SharedTableData asset path が複数回列挙されました。";
                    return false;
                }

                if (!physicalPaths.Add(asset.PhysicalPath))
                {
                    failureMessage = "同じ SharedTableData physical path が複数 asset に対応しています。";
                    return false;
                }

                if (asset.HasReparsePoint)
                {
                    failureMessage = "SharedTableData path に reparse point が含まれています。";
                    return false;
                }

                if (!asset.Exists)
                {
                    failureMessage = "SharedTableData physical file が存在しません。";
                    return false;
                }

                if (asset.IsOversize || asset.ByteCount > LocalizationKeyAuditLimits.MaximumRawAssetBytes)
                {
                    failureMessage = $"SharedTableData が 1 file 上限 {LocalizationKeyAuditLimits.MaximumRawAssetBytes} bytes を超えています。";
                    return false;
                }

                if (!string.IsNullOrEmpty(asset.ReadError))
                {
                    failureMessage = $"SharedTableData physical file を読み取れません: {GetSafeReadErrorCode(asset.ReadError)}";
                    return false;
                }

                totalBytes += asset.ByteCount;
                if (totalBytes > LocalizationKeyAuditLimits.MaximumTotalRawBytes)
                {
                    failureMessage = $"SharedTableData の総 byte 数が上限 {LocalizationKeyAuditLimits.MaximumTotalRawBytes} を超えています。";
                    return false;
                }

                if (!TryParseCollectionGuid(asset.CopyBytes(), out var collectionGuid, out failureMessage))
                {
                    return false;
                }

                validated.Add(new LocalizationKeyAuditRawIdentity(asset.AssetPath, collectionGuid));
            }

            failureAssetPath = string.Empty;
            failureMessage = string.Empty;
            identities = new ReadOnlyCollection<LocalizationKeyAuditRawIdentity>(validated.ToArray());
            return true;
        }

        /// <summary>opaqueなraw source error本文を結果へ出さず安全な型名だけにします。</summary>
        private static string GetSafeReadErrorCode(string readError)
        {
            if (string.IsNullOrEmpty(readError) || readError.Length > 128)
            {
                return "present";
            }

            for (var index = 0; index < readError.Length; index++)
            {
                var character = readError[index];
                var isAsciiLetter = character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z';
                if (!isAsciiLetter && !(character >= '0' && character <= '9') &&
                    character != '_' && character != '.')
                {
                    return "present";
                }
            }

            return readError;
        }

        /// <summary>
        /// SharedTableData script を持つ単一 document の direct field を 1 件だけ解析します。
        /// </summary>
        private static bool TryParseCollectionGuid(byte[] bytes, out Guid collectionGuid, out string failureMessage)
        {
            collectionGuid = Guid.Empty;
            failureMessage = string.Empty;
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == 0)
                {
                    failureMessage = "SharedTableData は binary data を含んでいます。";
                    return false;
                }
            }

            string yaml;
            try
            {
                yaml = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                failureMessage = "SharedTableData を strict UTF-8 Unity YAML として読めません。";
                return false;
            }

            if (yaml.Length > 0 && yaml[0] == '\uFEFF')
            {
                yaml = yaml.Substring(1);
            }

            var hasDocument = false;
            var isMonoBehaviourDocument = false;
            var monoBehaviourRootCount = 0;
            var topLevelContentCount = 0;
            var currentRootIsMonoBehaviour = false;
            var directScriptCount = 0;
            var targetScriptCount = 0;
            var directCollectionFieldCount = 0;
            var documentSerializedValue = string.Empty;
            var targetDocumentCount = 0;
            var serializedValue = string.Empty;
            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (IsDocumentBoundary(line))
                    {
                        if (!TryCompleteDocument(
                                hasDocument,
                                monoBehaviourRootCount,
                                topLevelContentCount,
                                directScriptCount,
                                targetScriptCount,
                                directCollectionFieldCount,
                                documentSerializedValue,
                                ref targetDocumentCount,
                                ref serializedValue,
                                out failureMessage))
                        {
                            return false;
                        }

                        hasDocument = true;
                        isMonoBehaviourDocument = IsMonoBehaviourDocumentHeader(line);
                        monoBehaviourRootCount = 0;
                        topLevelContentCount = 0;
                        currentRootIsMonoBehaviour = false;
                        directScriptCount = 0;
                        targetScriptCount = 0;
                        directCollectionFieldCount = 0;
                        documentSerializedValue = string.Empty;
                        continue;
                    }

                    if (string.Equals(line, "MonoBehaviour:", StringComparison.Ordinal))
                    {
                        monoBehaviourRootCount++;
                        topLevelContentCount++;
                        currentRootIsMonoBehaviour = hasDocument &&
                            isMonoBehaviourDocument &&
                            monoBehaviourRootCount == 1;
                        continue;
                    }

                    if (IsOtherTopLevelContent(line))
                    {
                        topLevelContentCount++;
                        currentRootIsMonoBehaviour = false;
                    }

                    if (TryGetExactYamlValue(
                            line,
                            ScriptFieldName,
                            out var scriptIndentation,
                            out var scriptUsesSpacesOnly,
                            out var scriptValue))
                    {
                        var isTargetScript = TryGetInlineMappingValue(scriptValue, "guid", out var scriptGuid) &&
                            string.Equals(
                                scriptGuid,
                                UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid,
                                StringComparison.OrdinalIgnoreCase);
                        var isDirectScript = hasDocument &&
                            isMonoBehaviourDocument &&
                            monoBehaviourRootCount == 1 &&
                            currentRootIsMonoBehaviour &&
                            scriptUsesSpacesOnly &&
                            scriptIndentation == DirectFieldIndentation;
                        if (isTargetScript && !isDirectScript)
                        {
                            failureMessage = "SharedTableData の m_Script が MonoBehaviour root の direct field ではありません。";
                            return false;
                        }

                        if (isDirectScript)
                        {
                            directScriptCount++;
                            if (isTargetScript)
                            {
                                targetScriptCount++;
                            }
                        }

                        continue;
                    }

                    if (!TryGetExactYamlValue(
                            line,
                            CollectionGuidFieldName,
                            out var fieldIndentation,
                            out var fieldUsesSpacesOnly,
                            out var value))
                    {
                        continue;
                    }

                    var isDirectCollectionField = hasDocument &&
                        isMonoBehaviourDocument &&
                        monoBehaviourRootCount == 1 &&
                        currentRootIsMonoBehaviour &&
                        fieldUsesSpacesOnly &&
                        fieldIndentation == DirectFieldIndentation;
                    if (!isDirectCollectionField)
                    {
                        failureMessage = $"Unity YAML field {CollectionGuidFieldName} が MonoBehaviour root の direct field ではありません。";
                        return false;
                    }

                    directCollectionFieldCount++;
                    if (directCollectionFieldCount == 1)
                    {
                        documentSerializedValue = value;
                    }
                }
            }

            if (!TryCompleteDocument(
                    hasDocument,
                    monoBehaviourRootCount,
                    topLevelContentCount,
                    directScriptCount,
                    targetScriptCount,
                    directCollectionFieldCount,
                    documentSerializedValue,
                    ref targetDocumentCount,
                    ref serializedValue,
                    out failureMessage))
            {
                return false;
            }

            if (targetDocumentCount == 0)
            {
                failureMessage = "SharedTableData script GUID を持つ Unity YAML document がありません。";
                return false;
            }

            if (string.IsNullOrEmpty(serializedValue))
            {
                failureMessage = $"Unity YAML field {CollectionGuidFieldName} が空です。typed load は asset を dirty にする可能性があります。";
                return false;
            }

            if (!Guid.TryParse(serializedValue, out collectionGuid))
            {
                failureMessage = $"Unity YAML field {CollectionGuidFieldName} を GUID として解析できません。";
                return false;
            }

            if (collectionGuid == Guid.Empty)
            {
                failureMessage = $"Unity YAML field {CollectionGuidFieldName} が empty GUID です。typed load は asset を dirty にする可能性があります。";
                return false;
            }

            return true;
        }

        /// <summary>1 document の script と collection field の相関と一意性を確定します。</summary>
        private static bool TryCompleteDocument(
            bool hasDocument,
            int monoBehaviourRootCount,
            int topLevelContentCount,
            int directScriptCount,
            int targetScriptCount,
            int directCollectionFieldCount,
            string documentSerializedValue,
            ref int targetDocumentCount,
            ref string serializedValue,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (!hasDocument)
            {
                return true;
            }

            if (targetScriptCount == 0)
            {
                if (directCollectionFieldCount > 0)
                {
                    failureMessage = $"Unity YAML field {CollectionGuidFieldName} が SharedTableData script と同じ document にありません。";
                    return false;
                }

                return true;
            }

            if (targetScriptCount != 1 || directScriptCount != 1)
            {
                failureMessage = "SharedTableData の direct m_Script field を一意に確定できません。";
                return false;
            }

            if (monoBehaviourRootCount != 1 || topLevelContentCount != 1)
            {
                failureMessage = "SharedTableData document の top-level mapping owner を MonoBehaviour 一件へ限定できません。";
                return false;
            }

            targetDocumentCount++;
            if (targetDocumentCount > 1)
            {
                failureMessage = $"SharedTableData script GUID を持つ Unity YAML document が {targetDocumentCount} 件あります。";
                return false;
            }

            if (directCollectionFieldCount == 0)
            {
                failureMessage = $"Unity YAML field {CollectionGuidFieldName} が SharedTableData document にありません。";
                return false;
            }

            if (directCollectionFieldCount > 1)
            {
                failureMessage = $"Unity YAML field {CollectionGuidFieldName} が SharedTableData document に {directCollectionFieldCount} 件あります。";
                return false;
            }

            serializedValue = documentSerializedValue;
            return true;
        }

        /// <summary>Unity YAML document 境界として扱う行かを返します。</summary>
        private static bool IsDocumentBoundary(string line)
        {
            return string.Equals(line, "---", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal);
        }

        /// <summary>標準の MonoBehaviour document header かを返します。</summary>
        private static bool IsMonoBehaviourDocumentHeader(string line)
        {
            return line.StartsWith("--- !u!114 &", StringComparison.Ordinal);
        }

        /// <summary>MonoBehaviour以外へownerを移すtop-level content行かを返します。</summary>
        private static bool IsOtherTopLevelContent(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                line[0] != ' ' &&
                line[0] != '\t' &&
                line[0] != '#' &&
                line[0] != '%';
        }

        /// <summary>行頭空白と exact key に続く YAML value を取得します。</summary>
        private static bool TryGetExactYamlValue(
            string line,
            string fieldName,
            out int indentation,
            out bool usesSpacesOnly,
            out string value)
        {
            indentation = 0;
            usesSpacesOnly = true;
            value = string.Empty;
            while (indentation < line.Length && (line[indentation] == ' ' || line[indentation] == '\t'))
            {
                usesSpacesOnly &= line[indentation] == ' ';
                indentation++;
            }

            if (indentation + fieldName.Length > line.Length ||
                string.Compare(
                    line,
                    indentation,
                    fieldName,
                    0,
                    fieldName.Length,
                    StringComparison.Ordinal) != 0)
            {
                return false;
            }

            var cursor = indentation + fieldName.Length;
            while (cursor < line.Length && (line[cursor] == ' ' || line[cursor] == '\t'))
            {
                cursor++;
            }

            if (cursor >= line.Length || line[cursor] != ':')
            {
                return false;
            }

            value = line.Substring(cursor + 1).Trim();
            return true;
        }

        /// <summary>inline mapping の exact key を一件だけ取得します。</summary>
        private static bool TryGetInlineMappingValue(
            string serializedValue,
            string key,
            out string value)
        {
            value = string.Empty;
            var trimmed = serializedValue?.Trim() ?? string.Empty;
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            {
                return false;
            }

            var found = false;
            var entries = trimmed.Substring(1, trimmed.Length - 2).Split(',');
            for (var index = 0; index < entries.Length; index++)
            {
                var separator = entries[index].IndexOf(':');
                if (separator < 0 ||
                    !string.Equals(entries[index].Substring(0, separator).Trim(), key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found)
                {
                    value = string.Empty;
                    return false;
                }

                value = entries[index].Substring(separator + 1).Trim();
                found = true;
            }

            return found;
        }

        /// <summary>asset path または absolute physical path の基本形を検証します。</summary>
        private static bool TryValidatePath(string path, bool requireRooted, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || path.Length > LocalizationKeyAuditLimits.MaximumTextCharacters)
            {
                failureMessage = requireRooted
                    ? "SharedTableData physical path が空または長すぎます。"
                    : "SharedTableData asset path が空または長すぎます。";
                return false;
            }

            if (path.IndexOf('\0') >= 0 || (requireRooted && !Path.IsPathRooted(path)))
            {
                failureMessage = requireRooted
                    ? "SharedTableData physical path が absolute path ではありません。"
                    : "SharedTableData asset path が不正です。";
                return false;
            }

            if (requireRooted)
            {
                try
                {
                    var comparison = Path.DirectorySeparatorChar == '\\'
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                    var normalizedInput = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    if (!string.Equals(normalizedInput, Path.GetFullPath(path), comparison))
                    {
                        failureMessage = "SharedTableData physical path が正規化済み absolute path ではありません。";
                        return false;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException ||
                                                   exception is NotSupportedException ||
                                                   exception is PathTooLongException)
                {
                    failureMessage = $"SharedTableData physical path を正規化できません: {exception.GetType().Name}";
                    return false;
                }
            }

            if (!requireRooted)
            {
                if (path.IndexOf('\\') >= 0 ||
                    (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                     !path.StartsWith("Packages/", StringComparison.Ordinal)))
                {
                    failureMessage = "SharedTableData asset path が Unity relative path ではありません。";
                    return false;
                }

                var segments = path.Split('/');
                for (var index = 0; index < segments.Length; index++)
                {
                    if (segments[index].Length == 0 || segments[index] == "." || segments[index] == "..")
                    {
                        failureMessage = "SharedTableData asset path に不正な segment があります。";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>raw asset を asset path、physical path の順に並べます。</summary>
        private static int CompareRawAssets(LocalizationKeyAuditRawAsset left, LocalizationKeyAuditRawAsset right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.PhysicalPath, right.PhysicalPath, StringComparison.Ordinal);
        }
    }
}
