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
        private readonly string _defaultValue;

        private string _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugText(string label, Func<string> getter, Action<string> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultValue = getter() ?? string.Empty;
            IsExpandable = false;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugText(string label, string initialValue = null) : base(label)
        {
            _stored = initialValue ?? string.Empty;
            _defaultValue = _stored;
            IsExpandable = false;
        }

        /// <summary>入力欄が空のときに薄く出す案内文。</summary>
        public string Placeholder { get; set; } = string.Empty;

        /// <summary>現在値。null を渡すと空文字として扱う。</summary>
        public string Value
        {
            get => (_getter != null ? _getter() : _stored) ?? string.Empty;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(Value, next, StringComparison.Ordinal)) return;

                if (_setter != null) _setter(next);
                else _stored = next;

                NotifyChanged();
            }
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Text;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override bool IsModified => !string.Equals(Value, _defaultValue, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override string GetValueText() =>
            Value.Length == 0 ? Placeholder : Value;

        /// <inheritdoc/>
        public override string GetEditText() => Value;

        /// <inheritdoc/>
        public override bool CommitEditText(string text)
        {
            Value = text;
            return true;
        }

        /// <inheritdoc/>
        public override void ResetToDefault() => Value = _defaultValue;
    }
}
