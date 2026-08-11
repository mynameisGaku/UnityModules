using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu
{
    /// <summary>
    /// メニューの見た目。<see cref="DebugMenuRoot"/> の状態を映し、
    /// マウス操作は既存のメニュー操作へ渡す。
    /// <para>
    /// 行の並びはページ側が平坦化したものをそのまま使う。木を辿りながら描く実装だと、
    /// 仮想化が効かず、字下げと折り畳みの計算が描画側に漏れてくる。
    /// </para>
    /// </summary>
    public sealed class DebugMenuView
    {
        private const float DoubleClickWindowSeconds = 0.5f;

        private readonly DebugMenuRoot _menu;
        private readonly List<DebugRow> _rows = new List<DebugRow>();

        private readonly VisualElement _root;
        private readonly Button _backPage;
        private readonly Button _previousPage;
        private readonly Label _breadcrumb;
        private readonly Button _nextPage;
        private readonly Label _counter;
        private readonly Label _pageHeader;
        private readonly VisualElement _descriptionPanel;
        private readonly Label _description;
        private readonly Label _hints;
        private readonly ListView _list;

        private int _lastCursor = -1;
        private DebugPage _lastPage;
        private DebugElement _lastClickedElement;
        private float _lastClickTime = float.NegativeInfinity;
        private DebugRowView _editingRow;
        private bool _textInputEnded;

        /// <summary>メニューとテーマを指定して見た目を組み立てる。</summary>
        /// <param name="menu">映す対象。</param>
        /// <param name="theme">色と寸法。省略すると既定。</param>
        public DebugMenuView(DebugMenuRoot menu, DebugMenuTheme theme = null)
        {
            _menu = menu;
            Theme = theme ?? new DebugMenuTheme();

            _root = new VisualElement
            {
                name = "debug-menu",
                style =
                {
                    position = Position.Absolute,
                    left = 0f,
                    top = 0f,
                    right = 0f,
                    bottom = 0f,
                    backgroundColor = Theme.Background,
                    flexDirection = FlexDirection.Column,
                    paddingLeft = Theme.EffectivePanelMargin,
                    paddingRight = Theme.EffectivePanelMargin,
                    paddingTop = Theme.EffectiveTopMargin,
                    paddingBottom = Theme.EffectiveTopMargin,
                    overflow = Overflow.Hidden,
                },
            };
            // ポインター押下だけを捕捉する。WheelEvent は ListView の既定スクロールへそのまま渡す。
            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);

            // 固定幅の窓ではなく、全画面の左上へタイトルと一覧を置く。
            var titleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = Mathf.Max(
                        Theme.EffectiveRowHeight,
                        Theme.EffectiveRowHeight * Theme.HeaderButtonSizeRatio),
                    flexShrink = 0f,
                    overflow = Overflow.Hidden,
                },
            };

            var title = new Label("DebugTop")
            {
                name = "debug-menu-title",
                style =
                {
                    color = Theme.Title,
                    fontSize = Theme.EffectiveFontSize,
                    flexGrow = 1f,
                    flexShrink = 1f,
                    minWidth = 0f,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };

            _breadcrumb = new Label
            {
                name = "debug-menu-breadcrumb",
                style =
                {
                    color = Theme.Breadcrumb,
                    fontSize = Theme.EffectiveFontSize,
                    flexGrow = 1f,
                    flexShrink = 1f,
                    minWidth = 0f,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };

            _backPage = MakeHeaderButton("←", "親ページへ戻る", MoveBack);
            _previousPage = MakeHeaderButton("◀", "前のページ ([)", () => MoveRootPage(-1));
            _nextPage = MakeHeaderButton("▶", "次のページ (])", () => MoveRootPage(1));

            _counter = new Label
            {
                name = "debug-menu-counter",
                style =
                {
                    color = Theme.TextDim,
                    fontSize = Theme.EffectiveFontSize - 2,
                    marginLeft = Theme.EffectiveRowHeight * 0.5f,
                    flexShrink = 0f,
                    unityTextAlign = TextAnchor.MiddleRight,
                },
            };

            titleRow.Add(title);
            titleRow.Add(_backPage);
            titleRow.Add(_previousPage);
            titleRow.Add(_nextPage);

            var titleSpacer = new VisualElement { style = { height = Theme.EffectiveRowHeight, flexShrink = 0f } };
            var breadcrumbRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = Theme.EffectiveRowHeight,
                    flexShrink = 0f,
                    overflow = Overflow.Hidden,
                },
            };
            breadcrumbRow.Add(_breadcrumb);
            breadcrumbRow.Add(_counter);

            var breadcrumbSpacer = new VisualElement { style = { height = Theme.EffectiveRowHeight, flexShrink = 0f } };

            _pageHeader = new Label
            {
                name = "debug-menu-page-header",
                style =
                {
                    color = Theme.GroupText,
                    fontSize = Theme.EffectiveFontSize,
                    height = Theme.EffectiveRowHeight,
                    flexShrink = 0f,
                    minWidth = 0f,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };

            var pageHeaderSpacer = new VisualElement { style = { height = Theme.EffectiveRowHeight, flexShrink = 0f } };

            // ── 本体：行の並び ──
            _list = new ListView
            {
                name = "debug-menu-list",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                showBorder = false,
                itemsSource = _rows,
                fixedItemHeight = Theme.EffectiveRowHeight,
                makeItem = MakeRow,
                bindItem = BindRow,
                unbindItem = UnbindRow,
                style =
                {
                    flexGrow = 1f,
                    backgroundColor = Color.clear,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };

            // 説明は右下へ浮かせ、一覧の高さを奪わない。
            _descriptionPanel = new VisualElement
            {
                name = "debug-menu-description-panel",
                style =
                {
                    position = Position.Absolute,
                    right = Theme.EffectiveRowHeight * 0.8f,
                    bottom = Theme.EffectiveRowHeight * 0.8f,
                    maxWidth = Length.Percent(55f),
                    backgroundColor = Theme.DescriptionBackground,
                    paddingLeft = Theme.EffectiveRowHeight * 0.5f,
                    paddingRight = Theme.EffectiveRowHeight * 0.5f,
                    paddingTop = Theme.EffectiveRowHeight * 0.5f,
                    paddingBottom = Theme.EffectiveRowHeight * 0.5f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopColor = Theme.DescriptionBorder,
                    borderBottomColor = Theme.DescriptionBorder,
                    borderLeftColor = Theme.DescriptionBorder,
                    borderRightColor = Theme.DescriptionBorder,
                    display = DisplayStyle.None,
                },
            };

            _description = new Label
            {
                name = "debug-menu-description",
                style =
                {
                    color = Theme.DescriptionText,
                    fontSize = Theme.EffectiveFontSize,
                    whiteSpace = WhiteSpace.Normal,
                    marginLeft = 0f,
                    marginRight = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                },
            };

            _hints = new Label
            {
                text = "↑↓ 移動   ←→ 変更   [ ] ページ   Enter 決定/入力   Esc 戻る\nクリック 選択   ダブルクリック 決定   値をダブルクリック 入力   ドラッグ 変更   ヘッダー 戻る/ページ",
                name = "debug-menu-hints",
                style =
                {
                    position = Position.Absolute,
                    left = Theme.EffectivePanelMargin,
                    right = Theme.EffectivePanelMargin,
                    bottom = Theme.EffectiveTopMargin,
                    color = Theme.TextDim,
                    fontSize = Mathf.Max(12, Theme.EffectiveFontSize - 4),
                    whiteSpace = WhiteSpace.Normal,
                    overflow = Overflow.Hidden,
                    display = DisplayStyle.None,
                },
            };

            _descriptionPanel.Add(_description);

            _root.Add(titleRow);
            _root.Add(titleSpacer);
            _root.Add(breadcrumbRow);
            _root.Add(breadcrumbSpacer);
            _root.Add(_pageHeader);
            _root.Add(pageHeaderSpacer);
            _root.Add(_list);
            _root.Add(_descriptionPanel);
            _root.Add(_hints);
        }

        /// <summary>色と寸法。</summary>
        public DebugMenuTheme Theme { get; }

        /// <summary>UI の根。<c>UIDocument.rootVisualElement</c> へ足して使う。</summary>
        public VisualElement Root => _root;

        /// <summary>脚に出す操作の手掛かり。</summary>
        public string Hints
        {
            get => _hints.text;
            set
            {
                _hints.text = value;
                _hints.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>直接入力欄が開いているか。</summary>
        public bool IsEditingText => _editingRow != null && _editingRow.IsEditingText;

        /// <summary>非表示へ切り替える前に、可視行のドラッグと文字入力を全て終える。</summary>
        public void CancelPointerInteractions()
        {
            foreach (var row in _list.Query<DebugRowView>().Build())
            {
                row.CancelPointerInteractions();
                row.CancelTextEdit();
            }

            ResetClickState();
        }

        /// <summary>
        /// 文字入力がメニュー操作用キーを消費すべきフレームかを返す。
        /// 終了キーを同じフレームの決定・取消として二重処理しないために Controller が使う。
        /// </summary>
        public bool ConsumeTextInput()
        {
            var consumed = IsEditingText || _textInputEnded;
            _textInputEnded = false;
            return consumed;
        }

        /// <summary>現在の行が直接入力できるなら入力欄を開く。</summary>
        /// <returns>入力欄を開けたなら true。</returns>
        public bool TryBeginEditCurrent()
        {
            var page = _menu.CurrentPage;
            if (page == null) return false;

            var element = page.CurrentElement;
            if (element == null || element.PrefersDecide) return false;

            return BeginEditRow(page.CursorIndex);
        }

        /// <summary>
        /// メニューの状態を映す。毎フレーム呼んでよい。
        /// <para>
        /// 行の並びが変わっていなければ <see cref="ListView"/> の作り直しはせず、
        /// カーソルの移動だけを反映する。項目数に比例した処理を毎フレーム走らせないため。
        /// </para>
        /// </summary>
        public void Refresh()
        {
            var page = _menu.CurrentPage;
            if (page == null) return;

            if (!ReferenceEquals(_lastPage, page))
            {
                _lastPage = page;
                ResetClickState();
            }

            var visible = page.VisibleRows;

            if (HasRowsChanged(visible))
            {
                _rows.Clear();
                for (var i = 0; i < visible.Count; i++) _rows.Add(visible[i]);

                _list.Rebuild();
                _lastCursor = -1;
                ResetClickState();
            }

            _breadcrumb.text = BuildBreadcrumb();
            _pageHeader.text = page.Name;
            _counter.text = _rows.Count == 0 ? string.Empty : $"{page.CursorIndex + 1} / {_rows.Count}";
            _description.text = _menu.CurrentDescription;
            _descriptionPanel.style.display = string.IsNullOrEmpty(_description.text) ? DisplayStyle.None : DisplayStyle.Flex;

            var canMovePage = _menu.Pages.Count > 1;
            _backPage.style.display = _menu.Depth > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _previousPage.style.display = canMovePage ? DisplayStyle.Flex : DisplayStyle.None;
            _nextPage.style.display = canMovePage ? DisplayStyle.Flex : DisplayStyle.None;

            if (page.CursorIndex != _lastCursor)
            {
                _lastCursor = page.CursorIndex;
                _list.ScrollToItem(_lastCursor);
            }

            // 値は毎フレーム変わりうるので、見えている行だけ映し直す。
            RefreshVisibleRows();
        }

        private void RefreshVisibleRows()
        {
            var page = _menu.CurrentPage;
            if (page == null) return;

            foreach (var element in _list.Query<DebugRowView>().Build())
            {
                var index = element.RowIndex;
                if (index < 0 || index >= _rows.Count) continue;

                element.Bind(_rows[index], index == page.CursorIndex, index);
            }
        }

        private void BindRow(VisualElement view, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            if (!(view is DebugRowView row)) return;

            row.Bind(_rows[index], index == _menu.CurrentPage?.CursorIndex, index);
        }

        private VisualElement MakeRow() => new DebugRowView(Theme, SelectRow, ClickRow, ClickValue, DecideValue, AdjustRow, OnTextEditEnded);

        private static void UnbindRow(VisualElement view, int index)
        {
            if (view is DebugRowView row) row.Unbind();
        }

        private void SelectRow(int index)
        {
            var page = _menu.CurrentPage;
            if (page == null) return;

            page.CursorIndex = index;
            ResetClickState();
        }

        private void ClickRow(int index)
        {
            if (!RegisterClick(index, out var clickedElement)) return;

            if (clickedElement.CanTypeValue && !clickedElement.PrefersDecide) BeginEditRow(index);
            else _menu.Decide();
        }

        private void ClickValue(int index)
        {
            if (!RegisterClick(index, out var clickedElement)) return;

            if (clickedElement.CanTypeValue) BeginEditRow(index);
            else _menu.Decide();
        }

        private void DecideValue(int index)
        {
            var page = _menu.CurrentPage;
            if (page == null || index < 0 || index >= page.VisibleRows.Count) return;

            page.CursorIndex = index;
            ResetClickState();
            _menu.Decide();
            Refresh();
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 1) return;

            // 子要素より先に受け、入力欄やピッカー上でも同じ戻る操作にする。
            CancelPointerInteractions();
            _menu.Cancel();
            Refresh();
            evt.StopPropagation();
        }

        private bool RegisterClick(int index, out DebugElement clickedElement)
        {
            clickedElement = null;
            var page = _menu.CurrentPage;
            if (page == null || index < 0 || index >= page.VisibleRows.Count) return false;

            clickedElement = page.VisibleRows[index].Element;
            var now = Time.realtimeSinceStartup;
            var isDoubleClick = ReferenceEquals(_lastClickedElement, clickedElement) && now - _lastClickTime <= DoubleClickWindowSeconds;

            page.CursorIndex = index;
            if (isDoubleClick)
            {
                ResetClickState();
                return true;
            }

            _lastClickedElement = clickedElement;
            _lastClickTime = now;
            return false;
        }

        private bool BeginEditRow(int index)
        {
            var page = _menu.CurrentPage;
            if (page == null || index < 0 || index >= page.VisibleRows.Count) return false;

            var element = page.VisibleRows[index].Element;
            if (!element.CanTypeValue) return false;

            page.CursorIndex = index;
            ResetClickState();

            if (_editingRow != null && _editingRow.RowIndex != index)
            {
                if (!_editingRow.CommitTextEdit()) _editingRow.CancelTextEdit();
            }

            foreach (var row in _list.Query<DebugRowView>().Build())
            {
                if (row.RowIndex != index) continue;
                if (!row.BeginTextEdit()) return false;

                _editingRow = row;
                return true;
            }

            _list.ScrollToItem(index);
            return false;
        }

        private void OnTextEditEnded(DebugRowView row)
        {
            if (ReferenceEquals(_editingRow, row)) _editingRow = null;
            _textInputEnded = true;
        }

        private void ResetClickState()
        {
            _lastClickedElement = null;
            _lastClickTime = float.NegativeInfinity;
        }

        private void AdjustRow(int index, int delta)
        {
            SelectRow(index);
            _menu.Adjust(delta);
        }

        private Button MakeHeaderButton(string text, string tooltip, System.Action clicked)
        {
            var buttonSize = Theme.EffectiveRowHeight * Theme.HeaderButtonSizeRatio;
            var button = new Button(clicked)
            {
                text = text,
                tooltip = tooltip,
                focusable = false,
                style =
                {
                    width = buttonSize,
                    height = buttonSize,
                    minWidth = buttonSize,
                    flexShrink = 0f,
                    marginLeft = Theme.EffectiveRowHeight * Theme.HeaderButtonGapRatio,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    color = Theme.Text,
                    fontSize = Mathf.Max(12, Theme.EffectiveFontSize - 3),
                    backgroundColor = Theme.HeaderBackground,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopColor = Theme.InputFieldBorder,
                    borderBottomColor = Theme.InputFieldBorder,
                    borderLeftColor = Theme.InputFieldBorder,
                    borderRightColor = Theme.InputFieldBorder,
                },
            };

            return button;
        }

        private void MoveBack()
        {
            CancelPointerInteractions();
            if (_menu.PopPage()) Refresh();
        }

        private void MoveRootPage(int delta)
        {
            CancelPointerInteractions();
            _menu.MoveRootPage(delta);
            Refresh();
        }

        private bool HasRowsChanged(Containers.FastList<DebugRow> visible)
        {
            if (visible.Count != _rows.Count) return true;

            for (var i = 0; i < visible.Count; i++)
            {
                if (!ReferenceEquals(visible[i].Element, _rows[i].Element)) return true;
                if (visible[i].Depth != _rows[i].Depth) return true;
            }

            return false;
        }

        private string BuildBreadcrumb()
        {
            var stack = _menu.PageStack;
            if (stack.Count == 0) return string.Empty;

            var breadcrumb = new StringBuilder("DebugTop");
            for (var i = 0; i < stack.Count; i++) breadcrumb.Append(" - ").Append(stack[i].Name);
            return breadcrumb.ToString();
        }
    }
}
