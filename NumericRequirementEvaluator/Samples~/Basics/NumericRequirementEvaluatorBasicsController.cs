using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayRules.Samples
{
    /// <summary>複数数値条件の成立可否と全明細を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class NumericRequirementEvaluatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "numeric-requirement-evaluator-basics-card";
        public const string TitleElementName = "numeric-requirement-evaluator-basics-title";
        public const string DescriptionElementName = "numeric-requirement-evaluator-basics-description";
        public const string ConfigurationElementName = "numeric-requirement-evaluator-basics-configuration";
        public const string InputElementName = "numeric-requirement-evaluator-basics-input";
        public const string StageElementName = "numeric-requirement-evaluator-basics-stage";
        public const string ResultElementName = "numeric-requirement-evaluator-basics-result";
        public const string ButtonRowElementName = "numeric-requirement-evaluator-basics-buttons";
        public const string AllPassButtonElementName = "numeric-requirement-evaluator-basics-all-pass";
        public const string MixedButtonElementName = "numeric-requirement-evaluator-basics-mixed";
        public const string ToleranceButtonElementName = "numeric-requirement-evaluator-basics-tolerance";
        public const string StrictButtonElementName = "numeric-requirement-evaluator-basics-strict";
        public const string InvalidButtonElementName = "numeric-requirement-evaluator-basics-invalid";

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
        private NumericRequirementEvaluation _lastEvaluation;
        private NumericRequirementError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と評価が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public NumericRequirementError LastError => _lastError;
        /// <summary>最後の成功した数値条件評価を取得します。</summary>
        public NumericRequirementEvaluation LastEvaluation => _lastEvaluation;
        /// <summary>最後の入力配列が変更されなかったかを取得します。</summary>
        public bool LastInputPreserved => _lastInputPreserved;
        /// <summary>実Button操作数を取得します。</summary>
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
            _root.style.backgroundColor = new Color(0.025f, 0.045f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.055f, 0.12f, 0.2f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Numeric Requirement Evaluator Basics", 31f, new Color(0.95f, 0.98f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "複数の実値・基準値・比較方法を変更せず全件評価し、未達条件も同じ順序で返します。", 15f, new Color(0.8f, 0.92f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 REQUIREMENTS  ·  6 COMPARISONS  ·  ALL LINES  ·  NO STATE", 12f, new Color(0.52f, 0.86f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.9f, 0.96f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.46f, 1f, 0.82f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.9f, 0.97f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.02f, 0.065f, 0.12f, 1f);
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
                CreateButton(AllPassButtonElementName, "All pass · ≥ and ≤", () => Evaluate("ALL PASS", new[] { Requirement(1, 5d, 3d, NumericRequirementComparison.AtLeast), Requirement(2, 2d, 4d, NumericRequirementComparison.AtMost) })),
                CreateButton(MixedButtonElementName, "Mixed · one unmet", () => Evaluate("MIXED", new[] { Requirement(1, 5d, 3d, NumericRequirementComparison.AtLeast), Requirement(2, 5d, 4d, NumericRequirementComparison.AtMost) })),
                CreateButton(ToleranceButtonElementName, "Tolerance · ±0.01", () => Evaluate("TOLERANCE", new[] { Requirement(3, 1.005d, 1d, NumericRequirementComparison.EqualWithinTolerance, 0.01d) })),
                CreateButton(StrictButtonElementName, "Strict · 5 > 5", () => Evaluate("STRICT", new[] { Requirement(4, 5d, 5d, NumericRequirementComparison.GreaterThan) })),
                CreateButton(InvalidButtonElementName, "Invalid · duplicate ID", () => Evaluate("INVALID", new[] { Requirement(1, 5d, 3d, NumericRequirementComparison.AtLeast), Requirement(1, 2d, 4d, NumericRequirementComparison.AtMost) }))
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
            button.style.color = new Color(0.025f, 0.095f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.66f, 0.88f, 1f, 1f);
            return button;
        }

        private void Evaluate(string label, NumericRequirement[] requirements)
        {
            var before = (NumericRequirement[])requirements.Clone();
            _lastSucceeded = NumericRequirementEvaluator.TryEvaluate(requirements, out _lastEvaluation, out _lastError);
            _lastInputPreserved = ArraysEqual(requirements, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   REQUIREMENTS {requirements.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = _lastEvaluation.AllSatisfied ? "ALL SATISFIED  ·  every requirement passed" : "UNMET REQUIREMENTS  ·  every line is still observable";
                _result.text = FormatLines(_lastEvaluation);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "EVALUATION —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL LINES";
            }
        }

        private void ResetStateCore()
        {
            _lastEvaluation = null;
            _lastError = NumericRequirementError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   REQUIREMENTS —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose all-pass, mixed, tolerance, strict, or invalid input";
            _result.text = "ACTUAL —   ·   EXPECTED —   ·   DELTA —   ·   SATISFIED —";
        }

        private static NumericRequirement Requirement(int identifier, double actual, double expected, NumericRequirementComparison comparison, double tolerance = 0d)
            => new NumericRequirement(identifier, actual, expected, comparison, tolerance);

        private static string FormatLines(NumericRequirementEvaluation evaluation)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < evaluation.LineCount; index++)
            {
                evaluation.TryGetLine(index, out var line);
                if (index > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(line.Identifier)
                    .Append(": ").Append(Format(line.ActualValue))
                    .Append(' ').Append(Symbol(line.Comparison)).Append(' ')
                    .Append(Format(line.ExpectedValue))
                    .Append("  Δ ").Append(Format(line.Delta))
                    .Append(line.IsSatisfied ? "  PASS" : "  UNMET");
            }

            return builder.ToString();
        }

        private static string Symbol(NumericRequirementComparison comparison)
        {
            switch (comparison)
            {
                case NumericRequirementComparison.AtLeast: return "≥";
                case NumericRequirementComparison.AtMost: return "≤";
                case NumericRequirementComparison.GreaterThan: return ">";
                case NumericRequirementComparison.LessThan: return "<";
                case NumericRequirementComparison.EqualWithinTolerance: return "≈";
                case NumericRequirementComparison.OutsideTolerance: return "≉";
                default: return "?";
            }
        }

        private static bool ArraysEqual(NumericRequirement[] left, NumericRequirement[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
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
