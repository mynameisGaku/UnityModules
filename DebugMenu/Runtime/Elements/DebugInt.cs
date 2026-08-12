using System;
using System.Globalization;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 整数の行。左右キーで刻み幅ずつ動かし、決定で直接打ち込める。
    /// <para>
    /// 上下限を持つと、右カラムにスライダーの位置も出せる。桁の大きい値は
    /// 左右キーで送るのが現実的でないため、打ち込みを既定の決定操作にしてある。
    /// </para>
    /// </summary>
    public sealed class DebugInt : DebugElement
    {
        private readonly Func<int> _getter;
        private readonly Action<int> _setter;
        private readonly Func<int, bool> _trySetter;
        private int _defaultValue;
        private bool _hasDefaultValue;

        private int _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugInt(string label, Func<int> getter, Action<int> setter) : base(label)
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

        /// <summary>親の複合値へ書き込み、親側の失敗を子行へ返せる成分行を作る。</summary>
        private DebugInt(string label, Func<int> getter, Func<int, bool> trySetter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _trySetter = trySetter ?? throw new ArgumentNullException(nameof(trySetter));
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue;
                _hasDefaultValue = true;
            }

            IsExpandable = false;
        }

        /// <summary>親の設定失敗を子行の操作結果へ伝播する内部用の整数行を作る。</summary>
        internal static DebugInt CreateChecked(string label, Func<int> getter, Func<int, bool> trySetter) =>
            new DebugInt(label, getter, trySetter);

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugInt(string label, int initialValue = 0) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
            _hasDefaultValue = true;
            IsExpandable = false;
        }

        /// <summary>下限。既定は <see cref="int.MinValue"/>。</summary>
        public int Min { get; set; } = int.MinValue;

        /// <summary>上限。既定は <see cref="int.MaxValue"/>。</summary>
        public int Max { get; set; } = int.MaxValue;

        /// <summary>左右キー 1 回あたりの変化量。</summary>
        public int Step { get; set; } = 1;

        /// <summary>上下限を設定する。設定すると右カラムにスライダー位置も出る。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public DebugInt WithRange(int min, int max)
        {
            Min = Math.Min(min, max);
            Max = Math.Max(min, max);
            if (_getter == null) Value = Value;
            else if (TryGetCurrent(out var current))
            {
                var clamped = current < Min ? Min : current > Max ? Max : current;
                if (current != clamped)
                {
                    if (!TryWriteExternalValue(_setter, clamped)) return this;
                    NotifyChanged();
                }
            }
            return this;
        }

        /// <summary>刻み幅を設定する。</summary>
        /// <param name="step">左右キー 1 回あたりの変化量。</param>
        public DebugInt WithStep(int step)
        {
            Step = Math.Max(1, step);
            return this;
        }

        /// <summary>現在値。設定時は上下限で丸められる。</summary>
        public int Value
        {
            get
            {
                var value = _getter != null ? _getter() : _stored;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        /// <summary>上下限が両方とも設定されているか。</summary>
        private bool HasRange => Min != int.MinValue && Max != int.MaxValue;

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Int;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => TryGetCurrent(out var value) && value != _defaultValue;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override string GetValueText() => Value.ToString(CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override void OnAdjust(int delta)
        {
            if (!TryGetCurrent(out var value)) return;
            var adjusted = (long)value + (long)Step * delta;
            var saturated = adjusted < int.MinValue ? int.MinValue : adjusted > int.MaxValue ? int.MaxValue : (int)adjusted;
            TrySetValue(saturated, value);
        }

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            if (!TryGetCurrent(out var current)) return;
            TrySetValue(_defaultValue, current);
        }

        /// <inheritdoc/>
        public override string GetEditText() => Value.ToString(CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override bool CommitEditText(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return false;

            return TrySetValue(parsed);
        }

        /// <inheritdoc/>
        public override bool TryGetRatio(out float ratio)
        {
            if (!HasRange)
            {
                ratio = 0f;
                return false;
            }

            if (!TryGetCurrent(out var value))
            {
                ratio = 0f;
                return false;
            }

            var span = (float)Max - Min;
            ratio = span <= 0f ? 0f : Mathf.Clamp01((value - Min) / span);
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetRatio(float ratio)
        {
            if (!HasRange) return false;

            return TrySetValue(Mathf.RoundToInt(Mathf.Lerp(Min, Max, Mathf.Clamp01(ratio))));
        }

        /// <inheritdoc/>
        public override bool TryGetInt(out int value)
        {
            return TryGetCurrent(out value);
        }

        /// <inheritdoc/>
        public override bool TrySetInt(int value)
        {
            return TrySetValue(value);
        }

        /// <inheritdoc/>
        public override bool TryGetFloat(out float value)
        {
            if (!TryGetCurrent(out var current))
            {
                value = 0f;
                return false;
            }

            value = current;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetFloat(float value)
        {
            return TrySetValue(Mathf.RoundToInt(value));
        }

        private bool TryGetCurrent(out int value)
        {
            if (_getter == null) value = _stored;
            else if (!TryReadExternalValue(_getter, out value)) return false;

            CaptureDefaultIfNeeded(value);
            return true;
        }

        private void CaptureDefaultIfNeeded(int value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value;
            _hasDefaultValue = true;
        }

        private bool TrySetValue(int value)
        {
            if (!TryGetCurrent(out var current)) return false;
            return TrySetValue(value, current);
        }

        private bool TrySetValue(int value, int current)
        {
            var clamped = value < Min ? Min : value > Max ? Max : value;
            if (current == clamped)
            {
                ClearReadError("値設定");
                return true;
            }

            if (_trySetter != null)
            {
                if (!TryWriteExternalValue(_trySetter, clamped)) return false;
            }
            else if (_setter != null)
            {
                if (!TryWriteExternalValue(_setter, clamped)) return false;
            }
            else
            {
                _stored = clamped;
                ClearReadError("値設定");
            }

            NotifyChanged();
            return true;
        }
    }
}
