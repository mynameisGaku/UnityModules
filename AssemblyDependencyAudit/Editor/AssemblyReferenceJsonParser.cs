using System;
using System.Collections.Generic;
using System.Text;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmref JSON の strict shape 解析結果を表します。
    /// </summary>
    internal enum AssemblyReferenceJsonParseStatus
    {
        /// <summary>reference を 1 件取得できました。</summary>
        Valid,

        /// <summary>JSON 構文、重複key、または reference の型が不正です。</summary>
        InvalidJson,

        /// <summary>reference が無い、null、空、または空白だけです。</summary>
        MissingReference
    }

    /// <summary>
    /// JsonUtility の補正や duplicate key の last-win を許可せず asmref を解析します。
    /// </summary>
    internal static class AssemblyReferenceJsonParser
    {
        /// <summary>過度に深い JSON による stack 消費を防ぐ上限です。</summary>
        private const int MaximumDepth = 64;

        /// <summary>
        /// root object の reference property が exactly one のときだけ値を返します。
        /// </summary>
        internal static AssemblyReferenceJsonParseStatus Parse(string json, out string reference)
        {
            reference = string.Empty;
            if (json == null)
            {
                return AssemblyReferenceJsonParseStatus.InvalidJson;
            }

            var parser = new Parser(json);
            if (!parser.TryParseRoot(out var parsedReference, out var referenceFound, out var referenceHasValidType))
            {
                return AssemblyReferenceJsonParseStatus.InvalidJson;
            }

            if (!referenceHasValidType)
            {
                return AssemblyReferenceJsonParseStatus.InvalidJson;
            }

            reference = parsedReference ?? string.Empty;
            return !referenceFound || string.IsNullOrWhiteSpace(reference)
                ? AssemblyReferenceJsonParseStatus.MissingReference
                : AssemblyReferenceJsonParseStatus.Valid;
        }

        /// <summary>1 本の JSON text を cursor で検証します。</summary>
        private sealed class Parser
        {
            /// <summary>解析対象の全 text です。</summary>
            private readonly string _text;

            /// <summary>次に読む UTF-16 code unit の位置です。</summary>
            private int _position;

            /// <summary>解析対象を保持します。</summary>
            internal Parser(string text)
            {
                _text = text;
            }

            /// <summary>
            /// root object 全体を検証し、直下の reference だけを取得します。
            /// </summary>
            internal bool TryParseRoot(
                out string reference,
                out bool referenceFound,
                out bool referenceHasValidType)
            {
                reference = string.Empty;
                referenceFound = false;
                referenceHasValidType = true;
                SkipWhitespace();
                if (!TryConsume('{'))
                {
                    return false;
                }

                var propertyNames = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    SkipWhitespace();
                    return IsAtEnd;
                }

                while (true)
                {
                    if (!TryParseString(out var propertyName) || !propertyNames.Add(propertyName))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (!TryConsume(':'))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (string.Equals(propertyName, "reference", StringComparison.Ordinal))
                    {
                        referenceFound = true;
                        if (Peek() == '"')
                        {
                            if (!TryParseString(out reference))
                            {
                                return false;
                            }
                        }
                        else if (TryParseLiteral("null"))
                        {
                            reference = string.Empty;
                        }
                        else
                        {
                            referenceHasValidType = false;
                            if (!TryParseValue(1))
                            {
                                return false;
                            }
                        }
                    }
                    else if (!TryParseValue(1))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        SkipWhitespace();
                        return IsAtEnd;
                    }

                    if (!TryConsume(','))
                    {
                        return false;
                    }

                    SkipWhitespace();
                }
            }

            /// <summary>任意の JSON value を strict grammar で消費します。</summary>
            private bool TryParseValue(int depth)
            {
                if (depth > MaximumDepth)
                {
                    return false;
                }

                SkipWhitespace();
                switch (Peek())
                {
                    case '"':
                        return TryParseString(out _);
                    case '{':
                        return TryParseObject(depth + 1);
                    case '[':
                        return TryParseArray(depth + 1);
                    case 't':
                        return TryParseLiteral("true");
                    case 'f':
                        return TryParseLiteral("false");
                    case 'n':
                        return TryParseLiteral("null");
                    default:
                        return TryParseNumber();
                }
            }

            /// <summary>object と全 property を検証します。</summary>
            private bool TryParseObject(int depth)
            {
                if (depth > MaximumDepth || !TryConsume('{'))
                {
                    return false;
                }

                var propertyNames = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return true;
                }

                while (true)
                {
                    if (!TryParseString(out var propertyName) || !propertyNames.Add(propertyName))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (!TryConsume(':'))
                    {
                        return false;
                    }

                    if (!TryParseValue(depth))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return true;
                    }

                    if (!TryConsume(','))
                    {
                        return false;
                    }

                    SkipWhitespace();
                }
            }

            /// <summary>array と全 element を検証します。</summary>
            private bool TryParseArray(int depth)
            {
                if (depth > MaximumDepth || !TryConsume('['))
                {
                    return false;
                }

                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return true;
                }

                while (true)
                {
                    if (!TryParseValue(depth))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return true;
                    }

                    if (!TryConsume(','))
                    {
                        return false;
                    }

                    SkipWhitespace();
                }
            }

            /// <summary>escape を復元しながら JSON string を消費します。</summary>
            private bool TryParseString(out string value)
            {
                value = string.Empty;
                if (!TryConsume('"'))
                {
                    return false;
                }

                var builder = new StringBuilder();
                while (!IsAtEnd)
                {
                    var character = _text[_position++];
                    if (character == '"')
                    {
                        value = builder.ToString();
                        return true;
                    }

                    if (character < 0x20)
                    {
                        return false;
                    }

                    if (character != '\\')
                    {
                        if (char.IsHighSurrogate(character))
                        {
                            if (IsAtEnd || !char.IsLowSurrogate(_text[_position]))
                            {
                                return false;
                            }

                            builder.Append(character);
                            builder.Append(_text[_position++]);
                            continue;
                        }

                        if (char.IsLowSurrogate(character))
                        {
                            return false;
                        }

                        builder.Append(character);
                        continue;
                    }

                    if (IsAtEnd)
                    {
                        return false;
                    }

                    var escaped = _text[_position++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (!TryParseHexCharacter(out var unicodeCharacter))
                            {
                                return false;
                            }

                            if (char.IsHighSurrogate(unicodeCharacter))
                            {
                                if (_position + 2 > _text.Length ||
                                    _text[_position] != '\\' ||
                                    _text[_position + 1] != 'u')
                                {
                                    return false;
                                }

                                _position += 2;
                                if (!TryParseHexCharacter(out var lowSurrogate) ||
                                    !char.IsLowSurrogate(lowSurrogate))
                                {
                                    return false;
                                }

                                builder.Append(unicodeCharacter);
                                builder.Append(lowSurrogate);
                                break;
                            }

                            if (char.IsLowSurrogate(unicodeCharacter))
                            {
                                return false;
                            }

                            builder.Append(unicodeCharacter);
                            break;
                        default:
                            return false;
                    }
                }

                return false;
            }

            /// <summary>4 桁の16進 escape を 1 code unit として取得します。</summary>
            private bool TryParseHexCharacter(out char value)
            {
                value = default;
                if (_position + 4 > _text.Length)
                {
                    return false;
                }

                var number = 0;
                for (var index = 0; index < 4; index++)
                {
                    var digit = GetHexValue(_text[_position++]);
                    if (digit < 0)
                    {
                        return false;
                    }

                    number = (number * 16) + digit;
                }

                value = (char)number;
                return true;
            }

            /// <summary>JSON number grammar に一致する部分を消費します。</summary>
            private bool TryParseNumber()
            {
                var start = _position;
                TryConsume('-');
                if (TryConsume('0'))
                {
                    if (IsDigit(Peek()))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!IsDigitOneToNine(Peek()))
                    {
                        _position = start;
                        return false;
                    }

                    while (IsDigit(Peek()))
                    {
                        _position++;
                    }
                }

                if (TryConsume('.'))
                {
                    if (!IsDigit(Peek()))
                    {
                        return false;
                    }

                    while (IsDigit(Peek()))
                    {
                        _position++;
                    }
                }

                if (Peek() == 'e' || Peek() == 'E')
                {
                    _position++;
                    if (Peek() == '+' || Peek() == '-')
                    {
                        _position++;
                    }

                    if (!IsDigit(Peek()))
                    {
                        return false;
                    }

                    while (IsDigit(Peek()))
                    {
                        _position++;
                    }
                }

                return _position > start;
            }

            /// <summary>指定した JSON literal を現在位置から消費します。</summary>
            private bool TryParseLiteral(string literal)
            {
                if (_position + literal.Length > _text.Length ||
                    !string.Equals(_text.Substring(_position, literal.Length), literal, StringComparison.Ordinal))
                {
                    return false;
                }

                _position += literal.Length;
                return true;
            }

            /// <summary>JSON が許可する4種類の空白を読み飛ばします。</summary>
            private void SkipWhitespace()
            {
                while (!IsAtEnd)
                {
                    var character = _text[_position];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n')
                    {
                        return;
                    }

                    _position++;
                }
            }

            /// <summary>現在位置の文字を一致時だけ消費します。</summary>
            private bool TryConsume(char expected)
            {
                if (Peek() != expected)
                {
                    return false;
                }

                _position++;
                return true;
            }

            /// <summary>現在位置の文字を返し、終端では null character を返します。</summary>
            private char Peek()
            {
                return IsAtEnd ? '\0' : _text[_position];
            }

            /// <summary>全 text を消費したかを返します。</summary>
            private bool IsAtEnd => _position >= _text.Length;

            /// <summary>10進 digit かを返します。</summary>
            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            /// <summary>先頭0を除く10進 digit かを返します。</summary>
            private static bool IsDigitOneToNine(char value)
            {
                return value >= '1' && value <= '9';
            }

            /// <summary>16進 digit を数値へ変換し、対象外では -1 を返します。</summary>
            private static int GetHexValue(char value)
            {
                if (value >= '0' && value <= '9')
                {
                    return value - '0';
                }

                if (value >= 'a' && value <= 'f')
                {
                    return value - 'a' + 10;
                }

                return value >= 'A' && value <= 'F' ? value - 'A' + 10 : -1;
            }
        }
    }
}
