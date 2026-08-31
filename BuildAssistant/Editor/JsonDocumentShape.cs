using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BuildAssistant.Editor
{
    /// <summary>JSONの値を保持せず、項目名と入れ子構造だけを安全に読み取ります。</summary>
    internal sealed class JsonDocumentShape
    {
        private const int MaximumDepth = 16;
        private const int MaximumValueCount = 2000000;
        private const int MaximumContainerCount = 300000;
        private const int MaximumObjectMemberCount = 64;
        private const int MaximumGeneralArrayItemCount = 4096;
        private static readonly JsonDocumentShape StringShape = new JsonDocumentShape(JsonValueKind.String, null, null);
        private static readonly JsonDocumentShape IntegerShape = new JsonDocumentShape(JsonValueKind.Integer, null, null);
        private static readonly JsonDocumentShape NumberShape = new JsonDocumentShape(JsonValueKind.Number, null, null);
        private static readonly JsonDocumentShape BooleanShape = new JsonDocumentShape(JsonValueKind.Boolean, null, null);
        private static readonly JsonDocumentShape NullShape = new JsonDocumentShape(JsonValueKind.Null, null, null);
        private readonly JsonValueKind kind;
        private readonly Dictionary<string, JsonDocumentShape> members;
        private readonly List<JsonDocumentShape> items;

        private JsonDocumentShape(JsonValueKind kind, Dictionary<string, JsonDocumentShape> members, List<JsonDocumentShape> items)
        {
            this.kind = kind;
            this.members = members;
            this.items = items;
        }

        /// <summary>文字列値かを返します。</summary>
        internal bool IsString => kind == JsonValueKind.String;

        /// <summary>32ビット符号付き整数かを返します。</summary>
        internal bool IsInteger => kind == JsonValueKind.Integer;

        /// <summary>真偽値かを返します。</summary>
        internal bool IsBoolean => kind == JsonValueKind.Boolean;

        /// <summary>指定した項目だけを一度ずつ持つオブジェクトかを確認します。</summary>
        internal bool HasExactMembers(IReadOnlyList<string> requiredMembers)
        {
            if (kind != JsonValueKind.Object || requiredMembers == null || members.Count != requiredMembers.Count)
                return false;
            for (var index = 0; index < requiredMembers.Count; index++)
            {
                if (!members.ContainsKey(requiredMembers[index]))
                    return false;
            }
            return true;
        }

        /// <summary>オブジェクトから指定した項目を取得します。</summary>
        internal bool TryGetMember(string name, out JsonDocumentShape value)
        {
            value = null;
            return kind == JsonValueKind.Object && members.TryGetValue(name, out value);
        }

        /// <summary>配列の全要素を取得します。</summary>
        internal bool TryGetItems(out IReadOnlyList<JsonDocumentShape> values)
        {
            values = items;
            return kind == JsonValueKind.Array;
        }

        /// <summary>JSONを最後まで解析し、重複項目や過剰な深さを拒否します。</summary>
        internal static bool TryParse(string json, out JsonDocumentShape shape)
        {
            shape = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                shape = new Parser(json).Parse();
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is InvalidDataException || exception is OverflowException)
            {
                return false;
            }
        }

        private enum JsonValueKind
        {
            String,
            Integer,
            Number,
            Boolean,
            Null,
            Object,
            Array
        }

        /// <summary>文書容量に比例した作業量でJSON構造を読み取ります。</summary>
        private sealed class Parser
        {
            private readonly string json;
            private int position;
            private int valueCount;
            private int containerCount;

            internal Parser(string json)
            {
                this.json = json;
            }

            internal JsonDocumentShape Parse()
            {
                SkipWhitespace();
                var result = ParseValue(0, string.Empty);
                SkipWhitespace();
                if (position != json.Length)
                    throw new FormatException("JSON文書の末尾に余分な内容があります。");
                return result;
            }

            private JsonDocumentShape ParseValue(int depth, string memberName)
            {
                valueCount = checked(valueCount + 1);
                if (valueCount > MaximumValueCount)
                    throw new InvalidDataException("JSON文書の項目数が上限を超えています。");
                if (depth > MaximumDepth || position >= json.Length)
                    throw new FormatException("JSON文書の入れ子が深すぎるか、値がありません。");

                switch (json[position])
                {
                    case '{':
                        CountContainer();
                        return ParseObject(depth);
                    case '[':
                        CountContainer();
                        return ParseArray(depth, memberName);
                    case '"':
                        ParseString(false);
                        return StringShape;
                    case 't':
                        ConsumeLiteral("true");
                        return BooleanShape;
                    case 'f':
                        ConsumeLiteral("false");
                        return BooleanShape;
                    case 'n':
                        ConsumeLiteral("null");
                        return NullShape;
                    default:
                        return ParseNumber() ? IntegerShape : NumberShape;
                }
            }

            private JsonDocumentShape ParseObject(int depth)
            {
                Expect('{');
                SkipWhitespace();
                var parsedMembers = new Dictionary<string, JsonDocumentShape>(StringComparer.Ordinal);
                if (TryConsume('}'))
                    return new JsonDocumentShape(JsonValueKind.Object, parsedMembers, null);

                while (true)
                {
                    if (position >= json.Length || json[position] != '"')
                        throw new FormatException("JSONオブジェクトの項目名がありません。");
                    var name = ParseString(true);
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    var value = ParseValue(depth + 1, name);
                    if (parsedMembers.ContainsKey(name))
                        throw new FormatException("JSONオブジェクトに重複した項目があります。");
                    if (parsedMembers.Count >= MaximumObjectMemberCount)
                        throw new InvalidDataException("JSONオブジェクトの項目数が上限を超えています。");
                    parsedMembers.Add(name, value);
                    SkipWhitespace();
                    if (TryConsume('}'))
                        return new JsonDocumentShape(JsonValueKind.Object, parsedMembers, null);
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private JsonDocumentShape ParseArray(int depth, string memberName)
            {
                Expect('[');
                SkipWhitespace();
                var parsedItems = new List<JsonDocumentShape>();
                var maximumItemCount = GetMaximumArrayItemCount(memberName);
                if (TryConsume(']'))
                    return new JsonDocumentShape(JsonValueKind.Array, null, parsedItems);

                while (true)
                {
                    if (parsedItems.Count >= maximumItemCount)
                        throw new InvalidDataException("JSON配列の要素数が上限を超えています。");
                    parsedItems.Add(ParseValue(depth + 1, string.Empty));
                    SkipWhitespace();
                    if (TryConsume(']'))
                        return new JsonDocumentShape(JsonValueKind.Array, null, parsedItems);
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private static int GetMaximumArrayItemCount(string memberName)
            {
                switch (memberName)
                {
                    case "entries": return HistoryStore.MaximumEntryCount;
                    case "effectiveDefines": return HistoryStore.MaximumDefineCount;
                    case "scenes": return HistoryStore.MaximumSceneCount;
                    case "assets": return HistoryStore.MaximumAssetCount;
                    case "types": return HistoryStore.MaximumTypeCount;
                    default: return MaximumGeneralArrayItemCount;
                }
            }

            private void CountContainer()
            {
                containerCount = checked(containerCount + 1);
                if (containerCount > MaximumContainerCount)
                    throw new InvalidDataException("JSON文書のオブジェクトと配列の数が上限を超えています。");
            }

            private string ParseString(bool capture)
            {
                Expect('"');
                var result = capture ? new StringBuilder() : null;
                while (position < json.Length)
                {
                    var character = json[position++];
                    if (character == '"')
                        return capture ? result.ToString() : string.Empty;
                    if (character < ' ')
                        throw new FormatException("JSON文字列に使用できない制御文字があります。");
                    if (character != '\\')
                    {
                        result?.Append(character);
                        continue;
                    }

                    if (capture)
                        throw new FormatException("JSONの項目名にはエスケープを使用できません。");
                    if (position >= json.Length)
                        throw new FormatException("JSON文字列のエスケープが途中で終わっています。");
                    var escaped = json[position++];
                    switch (escaped)
                    {
                        case '"': result?.Append('"'); break;
                        case '\\': result?.Append('\\'); break;
                        case '/': result?.Append('/'); break;
                        case 'b': result?.Append('\b'); break;
                        case 'f': result?.Append('\f'); break;
                        case 'n': result?.Append('\n'); break;
                        case 'r': result?.Append('\r'); break;
                        case 't': result?.Append('\t'); break;
                        case 'u': result?.Append(ParseUnicodeEscape()); break;
                        default: throw new FormatException("JSON文字列に未知のエスケープがあります。");
                    }
                }
                throw new FormatException("JSON文字列が閉じられていません。");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length)
                    throw new FormatException("JSON文字列のUnicodeエスケープが途中で終わっています。");
                var value = 0;
                for (var index = 0; index < 4; index++)
                {
                    var digit = json[position++];
                    value = checked(value * 16 + ParseHexDigit(digit));
                }
                return (char)value;
            }

            private static int ParseHexDigit(char value)
            {
                if (value >= '0' && value <= '9')
                    return value - '0';
                if (value >= 'a' && value <= 'f')
                    return value - 'a' + 10;
                if (value >= 'A' && value <= 'F')
                    return value - 'A' + 10;
                throw new FormatException("JSON文字列のUnicodeエスケープが不正です。");
            }

            private bool ParseNumber()
            {
                var start = position;
                TryConsume('-');
                if (position >= json.Length)
                    throw new FormatException("JSON数値が途中で終わっています。");
                if (TryConsume('0'))
                {
                    if (position < json.Length && char.IsDigit(json[position]))
                        throw new FormatException("JSON数値の先頭に余分なゼロがあります。");
                }
                else
                {
                    ConsumeDigits(true);
                }

                var hasFractionOrExponent = TryConsume('.');
                if (hasFractionOrExponent)
                    ConsumeDigits(true);
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    hasFractionOrExponent = true;
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-'))
                        position++;
                    ConsumeDigits(true);
                }
                return !hasFractionOrExponent && int.TryParse(json.Substring(start, position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
            }

            private void ConsumeDigits(bool requireOne)
            {
                var start = position;
                while (position < json.Length && json[position] >= '0' && json[position] <= '9')
                    position++;
                if (requireOne && position == start)
                    throw new FormatException("JSON数値に必要な数字がありません。");
            }

            private void ConsumeLiteral(string literal)
            {
                if (position + literal.Length > json.Length || string.CompareOrdinal(json, position, literal, 0, literal.Length) != 0)
                    throw new FormatException("JSONの真偽値またはnullが不正です。");
                position += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (position < json.Length)
                {
                    var character = json[position];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n')
                        return;
                    position++;
                }
            }

            private bool TryConsume(char expected)
            {
                if (position >= json.Length || json[position] != expected)
                    return false;
                position++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected))
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "JSON内に必要な文字 '{0}' がありません。", expected));
            }
        }
    }
}
