using System;

namespace DebugMenu
{
    /// <summary>
    /// 文字列の行。決定で入力欄が開く。
    /// <para>
    /// 左右キーでは変えられない。文字列は連続量ではないので、
    /// 送って探すより打ち込む方が速いため。
    /// </para>
    /// </summary>
    public sealed class DebugText : DebugElement
    {
        private readonly Func<string> _getter;
        private readonly Action<string> _setter;
        private string _defaultValue = string.Empty;
        private bool _hasDefaultValue;

        private string _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugText(string label, Func<string> getter, Action<string> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue ?? string.Empty;
                _hasDefaultValue = true;
            }
            IsExpandable = false;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugText(string label, string initialValue = null) : base(label)
        {
            _stored = initialValue ?? string.Empty;
            _defaultValue = _stored;
            _hasDefaultValue = true;
            IsExpandable = false;
        }

        /// <summary>入力欄が空のときに薄く出す案内文。</summary>
        public string Placeholder { get; set; } = string.Empty;

        /// <summary>現在値。null を渡すと空文字として扱う。</summary>
        public string Value
        {
            get
            {
                var value = (_getter != null ? _getter() : _stored) ?? string.Empty;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Text;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override bool IsModified => TryGetCurrent(out var value) && !string.Equals(value, _defaultValue, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override string GetValueText() =>
            Value.Length == 0 ? Placeholder : Value;

        /// <inheritdoc/>
        public override string GetEditText() => Value;

        /// <inheritdoc/>
        public override bool CommitEditText(string text)
        {
            return TrySetValue(text);
        }

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            if (!TryGetCurrent(out var current)) return;
            TrySetValue(_defaultValue, current);
        }

        private bool TryGetCurrent(out string value)
        {
            if (_getter == null)
            {
                value = _stored ?? string.Empty;
                return true;
            }

            if (!TryReadExternalValue(_getter, out value)) return false;

            value ??= string.Empty;
            CaptureDefaultIfNeeded(value);
            return true;
        }

        private void CaptureDefaultIfNeeded(string value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value ?? string.Empty;
            _hasDefaultValue = true;
        }

        private bool TrySetValue(string value)
        {
            if (!TryGetCurrent(out var current)) return false;

            return TrySetValue(value, current);
        }

        private bool TrySetValue(string value, string current)
        {
            var next = value ?? string.Empty;
            if (string.Equals(current, next, StringComparison.Ordinal))
            {
                ClearReadError("値設定");
                return true;
            }

            if (_setter != null)
            {
                if (!TryWriteExternalValue(_setter, next)) return false;
            }
            else
            {
                _stored = next;
                ClearReadError("値設定");
            }

            NotifyChanged();
            return true;
        }
    }
}
