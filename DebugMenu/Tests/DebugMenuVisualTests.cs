using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DebugMenu.Tests
{
    /// <summary>ランタイムデバッグメニューの既定色、配置、操作を検証する。</summary>
    public sealed class DebugMenuVisualTests
    {
        private const float Tolerance = 0.001f;

        private sealed class ThrowingRatioElement : DebugElement
        {
            public ThrowingRatioElement() : base("Custom Ratio") { }

            public bool ThrowOnSet { get; set; } = true;
            public float Ratio { get; private set; } = 0.5f;
            public override bool IsAdjustable => true;
            public override DebugValueKind ValueKind => DebugValueKind.Float;
            public override string GetValueText() => Ratio.ToString("F1");

            public override bool TryGetRatio(out float ratio)
            {
                ratio = Ratio;
                return true;
            }

            public override bool TrySetRatio(float ratio)
            {
                if (ThrowOnSet) throw new System.InvalidOperationException("custom ratio failed");
                Ratio = ratio;
                return true;
            }
        }

        private sealed class ThrowingTextCommitElement : DebugElement
        {
            public ThrowingTextCommitElement() : base("Custom Text") { }

            public override bool CanTypeValue => true;
            public override DebugValueKind ValueKind => DebugValueKind.Text;
            public override string GetValueText() => "before";
            public override string GetEditText() => "before";
            public override bool CommitEditText(string text) =>
                throw new System.InvalidOperationException("custom text setter failed");
        }

        private GameObject _panelObject;
        private PanelSettings _panelSettings;
        private UIDocument _document;

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);

            _panelObject = null;
            _panelSettings = null;
            _document = null;
        }

        [Test]
        public void DefaultTheme_UsesStandardPalette()
        {
            var theme = new DebugMenuTheme();

            AssertColor(new Color(0.02f, 0.03f, 0.05f, 0.82f), theme.Background);
            AssertColor(new Color(0.20f, 0.35f, 0.60f, 0.85f), theme.SelectionBackground);
            AssertColor(new Color(0.30f, 0.45f, 0.70f, 0.35f), theme.HoverBackground);
            AssertColor(new Color(0.78f, 0.78f, 0.78f, 1f), theme.Text);
            AssertColor(Color.white, theme.SelectedText);
            AssertColor(new Color(0.60f, 0.75f, 0.95f, 1f), theme.Breadcrumb);
            AssertColor(new Color(0.95f, 0.90f, 0.55f, 1f), theme.GroupText);
            AssertColor(new Color(0.95f, 0.75f, 0.35f, 0.95f), theme.Modified);
            AssertColor(new Color(0.98f, 0.62f, 0.30f, 1f), theme.Warning);
            AssertColor(new Color(0.98f, 0.85f, 0.35f, 1f), theme.Favorite);
            AssertColor(new Color(0.04f, 0.05f, 0.07f, 1f), theme.InputFieldBackground);
            AssertColor(new Color(0.30f, 0.34f, 0.42f, 1f), theme.InputFieldBorder);
            AssertColor(new Color(0.45f, 0.70f, 0.98f, 1f), theme.ActiveInputFieldBorder);
            AssertColor(new Color(0.05f, 0.06f, 0.09f, 0.82f), theme.DescriptionBackground);
            AssertColor(new Color(0.45f, 0.55f, 0.70f, 0.85f), theme.DescriptionBorder);
            AssertColor(new Color(0.88f, 0.88f, 0.88f, 1f), theme.DescriptionText);
            AssertColor(new Color(0.04f, 0.05f, 0.07f, 0.96f), theme.ToastBackground);
            AssertColor(new Color(0.45f, 0.85f, 0.60f, 1f), theme.ToastSuccess);
        }

        [Test]
        public void DefaultTheme_UsesStandardDimensions()
        {
            var theme = new DebugMenuTheme();

            Assert.That(theme.RowHeight, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(theme.IndentWidth, Is.EqualTo(20f).Within(Tolerance));
            Assert.AreEqual(20, theme.FontSize);
            Assert.That(theme.PanelMargin, Is.EqualTo(24f).Within(Tolerance));
            Assert.That(theme.TopMargin, Is.EqualTo(16f).Within(Tolerance));
            Assert.That(theme.ValueColumnRatio, Is.EqualTo(12f).Within(Tolerance));
            Assert.That(theme.EditFieldWidthRatio, Is.EqualTo(6.5f).Within(Tolerance));
            Assert.That(theme.SliderWidthRatio, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(theme.GraphWidthRatio, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(theme.ColorPickerHeight, Is.EqualTo(120f).Within(Tolerance));
            Assert.That(theme.PanelMargin + theme.RowHeight * theme.ValueColumnRatio, Is.EqualTo(264f).Within(Tolerance),
                "値列の画面上の開始位置が標準値とずれている");
        }

        [Test]
        public void Theme_SetSizesChangesFontAndGuiGeometryAndKeepsTextInsideRows()
        {
            var theme = new DebugMenuTheme().SetSizes(26, 1.25f);
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var menuView = new DebugMenuView(menu, theme);
            var rowView = new DebugRowView(theme);
            rowView.Bind(new DebugRow(new DebugBool("God Mode", true), 0), false, 0);

            Assert.AreEqual(26, theme.EffectiveFontSize);
            Assert.That(theme.EffectiveRowHeight, Is.EqualTo(26f).Within(Tolerance));
            Assert.That(theme.EffectiveIndentWidth, Is.EqualTo(25f).Within(Tolerance));
            Assert.That(theme.EffectivePanelMargin, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(theme.EffectiveTopMargin, Is.EqualTo(20f).Within(Tolerance));
            AssertLength(30f, menuView.Root.style.paddingLeft);
            Assert.That(
                menuView.Root.Q<Label>("debug-menu-title").style.fontSize.value.value,
                Is.EqualTo(26f).Within(Tolerance));
            AssertLength(26f, rowView.Q<VisualElement>("debug-menu-row-header").style.height);
            AssertLength(26f * theme.CheckboxSizeRatio, rowView.Q<VisualElement>("debug-menu-checkbox").style.width);
        }

        [Test]
        public void Theme_LargeFontRaisesMinimumRowHeightEvenAtSmallGuiScale()
        {
            var theme = new DebugMenuTheme().SetSizes(48, 0.5f);
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var menuView = new DebugMenuView(menu, theme);
            var rowView = new DebugRowView(theme);
            rowView.Bind(new DebugRow(new DebugText("Name", "Player"), 0), false, 0);

            Assert.That(theme.EffectiveRowHeight, Is.EqualTo(48f).Within(Tolerance));
            AssertLength(48f, rowView.Q<VisualElement>("debug-menu-row-header").style.height);
            AssertLength(48f, menuView.Root.Q<Label>("debug-menu-page-header").style.height);
        }

        [Test]
        public void Theme_ZeroGuiScaleUsesLegacyDefaultScale()
        {
            var theme = new DebugMenuTheme { GuiScale = 0f, RowHeight = 20f };

            Assert.That(theme.EffectiveGuiScale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(theme.EffectiveRowHeight, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void MenuView_UsesFullScreenFrame()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var theme = new DebugMenuTheme();
            var view = new DebugMenuView(menu, theme);
            var root = view.Root;

            Assert.AreEqual("debug-menu", root.name);
            Assert.AreEqual(Position.Absolute, root.style.position.value);
            AssertLength(0f, root.style.left);
            AssertLength(0f, root.style.top);
            AssertLength(0f, root.style.right);
            AssertLength(0f, root.style.bottom);
            AssertLength(theme.PanelMargin, root.style.paddingLeft);
            AssertLength(theme.PanelMargin, root.style.paddingRight);
            AssertLength(theme.TopMargin, root.style.paddingTop);
            AssertLength(theme.TopMargin, root.style.paddingBottom);
            AssertColor(theme.Background, root.style.backgroundColor.value);

            var title = root.Q<Label>("debug-menu-title");
            var breadcrumb = root.Q<Label>("debug-menu-breadcrumb");
            var pageHeader = root.Q<Label>("debug-menu-page-header");
            Assert.NotNull(title);
            Assert.NotNull(breadcrumb);
            Assert.NotNull(pageHeader);
            Assert.AreEqual("DebugTop", title.text);
            Assert.AreEqual(LengthUnit.Pixel, title.style.fontSize.value.unit);
            Assert.That(title.style.fontSize.value.value, Is.EqualTo(theme.FontSize).Within(Tolerance));
            AssertColor(theme.Title, title.style.color.value);
            AssertColor(theme.Breadcrumb, breadcrumb.style.color.value);
            AssertColor(theme.GroupText, pageHeader.style.color.value);
        }

        [Test]
        public void MenuView_UsesFloatingDescriptionPanel()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var theme = new DebugMenuTheme();
            var view = new DebugMenuView(menu, theme);
            var panel = view.Root.Q<VisualElement>("debug-menu-description-panel");
            var description = view.Root.Q<Label>("debug-menu-description");

            Assert.NotNull(panel);
            Assert.NotNull(description);
            Assert.AreEqual(Position.Absolute, panel.style.position.value);
            AssertLength(theme.RowHeight * 0.8f, panel.style.right);
            AssertLength(theme.RowHeight * 0.8f, panel.style.bottom);
            Assert.AreEqual(DisplayStyle.None, panel.style.display.value);
            AssertColor(theme.DescriptionBackground, panel.style.backgroundColor.value);
            AssertColor(theme.DescriptionBorder, panel.style.borderTopColor.value);
            Assert.That(panel.style.borderTopWidth.value, Is.EqualTo(1f).Within(Tolerance));
            AssertColor(theme.DescriptionText, description.style.color.value);
        }

        [Test]
        public void MenuView_ShowsCurrentToastWithKindColor()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var theme = new DebugMenuTheme();
            var toasts = new DebugMenuToastService();
            var view = new DebugMenuView(menu, theme, toasts);

            toasts.Show("Saved", DebugMenuToastKind.Success);
            view.Refresh();

            var panel = view.Root.Q<VisualElement>("debug-menu-toast");
            var label = view.Root.Q<Label>("debug-menu-toast-text");
            Assert.AreEqual(DisplayStyle.Flex, panel.style.display.value);
            Assert.AreEqual("Saved", label.text);
            AssertColor(theme.ToastSuccess, label.style.color.value);
            AssertColor(theme.ToastSuccess, panel.style.borderTopColor.value);
        }

        [Test]
        public void MenuView_CreatesPointerLocalHoverDescription()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var view = new DebugMenuView(menu);
            var panel = view.Root.Q<VisualElement>("debug-menu-hover-tooltip");
            var label = view.Root.Q<Label>("debug-menu-hover-tooltip-text");

            Assert.NotNull(panel);
            Assert.NotNull(label);
            Assert.AreEqual(Position.Absolute, panel.style.position.value);
            Assert.AreEqual(PickingMode.Ignore, panel.pickingMode);
            Assert.AreEqual(DisplayStyle.None, panel.style.display.value);
        }

        [Test]
        public void MenuView_BreadcrumbContainsCompletePagePath()
        {
            var menu = new DebugMenuRoot();
            var rootPage = menu.AddPage("Game");
            var childPage = new DebugPage("Advanced");
            rootPage.AddChildPage(childPage);
            menu.PushPage(childPage);
            var view = new DebugMenuView(menu);

            view.Refresh();

            Assert.AreEqual("DebugTop - Game - Advanced", view.Root.Q<Label>("debug-menu-breadcrumb").text);
        }

        [Test]
        public void MenuView_EmptyMenuClearsPageDisplayAndInteractionStateIdempotently()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            page.Root.Add(new DebugText("Name", "Player") { Description = "Current name" });
            var view = new DebugMenuView(menu);
            view.Refresh();
            var editingRow = new DebugRowView(new DebugMenuTheme());
            editingRow.Bind(page.VisibleRows[0], true, 0);
            Assert.IsTrue(editingRow.BeginTextEdit());
            WritePrivateField(view, "_editingRow", editingRow);

            menu.ClearPages();
            view.Refresh();

            var rows = (System.Collections.IList)ReadPrivateField(view, "_rows");
            var list = view.Root.Q<ListView>("debug-menu-list");
            Assert.AreEqual(0, rows.Count);
            Assert.AreEqual(0, list.itemsSource.Count);
            Assert.AreEqual(string.Empty, view.Root.Q<Label>("debug-menu-breadcrumb").text);
            Assert.AreEqual(string.Empty, view.Root.Q<Label>("debug-menu-page-header").text);
            Assert.AreEqual(string.Empty, view.Root.Q<Label>("debug-menu-counter").text);
            Assert.AreEqual(string.Empty, view.Root.Q<Label>("debug-menu-description").text);
            Assert.AreEqual(DisplayStyle.None, view.Root.Q<VisualElement>("debug-menu-description-panel").style.display.value);
            Assert.AreEqual(DisplayStyle.None, view.Root.Q<VisualElement>("debug-menu-hover-tooltip").style.display.value);
            Assert.AreEqual(DisplayStyle.None, ReadPrivateField(view, "_backPage") is Button back ? back.style.display.value : DisplayStyle.Flex);
            Assert.AreEqual(DisplayStyle.None, ReadPrivateField(view, "_previousPage") is Button previous ? previous.style.display.value : DisplayStyle.Flex);
            Assert.AreEqual(DisplayStyle.None, ReadPrivateField(view, "_nextPage") is Button next ? next.style.display.value : DisplayStyle.Flex);
            Assert.IsFalse(view.IsEditingText);
            Assert.IsFalse(view.HasActivePointerInteraction);

            var rowsReference = ReadPrivateField(view, "_rows");
            Assert.DoesNotThrow(view.Refresh);
            Assert.AreSame(rowsReference, ReadPrivateField(view, "_rows"));
            Assert.AreEqual(true, ReadPrivateField(view, "_pageDisplayIsEmpty"));
            Assert.IsFalse(view.ConsumeTextInput(), "空表示の再更新で入力終了状態が再発している");
            Assert.AreEqual(0, list.itemsSource.Count);
        }

        [UnityTest]
        public System.Collections.IEnumerator MenuView_EmptyMenuEndsRealizedTextEdit()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Add(new DebugText("Name", "Player"));
            var view = new DebugMenuView(menu);
            AttachToPanel(view.Root);
            view.Refresh();
            yield return null;

            Assert.IsTrue(view.TryBeginEditCurrent());
            var editingRow = (DebugRowView)ReadPrivateField(view, "_editingRow");
            Assert.IsTrue(editingRow.IsEditingText);

            menu.ClearPages();
            view.Refresh();

            Assert.IsFalse(editingRow.IsEditingText);
            Assert.IsFalse(view.IsEditingText);
            Assert.IsFalse(view.ConsumeTextInput());
        }

        [UnityTest]
        public System.Collections.IEnumerator MenuView_EmptyMenuReleasesRealizedPointerInteraction()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Float("Volume", 0.5f).WithRange(0f, 1f);
            var view = new DebugMenuView(menu);
            AttachToPanel(view.Root);
            view.Refresh();
            yield return null;

            var row = view.Root.Q<DebugRowView>();
            Assert.NotNull(row);
            var slider = row.Q<VisualElement>("debug-menu-slider");
            SendPointerDownAt(slider, slider.worldBound.center);
            Assert.IsTrue(row.HasActivePointerInteraction);

            menu.ClearPages();
            view.Refresh();

            Assert.IsFalse(row.HasActivePointerInteraction);
            Assert.IsFalse(view.HasActivePointerInteraction);
        }

        [Test]
        public void RowView_UsesStandardValueColumnAndControls()
        {
            var theme = new DebugMenuTheme();
            var element = new DebugInt("Speed", 3).WithRange(0, 10);
            var row = new DebugRow(element, 0);
            var view = new DebugRowView(theme);

            view.Bind(row, true, 0);

            var header = view.Q<VisualElement>("debug-menu-row-header");
            var favorite = view.Q<Label>("debug-menu-favorite");
            var marker = view.Q<Label>("debug-menu-marker");
            var label = view.Q<Label>("debug-menu-label");
            var value = view.Q<Label>("debug-menu-value");
            var slider = view.Q<VisualElement>("debug-menu-slider");
            var sliderRail = view.Q<VisualElement>("debug-menu-slider-rail");
            var modifiedMark = view.Q<VisualElement>("debug-menu-modified-mark");

            Assert.NotNull(header);
            Assert.NotNull(favorite);
            Assert.NotNull(marker);
            Assert.NotNull(label);
            Assert.NotNull(value);
            Assert.NotNull(slider);
            Assert.NotNull(sliderRail);
            Assert.NotNull(modifiedMark);
            AssertLength(theme.RowHeight, header.style.height);
            AssertLength(theme.RowHeight, favorite.style.width);
            AssertLength(theme.RowHeight, marker.style.width);
            AssertLength(
                theme.RowHeight * (theme.ValueColumnRatio - 2f - theme.ColumnGapRatio),
                label.style.width);
            AssertLength(theme.RowHeight * theme.EditFieldWidthRatio, value.style.width);
            AssertLength(theme.RowHeight * theme.SliderWidthRatio, slider.style.width);
            AssertLength(theme.RowHeight * 0.55f, slider.style.height);
            AssertLength(theme.RowHeight * 0.12f, sliderRail.style.height);
            Assert.AreEqual(Position.Absolute, modifiedMark.style.position.value);
            AssertLength(theme.RowHeight * 0.12f, modifiedMark.style.width);
            AssertColor(theme.SelectionBackground, view.style.backgroundColor.value);
            AssertColor(theme.SelectedText, label.style.color.value);
            AssertColor(theme.SelectedText, value.style.color.value);
            AssertColor(theme.InputFieldBackground, value.style.backgroundColor.value);
            AssertColor(theme.InputFieldBorder, value.style.borderTopColor.value);
            Assert.That(value.style.borderTopWidth.value, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void RowView_ProviderExceptionShowsOnlyThatRowAsErrorAndRecovers()
        {
            var shouldThrow = true;
            var theme = new DebugMenuTheme();
            var element = new DebugWatch("Network", () => shouldThrow
                ? throw new System.InvalidOperationException("provider failed")
                : "Connected");
            var view = new DebugRowView(theme);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => view.Bind(new DebugRow(element, 0), false, 0));
            Assert.That(view.Q<Label>("debug-menu-value").text, Does.StartWith("ERROR:"));
            AssertColor(theme.Warning, view.Q<Label>("debug-menu-label").style.color.value);

            shouldThrow = false;
            Assert.DoesNotThrow(() => view.Bind(new DebugRow(element, 0), false, 0));
            Assert.AreEqual("Connected", view.Q<Label>("debug-menu-value").text);
            AssertColor(theme.Text, view.Q<Label>("debug-menu-label").style.color.value);
        }

        [Test]
        public void RowView_LateGetterFailuresDisableAllValueControls()
        {
            var enumReads = 0;
            var enumArmed = false;
            var choice = new DebugEnum("Quality", new[] { "Low", "High" }, () =>
            {
                if (enumArmed && ++enumReads == 2) throw new System.InvalidOperationException("selection failed");
                return 0;
            }, _ => { });
            enumReads = 0;
            enumArmed = true;
            AssertLateReadFailure(choice);

            var ratioReads = 0;
            var ratioArmed = false;
            var ratio = new DebugFloat("Volume", () =>
            {
                if (ratioArmed && ++ratioReads == 2) throw new System.InvalidOperationException("ratio failed");
                return 0.5f;
            }, _ => { }).WithRange(0f, 1f);
            ratioReads = 0;
            ratioArmed = true;
            AssertLateReadFailure(ratio);

            var modifiedReads = 0;
            var modifiedArmed = false;
            var modified = new DebugInt("Count", () =>
            {
                if (modifiedArmed && ++modifiedReads == 2) throw new System.InvalidOperationException("modified failed");
                return 1;
            }, _ => { });
            modifiedReads = 0;
            modifiedArmed = true;
            AssertLateReadFailure(modified);

            var warningReads = 0;
            var warningArmed = false;
            var warned = new DebugInt("Budget", () =>
            {
                if (warningArmed && ++warningReads == 2) throw new System.InvalidOperationException("warning failed");
                return 1;
            }, _ => { });
            warned.SetWarnRange(0f, 2f);
            warningReads = 0;
            warningArmed = true;
            AssertLateReadFailure(warned);
        }

        [Test]
        public void SliderPointer_GetterFailureStopsDragAndDisablesControls()
        {
            var shouldThrow = false;
            var setterCalls = 0;
            var element = new DebugFloat(
                "Volume",
                () => shouldThrow ? throw new System.InvalidOperationException("pointer failed") : 0.5f,
                _ => setterCalls++).WithRange(0f, 1f);
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);
            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => SendPointerDown(view.Q<VisualElement>("debug-menu-slider"), 0));

            Assert.IsTrue(element.HasReadError);
            Assert.IsFalse(view.HasActivePointerInteraction);
            Assert.AreEqual(0, setterCalls);
            AssertErrorControlsDisabled(view);
        }

        [Test]
        public void SliderPointer_CustomSetterExceptionStopsDragAndAllowsRetry()
        {
            var element = new ThrowingRatioElement();
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => SendPointerDown(view.Q<VisualElement>("debug-menu-slider"), 0));

            Assert.IsTrue(element.HasReadError);
            Assert.IsFalse(view.HasActivePointerInteraction);
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("debug-menu-slider").style.display.value);
        }

        [Test]
        public void SliderPointer_MoveSetterFailureReleasesCaptureAndAllowsNextDown()
        {
            var element = new ThrowingRatioElement { ThrowOnSet = false };
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);
            var slider = view.Q<VisualElement>("debug-menu-slider");

            SendPointerDownAt(slider, slider.worldBound.center);
            Assert.IsTrue(view.HasActivePointerInteraction);
            element.ThrowOnSet = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => SendPointerMoveAt(slider, slider.worldBound.center + Vector2.right));

            Assert.IsFalse(view.HasActivePointerInteraction);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<Label>("debug-menu-error").style.display.value);

            element.ThrowOnSet = false;
            SendPointerDownAt(slider, slider.worldBound.center);
            Assert.IsTrue(view.HasActivePointerInteraction);
            SendPointerUpAt(slider, slider.worldBound.center);
            Assert.IsFalse(element.HasError);
        }

        [Test]
        public void TextEdit_CustomCommitExceptionEndsEditorAndAllowsRetry()
        {
            var element = new ThrowingTextCommitElement();
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);
            Assert.IsTrue(view.BeginTextEdit());
            view.Q<TextField>("debug-menu-editor").value = "after";

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            var committed = true;
            Assert.DoesNotThrow(() => committed = view.CommitTextEdit());

            Assert.IsFalse(committed);
            Assert.IsTrue(view.IsEditingText);
            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual("ERROR: 値設定", element.ReadErrorText);
            AssertColor(new DebugMenuTheme().Warning, view.Q<TextField>("debug-menu-editor").style.color.value);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<Label>("debug-menu-error").style.display.value);
            StringAssert.Contains("custom text setter failed", view.tooltip);
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.IsTrue(view.BeginTextEdit(), "値設定エラー後に入力欄を再試行できない");
        }

        [Test]
        public void Checkbox_SetterFailureKeepsControlForRecovery()
        {
            var throwOnWrite = true;
            var value = false;
            var element = new DebugBool("God Mode", () => value, next =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("checkbox setter failed");
                value = next;
            });
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(element.TrySetBool(true));
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("debug-menu-checkbox").style.display.value);
            Assert.IsNull(view.Q<Label>("debug-menu-value"), "Bool行へ通常の文字値欄を追加している");
            Assert.AreEqual(DisplayStyle.Flex, view.Q<Label>("debug-menu-error").style.display.value);
            AssertColor(new DebugMenuTheme().Warning, view.Q<Label>("debug-menu-label").style.color.value);
            StringAssert.Contains("checkbox setter failed", view.tooltip);

            throwOnWrite = false;
            Assert.IsTrue(element.TrySetBool(true));
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.IsFalse(element.HasReadError);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("debug-menu-checkmark").style.display.value);
        }

        [Test]
        public void Color_SetterFailureKeepsSwatchAndPickerForRecovery()
        {
            var throwOnWrite = true;
            var value = Color.red;
            var element = new DebugColor("Tint", () => value, next =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("color setter failed");
                value = next;
            })
            {
                IsExpanded = true,
            };
            var view = new DebugRowView(new DebugMenuTheme());
            view.Bind(new DebugRow(element, 0), false, 0);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(element.CommitEditText("#00FF00"));
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<VisualElement>("debug-menu-color-swatch").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<DebugColorPickerView>().style.display.value);

            throwOnWrite = false;
            Assert.IsTrue(element.CommitEditText("#00FF00"));
            view.Bind(new DebugRow(element, 0), false, 0);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void PathAndVector_SetterFailureKeepsTextEntryForRecovery()
        {
            var throwOnWrite = true;
            var pathValue = "before.txt";
            var vectorValue = new Vector4(1f, 2f, 0f, 0f);
            var path = new DebugPath("Path", DebugPathMode.File, () => pathValue, next =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("path setter failed");
                pathValue = next;
            });
            var vector = new DebugVector("Vector", 2, () => vectorValue, next =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("vector setter failed");
                vectorValue = next;
            });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(path.CommitEditText("after.txt"));
            Assert.IsFalse(vector.CommitEditText("3, 4"));

            var pathView = new DebugRowView(new DebugMenuTheme());
            pathView.Bind(new DebugRow(path, 0), false, 0);
            Assert.IsTrue(pathView.BeginTextEdit(), "パスの設定失敗後に入力欄を再試行できない");
            var vectorView = new DebugRowView(new DebugMenuTheme());
            vectorView.Bind(new DebugRow(vector, 0), false, 0);
            Assert.IsTrue(vectorView.BeginTextEdit(), "ベクトルの設定失敗後に入力欄を再試行できない");

            throwOnWrite = false;
            Assert.IsTrue(path.CommitEditText("after.txt"));
            Assert.IsTrue(vector.CommitEditText("3, 4"));
            Assert.IsFalse(path.HasReadError);
            Assert.IsFalse(vector.HasReadError);
        }

        [Test]
        public void RowView_ShowsModifiedMarkInsteadOfRecoloringValue()
        {
            var theme = new DebugMenuTheme();
            var element = new DebugInt("Count", 1) { Value = 2 };
            var row = new DebugRow(element, 0);
            var view = new DebugRowView(theme);

            view.Bind(row, false, 0);

            var modifiedMark = view.Q<VisualElement>("debug-menu-modified-mark");
            var value = view.Q<Label>("debug-menu-value");
            Assert.AreEqual(DisplayStyle.Flex, modifiedMark.style.display.value);
            AssertColor(theme.Modified, modifiedMark.style.backgroundColor.value);
            AssertColor(theme.Value, value.style.color.value);
        }

        [Test]
        public void BoolRow_UsesCompactCheckboxInsteadOfTextValue()
        {
            var theme = new DebugMenuTheme();
            var row = new DebugRow(new DebugBool("God Mode", true), 0);
            var view = new DebugRowView(theme);

            view.Bind(row, false, 0);

            var checkbox = view.Q<VisualElement>("debug-menu-checkbox");
            var value = view.Q<Label>("debug-menu-value");
            Assert.NotNull(checkbox);
            AssertLength(theme.RowHeight * 0.55f, checkbox.style.width);
            AssertLength(theme.RowHeight * 0.55f, checkbox.style.height);
            Assert.AreEqual(DisplayStyle.Flex, checkbox.style.display.value);
            Assert.IsNull(value, "Bool 行はテキスト値欄をレイアウトへ追加しない");
            Assert.AreEqual("✓", checkbox.Q<Label>().text);
        }

        [Test]
        public void ExpandedWidgets_UseThemeWidthRatios()
        {
            var theme = new DebugMenuTheme();
            var graph = new DebugGraphView(theme);
            var picker = new DebugColorPickerView(theme);
            var pickerPadding = theme.RowHeight * 0.3f;

            AssertLength(theme.RowHeight * theme.GraphWidthRatio, graph.style.width);
            AssertLength(theme.RowHeight * theme.ValueColumnRatio, graph.style.marginLeft);
            Assert.AreEqual(DisplayStyle.None, graph.style.display.value);

            AssertLength(theme.ColorPickerHeight * 1.1f + pickerPadding * 2f, picker.style.width);
            AssertLength(theme.ColorPickerHeight + pickerPadding * 2f, picker.style.height);
            AssertLength(theme.RowHeight * 2f, picker.style.marginLeft);
            AssertLength(pickerPadding, picker.style.paddingLeft);
            AssertColor(theme.ColorPickerBackground, picker.style.backgroundColor.value);
            AssertColor(theme.ColorPickerPanelBorder, picker.style.borderTopColor.value);
            Assert.AreEqual(DisplayStyle.None, picker.style.display.value);
        }

        [Test]
        public void NarrowRows_UseShrinkableTextAndAdjustableControlLayout()
        {
            var theme = new DebugMenuTheme();
            var textView = new DebugRowView(theme);
            var numberView = new DebugRowView(theme);
            textView.Bind(
                new DebugRow(new DebugText("とても長い表示名", "Zsadwa1312312312312312312312"), 0),
                false,
                0);
            numberView.Bind(
                new DebugRow(new DebugFloat("とても長い数値項目", 8.74f).WithRange(0f, 10f), 0),
                false,
                1);

            var textHeader = textView.Q<VisualElement>("debug-menu-row-header");
            var textLabel = textView.Q<Label>("debug-menu-label");
            var textControls = textView.Q<VisualElement>("debug-menu-value-controls");
            var textValue = textView.Q<Label>("debug-menu-value");
            var numberValue = numberView.Q<Label>("debug-menu-value");
            var slider = numberView.Q<VisualElement>("debug-menu-slider");

            Assert.AreEqual(Overflow.Hidden, textView.style.overflow.value);
            Assert.AreEqual(Overflow.Hidden, textHeader.style.overflow.value);
            Assert.That(textLabel.style.flexShrink.value, Is.EqualTo(1f).Within(Tolerance));
            AssertLength(0f, textLabel.style.minWidth);
            Assert.AreEqual(Overflow.Hidden, textLabel.style.overflow.value);
            Assert.AreEqual(TextOverflow.Ellipsis, textLabel.style.textOverflow.value);
            Assert.AreEqual(WhiteSpace.NoWrap, textLabel.style.whiteSpace.value);

            Assert.That(textControls.style.flexGrow.value, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(textControls.style.flexShrink.value, Is.EqualTo(1f).Within(Tolerance));
            AssertLength(0f, textControls.style.minWidth);
            Assert.AreEqual(Overflow.Hidden, textControls.style.overflow.value);

            Assert.That(textValue.style.flexGrow.value, Is.EqualTo(0f).Within(Tolerance),
                "入力欄を空き幅いっぱいへ引き伸ばしてはいけない");
            Assert.That(textValue.style.flexShrink.value, Is.EqualTo(1f).Within(Tolerance));
            AssertLength(0f, textValue.style.minWidth);
            Assert.AreEqual(Overflow.Hidden, textValue.style.overflow.value);
            Assert.AreEqual(TextOverflow.Ellipsis, textValue.style.textOverflow.value);
            Assert.AreEqual("Zsadwa1312312312312312312312", textValue.text, "モデルの文字列を切り詰めてはいけない");

            Assert.That(numberValue.style.flexGrow.value, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(numberValue.style.flexShrink.value, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(slider.style.flexShrink.value, Is.EqualTo(1f).Within(Tolerance));
            AssertLength(0f, slider.style.minWidth);
        }

        [Test]
        public void InputField_KeepsPreferredWidthAndRightGutterThenShrinksOnlyWhenNeeded()
        {
            const float wideRowWidth = 600f;
            const float narrowRowWidth = 234f;
            var theme = new DebugMenuTheme();
            var element = new DebugText("表示名", "Zsadwa1312312312312312312312");
            var view = new DebugRowView(theme);
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);

            SendGeometryChanged(view, wideRowWidth);
            var value = view.Q<Label>("debug-menu-value");
            var editor = view.Q<TextField>("debug-menu-editor");
            var controls = view.Q<VisualElement>("debug-menu-value-controls");
            AssertLength(theme.EffectiveRowHeight * theme.EditFieldWidthRatio, value.style.width);
            AssertLength(theme.EffectiveRowHeight * theme.EditFieldWidthRatio, editor.style.width);
            AssertLength(theme.EffectiveRowHeight * theme.RowEndPaddingRatio, controls.style.marginRight);
            Assert.That(value.style.flexGrow.value, Is.EqualTo(0f).Within(Tolerance));

            SendGeometryChanged(view, narrowRowWidth);
            var expectedNarrowWidth = theme.EffectiveRowHeight * theme.EditFieldMinimumWidthRatio;
            AssertLength(expectedNarrowWidth, value.style.width);
            AssertLength(expectedNarrowWidth, editor.style.width);
            Assert.AreEqual("Zsadwa1312312312312312312312", value.text, "表示だけを縮め、値は失わない");

            Assert.IsTrue(view.BeginTextEdit());
            AssertLength(expectedNarrowWidth, editor.style.width);
        }

        [Test]
        public void NarrowNumericRow_PreservesButtonsValueAndMinimumUsableSlider()
        {
            const float narrowRowWidth = 234f;
            var theme = new DebugMenuTheme();
            var view = new DebugRowView(theme);
            view.Bind(new DebugRow(new DebugFloat("移動速度", 8.74f).WithRange(0f, 10f), 0), false, 0);
            AttachToPanel(view);

            SendGeometryChanged(view, narrowRowWidth);

            AssertLength(
                theme.EffectiveRowHeight * theme.NumericFieldMinimumWidthRatio,
                view.Q<Label>("debug-menu-value").style.width);
            AssertLength(
                theme.EffectiveRowHeight * theme.SliderMinimumWidthRatio,
                view.Q<VisualElement>("debug-menu-slider").style.width);
        }

        [Test]
        public void ActiveInputField_UsesReadableTextSelectionAndCursorColors()
        {
            var theme = new DebugMenuTheme();
            var view = new DebugRowView(theme);
            view.Bind(new DebugRow(new DebugText("Name", "Player"), 0), false, 0);
            AttachToPanel(view);
            Assert.IsTrue(view.BeginTextEdit());
            var editor = view.Q<TextField>("debug-menu-editor");
            var input = view.Q<VisualElement>("debug-menu-editor-input");
            var text = view.Q<VisualElement>("debug-menu-editor-text");

            Assert.NotNull(editor);
            Assert.NotNull(input);
            Assert.NotNull(text);
            AssertColor(theme.InputFieldText, editor.style.color.value);
            AssertColor(theme.InputFieldText, input.style.color.value);
            AssertColor(theme.InputFieldText, text.style.color.value);
            AssertColor(theme.InputFieldText, input.resolvedStyle.color);
            AssertColor(theme.InputFieldText, text.resolvedStyle.color);
            Assert.AreEqual("Player", editor.value);
#pragma warning disable CS0618
            AssertColor(theme.InputFieldSelection, editor.textSelection.selectionColor);
            AssertColor(theme.InputFieldCursor, editor.textSelection.cursorColor);
#pragma warning restore CS0618
            Assert.AreNotEqual(theme.InputFieldSelection, theme.InputFieldText);
        }

        [Test]
        public void TextEdit_GetterFailureEndsEditingAndShowsError()
        {
            var shouldThrow = false;
            var value = "Player";
            var ended = 0;
            var element = new DebugText(
                "Name",
                () => shouldThrow ? throw new System.InvalidOperationException("name failed") : value,
                next => value = next);
            var view = new DebugRowView(new DebugMenuTheme(), null, null, null, null, _ => ended++);
            view.Bind(new DebugRow(element, 0), false, 0);
            AttachToPanel(view);
            Assert.IsTrue(view.BeginTextEdit());
            view.Q<TextField>("debug-menu-editor").value = "Changed";
            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(view.CommitTextEdit());

            Assert.IsFalse(view.IsEditingText);
            Assert.AreEqual(1, ended);
            Assert.IsTrue(element.HasReadError);
            Assert.That(view.Q<Label>("debug-menu-value").text, Does.StartWith("ERROR:"));
            AssertErrorControlsDisabled(view);
        }

        [Test]
        public void TextEdit_RebindGetterFailureNotifiesEditEndedOnce()
        {
            var shouldThrow = false;
            var element = new DebugText(
                "Name",
                () => shouldThrow ? throw new System.InvalidOperationException("name failed") : "Player",
                _ => { });
            var ended = 0;
            var view = new DebugRowView(new DebugMenuTheme(), null, null, null, null, _ => ended++);
            var row = new DebugRow(element, 0);
            view.Bind(row, false, 0);
            AttachToPanel(view);
            Assert.IsTrue(view.BeginTextEdit());
            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => view.Bind(row, false, 0));

            Assert.IsFalse(view.IsEditingText);
            Assert.AreEqual(1, ended);
            Assert.IsTrue(element.HasReadError);
            AssertErrorControlsDisabled(view);
        }

        [Test]
        public void GraphRow_UsesGraphLineColorUntilRowStateOverridesIt()
        {
            var theme = new DebugMenuTheme
            {
                GraphLine = Color.magenta,
                Value = Color.cyan,
            };
            var graph = new DebugGraph("Frame", () => 1f);
            var row = new DebugRow(graph, 0);
            var view = new DebugRowView(theme);

            view.Bind(row, false, 0);
            var graphView = view.Q<DebugGraphView>();
            AssertColor(theme.GraphLine, ReadPrivateColor(graphView, "_lineColor"));

            view.Bind(row, true, 0);
            AssertColor(theme.SelectedText, ReadPrivateColor(graphView, "_lineColor"));
        }

        [Test]
        public void ThemeMigration_RestoresSizingDefaultsForOldSerializedData()
        {
            var theme = new DebugMenuTheme
            {
                GuiScale = 0f,
                EditFieldMinimumWidthRatio = 0f,
                SliderMinimumWidthRatio = 0f,
                InputFieldText = default,
            };
            typeof(DebugMenuTheme)
                .GetField("_sizeLayoutVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(theme, 0);

            theme.OnAfterDeserialize();

            Assert.That(theme.GuiScale, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(theme.EditFieldMinimumWidthRatio, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(theme.SliderMinimumWidthRatio, Is.EqualTo(1f).Within(Tolerance));
            AssertColor(Color.white, theme.InputFieldText);
        }

        [Test]
        public void ThemeMigration_RestoresToastAndHoverDefaultsForVersionOneData()
        {
            var theme = new DebugMenuTheme
            {
                ToastBackground = default,
                ToastSuccess = default,
                ToastMaxWidthRatio = 0f,
                HoverTooltipOffsetRatio = 0f,
                HoverTooltipMaxWidthRatio = 0f,
            };
            typeof(DebugMenuTheme)
                .GetField("_sizeLayoutVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(theme, 1);

            theme.OnAfterDeserialize();

            AssertColor(new Color(0.04f, 0.05f, 0.07f, 0.96f), theme.ToastBackground);
            AssertColor(new Color(0.45f, 0.85f, 0.60f, 1f), theme.ToastSuccess);
            Assert.That(theme.ToastMaxWidthRatio, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(theme.HoverTooltipOffsetRatio, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(theme.HoverTooltipMaxWidthRatio, Is.EqualTo(0.45f).Within(Tolerance));
        }

        [Test]
        public void ApplyTheme_WaitsForActiveTextEditInsteadOfDiscardingInput()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay");
            var menuView = new DebugMenuView(menu);
            var editingRow = new DebugRowView(new DebugMenuTheme());
            editingRow.Bind(new DebugRow(new DebugText("Name", "Player"), 0), false, 0);
            Assert.IsTrue(editingRow.BeginTextEdit());
            WritePrivateField(menuView, "_editingRow", editingRow);

            var controllerObject = new GameObject("DebugMenuThemeApplyTest");
            try
            {
                var controller = controllerObject.AddComponent<DebugMenuController>();
                WritePrivateField(controller, "_view", menuView);

                controller.ApplyTheme();

                Assert.IsTrue(editingRow.IsEditingText);
                Assert.AreSame(menuView, ReadPrivateField(controller, "_view"));
                Assert.AreEqual(true, ReadPrivateField(controller, "_themeRefreshPending"));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void NarrowRows_ClampExpandedGraphAndColorPickerToAvailableWidth()
        {
            const float rowWidth = 150f;
            var theme = new DebugMenuTheme();
            var color = new DebugColor("Tint", Color.cyan);
            color.OnDecide();
            var graph = new DebugGraph("Frame", () => 1f);
            var colorView = new DebugRowView(theme);
            var graphView = new DebugRowView(theme);
            colorView.Bind(new DebugRow(color, 0), false, 0);
            graphView.Bind(new DebugRow(graph, 0), false, 1);

            var host = new VisualElement
            {
                style =
                {
                    width = rowWidth,
                    height = 320f,
                    flexDirection = FlexDirection.Column,
                },
            };
            host.Add(colorView);
            host.Add(graphView);
            AttachToPanel(host);
            SendGeometryChanged(colorView, rowWidth);
            SendGeometryChanged(graphView, rowWidth);

            var picker = colorView.Q<DebugColorPickerView>();
            var graphElement = graphView.Q<DebugGraphView>();
            AssertLength(theme.RowHeight * 2f, picker.style.marginLeft);
            AssertLength(rowWidth - theme.RowHeight * 2f, picker.style.width);
            AssertLength(theme.RowHeight * 2f, graphElement.style.marginLeft);
            AssertLength(rowWidth - theme.RowHeight * 2f, graphElement.style.width);

            SendGeometryChanged(colorView, 600f);
            SendGeometryChanged(graphView, 600f);
            var pickerPadding = theme.RowHeight * 0.3f;
            AssertLength(theme.RowHeight * 2f, picker.style.marginLeft);
            AssertLength(theme.ColorPickerHeight * 1.1f + pickerPadding * 2f, picker.style.width);
            AssertLength(theme.RowHeight * theme.ValueColumnRatio, graphElement.style.marginLeft);
            AssertLength(theme.RowHeight * theme.GraphWidthRatio, graphElement.style.width);
        }

        [Test]
        public void Checkbox_LeftClickSelectsAndTogglesImmediately()
        {
            var selectedIndex = -1;
            var decidedIndex = -1;
            var value = new DebugBool("God Mode", false);
            var view = new DebugRowView(
                new DebugMenuTheme(),
                index => selectedIndex = index,
                null,
                null,
                index =>
                {
                    decidedIndex = index;
                    value.OnDecide();
                },
                null,
                null);
            view.Bind(new DebugRow(value, 0), false, 3);
            AttachToPanel(view);

            SendPointerDown(view.Q<VisualElement>("debug-menu-checkbox"), 0);

            Assert.AreEqual(3, selectedIndex);
            Assert.AreEqual(3, decidedIndex);
            Assert.IsTrue(value.Value);
        }

        [Test]
        public void LegacyValueClickConstructor_CheckboxAndSwatchKeepValueClickRoute()
        {
            var selectedIndex = -1;
            var valueClickIndex = -1;
            var view = new DebugRowView(
                new DebugMenuTheme(),
                index => selectedIndex = index,
                null,
                index => valueClickIndex = index,
                null,
                null);

            view.Bind(new DebugRow(new DebugBool("God Mode", false), 0), false, 4);
            AttachToPanel(view);
            SendPointerDown(view.Q<VisualElement>("debug-menu-checkbox"), 0);

            Assert.AreEqual(4, selectedIndex);
            Assert.AreEqual(4, valueClickIndex);

            selectedIndex = -1;
            valueClickIndex = -1;
            view.Bind(new DebugRow(new DebugColor("Tint", Color.red), 0), false, 7);
            SendPointerDown(view.Q<VisualElement>("debug-menu-color-swatch"), 0);

            Assert.AreEqual(7, selectedIndex);
            Assert.AreEqual(7, valueClickIndex);
        }

        [Test]
        public void ColorSwatch_LeftClickSelectsAndTogglesPicker_ValueKeepsEditClick()
        {
            var selectedIndex = -1;
            var valueClickCount = 0;
            var color = new DebugColor("Tint", Color.red);
            var view = new DebugRowView(
                new DebugMenuTheme(),
                index => selectedIndex = index,
                null,
                index => valueClickCount++,
                index => color.OnDecide(),
                null,
                null);
            view.Bind(new DebugRow(color, 0), false, 2);
            AttachToPanel(view);
            var swatch = view.Q<VisualElement>("debug-menu-color-swatch");
            var value = view.Q<Label>("debug-menu-value");

            SendPointerDown(swatch, 0);
            view.Bind(new DebugRow(color, 0), true, 2);

            Assert.AreEqual(2, selectedIndex);
            Assert.IsTrue(color.IsExpanded);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<DebugColorPickerView>().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, value.style.display.value);
            Assert.AreEqual("#FF0000FF", value.text);

            SendPointerDown(value, 0);
            SendPointerDown(value, 0);
            Assert.AreEqual(2, valueClickCount, "16 進値欄のダブルクリック導線を外してはいけない");

            SendPointerDown(swatch, 0);
            Assert.IsFalse(color.IsExpanded);
        }

        [UnityTest]
        public System.Collections.IEnumerator ColorPicker_RepaintKeepsGetterFailureUntilReadRecovers()
        {
            var shouldThrow = false;
            var color = Color.red;
            var element = new DebugColor(
                "Tint",
                () => shouldThrow ? throw new System.InvalidOperationException("color failed") : color,
                next => color = next)
            {
                IsExpanded = true,
            };
            var picker = new DebugColorPickerView(new DebugMenuTheme());
            AttachToPanel(picker);
            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            picker.Bind(element);
            picker.MarkDirtyRepaint();
            yield return null;

            Assert.IsTrue(element.HasReadError, "描画失敗直後に値取得エラーが消えている");

            shouldThrow = false;
            picker.MarkDirtyRepaint();
            yield return null;

            Assert.IsFalse(element.HasReadError, "取得元の回復後も色選択面のエラーが残っている");
        }

        [UnityTest]
        public System.Collections.IEnumerator ColorPicker_PointerDownSetterFailureDoesNotCapturePointer()
        {
            var colorValue = Color.red;
            var element = new DebugColor(
                "Tint",
                () => colorValue,
                _ => throw new System.InvalidOperationException("pointer down setter failed"))
            {
                IsExpanded = true,
            };
            var picker = CreateAttachedColorPicker(element);
            yield return null;
            AssertPickerHasLayout(picker);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => SendPointerDownAt(picker, picker.worldBound.center));

            Assert.IsFalse(picker.HasActivePointerInteraction);
            Assert.IsFalse(picker.HasPointerCapture(PointerId.mousePointerId));
            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual(Color.red, colorValue);
        }

        [UnityTest]
        public System.Collections.IEnumerator ColorPicker_PointerMoveSetterFailureReleasesCapture()
        {
            var throwOnWrite = false;
            var colorValue = Color.red;
            var element = new DebugColor("Tint", () => colorValue, value =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("pointer move setter failed");
                colorValue = value;
            })
            {
                IsExpanded = true,
            };
            var picker = CreateAttachedColorPicker(element);
            yield return null;
            AssertPickerHasLayout(picker);
            var start = picker.worldBound.center;

            SendPointerDownAt(picker, start);
            Assert.IsTrue(picker.HasActivePointerInteraction);
            Assert.IsTrue(picker.HasPointerCapture(PointerId.mousePointerId));

            throwOnWrite = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => SendPointerMoveAt(picker, start + Vector2.right * 12f));

            Assert.IsFalse(picker.HasActivePointerInteraction);
            Assert.IsFalse(picker.HasPointerCapture(PointerId.mousePointerId));
            Assert.IsTrue(element.HasReadError);
        }

        [UnityTest]
        public System.Collections.IEnumerator ColorPicker_PointerUpSetterFailureReleasesCapture()
        {
            var throwOnWrite = false;
            var colorValue = Color.red;
            var element = new DebugColor("Tint", () => colorValue, value =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("pointer up setter failed");
                colorValue = value;
            })
            {
                IsExpanded = true,
            };
            var picker = CreateAttachedColorPicker(element);
            yield return null;
            AssertPickerHasLayout(picker);
            var start = picker.worldBound.center;

            SendPointerDownAt(picker, start);
            Assert.IsTrue(picker.HasActivePointerInteraction);
            Assert.IsTrue(picker.HasPointerCapture(PointerId.mousePointerId));

            throwOnWrite = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => SendPointerUpAt(picker, start + Vector2.left * 12f));

            Assert.IsFalse(picker.HasActivePointerInteraction);
            Assert.IsFalse(picker.HasPointerCapture(PointerId.mousePointerId));
            Assert.IsTrue(element.HasReadError);
        }

        [Test]
        public void Root_RightClickReturnsThenClosesAtTopLevel()
        {
            var menu = new DebugMenuRoot();
            var rootPage = menu.AddPage("Game");
            var childPage = new DebugPage("Advanced");
            rootPage.AddChildPage(childPage);
            menu.PushPage(childPage);
            menu.SetVisible(true);
            var view = new DebugMenuView(menu);
            view.Refresh();
            AttachToPanel(view.Root);
            var descendant = view.Root.Q<Label>("debug-menu-page-header");

            SendPointerDown(descendant, 1);

            Assert.AreSame(rootPage, menu.CurrentPage);
            Assert.IsTrue(menu.IsVisible);

            SendPointerDown(descendant, 1);

            Assert.IsFalse(menu.IsVisible);
        }

        [Test]
        public void Root_DoesNotConsumeListWheelEvent()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Game");
            var view = new DebugMenuView(menu);
            AttachToPanel(view.Root);
            var list = view.Root.Q<ListView>("debug-menu-list");
            var wheelReachedList = false;
            list.RegisterCallback<WheelEvent>(evt => wheelReachedList = true, TrickleDown.TrickleDown);
            var content = list.Q<ScrollView>().contentContainer;
            var systemEvent = new Event { type = EventType.ScrollWheel, delta = Vector2.down };

            using (var evt = WheelEvent.GetPooled(systemEvent))
            {
                evt.target = content;
                content.SendEvent(evt);
            }

            Assert.IsTrue(wheelReachedList, "WheelEvent は ListView の既定スクロールへ届く必要がある");
        }

        [Test]
        public void RowHover_ForwardsEnterMoveAndLeaveWithoutSelecting()
        {
            var selected = -1;
            var phases = new System.Collections.Generic.List<bool>();
            var view = new DebugRowView(
                new DebugMenuTheme(),
                index => selected = index,
                null,
                null,
                null,
                null,
                null,
                (index, hovered, position) => phases.Add(hovered));
            view.Bind(new DebugRow(new DebugText("Name", "Player") { Description = "Current player name" }, 0), false, 2);
            AttachToPanel(view);

            SendPointerEnter(view);
            SendPointerMove(view);
            SendPointerLeave(view);

            CollectionAssert.AreEqual(new[] { true, true, false }, phases);
            Assert.AreEqual(-1, selected);
        }

        private void AttachToPanel(VisualElement root)
        {
            if (_document == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelObject = new GameObject("DebugMenuVisualTestPanel");
                _document = _panelObject.AddComponent<UIDocument>();
                _document.panelSettings = _panelSettings;
            }

            _document.rootVisualElement.Clear();
            _document.rootVisualElement.Add(root);
            Assert.NotNull(root.panel, "イベント検証は実パネルへ接続してから行う");
        }

        private DebugColorPickerView CreateAttachedColorPicker(DebugColor element)
        {
            var host = new VisualElement();
            host.style.width = 480f;
            host.style.height = 320f;
            var picker = new DebugColorPickerView(new DebugMenuTheme());
            host.Add(picker);
            AttachToPanel(host);
            picker.Bind(element);
            return picker;
        }

        private static void AssertPickerHasLayout(DebugColorPickerView picker)
        {
            Assert.Greater(picker.contentRect.width, 2f, "色選択面の横幅が確定していない");
            Assert.Greater(picker.contentRect.height, 2f, "色選択面の高さが確定していない");
        }

        private static void AssertLength(float expected, StyleLength actual)
        {
            Assert.AreEqual(LengthUnit.Pixel, actual.value.unit);
            Assert.That(actual.value.value, Is.EqualTo(expected).Within(Tolerance));
        }

        private static Color ReadPrivateColor(object target, string fieldName)
        {
            return (Color)ReadPrivateField(target, fieldName);
        }

        private static object ReadPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"{fieldName} が見つからない");
            return field.GetValue(target);
        }

        private static void WritePrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"{fieldName} が見つからない");
            field.SetValue(target, value);
        }

        private static void AssertLateReadFailure(DebugElement element)
        {
            var view = new DebugRowView(new DebugMenuTheme());

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\]"));
            Assert.DoesNotThrow(() => view.Bind(new DebugRow(element, 0), false, 0));

            Assert.IsTrue(element.HasReadError);
            Assert.That(view.Q<Label>("debug-menu-value").text, Does.StartWith("ERROR:"));
            AssertErrorControlsDisabled(view);
        }

        private static void AssertErrorControlsDisabled(DebugRowView view)
        {
            var value = (VisualElement)ReadPrivateField(view, "_value");
            var editor = (VisualElement)ReadPrivateField(view, "_editor");
            var decrease = (VisualElement)ReadPrivateField(view, "_decrease");
            var increase = (VisualElement)ReadPrivateField(view, "_increase");
            var checkbox = (VisualElement)ReadPrivateField(view, "_checkbox");
            var swatch = (VisualElement)ReadPrivateField(view, "_swatch");
            var slider = (VisualElement)ReadPrivateField(view, "_sliderTrack");
            var picker = (VisualElement)ReadPrivateField(view, "_colorPicker");

            Assert.AreEqual(DisplayStyle.None, editor.style.display.value);
            Assert.AreEqual(DisplayStyle.None, decrease.style.display.value);
            Assert.AreEqual(DisplayStyle.None, increase.style.display.value);
            Assert.AreEqual(DisplayStyle.None, checkbox.style.display.value);
            Assert.AreEqual(DisplayStyle.None, swatch.style.display.value);
            Assert.AreEqual(DisplayStyle.None, slider.style.display.value);
            Assert.AreEqual(DisplayStyle.None, picker.style.display.value);
            Assert.AreEqual(PickingMode.Ignore, value.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, editor.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, decrease.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, increase.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, checkbox.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, swatch.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, slider.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, picker.pickingMode);
        }

        private static void SendPointerDown(VisualElement target, int button)
        {
            Assert.NotNull(target);
            Assert.NotNull(target.panel, "PointerDownEvent は実パネルへ接続した要素へ送る");
            var systemEvent = new Event { type = EventType.MouseDown, button = button };
            using (var evt = PointerDownEvent.GetPooled(systemEvent))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SendPointerDownAt(VisualElement target, Vector2 position)
        {
            var systemEvent = new Event { type = EventType.MouseDown, button = 0, mousePosition = position };
            using (var evt = PointerDownEvent.GetPooled(systemEvent))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SendPointerMoveAt(VisualElement target, Vector2 position)
        {
            var systemEvent = new Event { type = EventType.MouseDrag, button = 0, mousePosition = position };
            using (var evt = PointerMoveEvent.GetPooled(systemEvent))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SendPointerUpAt(VisualElement target, Vector2 position)
        {
            var systemEvent = new Event { type = EventType.MouseUp, button = 0, mousePosition = position };
            using (var evt = PointerUpEvent.GetPooled(systemEvent))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SendPointerEnter(VisualElement target)
        {
            using var evt = PointerEnterEvent.GetPooled(new Event { type = EventType.MouseMove });
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SendPointerMove(VisualElement target)
        {
            using var evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove });
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SendPointerLeave(VisualElement target)
        {
            using var evt = PointerLeaveEvent.GetPooled(new Event { type = EventType.MouseMove });
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SendGeometryChanged(VisualElement target, float width)
        {
            Assert.NotNull(target);
            Assert.NotNull(target.panel, "GeometryChangedEvent は実パネルへ接続した要素へ送る");
            using (var evt = GeometryChangedEvent.GetPooled(Rect.zero, new Rect(0f, 0f, width, 20f)))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance));
        }

    }
}
