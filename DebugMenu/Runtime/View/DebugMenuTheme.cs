using System;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 見た目の設定。色と寸法だけを持ち、レイアウトの構造は持たない。
    /// <para>
    /// スタイルをアセット（USS）ではなくコードで持つのは、このモジュールを
    /// <b>フォルダごとコピーするだけで動く</b>状態に保つため。USS を使うと
    /// アセットの読み込み経路（Resources か直接参照か）を利用側に強いることになる。
    /// 差し替えたい場合は <see cref="DebugMenuController.Theme"/> の各値を変更するか、
    /// <see cref="DebugMenuView"/> のコンストラクタへ別のテーマを渡す。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class DebugMenuTheme : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private int _sizeLayoutVersion = 1;

        [Header("基本サイズ")]
        [Tooltip("文字以外の GUI 寸法へ一括で掛ける倍率。1 が標準寸法。")]
        [Range(0.5f, 2f)]
        public float GuiScale = 1f;

        /// <summary>文字の大きさ。GUI 寸法とは別に変更できる。</summary>
        [Range(8, 48)]
        public int FontSize = 20;

        /// <summary>1 行の基準高さ（ピクセル）。実寸は <see cref="EffectiveRowHeight"/>。</summary>
        [Min(1f)]
        public float RowHeight = 20f;

        /// <summary>字下げ 1 段あたりの基準幅（ピクセル）。</summary>
        [Min(0f)]
        public float IndentWidth = 20f;

        [Header("レイアウト")]
        /// <summary>旧固定幅レイアウトとの互換用。全画面レイアウトでは使わない。</summary>
        [Min(0f)]
        public float PanelWidth = 500f;

        /// <summary>内容の左端。標準は 24 ピクセル。</summary>
        [Min(0f)]
        public float PanelMargin = 24f;

        /// <summary>内容の上端。標準は 16 ピクセル。</summary>
        [Min(0f)]
        public float TopMargin = 16f;

        /// <summary>行の左端から値列までの距離を、行高の倍率で表す。</summary>
        [Min(0f)]
        public float ValueColumnRatio = 12f;

        /// <summary>狭い行でも表示名へ確保したい幅を、行高の倍率で表す。</summary>
        [Min(0f)]
        public float MinimumLabelWidthRatio = 3f;

        /// <summary>表示名と値列の間隔を、行高の倍率で表す。</summary>
        [Min(0f)]
        public float ColumnGapRatio = 0.35f;

        /// <summary>値列の右端へ必ず残す余白を、行高の倍率で表す。</summary>
        [Min(0f)]
        public float RowEndPaddingRatio = 0.4f;

        [Header("操作部品")]
        /// <summary>文字を直接入力できる欄の上限幅を、行高の倍率で表す。</summary>
        [Min(0.1f)]
        public float EditFieldWidthRatio = 6.5f;

        /// <summary>狭い行で直接入力欄へ優先して残す幅を、行高の倍率で表す。</summary>
        [Min(0.1f)]
        public float EditFieldMinimumWidthRatio = 4f;

        /// <summary>狭い行で数値入力欄へ残す幅。文字列より短くても判読できる。</summary>
        [Min(0.1f)]
        public float NumericFieldMinimumWidthRatio = 2.5f;

        /// <summary>範囲スライダーの上限幅を、行高の倍率で表す。</summary>
        [Min(0.1f)]
        public float SliderWidthRatio = 5f;

        /// <summary>値欄に余裕があるときスライダーへ残す最小幅。</summary>
        [Min(0f)]
        public float SliderMinimumWidthRatio = 1f;

        /// <summary>左右変更ボタン 1 個の幅。</summary>
        [Min(0.1f)]
        public float AdjustButtonWidthRatio = 0.38f;

        /// <summary>ヘッダーの戻る・ページボタンの一辺。</summary>
        [Min(0.1f)]
        public float HeaderButtonSizeRatio = 1f;

        /// <summary>ヘッダーボタンどうしの間隔。</summary>
        [Min(0f)]
        public float HeaderButtonGapRatio = 0.16f;

        /// <summary>チェックボックスの一辺。</summary>
        [Min(0.1f)]
        public float CheckboxSizeRatio = 0.55f;

        /// <summary>色見本の幅。</summary>
        [Min(0.1f)]
        public float ColorSwatchWidthRatio = 1.6f;

        /// <summary>色見本の高さ。</summary>
        [Min(0.1f)]
        public float ColorSwatchHeightRatio = 0.55f;

        /// <summary>スライダーのクリック領域の高さ。</summary>
        [Min(0.1f)]
        public float SliderHeightRatio = 0.55f;

        /// <summary>スライダーの溝の高さ。</summary>
        [Min(0.01f)]
        public float SliderRailHeightRatio = 0.12f;

        /// <summary>操作部品どうしの間隔。</summary>
        [Min(0f)]
        public float ControlGapRatio = 0.35f;

        /// <summary>入力欄内の左右余白。</summary>
        [Min(0f)]
        public float InputHorizontalPaddingRatio = 0.25f;

        /// <summary>変更済みの行に出す左帯の幅。</summary>
        [Min(0.01f)]
        public float ModifiedMarkWidthRatio = 0.12f;

        [Header("展開表示")]
        /// <summary>折れ線領域の幅を、行高の倍率で表す。</summary>
        [Min(0.1f)]
        public float GraphWidthRatio = 10f;

        /// <summary>展開した HSV 面の基準高さ。</summary>
        [Min(1f)]
        public float ColorPickerHeight = 120f;

        /// <summary>色選択パネルの幅を HSV 面の高さに対する倍率で表す。</summary>
        [Min(0.1f)]
        public float ColorPickerWidthRatio = 1.1f;

        /// <summary>色選択パネル内の余白。</summary>
        [Min(0f)]
        public float ColorPickerPaddingRatio = 0.3f;

        /// <summary>展開内容を通常時に字下げする幅。</summary>
        [Min(0f)]
        public float ExpandedContentInsetRatio = 2f;

        /// <summary>狭幅時も展開内容へ残す最小幅。</summary>
        [Min(0.1f)]
        public float ExpandedContentMinimumWidthRatio = 4f;

        [Header("色")]
        /// <summary>画面全体へ敷く青みのある黒。</summary>
        public Color Background = new Color(0.02f, 0.03f, 0.05f, 0.82f);

        /// <summary>上部の補助ボタンへ敷く背景。</summary>
        public Color HeaderBackground = new Color(0.04f, 0.05f, 0.07f, 1f);

        /// <summary>選択されている行の背景。</summary>
        public Color SelectionBackground = new Color(0.20f, 0.35f, 0.60f, 0.85f);

        /// <summary>マウスを重ねている行の背景。</summary>
        public Color HoverBackground = new Color(0.30f, 0.45f, 0.70f, 0.35f);

        /// <summary>通常の文字色。</summary>
        public Color Text = new Color(0.78f, 0.78f, 0.78f, 1f);

        /// <summary>選択されている行の文字色。</summary>
        public Color SelectedText = Color.white;

        /// <summary>補助的な文字色。</summary>
        public Color TextDim = new Color(0.52f, 0.58f, 0.68f, 1f);

        /// <summary>タイトルの文字色。</summary>
        public Color Title = Color.white;

        /// <summary>現在位置を示すパンくずの文字色。</summary>
        public Color Breadcrumb = new Color(0.60f, 0.75f, 0.95f, 1f);

        /// <summary>右カラムの値の色。通常は左の表示名と同じ色。</summary>
        public Color Value = new Color(0.78f, 0.78f, 0.78f, 1f);

        /// <summary>既定値から変えられている行の左に出す印。</summary>
        public Color Modified = new Color(0.95f, 0.75f, 0.35f, 0.95f);

        /// <summary>注意範囲を外れた値の色。</summary>
        public Color Warning = new Color(0.98f, 0.62f, 0.30f, 1f);

        /// <summary>見出し行の文字色。</summary>
        public Color GroupText = new Color(0.95f, 0.90f, 0.55f, 1f);

        /// <summary>スライダーの溝。</summary>
        public Color SliderTrack = new Color(1f, 1f, 1f, 0.12f);

        /// <summary>スライダーの満たされた部分。</summary>
        public Color SliderFill = new Color(0.78f, 0.78f, 0.78f, 0.90f);

        /// <summary>折れ線の色。</summary>
        public Color GraphLine = new Color(0.78f, 0.78f, 0.78f, 1f);

        /// <summary>折れ線を描く領域の背景。</summary>
        public Color GraphBackground = new Color(0.78f, 0.78f, 0.78f, 0.12f);

        /// <summary>折れ線の目盛り線。</summary>
        public Color GraphGrid = new Color(1f, 1f, 1f, 0f);

        /// <summary>HSV 面と色相帯の枠。</summary>
        public Color ColorPickerBorder = new Color(0.78f, 0.78f, 0.78f, 0.75f);

        /// <summary>色選択パネルの背景。</summary>
        public Color ColorPickerBackground = new Color(0.07f, 0.08f, 0.11f, 1f);

        /// <summary>色選択パネルの外枠。</summary>
        public Color ColorPickerPanelBorder = new Color(0.45f, 0.52f, 0.65f, 1f);

        /// <summary>ピン留めされた行に添える印の色。</summary>
        public Color Favorite = new Color(0.98f, 0.85f, 0.35f, 1f);

        /// <summary>直接入力できる値欄の背景。</summary>
        public Color InputFieldBackground = new Color(0.04f, 0.05f, 0.07f, 1f);

        /// <summary>待機中の直接入力欄の枠。</summary>
        public Color InputFieldBorder = new Color(0.30f, 0.34f, 0.42f, 1f);

        /// <summary>入力中の直接入力欄の枠。</summary>
        public Color ActiveInputFieldBorder = new Color(0.45f, 0.70f, 0.98f, 1f);

        /// <summary>入力中の文字。選択色と明確に区別できる明色を使う。</summary>
        public Color InputFieldText = Color.white;

        /// <summary>入力中に選択されている文字の背景。</summary>
        public Color InputFieldSelection = new Color(0.20f, 0.45f, 0.75f, 0.80f);

        /// <summary>入力カーソルの色。</summary>
        public Color InputFieldCursor = Color.white;

        /// <summary>右下へ浮かせる説明欄の背景。</summary>
        public Color DescriptionBackground = new Color(0.05f, 0.06f, 0.09f, 0.82f);

        /// <summary>右下へ浮かせる説明欄の枠。</summary>
        public Color DescriptionBorder = new Color(0.45f, 0.55f, 0.70f, 0.85f);

        /// <summary>右下へ浮かせる説明文の色。</summary>
        public Color DescriptionText = new Color(0.88f, 0.88f, 0.88f, 1f);

        /// <summary>古い Scene で未保存の 0 も標準倍率として扱う。</summary>
        public float EffectiveGuiScale => GuiScale > 0f ? Mathf.Clamp(GuiScale, 0.25f, 4f) : 1f;

        /// <summary>実際に表示へ使う文字サイズ。</summary>
        public int EffectiveFontSize => Mathf.Max(8, FontSize);

        /// <summary>
        /// GUI 倍率を掛けた 1 行の高さ。
        /// 文字を行から切り落とさないよう、文字サイズを下限にする。
        /// </summary>
        public float EffectiveRowHeight => Mathf.Max(
            ScalePixels(Mathf.Max(1f, RowHeight)),
            EffectiveFontSize);

        /// <summary>GUI 倍率を掛けた字下げ幅。</summary>
        public float EffectiveIndentWidth => ScalePixels(Mathf.Max(0f, IndentWidth));

        /// <summary>GUI 倍率を掛けた左右余白。</summary>
        public float EffectivePanelMargin => ScalePixels(Mathf.Max(0f, PanelMargin));

        /// <summary>GUI 倍率を掛けた上下余白。</summary>
        public float EffectiveTopMargin => ScalePixels(Mathf.Max(0f, TopMargin));

        /// <summary>GUI 倍率を掛けた色選択面の高さ。</summary>
        public float EffectiveColorPickerHeight => ScalePixels(Mathf.Max(1f, ColorPickerHeight));

        /// <summary>基準ピクセル値へ GUI 倍率を掛ける。</summary>
        public float ScalePixels(float pixels) => pixels * EffectiveGuiScale;

        /// <summary>文字と GUI の基本倍率をまとめて変更する。</summary>
        /// <param name="fontSize">文字サイズ。</param>
        /// <param name="guiScale">文字以外の GUI 寸法倍率。</param>
        /// <returns>続けてテーマを設定できる同じインスタンス。</returns>
        public DebugMenuTheme SetSizes(int fontSize, float guiScale)
        {
            FontSize = Mathf.Max(8, fontSize);
            GuiScale = Mathf.Clamp(guiScale, 0.25f, 4f);
            return this;
        }

        /// <inheritdoc/>
        public void OnBeforeSerialize()
        {
            if (_sizeLayoutVersion <= 0) _sizeLayoutVersion = 1;
        }

        /// <inheritdoc/>
        public void OnAfterDeserialize()
        {
            if (_sizeLayoutVersion > 0) return;

            // 新しい寸法フィールドを持たない Scene / Prefab は全て 0 で届くため、
            // 以前の固定値へ戻して見た目を保つ。
            GuiScale = 1f;
            MinimumLabelWidthRatio = 3f;
            ColumnGapRatio = 0.35f;
            RowEndPaddingRatio = 0.4f;
            EditFieldMinimumWidthRatio = 4f;
            NumericFieldMinimumWidthRatio = 2.5f;
            SliderMinimumWidthRatio = 1f;
            AdjustButtonWidthRatio = 0.38f;
            HeaderButtonSizeRatio = 1f;
            HeaderButtonGapRatio = 0.16f;
            CheckboxSizeRatio = 0.55f;
            ColorSwatchWidthRatio = 1.6f;
            ColorSwatchHeightRatio = 0.55f;
            SliderHeightRatio = 0.55f;
            SliderRailHeightRatio = 0.12f;
            ControlGapRatio = 0.35f;
            InputHorizontalPaddingRatio = 0.25f;
            ModifiedMarkWidthRatio = 0.12f;
            ColorPickerWidthRatio = 1.1f;
            ColorPickerPaddingRatio = 0.3f;
            ExpandedContentInsetRatio = 2f;
            ExpandedContentMinimumWidthRatio = 4f;
            InputFieldText = Color.white;
            InputFieldSelection = new Color(0.20f, 0.45f, 0.75f, 0.80f);
            InputFieldCursor = Color.white;
            _sizeLayoutVersion = 1;
        }
    }
}
