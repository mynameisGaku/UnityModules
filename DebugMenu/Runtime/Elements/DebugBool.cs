using System;

namespace DebugMenu
{
    /// <summary>
    /// 真偽値の行。決定でも左右でも切り替わる。
    /// <para>
    /// 値の持ち方は 2 通り。<b>取得・設定の関数を渡す</b>とゲーム側の変数を直接覗きに行くので、
    /// メニューとゲームで値が二重管理にならない。関数を渡さない場合はこの行が値を抱えるので、
    /// 「メニューでしか使わないフラグ」を置くのに向く。
    /// </para>
    /// </summary>
    public sealed class DebugBool : DebugElement
    {
        private readonly Func<bool> _getter;
        private readonly Action<bool> _setter;
        private bool _defaultValue;
        private bool _hasDefaultValue;

        private bool _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugBool(string label, Func<bool> getter, Action<bool> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue;
                _hasDefaultValue = true;
            }
            IsExpandable = false;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugBool(string label, bool initialValue = false) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
            _hasDefaultValue = true;
            IsExpandable = false;
        }

        /// <summary>現在値。</summary>
        public bool Value
        {
            get
            {
                var value = _getter != null ? _getter() : _stored;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Bool;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => TryGetCurrent(out var value) && value != _defaultValue;

        /// <inheritdoc/>
        public override string GetValueText() => Value ? "ON" : "OFF";

        /// <inheritdoc/>
        public override void OnDecide()
        {
            if (!TryGetCurrent(out var value)) return;
            TrySetValue(!value, value);
        }

        /// <summary>左右どちらでも切り替える。ON/OFF の 2 値では方向に意味が無いため。</summary>
        /// <param name="delta">左で -1、右で +1。</param>
        public override void OnAdjust(int delta) => OnDecide();

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            if (!TryGetCurrent(out var current)) return;
            TrySetValue(_defaultValue, current);
        }

        /// <inheritdoc/>
        public override bool TryGetBool(out bool value)
        {
            return TryGetCurrent(out value);
        }

        /// <inheritdoc/>
        public override bool TrySetBool(bool value)
        {
            return TrySetValue(value);
        }

        /// <inheritdoc/>
        public override bool TryGetInt(out int value)
        {
            if (!TryGetCurrent(out var current))
            {
                value = 0;
                return false;
            }

            value = current ? 1 : 0;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetInt(int value)
        {
            return TrySetValue(value != 0);
        }

        private bool TryGetCurrent(out bool value)
        {
            if (_getter == null) value = _stored;
            else if (!TryReadExternalValue(_getter, out value)) return false;

            CaptureDefaultIfNeeded(value);
            return true;
        }

        private void CaptureDefaultIfNeeded(bool value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value;
            _hasDefaultValue = true;
        }

        private bool TrySetValue(bool value)
        {
            if (!TryGetCurrent(out var current)) return false;
            return TrySetValue(value, current);
        }

        private bool TrySetValue(bool value, bool current)
        {
            if (current == value) return true;

            if (_setter != null) _setter(value);
            else _stored = value;

            NotifyChanged();
            return true;
        }
    }
}
