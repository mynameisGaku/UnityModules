using System;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>実行中に適用できる外観プリセット。</summary>
    public enum DebugMenuAppearancePreset
    {
        /// <summary>小さい画面向けに文字と余白を詰める。</summary>
        Compact,

        /// <summary>既定の読みやすさと情報量へ戻す。</summary>
        Standard,

        /// <summary>離れた位置や高解像度画面でも読みやすく広げる。</summary>
        Large,
    }

    /// <summary>
    /// 実行中に文字サイズ、GUI倍率、行高、画面余白を調整するトップレベルページ。
    /// 値変更時は表示をその場で破棄せず、Controllerへ次フレーム以降の再適用を要求する。
    /// </summary>
    public sealed class DebugMenuAppearancePage
    {
        private const int MinimumFontSize = 8;
        private const int MaximumFontSize = 48;
        private const float MinimumGuiScale = 0.25f;
        private const float MaximumGuiScale = 4f;
        private const float MinimumRowHeight = 8f;
        private const float MaximumRowHeight = 96f;
        private const float MaximumMargin = 256f;

        private readonly DebugMenuTheme _theme;
        private readonly Action _requestApplyTheme;
        private readonly AppearanceValues _resetValues;

        /// <summary>対象テーマと遅延再適用要求を指定して作る。</summary>
        /// <param name="theme">実行中に変更するテーマ。</param>
        /// <param name="requestApplyTheme">安全なタイミングで表示を作り直す要求。</param>
        public DebugMenuAppearancePage(DebugMenuTheme theme, Action requestApplyTheme)
        {
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _requestApplyTheme = requestApplyTheme ?? throw new ArgumentNullException(nameof(requestApplyTheme));
            _resetValues = AppearanceValues.Capture(theme);

            Page = new DebugPage("Appearance")
            {
                Description = "文字、GUI倍率、行高、画面端からの余白を実行中に調整する。",
            };

            Page.Int("Font Size", () => _theme.FontSize, SetFontSize)
                .WithRange(MinimumFontSize, MaximumFontSize)
                .WithStep(1)
                .WithSaveKey("debug-menu.appearance.font-size");
            Page.Float("GUI Scale", () => _theme.GuiScale, SetGuiScale)
                .WithRange(MinimumGuiScale, MaximumGuiScale)
                .WithStep(0.05f)
                .WithDigits(2)
                .WithSaveKey("debug-menu.appearance.gui-scale");
            Page.Float("Row Height", () => _theme.RowHeight, SetRowHeight)
                .WithRange(MinimumRowHeight, MaximumRowHeight)
                .WithStep(1f)
                .WithDigits(0)
                .WithUnit("px")
                .WithSaveKey("debug-menu.appearance.row-height");
            Page.Float("Panel Margin", () => _theme.PanelMargin, SetPanelMargin)
                .WithRange(0f, MaximumMargin)
                .WithStep(2f)
                .WithDigits(0)
                .WithUnit("px")
                .WithSaveKey("debug-menu.appearance.panel-margin");
            Page.Float("Top Margin", () => _theme.TopMargin, SetTopMargin)
                .WithRange(0f, MaximumMargin)
                .WithStep(2f)
                .WithDigits(0)
                .WithUnit("px")
                .WithSaveKey("debug-menu.appearance.top-margin");

            Page.Group("Presets", group =>
            {
                group.Action("Compact", () => ApplyPreset(DebugMenuAppearancePreset.Compact));
                group.Action("Standard", () => ApplyPreset(DebugMenuAppearancePreset.Standard));
                group.Action("Large", () => ApplyPreset(DebugMenuAppearancePreset.Large));
                group.Action("Reset", Reset);
            });
        }

        /// <summary>トップレベルへ登録する外観ページ。</summary>
        public DebugPage Page { get; }

        /// <summary>指定プリセットをまとめて適用し、表示の遅延再適用を1回だけ要求する。</summary>
        /// <param name="preset">適用する外観プリセット。</param>
        public void ApplyPreset(DebugMenuAppearancePreset preset)
        {
            switch (preset)
            {
                case DebugMenuAppearancePreset.Compact:
                    ApplyValues(new AppearanceValues(14, 0.85f, 18f, 16f, 12f));
                    break;
                case DebugMenuAppearancePreset.Standard:
                    ApplyValues(new AppearanceValues(20, 1f, 20f, 24f, 16f));
                    break;
                case DebugMenuAppearancePreset.Large:
                    ApplyValues(new AppearanceValues(28, 1.25f, 24f, 32f, 24f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        /// <summary>このサービスを作った時点のテーマ寸法へ戻す。</summary>
        public void Reset() => ApplyValues(_resetValues);

        private void SetFontSize(int value)
        {
            var next = Mathf.Clamp(value, MinimumFontSize, MaximumFontSize);
            if (_theme.FontSize == next) return;
            ApplyValues(new AppearanceValues(next, _theme.GuiScale, _theme.RowHeight, _theme.PanelMargin, _theme.TopMargin));
        }

        private void SetGuiScale(float value)
        {
            var next = Mathf.Clamp(value, MinimumGuiScale, MaximumGuiScale);
            if (Mathf.Approximately(_theme.GuiScale, next)) return;
            ApplyValues(new AppearanceValues(_theme.FontSize, next, _theme.RowHeight, _theme.PanelMargin, _theme.TopMargin));
        }

        private void SetRowHeight(float value)
        {
            var next = Mathf.Clamp(value, MinimumRowHeight, MaximumRowHeight);
            if (Mathf.Approximately(_theme.RowHeight, next)) return;
            ApplyValues(new AppearanceValues(_theme.FontSize, _theme.GuiScale, next, _theme.PanelMargin, _theme.TopMargin));
        }

        private void SetPanelMargin(float value)
        {
            var next = Mathf.Clamp(value, 0f, MaximumMargin);
            if (Mathf.Approximately(_theme.PanelMargin, next)) return;
            ApplyValues(new AppearanceValues(_theme.FontSize, _theme.GuiScale, _theme.RowHeight, next, _theme.TopMargin));
        }

        private void SetTopMargin(float value)
        {
            var next = Mathf.Clamp(value, 0f, MaximumMargin);
            if (Mathf.Approximately(_theme.TopMargin, next)) return;
            ApplyValues(new AppearanceValues(_theme.FontSize, _theme.GuiScale, _theme.RowHeight, _theme.PanelMargin, next));
        }

        private void ApplyValues(in AppearanceValues values)
        {
            var next = new AppearanceValues(
                Mathf.Clamp(values.FontSize, MinimumFontSize, MaximumFontSize),
                Mathf.Clamp(values.GuiScale, MinimumGuiScale, MaximumGuiScale),
                Mathf.Clamp(values.RowHeight, MinimumRowHeight, MaximumRowHeight),
                Mathf.Clamp(values.PanelMargin, 0f, MaximumMargin),
                Mathf.Clamp(values.TopMargin, 0f, MaximumMargin));
            var changed =
                _theme.FontSize != next.FontSize ||
                !Mathf.Approximately(_theme.GuiScale, next.GuiScale) ||
                !Mathf.Approximately(_theme.RowHeight, next.RowHeight) ||
                !Mathf.Approximately(_theme.PanelMargin, next.PanelMargin) ||
                !Mathf.Approximately(_theme.TopMargin, next.TopMargin);
            if (!changed) return;

            var previous = AppearanceValues.Capture(_theme);
            AssignValues(next);
            try
            {
                _requestApplyTheme();
            }
            catch
            {
                AssignValues(previous);
                throw;
            }
        }

        private void AssignValues(in AppearanceValues values)
        {
            _theme.FontSize = values.FontSize;
            _theme.GuiScale = values.GuiScale;
            _theme.RowHeight = values.RowHeight;
            _theme.PanelMargin = values.PanelMargin;
            _theme.TopMargin = values.TopMargin;
        }

        /// <summary>Reset用に保持する5つの寸法値。</summary>
        private readonly struct AppearanceValues
        {
            public readonly int FontSize;
            public readonly float GuiScale;
            public readonly float RowHeight;
            public readonly float PanelMargin;
            public readonly float TopMargin;

            public AppearanceValues(int fontSize, float guiScale, float rowHeight, float panelMargin, float topMargin)
            {
                FontSize = fontSize;
                GuiScale = guiScale;
                RowHeight = rowHeight;
                PanelMargin = panelMargin;
                TopMargin = topMargin;
            }

            public static AppearanceValues Capture(DebugMenuTheme theme) => new AppearanceValues(
                theme.FontSize,
                theme.GuiScale,
                theme.RowHeight,
                theme.PanelMargin,
                theme.TopMargin);
        }
    }
}
