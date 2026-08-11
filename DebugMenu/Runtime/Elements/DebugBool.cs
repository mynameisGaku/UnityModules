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
        private readonly bool _defaultValue;

        private bool _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugBool(string label, Func<bool> getter, Action<bool> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultValue = getter();
            IsExpandable = false;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugBool(string label, bool initialValue = false) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
            IsExpandable = false;
        }

        /// <summary>現在値。</summary>
        public bool Value
        {
            get => _getter != null ? _getter() : _stored;
            set
            {
                if (Value == value) return;

                if (_setter != null) _setter(value);
                else _stored = value;

                NotifyChanged();
            }
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Bool;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => Value != _defaultValue;

        /// <inheritdoc/>
        public override string GetValueText() => Value ? "ON" : "OFF";

        /// <inheritdoc/>
        public override void OnDecide() => Value = !Value;

        /// <summary>左右どちらでも切り替える。ON/OFF の 2 値では方向に意味が無いため。</summary>
        /// <param name="delta">左で -1、右で +1。</param>
        public override void OnAdjust(int delta) => Value = !Value;

        /// <inheritdoc/>
        public override void ResetToDefault() => Value = _defaultValue;

        /// <inheritdoc/>
        public override bool TryGetBool(out bool value)
        {
            value = Value;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetBool(bool value)
        {
            Value = value;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetInt(out int value)
        {
            value = Value ? 1 : 0;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetInt(int value)
        {
            Value = value != 0;
            return true;
        }
    }
}
