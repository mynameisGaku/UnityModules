using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu
{
    /// <summary>
    /// 1 行分の見た目。左に表示名、右に値、必要なら下にスライダーを描く。
    /// <para>
    /// 生成と束縛を分けてあるのは、仮想化リストが行の実体を使い回すため。
    /// <see cref="Bind"/> は行が入れ替わるたびに呼ばれるので、ここで確保してはいけない。
    /// </para>
    /// </summary>
    public sealed class DebugRowView : VisualElement
    {
        private readonly DebugMenuTheme _theme;
        private readonly Action<int> _selectRow;
        private readonly Action<int> _clickRow;
        private readonly Action<int> _clickValue;
        private readonly Action<int> _decideValue;
        private readonly Action<int, int> _adjustRow;
        private readonly Action<DebugRowView> _editEnded;
        private readonly Action<int, bool, Vector2> _hoverRow;

        private readonly VisualElement _modifiedMark;
        private readonly VisualElement _header;
        private readonly Label _favorite;
        private readonly VisualElement _indent;
        private readonly Label _marker;
        private readonly Label _label;
        private readonly VisualElement _valueControls;
        private readonly Label _value;
        private readonly TextField _editor;
        private VisualElement _editorInput;
        private VisualElement _editorText;
        private readonly Label _decrease;
        private readonly Label _increase;
        private readonly VisualElement _checkbox;
        private readonly Label _checkmark;
        private readonly VisualElement _sliderTrack;
        private readonly VisualElement _sliderRail;
        private readonly VisualElement _sliderFill;
        private readonly VisualElement _swatch;
        private readonly DebugGraphView _graph;
        private readonly DebugColorPickerView _colorPicker;

        private int _rowIndex = -1;
        private int _sliderPointerId = -1;
        private DebugElement _sliderElement;
        private DebugElement _editingElement;
        private bool _endingEdit;
        private bool _selected;
        private bool _hovered;
        private bool _favoriteHovered;
        private int _rowDepth;
        private ValueLayoutKind _valueLayout = ValueLayoutKind.None;
        private Color _editorTextColor;

        /// <summary>テーマを指定して行の見た目を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        public DebugRowView(DebugMenuTheme theme) : this(theme, null, null, null, null, null, null, null) { }

        /// <summary>テーマとマウス操作の受け取り先を指定して行を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        /// <param name="selectRow">クリックされた行へカーソルを合わせる処理。</param>
        /// <param name="clickRow">クリックを選択または決定として処理する受け取り先。</param>
        /// <param name="adjustRow">左右ボタンで値を変える処理。</param>
        public DebugRowView(DebugMenuTheme theme, Action<int> selectRow, Action<int> clickRow, Action<int, int> adjustRow)
            : this(theme, selectRow, clickRow, null, null, adjustRow, null, null) { }

        /// <summary>値欄のクリックと文字編集終了の受け取り先も指定して行を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        /// <param name="selectRow">クリックされた行へカーソルを合わせる処理。</param>
        /// <param name="clickRow">行のクリックを選択または決定として処理する受け取り先。</param>
        /// <param name="clickValue">値欄のクリックを直接入力として処理する受け取り先。</param>
        /// <param name="adjustRow">左右ボタンで値を変える処理。</param>
        /// <param name="editEnded">文字編集が終了したことを受け取る処理。</param>
        public DebugRowView(
            DebugMenuTheme theme,
            Action<int> selectRow,
            Action<int> clickRow,
            Action<int> clickValue,
            Action<int, int> adjustRow,
            Action<DebugRowView> editEnded)
            : this(theme, selectRow, clickRow, clickValue, null, adjustRow, editEnded, null) { }

        /// <summary>即時に決定する値の受け取り先も指定して行を作る。</summary>
        /// <param name="theme">色と寸法。</param>
        /// <param name="selectRow">クリックされた行へカーソルを合わせる処理。</param>
        /// <param name="clickRow">行のクリックを選択または決定として処理する受け取り先。</param>
        /// <param name="clickValue">値欄のクリックを直接入力として処理する受け取り先。</param>
        /// <param name="decideValue">チェックと色見本を 1 クリックで決定する受け取り先。</param>
        /// <param name="adjustRow">左右ボタンで値を変える処理。</param>
        /// <param name="editEnded">文字編集が終了したことを受け取る処理。</param>
        public DebugRowView(
            DebugMenuTheme theme,
            Action<int> selectRow,
            Action<int> clickRow,
            Action<int> clickValue,
            Action<int> decideValue,
            Action<int, int> adjustRow,
            Action<DebugRowView> editEnded)
            : this(theme, selectRow, clickRow, clickValue, decideValue, adjustRow, editEnded, null) { }

        /// <summary>行ホバーの開始、移動、終了も受け取る構成で行を作る。</summary>
        public DebugRowView(
            DebugMenuTheme theme,
            Action<int> selectRow,
            Action<int> clickRow,
            Action<int> clickValue,
            Action<int> decideValue,
            Action<int, int> adjustRow,
            Action<DebugRowView> editEnded,
            Action<int, bool, Vector2> hoverRow)
        {
            _theme = theme;
            _editorTextColor = theme.InputFieldText;
            _selectRow = selectRow;
            _clickRow = clickRow;
            _clickValue = clickValue;
            _decideValue = decideValue;
            _adjustRow = adjustRow;
            _editEnded = editEnded;
            _hoverRow = hoverRow;

            var rowHeight = theme.EffectiveRowHeight;
            var fontSize = theme.EffectiveFontSize;
            var controlGap = rowHeight * theme.ControlGapRatio;

            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.position = Position.Relative;
            style.overflow = Overflow.Hidden;

            _modifiedMark = new VisualElement
            {
                name = "debug-menu-modified-mark",
                style =
                {
                    position = Position.Absolute,
                    left = 0f,
                    top = 0f,
                    bottom = 0f,
                    width = rowHeight * theme.ModifiedMarkWidthRatio,
                    backgroundColor = theme.Modified,
                    display = DisplayStyle.None,
                },
            };

            _header = new VisualElement
            {
                name = "debug-menu-row-header",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = rowHeight,
                    overflow = Overflow.Hidden,
                },
            };

            _favorite = MakeLabel(theme, theme.Favorite);
            _favorite.name = "debug-menu-favorite";
            _favorite.text = "★";
            _favorite.style.width = rowHeight;
            _favorite.style.flexShrink = 0f;
            _favorite.style.unityTextAlign = TextAnchor.MiddleCenter;

            _indent = new VisualElement { style = { width = 0f, flexShrink = 0f } };

            _marker = MakeLabel(theme, theme.TextDim);
            _marker.name = "debug-menu-marker";
            _marker.style.width = rowHeight;
            _marker.style.flexShrink = 0f;
            _marker.style.unityTextAlign = TextAnchor.MiddleCenter;

            _label = MakeLabel(theme, theme.Text);
            _label.name = "debug-menu-label";
            _label.style.flexGrow = 0f;
            _label.style.flexShrink = 1f;
            _label.style.minWidth = 0f;
            _label.style.overflow = Overflow.Hidden;
            _label.style.textOverflow = TextOverflow.Ellipsis;
            _label.style.whiteSpace = WhiteSpace.NoWrap;

            _valueControls = new VisualElement
            {
                name = "debug-menu-value-controls",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = rowHeight,
                    flexGrow = 1f,
                    flexShrink = 1f,
                    minWidth = 0f,
                    marginRight = rowHeight * theme.RowEndPaddingRatio,
                    overflow = Overflow.Hidden,
                },
            };

            _swatch = new VisualElement
            {
                name = "debug-menu-color-swatch",
                style =
                {
                    width = rowHeight * theme.ColorSwatchWidthRatio,
                    height = rowHeight * theme.ColorSwatchHeightRatio,
                    marginRight = controlGap,
                    flexShrink = 0f,
                    display = DisplayStyle.None,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopColor = theme.TextDim,
                    borderBottomColor = theme.TextDim,
                    borderLeftColor = theme.TextDim,
                    borderRightColor = theme.TextDim,
                },
            };

            _decrease = MakeButtonLabel(theme, "◀");
            _increase = MakeButtonLabel(theme, "▶");

            _value = MakeLabel(theme, theme.Value);
            _value.name = "debug-menu-value";
            _value.style.unityTextAlign = TextAnchor.MiddleLeft;
            _value.style.flexShrink = 1f;
            _value.style.minWidth = 0f;
            _value.style.overflow = Overflow.Hidden;
            _value.style.textOverflow = TextOverflow.Ellipsis;
            _value.style.whiteSpace = WhiteSpace.NoWrap;

            _checkbox = new VisualElement
            {
                name = "debug-menu-checkbox",
                style =
                {
                    width = rowHeight * theme.CheckboxSizeRatio,
                    height = rowHeight * theme.CheckboxSizeRatio,
                    flexShrink = 0f,
                    display = DisplayStyle.None,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                },
            };

            _checkmark = MakeLabel(theme, theme.Value);
            _checkmark.text = "✓";
            _checkmark.style.position = Position.Absolute;
            _checkmark.style.left = 0f;
            _checkmark.style.right = 0f;
            _checkmark.style.top = 0f;
            _checkmark.style.bottom = 0f;
            _checkmark.style.unityTextAlign = TextAnchor.MiddleCenter;
            _checkmark.style.fontSize = Mathf.Max(10, fontSize - 3);
            _checkbox.Add(_checkmark);

            _editor = new TextField
            {
                name = "debug-menu-editor",
                isDelayed = false,
                multiline = false,
                style =
                {
                    display = DisplayStyle.None,
                    width = rowHeight * theme.EditFieldWidthRatio,
                    minWidth = 0f,
                    height = rowHeight,
                    flexShrink = 1f,
                    fontSize = fontSize,
                    color = theme.InputFieldText,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    backgroundColor = theme.InputFieldBackground,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopColor = theme.ActiveInputFieldBorder,
                    borderBottomColor = theme.ActiveInputFieldBorder,
                    borderLeftColor = theme.ActiveInputFieldBorder,
                    borderRightColor = theme.ActiveInputFieldBorder,
                },
            };

            ApplyEditorTheme();

            _header.Add(_modifiedMark);
            _header.Add(_favorite);
            _header.Add(_indent);
            _header.Add(_marker);
            _header.Add(_label);
            _header.Add(_valueControls);

            // スライダーは値の右へ細い溝として置く。
            _sliderTrack = new VisualElement
            {
                name = "debug-menu-slider",
                style =
                {
                    width = rowHeight * theme.SliderWidthRatio,
                    height = rowHeight * theme.SliderHeightRatio,
                    marginLeft = controlGap,
                    marginRight = 0f,
                    backgroundColor = Color.clear,
                    display = DisplayStyle.None,
                    flexShrink = 1f,
                    minWidth = 0f,
                    justifyContent = Justify.Center,
                },
            };

            _sliderRail = new VisualElement
            {
                name = "debug-menu-slider-rail",
                style =
                {
                    width = Length.Percent(100f),
                    height = rowHeight * theme.SliderRailHeightRatio,
                    backgroundColor = theme.SliderTrack,
                },
            };

            _sliderFill = new VisualElement
            {
                style = { height = Length.Percent(100f), width = Length.Percent(0f), backgroundColor = theme.SliderFill },
            };

            _sliderRail.Add(_sliderFill);
            _sliderTrack.Add(_sliderRail);
            EnsureValueLayout(ValueLayoutKind.Standard);

            _graph = new DebugGraphView(theme);
            _colorPicker = new DebugColorPickerView(theme);

            Add(_header);
            Add(_graph);
            Add(_colorPicker);

            _header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            _value.RegisterCallback<PointerDownEvent>(OnValuePointerDown);
            _swatch.RegisterCallback<PointerDownEvent>(OnImmediateValuePointerDown);
            _checkbox.RegisterCallback<PointerDownEvent>(OnImmediateValuePointerDown);
            _favorite.RegisterCallback<PointerDownEvent>(OnFavoritePointerDown);
            _favorite.RegisterCallback<PointerEnterEvent>(evt => SetFavoriteHover(true));
            _favorite.RegisterCallback<PointerLeaveEvent>(evt => SetFavoriteHover(false));
            _decrease.RegisterCallback<PointerDownEvent>(evt => OnAdjustPointerDown(evt, -1));
            _increase.RegisterCallback<PointerDownEvent>(evt => OnAdjustPointerDown(evt, 1));
            _editor.RegisterCallback<KeyDownEvent>(OnEditorKeyDown);
            _editor.RegisterCallback<FocusOutEvent>(OnEditorFocusOut);
            _editor.RegisterCallback<AttachToPanelEvent>(evt => ApplyEditorTheme());

            _sliderTrack.RegisterCallback<PointerDownEvent>(OnSliderPointerDown);
            _sliderTrack.RegisterCallback<PointerMoveEvent>(OnSliderPointerMove);
            _sliderTrack.RegisterCallback<PointerUpEvent>(OnSliderPointerUp);
            _sliderTrack.RegisterCallback<PointerCancelEvent>(OnSliderPointerCancel);
            _sliderTrack.RegisterCallback<PointerCaptureOutEvent>(OnSliderPointerCaptureOut);

            _graph.RegisterCallback<PointerDownEvent>(OnExpandedContentPointerDown);
            _colorPicker.RegisterCallback<PointerDownEvent>(OnColorPickerPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>この行が今どの要素を映しているか。</summary>
        public DebugElement Element { get; private set; }

        /// <summary>現在割り当てられている可視行の位置。仮想化リストの再描画に使う。</summary>
        internal int RowIndex => _rowIndex;

        /// <summary>この行の直接入力欄が開いているか。</summary>
        public bool IsEditingText => _editingElement != null;

        /// <summary>この行でスライダーまたは色選択面をドラッグしているか。</summary>
        public bool HasActivePointerInteraction =>
            _sliderPointerId >= 0 || _colorPicker.HasActivePointerInteraction;

        /// <summary>行の内容を映す。使い回されるので、ここでは確保しない。</summary>
        /// <param name="row">映す行。</param>
        /// <param name="selected">カーソルが乗っているか。</param>
        /// <param name="rowIndex">現在の可視行における位置。</param>
        public void Bind(in DebugRow row, bool selected, int rowIndex)
        {
            var element = row.Element;
            if (!ReferenceEquals(Element, element) || _rowIndex != rowIndex)
            {
                if (_hovered) _hoverRow?.Invoke(_rowIndex, false, Vector2.zero);
                _hovered = false;
                CancelSliderDrag();
                FinishTextEditBeforeRebind();
            }

            Element = element;
            _rowIndex = rowIndex;
            _selected = selected;
            _rowDepth = row.Depth;

            var rowHeight = _theme.EffectiveRowHeight;
            _indent.style.width = row.Depth * _theme.EffectiveIndentWidth;
            var leftSlots = rowHeight * 2f + row.Depth * _theme.EffectiveIndentWidth;
            _label.style.marginRight = rowHeight * _theme.ColumnGapRatio;
            _label.style.width = Mathf.Max(
                0f,
                rowHeight * _theme.ValueColumnRatio - leftSlots - rowHeight * _theme.ColumnGapRatio);

            _marker.text = element.ShouldShowMarker
                ? element.IsExpandable ? element.IsExpanded ? "▼" : "▶" : "―"
                : string.Empty;

            _label.text = element.DisplayLabel;

            var valueText = element.GetValueText();
            if (!string.IsNullOrEmpty(element.Unit) && !string.IsNullOrEmpty(valueText)) valueText += " " + element.Unit;

            // 候補を持つ行は「2/5」を添える。何個中どれかが分からないと選びにくい。
            if (element.TryGetSelection(out var index, out var count)) valueText += $"  {index + 1}/{count}";

            _value.text = valueText;

            var adjustable = element.IsAdjustable;
            var boolElement = element as DebugBool;
            var isBool = boolElement != null;
            var isColor = element is DebugColor;
            EnsureValueLayout(isColor
                ? ValueLayoutKind.Color
                : isBool ? ValueLayoutKind.Bool : element.CanTypeValue ? ValueLayoutKind.Field : ValueLayoutKind.Standard);
            _decrease.style.display = adjustable && !IsEditingText ? DisplayStyle.Flex : DisplayStyle.None;
            _increase.style.display = adjustable && !IsEditingText ? DisplayStyle.Flex : DisplayStyle.None;
            _checkbox.style.display = isBool && !IsEditingText ? DisplayStyle.Flex : DisplayStyle.None;
            _checkmark.style.display = isBool && boolElement.Value ? DisplayStyle.Flex : DisplayStyle.None;
            _value.style.display = !IsEditingText && !isBool ? DisplayStyle.Flex : DisplayStyle.None;
            _editor.style.display = IsEditingText ? DisplayStyle.Flex : DisplayStyle.None;

            BindIdleField(element, isBool);

            BindSwatch(element);
            BindSlider(element);
            _graph.Bind(element as DebugGraph, ResolveGraphLineColor(element));
            _colorPicker.Bind(element as DebugColor);
            ApplyVisualState();
            ApplyResponsiveLayout(resolvedStyle.width);
        }

        /// <summary>狭い表示領域では、値欄と展開内容を行の内側へ寄せて縮める。</summary>
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var rowWidth = evt.newRect.width;
            if (rowWidth <= 0f || float.IsNaN(rowWidth) || float.IsInfinity(rowWidth)) return;

            ApplyResponsiveLayout(rowWidth);
        }

        private void ApplyResponsiveLayout(float rowWidth)
        {
            if (rowWidth <= 0f || float.IsNaN(rowWidth) || float.IsInfinity(rowWidth)) return;

            ApplyResponsiveHeaderLayout(rowWidth);

            var rowHeight = _theme.EffectiveRowHeight;
            var minimumExpandedWidth = rowHeight * _theme.ExpandedContentMinimumWidthRatio;
            var preferredInset = rowHeight * _theme.ExpandedContentInsetRatio;
            var compactInset = Mathf.Min(preferredInset, Mathf.Max(0f, rowWidth - minimumExpandedWidth));

            var preferredGraphLeft = rowHeight * _theme.ValueColumnRatio;
            var graphLeft = rowWidth - preferredGraphLeft >= minimumExpandedWidth
                ? preferredGraphLeft
                : compactInset;
            _graph.style.marginLeft = graphLeft;
            _graph.style.width = Mathf.Max(
                0f,
                Mathf.Min(rowHeight * _theme.GraphWidthRatio, rowWidth - graphLeft));

            var pickerPadding = rowHeight * _theme.ColorPickerPaddingRatio;
            var preferredPickerWidth =
                _theme.EffectiveColorPickerHeight * _theme.ColorPickerWidthRatio + pickerPadding * 2f;
            _colorPicker.style.marginLeft = compactInset;
            _colorPicker.style.width = Mathf.Max(0f, Mathf.Min(preferredPickerWidth, rowWidth - compactInset));
        }

        /// <summary>
        /// 広いときは指定された値列と入力欄幅を保ち、狭いときだけ値列を左へ寄せる。
        /// 入力欄は余白を埋めるためには伸ばさず、右端の余白も常に残す。
        /// </summary>
        private void ApplyResponsiveHeaderLayout(float rowWidth)
        {
            if (Element == null) return;

            var rowHeight = _theme.EffectiveRowHeight;
            var leadingWidth = rowHeight * 2f + _rowDepth * _theme.EffectiveIndentWidth;
            var columnGap = rowHeight * _theme.ColumnGapRatio;
            var endPadding = rowHeight * _theme.RowEndPaddingRatio;
            var preferredValueLeft = rowHeight * _theme.ValueColumnRatio;
            var minimumLabelWidth = rowHeight * _theme.MinimumLabelWidthRatio;
            var minimumControlsWidth = GetMinimumControlsWidth();

            var latestValueLeft = Mathf.Max(leadingWidth, rowWidth - endPadding - minimumControlsWidth);
            var valueLeft = Mathf.Min(preferredValueLeft, latestValueLeft);
            if (latestValueLeft >= leadingWidth + minimumLabelWidth + columnGap)
            {
                valueLeft = Mathf.Max(valueLeft, leadingWidth + minimumLabelWidth + columnGap);
            }

            valueLeft = Mathf.Clamp(valueLeft, leadingWidth, Mathf.Max(leadingWidth, rowWidth - endPadding));
            _label.style.width = Mathf.Max(0f, valueLeft - leadingWidth - columnGap);
            _valueControls.style.marginRight = endPadding;

            var controlsWidth = Mathf.Max(0f, rowWidth - valueLeft - endPadding);
            ApplyControlWidths(controlsWidth);
        }

        private float GetMinimumControlsWidth()
        {
            var rowHeight = _theme.EffectiveRowHeight;
            var minimumFieldWidth = GetMinimumFieldWidth();
            if (IsEditingText) return minimumFieldWidth;

            var gap = rowHeight * _theme.ControlGapRatio;
            var buttonWidth = rowHeight * _theme.AdjustButtonWidthRatio;
            switch (_valueLayout)
            {
                case ValueLayoutKind.Field:
                    return Element.IsAdjustable
                        ? minimumFieldWidth + rowHeight * _theme.SliderMinimumWidthRatio +
                          buttonWidth * 2f + gap * 4f
                        : minimumFieldWidth;
                case ValueLayoutKind.Color:
                    return rowHeight * _theme.ColorSwatchWidthRatio + gap + minimumFieldWidth;
                case ValueLayoutKind.Bool:
                    return rowHeight * _theme.CheckboxSizeRatio +
                           (Element.IsAdjustable ? buttonWidth * 2f + gap * 3f : 0f);
                default:
                    return Element.IsAdjustable ? buttonWidth * 2f + gap * 3f + rowHeight * 2f : 0f;
            }
        }

        private void ApplyControlWidths(float controlsWidth)
        {
            var rowHeight = _theme.EffectiveRowHeight;
            var preferredFieldWidth = rowHeight * _theme.EditFieldWidthRatio;
            var minimumFieldWidth = GetMinimumFieldWidth();
            var preferredSliderWidth = rowHeight * _theme.SliderWidthRatio;
            var minimumSliderWidth = rowHeight * _theme.SliderMinimumWidthRatio;
            var gap = rowHeight * _theme.ControlGapRatio;
            var buttonWidth = rowHeight * _theme.AdjustButtonWidthRatio;

            var fieldWidth = Mathf.Min(preferredFieldWidth, controlsWidth);
            var sliderWidth = Mathf.Min(preferredSliderWidth, controlsWidth);

            if (IsEditingText)
            {
                sliderWidth = 0f;
            }
            else if (_valueLayout == ValueLayoutKind.Color)
            {
                var fixedWidth = rowHeight * _theme.ColorSwatchWidthRatio + gap;
                fieldWidth = Mathf.Min(preferredFieldWidth, Mathf.Max(0f, controlsWidth - fixedWidth));
                sliderWidth = 0f;
            }
            else if (_valueLayout == ValueLayoutKind.Field && Element.IsAdjustable)
            {
                var fixedWidth = buttonWidth * 2f + gap * 4f;
                var flexibleWidth = Mathf.Max(0f, controlsWidth - fixedWidth);
                if (flexibleWidth < minimumFieldWidth + minimumSliderWidth)
                {
                    fieldWidth = Mathf.Min(preferredFieldWidth, flexibleWidth);
                    sliderWidth = 0f;
                }
                else
                {
                    fieldWidth = Mathf.Min(preferredFieldWidth, flexibleWidth - minimumSliderWidth);
                    sliderWidth = Mathf.Min(preferredSliderWidth, flexibleWidth - fieldWidth);
                }
            }
            else if (_valueLayout == ValueLayoutKind.Field)
            {
                sliderWidth = 0f;
            }

            if (IsEditingText || _valueLayout == ValueLayoutKind.Field || _valueLayout == ValueLayoutKind.Color)
            {
                _value.style.width = Mathf.Max(0f, fieldWidth);
                _editor.style.width = Mathf.Max(0f, fieldWidth);
            }
            _sliderTrack.style.width = Mathf.Max(0f, sliderWidth);
        }

        private float GetMinimumFieldWidth()
        {
            var ratio = Element is DebugInt || Element is DebugFloat
                ? _theme.NumericFieldMinimumWidthRatio
                : _theme.EditFieldMinimumWidthRatio;
            return _theme.EffectiveRowHeight * ratio;
        }

        /// <summary>仮想化リストから外れる前に、進行中のポインター操作を終える。</summary>
        public void Unbind()
        {
            if (_hovered) _hoverRow?.Invoke(_rowIndex, false, Vector2.zero);
            CancelPointerInteractions();
            FinishTextEditBeforeRebind();
            _colorPicker.Unbind();
            Element = null;
            _rowIndex = -1;
            _selected = false;
            _hovered = false;
            _favoriteHovered = false;
            style.backgroundColor = Color.clear;
        }

        /// <summary>メニューを閉じる前に、この行が捕捉しているポインターを全て放す。</summary>
        public void CancelPointerInteractions()
        {
            CancelSliderDrag();
            _colorPicker.CancelPointerInteraction();
        }

        /// <summary>現在の値を直接入力する欄を開く。</summary>
        /// <returns>入力可能な行で、入力欄を開けたなら true。</returns>
        public bool BeginTextEdit()
        {
            if (Element == null || !Element.CanTypeValue) return false;
            if (ReferenceEquals(_editingElement, Element)) return true;

            FinishTextEditBeforeRebind();
            CancelPointerInteractions();

            _editingElement = Element;
            _editor.value = Element.GetEditText();
            SetEditorTextColor(_theme.InputFieldText);
            _editor.style.display = DisplayStyle.Flex;
            _value.style.display = DisplayStyle.None;
            _checkbox.style.display = DisplayStyle.None;
            _swatch.style.display = DisplayStyle.None;
            _decrease.style.display = DisplayStyle.None;
            _increase.style.display = DisplayStyle.None;
            _sliderTrack.style.display = DisplayStyle.None;
            ApplyResponsiveLayout(resolvedStyle.width);

            schedule.Execute(() =>
            {
                if (!IsEditingText) return;
                ApplyEditorTheme();
                _editor.Focus();
                _editor.SelectAll();
                ApplyEditorTheme();
            });

            return true;
        }

        /// <summary>入力中の文字列を反映して欄を閉じる。解釈できなければ開いたままにする。</summary>
        /// <returns>入力中でないか、反映できたなら true。</returns>
        public bool CommitTextEdit() => EndTextEdit(true);

        /// <summary>入力中の変更を捨てて欄を閉じる。</summary>
        public void CancelTextEdit() => EndTextEdit(false);

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0) return;

            _clickRow?.Invoke(_rowIndex);
            evt.StopPropagation();
        }

        private void OnValuePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0) return;

            _clickValue?.Invoke(_rowIndex);
            evt.StopPropagation();
        }

        private void OnImmediateValuePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0 || Element == null) return;

            _selectRow?.Invoke(_rowIndex);
            if (_decideValue != null) _decideValue(_rowIndex);
            else _clickValue?.Invoke(_rowIndex);
            evt.StopPropagation();
        }

        private void OnFavoritePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0 || Element == null) return;

            _selectRow?.Invoke(_rowIndex);
            Element.SetFavorite(!Element.IsFavorite);
            ApplyVisualState();
            evt.StopPropagation();
        }

        private void OnAdjustPointerDown(PointerDownEvent evt, int delta)
        {
            if (evt.button != 0 || _rowIndex < 0 || Element == null || !Element.IsAdjustable) return;

            _adjustRow?.Invoke(_rowIndex, delta);
            evt.StopPropagation();
        }

        private void OnSliderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || Element == null || !Element.TryGetRatio(out _)) return;

            _selectRow?.Invoke(_rowIndex);
            _sliderElement = Element;
            _sliderPointerId = evt.pointerId;
            ApplySliderPosition(evt.localPosition.x, _sliderElement);
            _sliderTrack.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSliderPointerMove(PointerMoveEvent evt)
        {
            if (!_sliderTrack.HasPointerCapture(evt.pointerId)) return;

            ApplySliderPosition(evt.localPosition.x, _sliderElement);
            evt.StopPropagation();
        }

        private void OnSliderPointerUp(PointerUpEvent evt)
        {
            if (!_sliderTrack.HasPointerCapture(evt.pointerId)) return;

            ApplySliderPosition(evt.localPosition.x, _sliderElement);
            _sliderPointerId = -1;
            _sliderElement = null;
            _sliderTrack.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSliderPointerCancel(PointerCancelEvent evt)
        {
            if (!_sliderTrack.HasPointerCapture(evt.pointerId)) return;

            _sliderPointerId = -1;
            _sliderElement = null;
            _sliderTrack.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSliderPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _sliderPointerId = -1;
            _sliderElement = null;
        }

        private void CancelSliderDrag()
        {
            var pointerId = _sliderPointerId;
            _sliderPointerId = -1;
            _sliderElement = null;

            if (pointerId >= 0 && _sliderTrack.HasPointerCapture(pointerId)) _sliderTrack.ReleasePointer(pointerId);
        }

        private void ApplySliderPosition(float localX, DebugElement element)
        {
            var width = _sliderTrack.resolvedStyle.width;
            if (width <= 0f || element == null) return;

            element.TrySetRatio(Mathf.Clamp01(localX / width));
        }

        private void OnExpandedContentPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0) return;

            _clickRow?.Invoke(_rowIndex);
            evt.StopPropagation();
        }

        private void OnColorPickerPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _rowIndex < 0) return;
            _selectRow?.Invoke(_rowIndex);
        }

        private void OnEditorKeyDown(KeyDownEvent evt)
        {
            if (!IsEditingText) return;

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitTextEdit();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Escape) return;

            CancelTextEdit();
            evt.StopPropagation();
        }

        private void OnEditorFocusOut(FocusOutEvent evt)
        {
            if (_endingEdit || !IsEditingText) return;
            if (!CommitTextEdit()) CancelTextEdit();
        }

        private bool EndTextEdit(bool commit)
        {
            var element = _editingElement;
            if (element == null) return true;

            if (commit && !element.CommitEditText(_editor.value))
            {
                SetEditorTextColor(_theme.Warning);
                schedule.Execute(() =>
                {
                    if (IsEditingText) _editor.Focus();
                });
                return false;
            }

            _endingEdit = true;
            _editingElement = null;
            _editor.Blur();
            _editor.style.display = DisplayStyle.None;

            var adjustable = Element != null && Element.IsAdjustable;
            _decrease.style.display = adjustable ? DisplayStyle.Flex : DisplayStyle.None;
            _increase.style.display = adjustable ? DisplayStyle.Flex : DisplayStyle.None;
            _checkbox.style.display = Element is DebugBool ? DisplayStyle.Flex : DisplayStyle.None;
            _value.style.display = Element != null && !(Element is DebugBool)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            BindSwatch(Element);
            BindSlider(Element);
            ApplyVisualState();
            ApplyResponsiveLayout(resolvedStyle.width);
            _endingEdit = false;
            _editEnded?.Invoke(this);
            return true;
        }

        private void FinishTextEditBeforeRebind()
        {
            if (!IsEditingText) return;
            if (!CommitTextEdit()) CancelTextEdit();
        }

        private void BindSwatch(DebugElement element)
        {
            if (element == null)
            {
                _swatch.style.display = DisplayStyle.None;
                return;
            }

            if (!IsEditingText && element is DebugColor colorElement)
            {
                _swatch.style.display = DisplayStyle.Flex;
                _swatch.style.backgroundColor = colorElement.Value;
                return;
            }

            _swatch.style.display = DisplayStyle.None;
        }

        private void BindSlider(DebugElement element)
        {
            if (!IsEditingText && element != null && element.TryGetRatio(out var ratio))
            {
                _sliderTrack.style.display = DisplayStyle.Flex;
                _sliderFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
                return;
            }

            _sliderTrack.style.display = DisplayStyle.None;
        }

        private void SetEditorTextColor(Color color)
        {
            _editorTextColor = color;
            ApplyEditorTheme();
        }

        private void ApplyEditorTheme()
        {
            // TextField の文字要素は Panel 接続後に作り直されることがある。
            // 編集開始時にも取り直し、既定テーマの黒文字と余白を確実に上書きする。
            _editorInput = _editor.Q<VisualElement>(className: "unity-base-text-field__input")
                ?? _editor.Q<VisualElement>(className: "unity-text-field__input");
            _editorText = _editorInput?.Q<VisualElement>(className: "unity-text-element")
                ?? _editor.Q<VisualElement>(className: "unity-text-element");

            _editor.style.color = _editorTextColor;
            if (_editorInput != null)
            {
                var horizontalPadding = _theme.EffectiveRowHeight * _theme.InputHorizontalPaddingRatio;
                _editorInput.name = "debug-menu-editor-input";
                _editorInput.style.color = _editorTextColor;
                _editorInput.style.fontSize = _theme.EffectiveFontSize;
                _editorInput.style.unityTextAlign = TextAnchor.MiddleLeft;
                _editorInput.style.width = Length.Percent(100f);
                _editorInput.style.height = Length.Percent(100f);
                _editorInput.style.marginLeft = 0f;
                _editorInput.style.marginRight = 0f;
                _editorInput.style.marginTop = 0f;
                _editorInput.style.marginBottom = 0f;
                _editorInput.style.paddingLeft = horizontalPadding;
                _editorInput.style.paddingRight = horizontalPadding;
                _editorInput.style.paddingTop = 0f;
                _editorInput.style.paddingBottom = 0f;
                _editorInput.style.borderLeftWidth = 0f;
                _editorInput.style.borderRightWidth = 0f;
                _editorInput.style.borderTopWidth = 0f;
                _editorInput.style.borderBottomWidth = 0f;
                _editorInput.style.backgroundColor = Color.clear;
            }
            if (_editorText != null)
            {
                _editorText.name = "debug-menu-editor-text";
                _editorText.style.color = _editorTextColor;
                _editorText.style.fontSize = _theme.EffectiveFontSize;
                _editorText.style.unityTextAlign = TextAnchor.MiddleLeft;
            }

            // Unity 6 は USS 変数を推奨するが、フォルダコピーだけで完結するランタイムViewでは
            // 変数を生成できないため、互換APIへ明示的に設定する。
#pragma warning disable CS0618
            _editor.textSelection.selectionColor = _theme.InputFieldSelection;
            _editor.textSelection.cursorColor = _theme.InputFieldCursor;
#pragma warning restore CS0618
        }

        private void EnsureValueLayout(ValueLayoutKind layout)
        {
            if (_valueLayout == layout) return;

            _valueLayout = layout;
            _valueControls.Clear();
            var gap = _theme.EffectiveRowHeight * _theme.ControlGapRatio;
            _decrease.style.marginLeft = 0f;
            _decrease.style.marginRight = gap;
            _increase.style.marginLeft = gap;
            _increase.style.marginRight = gap;

            switch (layout)
            {
                case ValueLayoutKind.Field:
                    _valueControls.Add(_value);
                    _valueControls.Add(_editor);
                    _decrease.style.marginLeft = gap;
                    _valueControls.Add(_decrease);
                    _increase.style.marginLeft = 0f;
                    _valueControls.Add(_increase);
                    _valueControls.Add(_sliderTrack);
                    break;
                case ValueLayoutKind.Color:
                    _valueControls.Add(_swatch);
                    _valueControls.Add(_value);
                    _valueControls.Add(_editor);
                    break;
                case ValueLayoutKind.Bool:
                    _valueControls.Add(_decrease);
                    _valueControls.Add(_checkbox);
                    _valueControls.Add(_increase);
                    _valueControls.Add(_sliderTrack);
                    break;
                default:
                    _valueControls.Add(_decrease);
                    _valueControls.Add(_value);
                    _valueControls.Add(_increase);
                    _valueControls.Add(_sliderTrack);
                    break;
            }
        }

        private void BindIdleField(DebugElement element, bool useSpecialValue)
        {
            var showField = element.CanTypeValue && !useSpecialValue;
            var rowHeight = _theme.EffectiveRowHeight;
            if (showField)
            {
                _value.style.width = rowHeight * _theme.EditFieldWidthRatio;
                _value.style.minWidth = 0f;
                _value.style.height = rowHeight;
            }
            else
            {
                _value.style.width = StyleKeyword.Auto;
                _value.style.minWidth = 0f;
                _value.style.height = StyleKeyword.Auto;
            }

            // 入力欄は「余っている幅を埋める背景」ではない。指定幅を上限にし、
            // 狭いときだけ ApplyResponsiveHeaderLayout が縮める。
            _value.style.flexGrow = 0f;
            _editor.style.flexGrow = 0f;
            _value.style.paddingLeft = showField ? rowHeight * _theme.InputHorizontalPaddingRatio : 0f;
            _value.style.paddingRight = showField ? rowHeight * _theme.InputHorizontalPaddingRatio : 0f;
            _value.style.backgroundColor = showField ? _theme.InputFieldBackground : Color.clear;
            var borderWidth = showField ? 1f : 0f;
            _value.style.borderTopWidth = borderWidth;
            _value.style.borderBottomWidth = borderWidth;
            _value.style.borderLeftWidth = borderWidth;
            _value.style.borderRightWidth = borderWidth;
            _value.style.borderTopColor = _theme.InputFieldBorder;
            _value.style.borderBottomColor = _theme.InputFieldBorder;
            _value.style.borderLeftColor = _theme.InputFieldBorder;
            _value.style.borderRightColor = _theme.InputFieldBorder;
        }

        private void SetHover(bool hovered)
        {
            if (_hovered == hovered) return;

            _hovered = hovered;
            ApplyVisualState();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            SetHover(true);
            _hoverRow?.Invoke(_rowIndex, true, evt.position);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_hovered) _hoverRow?.Invoke(_rowIndex, true, evt.position);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            SetHover(false);
            _hoverRow?.Invoke(_rowIndex, false, evt.position);
        }

        private void SetFavoriteHover(bool hovered)
        {
            if (_favoriteHovered == hovered) return;

            _favoriteHovered = hovered;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            var element = Element;
            if (element == null) return;

            style.backgroundColor = _selected
                ? _theme.SelectionBackground
                : _hovered ? _theme.HoverBackground : Color.clear;

            _modifiedMark.style.display = element.IsModified ? DisplayStyle.Flex : DisplayStyle.None;

            var labelColor = ResolveLabelColor(element);
            var valueColor = ResolveValueColor(element);
            _label.style.color = labelColor;
            _marker.style.color = labelColor;
            _value.style.color = valueColor;
            _decrease.style.color = valueColor;
            _increase.style.color = valueColor;
            _checkmark.style.color = valueColor;
            _checkbox.style.borderTopColor = valueColor;
            _checkbox.style.borderBottomColor = valueColor;
            _checkbox.style.borderLeftColor = valueColor;
            _checkbox.style.borderRightColor = valueColor;
            _swatch.style.borderTopColor = valueColor;
            _swatch.style.borderBottomColor = valueColor;
            _swatch.style.borderLeftColor = valueColor;
            _swatch.style.borderRightColor = valueColor;
            _sliderFill.style.backgroundColor = valueColor;

            var trackColor = valueColor;
            trackColor.a *= 0.25f;
            _sliderRail.style.backgroundColor = trackColor;

            _favorite.style.color = _theme.Favorite;
            _favorite.style.opacity = element.IsFavorite ? 1f : _favoriteHovered ? 0.65f : 0.22f;
        }

        private Color ResolveLabelColor(DebugElement element)
        {
            if (element.TextColor.HasValue) return element.TextColor.Value;
            if (element is DebugGroup || element is DebugSeparator) return _theme.GroupText;

            return _selected ? _theme.SelectedText : _theme.Text;
        }

        private Color ResolveValueColor(DebugElement element)
        {
            if (element.IsValueWarned) return _theme.Warning;
            if (element.ValueColor.HasValue) return element.ValueColor.Value;

            return _selected ? _theme.SelectedText : _theme.Value;
        }

        private Color ResolveGraphLineColor(DebugElement element)
        {
            if (element.IsValueWarned) return _theme.Warning;
            if (element.ValueColor.HasValue) return element.ValueColor.Value;

            return _selected ? _theme.SelectedText : _theme.GraphLine;
        }

        private static Label MakeLabel(DebugMenuTheme theme, Color color)
        {
            return new Label
            {
                style =
                {
                    color = color,
                    fontSize = theme.EffectiveFontSize,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };
        }

        private static Label MakeButtonLabel(DebugMenuTheme theme, string text)
        {
            var label = MakeLabel(theme, theme.TextDim);
            label.text = text;
            label.style.width = theme.EffectiveRowHeight * theme.AdjustButtonWidthRatio;
            label.style.flexShrink = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = Mathf.Max(10, theme.EffectiveFontSize - 4);
            label.style.display = DisplayStyle.None;
            return label;
        }

        private enum ValueLayoutKind
        {
            None,
            Standard,
            Field,
            Bool,
            Color,
        }
    }
}
