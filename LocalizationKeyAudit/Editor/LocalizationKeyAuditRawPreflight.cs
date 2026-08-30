// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 共有テーブルデータの物理バイト列を検証し、型として読み取る前の読み取り専用保証を確立します。
    /// </summary>
    internal static class LocalizationKeyAuditRawPreflight
    {
        /// <summary>Unity形式のYAMLで厳密に探す直列化項目名です。</summary>
        internal const string CollectionGuidFieldName = "m_TableCollectionNameGuidString";

        /// <summary>Unity形式のYAMLで共有テーブルデータの型を識別する直下項目名です。</summary>
        private const string ScriptFieldName = "m_Script";

        /// <summary>Unity形式のYAMLのMonoBehaviour最上位直下で使われる空白数です。</summary>
        private const int DirectFieldIndentation = 2;

        /// <summary>
        /// 全ての未加工アセットの収集と検証が完了した場合だけ識別情報一覧を返します。
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
                failureMessage = "未加工の共有テーブルデータの取得元がありません。";
                return false;
            }

            List<LocalizationKeyAuditRawAsset> assets;
            try
            {
                var sourceAssets = source.ReadSharedTableDataAssets();
                if (sourceAssets == null)
                {
                    failureMessage = "未加工の共有テーブルデータの取得元から戻り値がありません。";
                    return false;
                }

                if (sourceAssets.Count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
                {
                    failureMessage = $"共有テーブルデータ数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。";
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
                failureMessage = $"未加工の共有テーブルデータの全件収集に失敗しました：{exception.GetType().Name}";
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
                    failureMessage = "未加工の共有テーブルデータ一覧に未設定の要素が含まれています。";
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
                    failureMessage = "同じ共有テーブルデータのアセットパスが複数回列挙されました。";
                    return false;
                }

                if (!physicalPaths.Add(asset.PhysicalPath))
                {
                    failureMessage = "同じ共有テーブルデータの物理パスが複数のアセットに対応しています。";
                    return false;
                }

                if (asset.HasReparsePoint)
                {
                    failureMessage = "共有テーブルデータのパスに再解析点が含まれています。";
                    return false;
                }

                if (!asset.Exists)
                {
                    failureMessage = "共有テーブルデータの物理ファイルが存在しません。";
                    return false;
                }

                if (asset.IsOversize || asset.ByteCount > LocalizationKeyAuditLimits.MaximumRawAssetBytes)
                {
                    failureMessage = $"共有テーブルデータがファイル1件あたりの上限 {LocalizationKeyAuditLimits.MaximumRawAssetBytes} バイトを超えています。";
                    return false;
                }

                if (!string.IsNullOrEmpty(asset.ReadError))
                {
                    failureMessage = $"共有テーブルデータの物理ファイルを読み取れません：{GetSafeReadErrorCode(asset.ReadError)}";
                    return false;
                }

                totalBytes += asset.ByteCount;
                if (totalBytes > LocalizationKeyAuditLimits.MaximumTotalRawBytes)
                {
                    failureMessage = $"共有テーブルデータの総バイト数が上限 {LocalizationKeyAuditLimits.MaximumTotalRawBytes} を超えています。";
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

        /// <summary>取得元の不透明な読み取りエラー本文を結果へ出さず、安全な型名または有無だけにします。</summary>
        private static string GetSafeReadErrorCode(string readError)
        {
            if (string.IsNullOrEmpty(readError) || readError.Length > 128)
            {
                return "あり";
            }

            for (var index = 0; index < readError.Length; index++)
            {
                var character = readError[index];
                var isAsciiLetter = character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z';
                if (!isAsciiLetter && !(character >= '0' && character <= '9') &&
                    character != '_' && character != '.')
                {
                    return "あり";
                }
            }

            return readError;
        }

        /// <summary>
        /// 共有テーブルデータのスクリプトを持つ単一文書の直下項目を1件だけ解析します。
        /// </summary>
        private static bool TryParseCollectionGuid(byte[] bytes, out Guid collectionGuid, out string failureMessage)
        {
            collectionGuid = Guid.Empty;
            failureMessage = string.Empty;
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == 0)
                {
                    failureMessage = "共有テーブルデータにバイナリデータが含まれています。";
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
                failureMessage = "共有テーブルデータを厳密なUTF-8のUnity形式のYAMLとして読めません。";
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
                            failureMessage = "共有テーブルデータのm_ScriptがMonoBehaviourの直下項目ではありません。";
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
                        failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} がMonoBehaviourの直下項目ではありません。";
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
                failureMessage = "共有テーブルデータのスクリプトGUIDを持つUnity形式のYAML文書がありません。";
                return false;
            }

            if (string.IsNullOrEmpty(serializedValue))
            {
                failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} が空です。型として読み取るとアセットが未保存変更ありの状態になる可能性があります。";
                return false;
            }

            if (!Guid.TryParse(serializedValue, out collectionGuid))
            {
                failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} をGUIDとして解析できません。";
                return false;
            }

            if (collectionGuid == Guid.Empty)
            {
                failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} が空のGUIDです。型として読み取るとアセットが未保存変更ありの状態になる可能性があります。";
                return false;
            }

            return true;
        }

        /// <summary>1文書内のスクリプトとコレクション項目の対応と一意性を確定します。</summary>
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
                    failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} が共有テーブルデータのスクリプトと同じ文書にありません。";
                    return false;
                }

                return true;
            }

            if (targetScriptCount != 1 || directScriptCount != 1)
            {
                failureMessage = "共有テーブルデータの直下にあるm_Script項目を一意に確定できません。";
                return false;
            }

            if (monoBehaviourRootCount != 1 || topLevelContentCount != 1)
            {
                failureMessage = "共有テーブルデータ文書の最上位マッピングの所有元をMonoBehaviour 1件に限定できません。";
                return false;
            }

            targetDocumentCount++;
            if (targetDocumentCount > 1)
            {
                failureMessage = $"共有テーブルデータのスクリプトGUIDを持つUnity形式のYAML文書が {targetDocumentCount} 件あります。";
                return false;
            }

            if (directCollectionFieldCount == 0)
            {
                failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} が共有テーブルデータ文書にありません。";
                return false;
            }

            if (directCollectionFieldCount > 1)
            {
                failureMessage = $"Unity形式のYAMLの項目 {CollectionGuidFieldName} が共有テーブルデータ文書に {directCollectionFieldCount} 件あります。";
                return false;
            }

            serializedValue = documentSerializedValue;
            return true;
        }

        /// <summary>Unity形式のYAML文書の境界として扱う行かを返します。</summary>
        private static bool IsDocumentBoundary(string line)
        {
            return string.Equals(line, "---", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal);
        }

        /// <summary>標準のMonoBehaviour文書ヘッダーかを返します。</summary>
        private static bool IsMonoBehaviourDocumentHeader(string line)
        {
            return line.StartsWith("--- !u!114 &", StringComparison.Ordinal);
        }

        /// <summary>所有元をMonoBehaviour以外へ移す最上位内容の行かを返します。</summary>
        private static bool IsOtherTopLevelContent(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                line[0] != ' ' &&
                line[0] != '\t' &&
                line[0] != '#' &&
                line[0] != '%';
        }

        /// <summary>行頭空白と完全一致するキーに続くYAML値を取得します。</summary>
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

        /// <summary>行内マッピングから完全一致するキーを1件だけ取得します。</summary>
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

        /// <summary>アセットパスまたは絶対物理パスの基本形を検証します。</summary>
        private static bool TryValidatePath(string path, bool requireRooted, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || path.Length > LocalizationKeyAuditLimits.MaximumTextCharacters)
            {
                failureMessage = requireRooted
                    ? "共有テーブルデータの物理パスが空か長すぎます。"
                    : "共有テーブルデータのアセットパスが空か長すぎます。";
                return false;
            }

            if (path.IndexOf('\0') >= 0 || (requireRooted && !Path.IsPathRooted(path)))
            {
                failureMessage = requireRooted
                    ? "共有テーブルデータの物理パスが絶対パスではありません。"
                    : "共有テーブルデータのアセットパスが不正です。";
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
                        failureMessage = "共有テーブルデータの物理パスが正規化済みの絶対パスではありません。";
                        return false;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException ||
                                                   exception is NotSupportedException ||
                                                   exception is PathTooLongException)
                {
                    failureMessage = $"共有テーブルデータの物理パスを正規化できません：{exception.GetType().Name}";
                    return false;
                }
            }

            if (!requireRooted)
            {
                if (path.IndexOf('\\') >= 0 ||
                    (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                     !path.StartsWith("Packages/", StringComparison.Ordinal)))
                {
                    failureMessage = "共有テーブルデータのアセットパスがUnityの相対パスではありません。";
                    return false;
                }

                var segments = path.Split('/');
                for (var index = 0; index < segments.Length; index++)
                {
                    if (segments[index].Length == 0 || segments[index] == "." || segments[index] == "..")
                    {
                        failureMessage = "共有テーブルデータのアセットパスに不正な区切り要素があります。";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>未加工アセットをアセットパス、物理パスの順に並べます。</summary>
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
