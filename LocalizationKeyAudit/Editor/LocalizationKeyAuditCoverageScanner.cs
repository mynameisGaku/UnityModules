// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// Unity YAML の GUID + key ID 形式だけを direct static reference として保守的に認識します。
    /// </summary>
    internal static class LocalizationKeyAuditCoverageScanner
    {
        /// <summary>全 asset を検証後にだけ complete coverage を返し、失敗時は partial references を破棄します。</summary>
        internal static LocalizationKeyAuditCoverage Scan(
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths,
            ILocalizationKeyAuditCoverageSource source,
            int maximumStaticReferences = LocalizationKeyAuditLimits.MaximumStaticReferences)
        {
            var declaredPaths = CopyAndValidateDeclaredPaths(declaredAssetPaths, out var pathFailure);
            if (!string.IsNullOrEmpty(pathFailure))
            {
                return CreateIncomplete(scopeDescription, declaredPaths, pathFailure);
            }

            if (string.IsNullOrWhiteSpace(scopeDescription) ||
                scopeDescription.Length > LocalizationKeyAuditLimits.MaximumTextCharacters ||
                !string.Equals(scopeDescription, scopeDescription.Trim(), StringComparison.Ordinal))
            {
                return CreateIncomplete(scopeDescription, declaredPaths, "coverage scope description が空、不正、または長すぎます。");
            }

            if (source == null)
            {
                return CreateIncomplete(scopeDescription, declaredPaths, "static reference coverage source がありません。");
            }

            if (maximumStaticReferences < 0 ||
                maximumStaticReferences > LocalizationKeyAuditLimits.MaximumStaticReferences)
            {
                return CreateIncomplete(scopeDescription, declaredPaths, "static reference 上限が不正です。");
            }

            List<LocalizationKeyAuditCoverageAsset> assets;
            try
            {
                var sourceAssets = source.ReadAssets(declaredPaths);
                if (sourceAssets == null)
                {
                    return CreateIncomplete(scopeDescription, declaredPaths, "coverage source が null を返しました。");
                }

                if (sourceAssets.Count > LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles)
                {
                    return CreateIncomplete(
                        scopeDescription,
                        declaredPaths,
                        $"coverage file 数が上限 {LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles} 件を超えています。");
                }

                assets = new List<LocalizationKeyAuditCoverageAsset>(sourceAssets.Count);
                for (var index = 0; index < sourceAssets.Count; index++)
                {
                    assets.Add(sourceAssets[index]);
                }
            }
            catch (Exception exception)
            {
                return CreateIncomplete(
                    scopeDescription,
                    declaredPaths,
                    $"coverage 全件収集に失敗しました: {exception.GetType().Name}");
            }

            assets.Sort(CompareAssets);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var references = new List<LocalizationKeyAuditStaticReference>();
            var referenceIdentities = new HashSet<string>(StringComparer.Ordinal);
            long totalBytes = 0;
            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];
                if (asset == null ||
                    !IsProjectAssetPath(asset.AssetPath) ||
                    !IsInsideDeclaredScope(asset.AssetPath, declaredPaths) ||
                    !paths.Add(asset.AssetPath))
                {
                    return CreateIncomplete(scopeDescription, declaredPaths, "coverage asset path が null、不正、または重複しています。");
                }

                if (!asset.Exists || asset.HasReparsePoint || asset.IsOversize ||
                    asset.ByteCount > LocalizationKeyAuditLimits.MaximumCoverageAssetBytes ||
                    !string.IsNullOrEmpty(asset.ReadError))
                {
                    return CreateIncomplete(
                        scopeDescription,
                        declaredPaths,
                        $"{asset.AssetPath} を安全に読み取れません: exists={asset.Exists}, reparse={asset.HasReparsePoint}, oversize={asset.IsOversize}, error={GetSafeReadErrorCode(asset.ReadError)}");
                }

                totalBytes += asset.ByteCount;
                if (totalBytes > LocalizationKeyAuditLimits.MaximumCoverageTotalBytes)
                {
                    return CreateIncomplete(
                        scopeDescription,
                        declaredPaths,
                        $"coverage 総 byte 数が上限 {LocalizationKeyAuditLimits.MaximumCoverageTotalBytes} を超えています。");
                }

                if (!TryParseAsset(
                        asset.AssetPath,
                        asset.CopyBytes(),
                        references,
                        referenceIdentities,
                        maximumStaticReferences,
                        out var failure))
                {
                    return CreateIncomplete(scopeDescription, declaredPaths, $"{asset.AssetPath}: {failure}");
                }

                if (references.Count > maximumStaticReferences)
                {
                    return CreateIncomplete(
                        scopeDescription,
                        declaredPaths,
                        $"static reference 数が上限 {maximumStaticReferences} 件を超えています。");
                }
            }

            references.Sort(CompareReferences);
            return new LocalizationKeyAuditCoverage(
                scopeDescription,
                declaredPaths,
                references,
                true,
                string.Empty);
        }

        /// <summary>1 Unity YAML asset から隣接する table/entry reference pair を抽出します。</summary>
        private static bool TryParseAsset(
            string assetPath,
            byte[] bytes,
            List<LocalizationKeyAuditStaticReference> references,
            HashSet<string> referenceIdentities,
            int maximumStaticReferences,
            out string failure)
        {
            failure = string.Empty;
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == 0)
                {
                    failure = "binary data は v1 static reference scope で未対応です。";
                    return false;
                }
            }

            string yaml;
            try
            {
                yaml = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\ufeff');
            }
            catch (DecoderFallbackException)
            {
                failure = "strict UTF-8 Unity YAML として読めません。";
                return false;
            }

            if (!yaml.StartsWith("%YAML ", StringComparison.Ordinal))
            {
                failure = "Unity YAML header がない text/binary format は未対応です。";
                return false;
            }

            if (!TryReadYamlLines(yaml, out var lines, out failure) ||
                !TryValidateScalarContexts(lines, out failure))
            {
                return false;
            }

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!TryReadLine(lines[lineIndex], out var indent, out var content, out failure))
                {
                    return false;
                }

                if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(content, "m_TableReference:", StringComparison.Ordinal))
                {
                    if (IsSequenceTableReference(content))
                    {
                        failure = "YAML sequence 内の m_TableReference は conservative parser で未対応です。";
                        return false;
                    }

                    if (content.IndexOf("m_TableReference", StringComparison.Ordinal) >= 0)
                    {
                        failure = "non-canonical、inline、またはflow mappingのm_TableReferenceはconservative parserで未対応です。";
                        return false;
                    }

                    continue;
                }

                var tableBlockEnd = FindBlockEnd(lines, lineIndex + 1, indent, out failure);
                if (tableBlockEnd < 0)
                {
                    return false;
                }

                var siblingIndex = FindNextContentLine(lines, tableBlockEnd, out var siblingIndent, out var siblingContent, out failure);
                if (siblingIndex < 0)
                {
                    if (string.IsNullOrEmpty(failure))
                    {
                        failure = "m_TableReference の後で YAML が終了し、entry reference の有無を確定できません。";
                    }

                    return false;
                }

                if (siblingIndent != indent || !string.Equals(siblingContent, "m_TableEntryReference:", StringComparison.Ordinal))
                {
                    failure = "m_TableReference の直後に同じ indent の exact m_TableEntryReference block がなく、reference shape を確定できません。";
                    return false;
                }

                if (!TryReadUniqueChildValue(
                        lines,
                        lineIndex + 1,
                        tableBlockEnd,
                        indent,
                        "m_TableCollectionName",
                        out var serializedTable,
                        out failure))
                {
                    return false;
                }

                var entryBlockEnd = FindBlockEnd(lines, siblingIndex + 1, siblingIndent, out failure);
                if (entryBlockEnd < 0 ||
                    !TryReadUniqueChildValue(
                        lines,
                        siblingIndex + 1,
                        entryBlockEnd,
                        siblingIndent,
                        "m_KeyId",
                        out var serializedId,
                        out failure))
                {
                    return false;
                }

                serializedTable = Unquote(serializedTable);
                if (!serializedTable.StartsWith("GUID:", StringComparison.Ordinal) ||
                    !Guid.TryParse(serializedTable.Substring(5), out var collectionGuid) ||
                    collectionGuid == Guid.Empty)
                {
                    failure = "name-based、empty、または malformed table reference は GUID identity coverage で未対応です。";
                    return false;
                }

                if (!long.TryParse(serializedId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entryId) || entryId == 0)
                {
                    failure = "name-based、empty、または malformed table entry reference は key ID coverage で未対応です。";
                    return false;
                }

                var identity = assetPath + "\0" + collectionGuid.ToString("N") + "\0" + entryId;
                if (!referenceIdentities.Contains(identity))
                {
                    if (references.Count >= maximumStaticReferences)
                    {
                        failure = $"static reference 数が上限 {maximumStaticReferences} 件を超えています。";
                        return false;
                    }

                    referenceIdentities.Add(identity);
                    references.Add(new LocalizationKeyAuditStaticReference(
                        assetPath,
                        collectionGuid,
                        entryId,
                        string.Empty,
                        string.Empty));
                }

                lineIndex = entryBlockEnd - 1;
            }

            return true;
        }

        /// <summary>block scalar本文を構造解析から除外し、tab indentationは全行で拒否します。</summary>
        private static bool TryValidateScalarContexts(
            string[] sourceLines,
            out string failure)
        {
            failure = string.Empty;
            for (var index = 0; index < sourceLines.Length; index++)
            {
                if (!TryReadLine(sourceLines[index], out var indent, out var content, out failure))
                {
                    return false;
                }

                if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsUnsupportedExplicitMappingKey(content))
                {
                    failure = "explicit mapping keyはv1 conservative static reference parserで未対応です。";
                    return false;
                }

                if (IsBlockScalarHeader(content))
                {
                    failure = "block scalarはv1 conservative static reference parserで未対応です。";
                    return false;
                }

                if (HasUnclosedQuotedScalar(content))
                {
                    failure = "複数行quoted scalarはv1 conservative static reference parserで未対応です。";
                    return false;
                }

                if (HasUnclosedFlowCollection(content))
                {
                    failure = "複数行flow collectionはv1 conservative static reference parserで未対応です。";
                    return false;
                }
            }

            return true;
        }

        /// <summary>CR/LF/CRLFを同じline境界として読み、Split allocation前にline数を制限します。</summary>
        private static bool TryReadYamlLines(string yaml, out string[] lines, out string failure)
        {
            failure = string.Empty;
            var values = new List<string>();
            using (var reader = new System.IO.StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!IsCoverageYamlLineCountWithinLimit(values.Count + 1))
                    {
                        lines = Array.Empty<string>();
                        failure = $"Unity YAML line数が上限 {LocalizationKeyAuditLimits.MaximumCoverageYamlLines} 件を超えています。";
                        return false;
                    }

                    values.Add(line);
                }
            }

            lines = values.ToArray();
            return true;
        }

        /// <summary>次のUnity YAML lineを保持する前にhard limitを確認します。</summary>
        internal static bool IsCoverageYamlLineCountWithinLimit(int count)
        {
            return count >= 0 && count <= LocalizationKeyAuditLimits.MaximumCoverageYamlLines;
        }

        /// <summary>mapping valueがYAML literal/folded block scalar indicatorかを調べます。</summary>
        private static bool IsBlockScalarHeader(string content)
        {
            var value = GetNormalizedNodeValue(content);
            if (value.Length == 0)
            {
                return false;
            }

            var tokens = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if ((token.StartsWith("&", StringComparison.Ordinal) && token.Length > 1) ||
                    token.StartsWith("!", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsBlockScalarIndicator(token))
                {
                    return false;
                }

                return index + 1 == tokens.Length ||
                    tokens[index + 1].StartsWith("#", StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>mapping/sequence valueで開始したquoteが同じline内に閉じているかを調べます。</summary>
        private static bool HasUnclosedQuotedScalar(string content)
        {
            var value = GetNormalizedNodeValue(content);
            while (value.StartsWith("&", StringComparison.Ordinal) ||
                   value.StartsWith("!", StringComparison.Ordinal))
            {
                var propertyEnd = value.IndexOf(' ');
                if (propertyEnd < 0)
                {
                    return false;
                }

                value = value.Substring(propertyEnd + 1).TrimStart(' ');
            }

            if (value.Length == 0 || (value[0] != '\'' && value[0] != '"'))
            {
                return false;
            }

            var quote = value[0];
            for (var index = 1; index < value.Length; index++)
            {
                if (quote == '"' && value[index] == '\\')
                {
                    index++;
                    continue;
                }

                if (value[index] != quote)
                {
                    continue;
                }

                if (quote == '\'' && index + 1 < value.Length && value[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>mapping valueとして開始したflow collectionが同じlineで閉じるかを調べます。</summary>
        private static bool HasUnclosedFlowCollection(string content)
        {
            var value = GetNormalizedNodeValue(content);
            while (value.StartsWith("&", StringComparison.Ordinal) ||
                   value.StartsWith("!", StringComparison.Ordinal))
            {
                var propertyEnd = value.IndexOf(' ');
                if (propertyEnd < 0)
                {
                    return false;
                }

                value = value.Substring(propertyEnd + 1).TrimStart(' ');
            }

            if (value.Length == 0 || (value[0] != '{' && value[0] != '['))
            {
                return false;
            }

            var curlyDepth = 0;
            var squareDepth = 0;
            var inSingleQuote = false;
            var inDoubleQuote = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (inDoubleQuote)
                {
                    if (character == '\\')
                    {
                        index++;
                    }
                    else if (character == '"')
                    {
                        inDoubleQuote = false;
                    }

                    continue;
                }

                if (inSingleQuote)
                {
                    if (character != '\'')
                    {
                        continue;
                    }

                    if (index + 1 < value.Length && value[index + 1] == '\'')
                    {
                        index++;
                        continue;
                    }

                    inSingleQuote = false;
                    continue;
                }

                if (character == '"')
                {
                    inDoubleQuote = true;
                }
                else if (character == '\'')
                {
                    inSingleQuote = true;
                }
                else if (character == '#')
                {
                    break;
                }
                else if (character == '{')
                {
                    curlyDepth++;
                }
                else if (character == '}')
                {
                    curlyDepth--;
                }
                else if (character == '[')
                {
                    squareDepth++;
                }
                else if (character == ']')
                {
                    squareDepth--;
                }

                if (curlyDepth < 0 || squareDepth < 0)
                {
                    return true;
                }
            }

            return inSingleQuote || inDoubleQuote || curlyDepth != 0 || squareDepth != 0;
        }

        /// <summary>nested sequence markerを除き、mappingならvalue側を返します。</summary>
        private static string GetNormalizedNodeValue(string content)
        {
            var node = RemoveLeadingSequenceMarkers(content);
            var separator = FindMappingValueSeparator(node);
            return separator >= 0
                ? node.Substring(separator + 1).TrimStart(' ')
                : node;
        }

        /// <summary>v1で構造を確定できないexplicit mapping keyを検出します。</summary>
        private static bool IsUnsupportedExplicitMappingKey(string content)
        {
            var node = RemoveLeadingSequenceMarkers(content);
            return string.Equals(node, "?", StringComparison.Ordinal) ||
                node.StartsWith("? ", StringComparison.Ordinal);
        }

        /// <summary>nested block sequence markerを順に除きます。</summary>
        private static string RemoveLeadingSequenceMarkers(string content)
        {
            var node = content;
            while (node.StartsWith("-", StringComparison.Ordinal) &&
                   (node.Length == 1 || node[1] == ' '))
            {
                node = node.Substring(1).TrimStart(' ');
            }

            return node;
        }

        /// <summary>mapping keyの終端colonをquoteやtag内部のcolonと区別します。</summary>
        private static int FindMappingValueSeparator(string content)
        {
            var inSingleQuote = false;
            var inDoubleQuote = false;
            var curlyDepth = 0;
            var squareDepth = 0;
            for (var index = 0; index < content.Length; index++)
            {
                var character = content[index];
                if (inDoubleQuote)
                {
                    if (character == '\\')
                    {
                        index++;
                    }
                    else if (character == '"')
                    {
                        inDoubleQuote = false;
                    }

                    continue;
                }

                if (inSingleQuote)
                {
                    if (character != '\'')
                    {
                        continue;
                    }

                    if (index + 1 < content.Length && content[index + 1] == '\'')
                    {
                        index++;
                        continue;
                    }

                    inSingleQuote = false;
                    continue;
                }

                if (character == '"')
                {
                    inDoubleQuote = true;
                    continue;
                }

                if (character == '\'')
                {
                    inSingleQuote = true;
                    continue;
                }

                if (character == '{')
                {
                    curlyDepth++;
                    continue;
                }

                if (character == '}')
                {
                    curlyDepth--;
                    continue;
                }

                if (character == '[')
                {
                    squareDepth++;
                    continue;
                }

                if (character == ']')
                {
                    squareDepth--;
                    continue;
                }

                if (character == ':' &&
                    curlyDepth == 0 && squareDepth == 0 &&
                    (index + 1 == content.Length || content[index + 1] == ' '))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>literal/folded scalar indicatorとchomping/indent suffixだけを受理します。</summary>
        private static bool IsBlockScalarIndicator(string token)
        {
            if (string.IsNullOrEmpty(token) || (token[0] != '|' && token[0] != '>'))
            {
                return false;
            }

            for (var index = 1; index < token.Length; index++)
            {
                if (token[index] != '+' && token[index] != '-' &&
                    (token[index] < '1' || token[index] > '9'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>sequence要素として現れたtable reference候補を安全側で検出します。</summary>
        private static bool IsSequenceTableReference(string content)
        {
            if (string.IsNullOrEmpty(content) || content[0] != '-')
            {
                return false;
            }

            var cursor = 1;
            while (cursor < content.Length && content[cursor] == ' ')
            {
                cursor++;
            }

            return content.Substring(cursor).StartsWith("m_TableReference:", StringComparison.Ordinal);
        }

        /// <summary>parent より深い行の終了位置を返します。</summary>
        private static int FindBlockEnd(string[] lines, int startIndex, int parentIndent, out string failure)
        {
            failure = string.Empty;
            for (var index = startIndex; index < lines.Length; index++)
            {
                if (!TryReadLine(lines[index], out var indent, out var content, out failure))
                {
                    return -1;
                }

                if (content.Length != 0 && indent <= parentIndent)
                {
                    return index;
                }
            }

            return lines.Length;
        }

        /// <summary>空行と comment を飛ばした次行を返します。</summary>
        private static int FindNextContentLine(
            string[] lines,
            int startIndex,
            out int indent,
            out string content,
            out string failure)
        {
            indent = 0;
            content = string.Empty;
            failure = string.Empty;
            for (var index = startIndex; index < lines.Length; index++)
            {
                if (!TryReadLine(lines[index], out indent, out content, out failure))
                {
                    return -1;
                }

                if (content.Length != 0 && !content.StartsWith("#", StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>block 内の exact YAML field value が 1 件だけかを検証します。</summary>
        private static bool TryReadUniqueChildValue(
            string[] lines,
            int startIndex,
            int endIndex,
            int parentIndent,
            string fieldName,
            out string value,
            out string failure)
        {
            value = string.Empty;
            failure = string.Empty;
            var count = 0;
            var prefix = fieldName + ":";
            for (var index = startIndex; index < endIndex; index++)
            {
                if (!TryReadLine(lines[index], out var indent, out var content, out failure))
                {
                    return false;
                }

                if (!content.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (content.Length > prefix.Length && content[prefix.Length] != ' ')
                {
                    continue;
                }

                if (indent != parentIndent + 2)
                {
                    failure = $"{fieldName} がexact direct childではなく、reference shapeを確定できません。";
                    return false;
                }

                count++;
                value = content.Substring(prefix.Length).TrimStart(' ');
            }

            if (count != 1 || value.Length == 0)
            {
                failure = $"{fieldName} が 1 件の non-empty scalar ではありません。";
                return false;
            }

            return true;
        }

        /// <summary>space indentation と content を分離し、tab indentation を拒否します。</summary>
        private static bool TryReadLine(string raw, out int indent, out string content, out string failure)
        {
            indent = 0;
            content = string.Empty;
            failure = string.Empty;
            var line = raw.EndsWith("\r", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 1) : raw;
            if (line.IndexOf('\t') >= 0)
            {
                failure = "tab character は conservative Unity YAML parser で未対応です。";
                return false;
            }

            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            content = line.Substring(indent);
            return true;
        }

        /// <summary>simple YAML quote だけを除きます。</summary>
        private static string Unquote(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '\'' && value[value.Length - 1] == '\'') ||
                 (value[0] == '"' && value[value.Length - 1] == '"')))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        /// <summary>scanner対象のUnity asset file pathかを調べます。</summary>
        private static bool IsProjectAssetPath(string path)
        {
            if (!IsDeclaredProjectPath(path) || path == "Assets")
            {
                return false;
            }

            var segments = path.Split('/');
            return segments[0] != "Packages" || segments.Length >= 3;
        }

        /// <summary>rootを含む安全なAssets/Packages declared pathかを調べます。</summary>
        private static bool IsDeclaredProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path.Length > LocalizationKeyAuditLimits.MaximumTextCharacters ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf('\0') >= 0)
            {
                return false;
            }

            var segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == "." || segments[index] == ".." ||
                    segments[index].IndexOf('~') >= 0 || segments[index].IndexOf(':') >= 0 ||
                    segments[index].EndsWith(".", StringComparison.Ordinal) ||
                    segments[index].EndsWith(" ", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return segments[0] == "Assets" ||
                (segments[0] == "Packages" && segments.Length >= 2);
        }

        /// <summary>source asset が宣言済み asset または folder の内側かを調べます。</summary>
        private static bool IsInsideDeclaredScope(string assetPath, IReadOnlyList<string> declaredPaths)
        {
            for (var index = 0; index < declaredPaths.Count; index++)
            {
                var declared = declaredPaths[index];
                if (string.Equals(assetPath, declared, StringComparison.Ordinal) ||
                    assetPath.StartsWith(declared.TrimEnd('/') + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>partial references を持たない incomplete coverage を作ります。</summary>
        private static LocalizationKeyAuditCoverage CreateIncomplete(
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths,
            string reason)
        {
            return new LocalizationKeyAuditCoverage(
                scopeDescription,
                declaredAssetPaths,
                Array.Empty<LocalizationKeyAuditStaticReference>(),
                false,
                reason);
        }

        /// <summary>filesystem access前にAssets/Packages pathをbounded snapshotにします。</summary>
        private static IReadOnlyList<string> CopyAndValidateDeclaredPaths(
            IReadOnlyList<string> source,
            out string failure)
        {
            failure = string.Empty;
            if (source == null || source.Count == 0)
            {
                failure = "static reference の asset scope path を 1 件以上宣言してください。";
                return Array.Empty<string>();
            }

            if (source.Count > LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths)
            {
                failure = $"declared asset path 数が上限 {LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths} 件を超えています。";
                return Array.Empty<string>();
            }

            var paths = new List<string>(source.Count);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var logicalRoot = string.Empty;
            for (var index = 0; index < source.Count; index++)
            {
                var path = source[index];
                if (!IsDeclaredProjectPath(path) || !unique.Add(path))
                {
                    failure = "declared asset path が不正、重複、または対応scope外です。";
                    return Array.Empty<string>();
                }

                var candidateRoot = GetLogicalRoot(path);
                if (logicalRoot.Length == 0)
                {
                    logicalRoot = candidateRoot;
                }
                else if (!string.Equals(logicalRoot, candidateRoot, StringComparison.Ordinal))
                {
                    failure = "1 回の監査で宣言できるlogical rootはAssetsまたは1つのregistered packageだけです。";
                    return Array.Empty<string>();
                }

                paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(paths.ToArray());
        }

        /// <summary>opaqueなsource error本文を結果へ出さず、安全な状態codeだけにします。</summary>
        private static string GetSafeReadErrorCode(string readError)
        {
            if (string.IsNullOrEmpty(readError))
            {
                return "none";
            }

            if (readError.Length > 128)
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

        /// <summary>declared pathをAssetsまたはPackages/package-name rootへ正規化します。</summary>
        private static string GetLogicalRoot(string path)
        {
            if (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "Assets";
            }

            var separator = path.IndexOf('/', "Packages/".Length);
            return separator < 0 ? path : path.Substring(0, separator);
        }

        /// <summary>asset path で決定論的に並べます。</summary>
        private static int CompareAssets(LocalizationKeyAuditCoverageAsset left, LocalizationKeyAuditCoverageAsset right)
        {
            return string.Compare(left?.AssetPath, right?.AssetPath, StringComparison.Ordinal);
        }

        /// <summary>source path、GUID、entry ID の順に並べます。</summary>
        private static int CompareReferences(LocalizationKeyAuditStaticReference left, LocalizationKeyAuditStaticReference right)
        {
            var comparison = string.Compare(left.SourceAssetPath, right.SourceAssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.CollectionGuid.CompareTo(right.CollectionGuid);
            return comparison != 0 ? comparison : left.EntryId.CompareTo(right.EntryId);
        }
    }
}
