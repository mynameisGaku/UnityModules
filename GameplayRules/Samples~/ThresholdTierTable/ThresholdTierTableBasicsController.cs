using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayProgression.Samples
{
    /// <summary>3段階のthreshold tableへ5種類の値を評価し、現在tierと次tier進捗を確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ThresholdTierTableBasicsController : MonoBehaviour
    {
        public const string CardElementName = "threshold-tier-table-basics-card";
        public const string TitleElementName = "threshold-tier-table-basics-title";
        public const string DescriptionElementName = "threshold-tier-table-basics-description";
        public const string ConfigurationElementName = "threshold-tier-table-basics-configuration";
        public const string InputElementName = "threshold-tier-table-basics-input";
        public const string StageElementName = "threshold-tier-table-basics-stage";
        public const string ResultElementName = "threshold-tier-table-basics-result";
        public const string ButtonRowElementName = "threshold-tier-table-basics-buttons";
        public const string BelowButtonElementName = "threshold-tier-table-basics-below";
        public const string BronzeButtonElementName = "threshold-tier-table-basics-bronze";
        public const string MidButtonElementName = "threshold-tier-table-basics-mid";
        public const string SilverGoldButtonElementName = "threshold-tier-table-basics-silver-gold";
        public const string GoldButtonElementName = "threshold-tier-table-basics-gold";

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
        private ThresholdTierTable _table;
        private ThresholdTierEvaluation _lastEvaluation;
        private bool _hasEvaluation;
        private int _buttonActionCount;

        /// <summary>設定済みtier数。</summary>
        public int TierCount => _table?.Count ?? 0;

        /// <summary>最後の評価が存在するか。</summary>
        public bool HasEvaluation => _hasEvaluation;

        /// <summary>最後の評価結果。</summary>
        public ThresholdTierEvaluation LastEvaluation => _lastEvaluation;

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

            _title = AddLabel(TitleElementName, "Threshold Tier Table Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "有限値を複数thresholdへ割り当て、現在tierと次tierまでの進捗を返します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "BRONZE 0  ·  SILVER 100  ·  GOLD 300  ·  INCLUSIVE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(BelowButtonElementName, "Query -10", () => EvaluateValue(-10d)),
                CreateButton(BronzeButtonElementName, "Query 0 · Bronze", () => EvaluateValue(0d)),
                CreateButton(MidButtonElementName, "Query 50 · 50%", () => EvaluateValue(50d)),
                CreateButton(SilverGoldButtonElementName, "Query 250 · 75%", () => EvaluateValue(250d)),
                CreateButton(GoldButtonElementName, "Query 500 · Gold", () => EvaluateValue(500d))
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

        private void EvaluateValue(double value)
        {
            if (!_table.TryEvaluate(value, out _lastEvaluation, out var error)) throw new InvalidOperationException($"Tier evaluation failed: {error}");
            _hasEvaluation = true;
            _buttonActionCount++;
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!ThresholdTierTable.TryCreate(3, out _table, out var createError)) throw new InvalidOperationException($"Tier table creation failed: {createError}");
            AddTier(3, 300d);
            AddTier(1, 0d);
            AddTier(2, 100d);
            _lastEvaluation = default;
            _hasEvaluation = false;
            _buttonActionCount = 0;
            _input.text = "QUERY —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose a value below, within, or above the configured tiers";
            _result.text = "CURRENT —   ·   NEXT —   ·   PROGRESS —";
        }

        private void AddTier(int id, double minimumValue)
        {
            if (!_table.TryAddTier(id, minimumValue, out var error)) throw new InvalidOperationException($"Tier setup failed: {error}");
        }

        private void RefreshLabels()
        {
            var evaluation = _lastEvaluation;
            _input.text = $"QUERY {Format(evaluation.QueryValue)}   ·   TIER COUNT {_table.Count}/{_table.Capacity}   ·   ACTIONS {_buttonActionCount}";
            var current = evaluation.HasCurrentTier ? TierName(evaluation.CurrentTier.Id) : "NOT REACHED";
            var next = evaluation.HasNextTier ? $"{TierName(evaluation.NextTier.Id)} @{Format(evaluation.NextTier.MinimumValue)}" : "TERMINAL";
            _stage.text = evaluation.HasCurrentTier
                ? $"Current {current}  ·  threshold {Format(evaluation.CurrentTier.MinimumValue)}"
                : "Below first tier  ·  no current tier";
            _result.text = $"CURRENT {current}   ·   NEXT {next}   ·   PROGRESS {Format(evaluation.ProgressToNext * 100d)}%";
        }

        private static string TierName(int id)
        {
            if (id == 1) return "BRONZE";
            if (id == 2) return "SILVER";
            if (id == 3) return "GOLD";
            return $"TIER {id}";
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
