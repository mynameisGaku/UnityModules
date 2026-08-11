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
        private readonly int _defaultValue;

        private int _stored;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugInt(string label, Func<int> getter, Action<int> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultValue = getter();
            IsExpandable = false;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugInt(string label, int initialValue = 0) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
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
            Value = Value;   // 範囲内へ丸め直す
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
            get => _getter != null ? _getter() : _stored;
            set
            {
                var clamped = value < Min ? Min : value > Max ? Max : value;
                if (Value == clamped) return;

                if (_setter != null) _setter(clamped);
                else _stored = clamped;

                NotifyChanged();
            }
        }

        /// <summary>上下限が両方とも設定されているか。</summary>
        private bool HasRange => Min != int.MinValue && Max != int.MaxValue;

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Int;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => Value != _defaultValue;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override string GetValueText() => Value.ToString(CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override void OnAdjust(int delta) => Value += Step * delta;

        /// <inheritdoc/>
        public override void ResetToDefault() => Value = _defaultValue;

        /// <inheritdoc/>
        public override string GetEditText() => Value.ToString(CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override bool CommitEditText(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return false;

            Value = parsed;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetRatio(out float ratio)
        {
            if (!HasRange)
            {
                ratio = 0f;
                return false;
            }

            var span = (float)Max - Min;
            ratio = span <= 0f ? 0f : Mathf.Clamp01((Value - Min) / span);
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetRatio(float ratio)
        {
            if (!HasRange) return false;

            Value = Mathf.RoundToInt(Mathf.Lerp(Min, Max, Mathf.Clamp01(ratio)));
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetInt(out int value)
        {
            value = Value;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetInt(int value)
        {
            Value = value;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetFloat(out float value)
        {
            value = Value;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetFloat(float value)
        {
            Value = Mathf.RoundToInt(value);
            return true;
        }
    }
}
