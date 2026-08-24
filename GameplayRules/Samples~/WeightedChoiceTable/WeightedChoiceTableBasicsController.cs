using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplaySelection.Samples
{
    /// <summary>3つのweight追加と2つのnormalized sample選択を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WeightedChoiceTableBasicsController : MonoBehaviour
    {
        public const string CardElementName = "weighted-choice-table-basics-card";
        public const string TitleElementName = "weighted-choice-table-basics-title";
        public const string DescriptionElementName = "weighted-choice-table-basics-description";
        public const string ConfigurationElementName = "weighted-choice-table-basics-configuration";
        public const string InputElementName = "weighted-choice-table-basics-input";
        public const string StageElementName = "weighted-choice-table-basics-stage";
        public const string ResultElementName = "weighted-choice-table-basics-result";
        public const string ButtonRowElementName = "weighted-choice-table-basics-buttons";
        public const string AddCommonButtonElementName = "weighted-choice-table-basics-add-common";
        public const string AddRareButtonElementName = "weighted-choice-table-basics-add-rare";
        public const string AddEpicButtonElementName = "weighted-choice-table-basics-add-epic";
        public const string SelectRareButtonElementName = "weighted-choice-table-basics-select-rare";
        public const string SelectEpicButtonElementName = "weighted-choice-table-basics-select-epic";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _input;
        private Label _stage;
        private Label _result;
        private Button[] _buttons;
        private WeightedChoiceTable _table;
        private WeightedChoiceChangeResult _lastChange;
        private WeightedChoiceSelectionResult _lastSelection;
        private int _buttonActionCount;

        /// <summary>現在のentry件数。</summary>
        public int EntryCount => _table?.EntryCount ?? 0;

        /// <summary>現在の有限weight合計。</summary>
        public double TotalWeight => _table?.TotalWeight ?? 0d;

        /// <summary>最後のentry変更結果。</summary>
        public WeightedChoiceChangeResult LastChange => _lastChange;

        /// <summary>最後のsample選択結果。</summary>
        public WeightedChoiceSelectionResult LastSelection => _lastSelection;

        /// <summary>実Button操作数。</summary>
        public int ButtonActionCount => _buttonActionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            BuildUi();
            ResetStateCore();
        }

        private void OnDisable()
        {
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            if (_document != null && _document.rootVisualElement != null) _document.rootVisualElement.Clear();
            _buttons = null;
            _root = null;
            _card = null;
            _buttonRow = null;
        }

        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.025f, 0.035f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.07f, 0.075f, 0.19f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Weighted Choice Table Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "ID昇順の正weightを累積区間へ変換し、同じsampleから同じentryを選びます。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "MAX 32  ·  WEIGHT > 0  ·  SAMPLE [0, 1)  ·  NO RNG", 12f, new Color(0.55f, 1f, 0.82f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.92f, 0.93f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.48f, 0.90f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.89f, 0.90f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.025f, 0.025f, 0.09f, 1f);
            _result.style.borderTopLeftRadius = 10f;
            _result.style.borderTopRightRadius = 10f;
            _result.style.borderBottomLeftRadius = 10f;
            _result.style.borderBottomRightRadius = 10f;
            _result.style.paddingTop = 8f;
            _result.style.paddingBottom = 8f;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);

            _buttons = new[]
            {
                CreateButton(AddCommonButtonElementName, "Add Common 6", () => ApplyChange(_table.Add(10, 6d), "ID 10  ·  Common weight 6  ·  [0, 6)")),
                CreateButton(AddRareButtonElementName, "Add Rare 3", () => ApplyChange(_table.Add(20, 3d), "ID 20  ·  Rare weight 3  ·  [6, 9)")),
                CreateButton(AddEpicButtonElementName, "Add Epic 1", () => ApplyChange(_table.Add(30, 1d), "ID 30  ·  Epic weight 1  ·  [9, 10)")),
                CreateButton(SelectRareButtonElementName, "Select 0.65 → Rare", () => ApplySelection(0.65d, "sample 0.65  ·  ticket 6.5  ·  Rare ID 20")),
                CreateButton(SelectEpicButtonElementName, "Select 0.95 → Epic", () => ApplySelection(0.95d, "sample 0.95  ·  ticket 9.5  ·  Epic ID 30"))
            };
            for (var index = 0; index < _buttons.Length; index++) _buttonRow.Add(_buttons[index]);

            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ApplyResponsiveLayout();
        }

        private Label AddLabel(string elementName, string text, float fontSize, Color color)
        {
            var label = new Label(text) { name = elementName };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            _card.Add(label);
            return label;
        }

        private static Button CreateButton(string elementName, string text, Action callback)
        {
            var button = new Button(callback) { name = elementName, text = text };
            button.style.flexGrow = 1f;
            button.style.color = new Color(0.05f, 0.06f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.75f, 0.81f, 1f, 1f);
            return button;
        }

        private void ApplyChange(WeightedChoiceChangeResult result, string successStage)
        {
            _lastChange = result;
            _buttonActionCount++;
            _stage.text = result.Succeeded ? successStage : $"Change rejected  ·  {result.Error}";
            RefreshLabels();
        }

        private void ApplySelection(double sample, string successStage)
        {
            _lastSelection = _table.Select(sample);
            _buttonActionCount++;
            _stage.text = _lastSelection.Succeeded ? successStage : $"Selection rejected  ·  {_lastSelection.Error}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _table = new WeightedChoiceTable();
            _lastChange = _table.Clear();
            _lastSelection = default;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  add three entries, then select two samples";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"ENTRIES {EntryCount}   ·   TOTAL {Format(TotalWeight)}   ·   LAST CHANGE {_lastChange.Error}   ·   ACTIONS {_buttonActionCount}";
            _result.text = _lastSelection.Succeeded
                ? $"SELECTED ID {_lastSelection.SelectedIdentifier}   ·   INDEX {_lastSelection.SelectedIndex}   ·   WEIGHT {Format(_lastSelection.SelectedWeight)}   ·   RANGE [{Format(_lastSelection.IntervalStart)}, {Format(_lastSelection.IntervalEnd)})"
                : "SELECTED —   ·   INDEX —   ·   WEIGHT —   ·   RANGE —";
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private void HandleGeometryChanged(GeometryChangedEvent _) => ApplyResponsiveLayout();

        private void ApplyResponsiveLayout()
        {
            if (_root == null || _card == null || _buttons == null) return;
            var compact = _root.resolvedStyle.width > 0f && (_root.resolvedStyle.width < 720f || _root.resolvedStyle.height < 440f);
            _card.style.paddingLeft = compact ? 14f : 32f;
            _card.style.paddingRight = compact ? 14f : 32f;
            _card.style.paddingTop = compact ? 10f : 24f;
            _card.style.paddingBottom = compact ? 10f : 24f;
            _title.style.fontSize = compact ? 23f : 31f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 11f : 15f;
            _description.style.marginBottom = compact ? 5f : 10f;
            _configuration.style.fontSize = compact ? 9.5f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _input.style.fontSize = compact ? 10f : 13f;
            _input.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 9f : 12f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
            _buttonRow.style.marginTop = compact ? 1f : 3f;
            for (var index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].style.flexBasis = compact ? 160f : 130f;
                _buttons[index].style.minWidth = compact ? 140f : 110f;
                _buttons[index].style.minHeight = compact ? 30f : 42f;
                _buttons[index].style.fontSize = compact ? 10.5f : 12f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
