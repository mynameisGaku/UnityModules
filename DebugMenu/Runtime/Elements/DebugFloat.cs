using System;
using System.Globalization;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 小数の行。左右キーで刻み幅ずつ動かし、決定で直接打ち込める。
    /// <para>
    /// 表示桁数を持たせているのは、右カラムに <c>0.30000001</c> のような値が出ると
    /// 読み取れないため。打ち込みの中身は丸めずに実値を出すので、精度は失われない。
    /// </para>
    /// </summary>
    public sealed class DebugFloat : DebugElement
    {
        private readonly Func<float> _getter;
        private readonly Action<float> _setter;
        private readonly Func<float, bool> _trySetter;
        private readonly bool _isSaveable = true;
        private float _defaultValue;
        private bool _hasDefaultValue;

        private float _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugFloat(string label, Func<float> getter, Action<float> setter) : base(label)
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
        private DebugFloat(string label, Func<float> getter, Func<float, bool> trySetter, bool isSaveable) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _trySetter = trySetter ?? throw new ArgumentNullException(nameof(trySetter));
            _isSaveable = isSaveable;
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue;
                _hasDefaultValue = true;
            }

            IsExpandable = false;
        }

        /// <summary>親の設定失敗を子行の操作結果へ伝播する内部用の小数行を作る。</summary>
        internal static DebugFloat CreateChecked(
            string label,
            Func<float> getter,
            Func<float, bool> trySetter,
            bool isSaveable = true) =>
            new DebugFloat(label, getter, trySetter, isSaveable);

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugFloat(string label, float initialValue = 0f) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
            _hasDefaultValue = true;
            IsExpandable = false;
        }

        /// <summary>下限。</summary>
        public float Min { get; set; } = float.NegativeInfinity;

        /// <summary>上限。</summary>
        public float Max { get; set; } = float.PositiveInfinity;

        /// <summary>左右キー 1 回あたりの変化量。</summary>
        public float Step { get; set; } = 0.1f;

        /// <summary>右カラムに出すときの小数点以下の桁数。</summary>
        public int Digits { get; set; } = 2;

        /// <summary>上下限を設定する。設定すると右カラムにスライダー位置も出る。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public DebugFloat WithRange(float min, float max)
        {
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            if (_getter == null) Value = Value;
            else if (TryGetCurrent(out var current))
            {
                var clamped = Mathf.Clamp(current, Min, Max);
                if (!Mathf.Approximately(current, clamped))
                {
                    if (!TryWriteValue(clamped)) return this;
                    NotifyChanged();
                }
            }
            return this;
        }

        /// <summary>刻み幅を設定する。</summary>
        /// <param name="step">左右キー 1 回あたりの変化量。</param>
        public DebugFloat WithStep(float step)
        {
            Step = Mathf.Abs(step);
            return this;
        }

        /// <summary>表示桁数を設定する。</summary>
        /// <param name="digits">小数点以下の桁数。</param>
        public DebugFloat WithDigits(int digits)
        {
            Digits = Mathf.Clamp(digits, 0, 9);
            return this;
        }

        /// <summary>現在値。設定時は上下限で丸められる。</summary>
        public float Value
        {
            get
            {
                var value = _getter != null ? _getter() : _stored;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        /// <summary>上下限が両方とも有限か。</summary>
        private bool HasRange => !float.IsInfinity(Min) && !float.IsInfinity(Max);

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Float;

        /// <inheritdoc/>
        public override bool IsSaveable => _isSaveable;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => TryGetCurrent(out var value) && !Mathf.Approximately(value, _defaultValue);

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override string GetValueText() => Value.ToString("F" + Digits, CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override void OnAdjust(int delta)
        {
            if (!TryGetCurrent(out var value)) return;
            TrySetValue(value + Step * delta, value);
        }

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            if (!TryGetCurrent(out var current)) return;
            TrySetValue(_defaultValue, current);
        }

        /// <summary>打ち込みには丸めた値ではなく実値を出す。打ち直しで精度が落ちないようにするため。</summary>
        public override string GetEditText() => Value.ToString("R", CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override bool CommitEditText(string text)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return false;
            if (float.IsNaN(parsed)) return false;

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

            var span = Max - Min;
            ratio = span <= 0f ? 0f : Mathf.Clamp01((value - Min) / span);
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetRatio(float ratio)
        {
            if (!HasRange) return false;

            return TrySetValue(Mathf.Lerp(Min, Max, Mathf.Clamp01(ratio)));
        }

        /// <inheritdoc/>
        public override bool TryGetFloat(out float value)
        {
            return TryGetCurrent(out value);
        }

        /// <inheritdoc/>
        public override bool TrySetFloat(float value)
        {
            return TrySetValue(value);
        }

        private bool TryGetCurrent(out float value)
        {
            if (_getter == null) value = _stored;
            else if (!TryReadExternalValue(_getter, out value)) return false;

            CaptureDefaultIfNeeded(value);
            return true;
        }

        private void CaptureDefaultIfNeeded(float value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value;
            _hasDefaultValue = true;
        }

        private bool TrySetValue(float value)
        {
            if (!TryGetCurrent(out var current)) return false;
            return TrySetValue(value, current);
        }

        private bool TrySetValue(float value, float current)
        {
            var clamped = Mathf.Clamp(value, Min, Max);

            // 完全一致で弾くと、丸め誤差で毎回通知が飛ぶ。表示桁で見て同じなら変化なしとする。
            if (Mathf.Approximately(current, clamped))
            {
                ClearReadError("値設定");
                return true;
            }

            if (!TryWriteValue(clamped)) return false;

            NotifyChanged();
            return true;
        }

        /// <summary>通常の設定関数と、親へ失敗を返す設定処理を同じ入口から呼ぶ。</summary>
        private bool TryWriteValue(float value)
        {
            if (_trySetter != null) return TryWriteExternalValue(_trySetter, value);
            if (_setter != null) return TryWriteExternalValue(_setter, value);

            _stored = value;
            ClearReadError("値設定");
            return true;
        }
    }
}
