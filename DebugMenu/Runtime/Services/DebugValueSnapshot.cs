using System;
using System.Globalization;

namespace DebugMenu
{
    /// <summary>
    /// 行の値を型を問わず 1 つの形で持つ写し。
    /// <para>
    /// 取り消しと保存の両方が「値を控えて、あとで戻す」を必要とするので、
    /// その最小公倍数をここに置いてある。行の具体型を知らずに扱えるのが要点。
    /// </para>
    /// <para>
    /// 文字列と色は打ち込み用の文字列を経由する。数値と真偽値は専用の口を使う ——
    /// 文字列化を挟むと丸め誤差が入るため。
    /// </para>
    /// </summary>
    public readonly struct DebugValueSnapshot : IEquatable<DebugValueSnapshot>
    {
        /// <summary>控えた値の種類。<see cref="DebugValueKind.None"/> なら中身は無い。</summary>
        public readonly DebugValueKind Kind;

        private readonly bool _boolValue;
        private readonly int _intValue;
        private readonly float _floatValue;
        private readonly string _textValue;

        private DebugValueSnapshot(DebugValueKind kind, bool boolValue, int intValue, float floatValue, string textValue)
        {
            Kind = kind;
            _boolValue = boolValue;
            _intValue = intValue;
            _floatValue = floatValue;
            _textValue = textValue;
        }

        /// <summary>中身を持っているか。</summary>
        public bool HasValue => Kind != DebugValueKind.None;

        /// <summary>行から値を控える。控えられない行なら <see cref="HasValue"/> が false になる。</summary>
        /// <param name="element">控える対象の行。</param>
        public static DebugValueSnapshot Capture(DebugElement element)
        {
            if (element == null) return default;

            switch (element.ValueKind)
            {
                case DebugValueKind.Bool:
                    return element.TryGetBool(out var boolValue)
                        ? new DebugValueSnapshot(DebugValueKind.Bool, boolValue, 0, 0f, null)
                        : default;

                case DebugValueKind.Int:
                case DebugValueKind.Enum:
                    return element.TryGetInt(out var intValue)
                        ? new DebugValueSnapshot(element.ValueKind, false, intValue, 0f, null)
                        : default;

                case DebugValueKind.Float:
                    return element.TryGetFloat(out var floatValue)
                        ? new DebugValueSnapshot(DebugValueKind.Float, false, 0, floatValue, null)
                        : default;

                case DebugValueKind.Text:
                case DebugValueKind.Color:
                case DebugValueKind.Vector:
                    // 専用の口が無い型は、打ち込み用の文字列を正とする。
                    return element.TryGetEditText(out var editText)
                        ? new DebugValueSnapshot(element.ValueKind, false, 0, 0f, editText)
                        : default;

                default:
                    return default;
            }
        }

        /// <summary>
        /// 利用側の取得関数を例外境界の内側で呼び、失敗した行だけを値無しとして扱う。
        /// 履歴や保存の走査で1行の失敗が全体へ波及しないようにするための入口。
        /// </summary>
        /// <param name="element">控える対象の行。</param>
        /// <param name="snapshot">取得できた値。失敗時は空。</param>
        /// <returns>例外を出さずに取得処理を完了できたか。</returns>
        public static bool TryCapture(DebugElement element, out DebugValueSnapshot snapshot)
        {
            if (element == null)
            {
                snapshot = default;
                return false;
            }

            try
            {
                var valueKind = element.ValueKind;
                if (valueKind == DebugValueKind.None)
                {
                    snapshot = default;
                    return true;
                }

                snapshot = Capture(element);
                if (valueKind != DebugValueKind.None && !snapshot.HasValue)
                {
                    if (!element.HasReadError)
                    {
                        element.ReportReadError(
                            "値取得",
                            new InvalidOperationException($"{valueKind} の値を取得できなかった。"));
                    }

                    return false;
                }

                element.ClearReadError("値取得");
                return true;
            }
            catch (Exception exception)
            {
                snapshot = default;
                element.ReportReadError("値取得", exception);
                return false;
            }
        }

        /// <summary>控えた値を行へ書き戻す。書き戻せたら true。</summary>
        /// <param name="element">書き戻す対象の行。</param>
        public bool Apply(DebugElement element)
        {
            if (element == null || !HasValue) return false;

            try
            {
                // 種類が食い違う相手へ書き戻すと、意味の違う値が入る。
                if (element.ValueKind != Kind) return false;

                var applied = Kind switch
                {
                    DebugValueKind.Bool => element.TrySetBool(_boolValue),
                    DebugValueKind.Int => element.TrySetInt(_intValue),
                    DebugValueKind.Enum => element.TrySetInt(_intValue),
                    DebugValueKind.Float => element.TrySetFloat(_floatValue),
                    DebugValueKind.Text => element.CommitEditTextSafely(_textValue),
                    DebugValueKind.Color => element.CommitEditTextSafely(_textValue),
                    DebugValueKind.Vector => element.CommitEditTextSafely(_textValue),
                    _ => false,
                };

                if (applied) element.ClearReadError("値設定");
                return applied;
            }
            catch (Exception exception)
            {
                element.ReportReadError("値設定", exception);
                return false;
            }
        }

        /// <summary>保存用の文字列にする。</summary>
        public string ToStorageString()
        {
            switch (Kind)
            {
                case DebugValueKind.Bool:
                    return _boolValue ? "1" : "0";
                case DebugValueKind.Int:
                case DebugValueKind.Enum:
                    return _intValue.ToString(CultureInfo.InvariantCulture);
                case DebugValueKind.Float:
                    return _floatValue.ToString("R", CultureInfo.InvariantCulture);
                case DebugValueKind.Text:
                case DebugValueKind.Color:
                case DebugValueKind.Vector:
                    return _textValue ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>保存用の文字列から復元する。</summary>
        /// <param name="kind">値の種類。</param>
        /// <param name="text">保存されていた文字列。Boolは1/0または大文字小文字を区別しないtrue/falseだけを受け付ける。</param>
        /// <param name="snapshot">復元した写し。</param>
        public static bool TryParse(DebugValueKind kind, string text, out DebugValueSnapshot snapshot)
        {
            switch (kind)
            {
                case DebugValueKind.Bool:
                    if (string.Equals(text, "1", StringComparison.Ordinal) ||
                        string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        snapshot = new DebugValueSnapshot(kind, true, 0, 0f, null);
                        return true;
                    }

                    if (string.Equals(text, "0", StringComparison.Ordinal) ||
                        string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        snapshot = new DebugValueSnapshot(kind, false, 0, 0f, null);
                        return true;
                    }

                    break;

                case DebugValueKind.Int:
                case DebugValueKind.Enum:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        snapshot = new DebugValueSnapshot(kind, false, intValue, 0f, null);
                        return true;
                    }

                    break;

                case DebugValueKind.Float:
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        snapshot = new DebugValueSnapshot(kind, false, 0, floatValue, null);
                        return true;
                    }

                    break;

                case DebugValueKind.Text:
                case DebugValueKind.Color:
                case DebugValueKind.Vector:
                    snapshot = new DebugValueSnapshot(kind, false, 0, 0f, text ?? string.Empty);
                    return true;
            }

            snapshot = default;
            return false;
        }

        /// <summary>同じ種類で同じ値か。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(DebugValueSnapshot other)
        {
            if (Kind != other.Kind) return false;

            switch (Kind)
            {
                case DebugValueKind.Bool:
                    return _boolValue == other._boolValue;
                case DebugValueKind.Int:
                case DebugValueKind.Enum:
                    return _intValue == other._intValue;
                case DebugValueKind.Float:
                    return _floatValue.Equals(other._floatValue);
                case DebugValueKind.Text:
                case DebugValueKind.Color:
                case DebugValueKind.Vector:
                    return string.Equals(_textValue, other._textValue, StringComparison.Ordinal);
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is DebugValueSnapshot other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ ToStorageString().GetHashCode());

        /// <inheritdoc/>
        public override string ToString() => HasValue ? $"{Kind}:{ToStorageString()}" : "<none>";
    }
}
