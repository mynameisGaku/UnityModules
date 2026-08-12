using System;
using System.Globalization;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 色の行。右カラムに色見本を出し、展開すると HSV の面で選べる。
    /// <para>
    /// 内部で色相・彩度・明度を保持しているのは、RGB から毎回変換すると
    /// <b>黒や白のところで色相が失われる</b>ため。黒に落としてから戻すと
    /// 元の色相に戻らない、という操作感を避けている。
    /// </para>
    /// </summary>
    public sealed class DebugColor : DebugElement
    {
        private readonly Func<Color> _getter;
        private readonly Action<Color> _setter;
        private Color _defaultValue;
        private bool _hasDefaultValue;

        private Color _stored;
        private float _hue;
        private float _saturation;
        private float _brightness;
        private bool _showAlpha = true;

        /// <summary>ゲーム側の値を直接読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugColor(string label, Func<Color> getter, Action<Color> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue;
                _hasDefaultValue = true;
                SyncHsvFrom(initialValue);
            }
            MarkerVisibility = DebugMarkerVisibility.Always;
        }

        /// <summary>この行が値を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugColor(string label, Color initialValue) : base(label)
        {
            _stored = initialValue;
            _defaultValue = initialValue;
            _hasDefaultValue = true;
            SyncHsvFrom(initialValue);
            MarkerVisibility = DebugMarkerVisibility.Always;
        }

        /// <summary>アルファも編集させるか。false なら常に不透明として扱う。</summary>
        public bool ShowAlpha
        {
            get => _showAlpha;
            set
            {
                if (_showAlpha == value) return;

                if (value)
                {
                    _showAlpha = true;
                    return;
                }

                if (!TryGetColor(out var color))
                {
                    return;
                }

                if (Mathf.Approximately(color.a, 1f))
                {
                    _showAlpha = false;
                    return;
                }

                color.a = 1f;
                if (WriteValue(color)) _showAlpha = false;
            }
        }

        /// <summary>現在の色。</summary>
        public Color Value
        {
            get
            {
                var value = ReadRawValue();
                if (!ShowAlpha) value.a = 1f;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        private Color ReadRawValue() => _getter != null ? _getter() : _stored;

        /// <summary>外部取得関数の失敗を行内へ閉じて現在色を読む。</summary>
        /// <param name="value">取得できた現在色。</param>
        /// <returns>現在色を取得できたか。</returns>
        internal bool TryGetColor(out Color value)
        {
            if (_getter == null) value = _stored;
            else if (!TryReadExternalValue(_getter, out value)) return false;

            CaptureDefaultIfNeeded(value);
            if (!ShowAlpha) value.a = 1f;
            return true;
        }

        private bool WriteValue(Color value)
        {
            if (_setter != null)
            {
                if (!TryWriteExternalValue(_setter, value)) return false;
            }
            else
            {
                _stored = value;
                ClearReadError("値設定");
            }

            SyncHsvFrom(value);
            NotifyChanged();
            return true;
        }

        /// <summary>選択中の色相（0〜1）。</summary>
        public float Hue => _hue;

        /// <summary>選択中の彩度（0〜1）。</summary>
        public float Saturation => _saturation;

        /// <summary>選択中の明度（0〜1）。</summary>
        public float Brightness => _brightness;

        /// <summary>
        /// HSV を指定して色を書き換える。カラーピッカーの面と帯から呼ぶ。
        /// <para>
        /// RGB を経由せず HSV を保持するので、明度を 0 にしてから戻しても色相が残る。
        /// </para>
        /// </summary>
        /// <param name="hue">色相（0〜1）。</param>
        /// <param name="saturation">彩度（0〜1）。</param>
        /// <param name="brightness">明度（0〜1）。</param>
        public void SetHsv(float hue, float saturation, float brightness)
        {
            TrySetHsv(hue, saturation, brightness);
        }

        /// <summary>HSVを書き換え、利用側の設定関数まで正常に完了したかを返す。</summary>
        internal bool TrySetHsv(float hue, float saturation, float brightness)
        {
            if (!TryGetColor(out var current)) return false;

            var nextHue = Mathf.Repeat(hue, 1f);
            var nextSaturation = Mathf.Clamp01(saturation);
            var nextBrightness = Mathf.Clamp01(brightness);
            var rgb = Color.HSVToRGB(nextHue, nextSaturation, nextBrightness);
            rgb.a = ShowAlpha ? current.a : 1f;

            if (current != rgb && !WriteValue(rgb)) return false;
            if (current == rgb) ClearReadError("値設定");

            _hue = nextHue;
            _saturation = nextSaturation;
            _brightness = nextBrightness;
            return true;
        }

        /// <summary>アルファだけを書き換える。</summary>
        /// <param name="alpha">不透明度（0〜1）。</param>
        public void SetAlpha(float alpha)
        {
            TrySetAlpha(alpha);
        }

        /// <summary>アルファを書き換え、利用側の設定関数まで正常に完了したかを返す。</summary>
        internal bool TrySetAlpha(float alpha)
        {
            if (!TryGetColor(out var color)) return false;

            var current = color;
            color.a = ShowAlpha ? Mathf.Clamp01(alpha) : 1f;
            if (current == color)
            {
                ClearReadError("値設定");
                return true;
            }

            return WriteValue(color);
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Color;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <summary>決定キーは文字入力より先に HSV 面の開閉へ使う。</summary>
        public override bool PrefersDecide => true;

        /// <summary>決定キーで HSV 面の表示を切り替える。</summary>
        public override void OnDecide()
        {
            if (IsExpandable) IsExpanded = !IsExpanded;
        }

        /// <inheritdoc/>
        public override bool IsModified
        {
            get
            {
                var defaultValue = _defaultValue;
                if (!ShowAlpha) defaultValue.a = 1f;
                return TryGetColor(out var value) && value != defaultValue;
            }
        }

        /// <summary>右カラムには 16 進表記を出す。色見本は描画側が別に描く。</summary>
        public override string GetValueText() =>
            ShowAlpha ? "#" + ColorUtility.ToHtmlStringRGBA(Value) : "#" + ColorUtility.ToHtmlStringRGB(Value);

        /// <inheritdoc/>
        public override string GetEditText() => GetValueText();

        /// <summary>16 進表記を受け取る。<c>#</c> の有無どちらでも通す。</summary>
        /// <param name="text">打ち終えた文字列。</param>
        public override bool CommitEditText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            var normalized = text[0] == '#' ? text : "#" + text;
            if (!ColorUtility.TryParseHtmlString(normalized, out var parsed)) return false;

            return TrySetValue(parsed);
        }

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            if (!TryGetColor(out var current)) return;

            var defaultValue = _defaultValue;
            if (!ShowAlpha) defaultValue.a = 1f;
            TrySetValue(defaultValue, current);
        }

        /// <summary>
        /// 外から色が変わったときに HSV を追従させる。
        /// <para>
        /// 明度や彩度が 0 の色は色相を復元できないので、そのときは今の色相を保つ。
        /// </para>
        /// </summary>
        public void SyncHsvFrom(Color color)
        {
            Color.RGBToHSV(color, out var hue, out var saturation, out var brightness);

            // 無彩色・黒からは色相が取れない。取れないときは今の値を残す。
            if (saturation > 0f && brightness > 0f) _hue = hue;

            _saturation = saturation;
            _brightness = brightness;
        }

        /// <summary>16 進表記から色を作る補助。</summary>
        /// <param name="hex">"#RRGGBB" または "#RRGGBBAA"。</param>
        /// <param name="color">解釈できた色。</param>
        public static bool TryParseHex(string hex, out Color color)
        {
            if (string.IsNullOrEmpty(hex))
            {
                color = default;
                return false;
            }

            var normalized = hex[0] == '#' ? hex : "#" + hex;
            return ColorUtility.TryParseHtmlString(normalized, out color);
        }

        /// <summary>色を <c>0.00, 0.00, 0.00</c> 形式で表す（診断用）。</summary>
        public string ToComponentString()
        {
            var value = Value;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:F2}, {1:F2}, {2:F2}, {3:F2}",
                value.r, value.g, value.b, value.a);
        }

        private bool TrySetValue(Color value)
        {
            if (!TryGetColor(out var current)) return false;

            return TrySetValue(value, current);
        }

        private bool TrySetValue(Color value, Color current)
        {
            var next = value;
            if (!ShowAlpha) next.a = 1f;
            if (current == next)
            {
                ClearReadError("値設定");
                return true;
            }

            return WriteValue(next);
        }

        private void CaptureDefaultIfNeeded(Color value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value;
            _hasDefaultValue = true;
            SyncHsvFrom(value);
        }
    }
}
