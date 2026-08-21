using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayDamage.Samples
{
    /// <summary>固定軽減・率軽減・入力順・0下限を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DamageMitigationEvaluatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "damage-mitigation-evaluator-basics-card";
        public const string TitleElementName = "damage-mitigation-evaluator-basics-title";
        public const string DescriptionElementName = "damage-mitigation-evaluator-basics-description";
        public const string ConfigurationElementName = "damage-mitigation-evaluator-basics-configuration";
        public const string InputElementName = "damage-mitigation-evaluator-basics-input";
        public const string StageElementName = "damage-mitigation-evaluator-basics-stage";
        public const string ResultElementName = "damage-mitigation-evaluator-basics-result";
        public const string ButtonRowElementName = "damage-mitigation-evaluator-basics-buttons";
        public const string FlatButtonElementName = "damage-mitigation-evaluator-basics-flat";
        public const string RatioButtonElementName = "damage-mitigation-evaluator-basics-ratio";
        public const string OrderedButtonElementName = "damage-mitigation-evaluator-basics-ordered";
        public const string ClampButtonElementName = "damage-mitigation-evaluator-basics-clamp";
        public const string InvalidButtonElementName = "damage-mitigation-evaluator-basics-invalid";

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
        private DamageMitigationEvaluation _lastEvaluation;
        private DamageMitigationError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と評価が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public DamageMitigationError LastError => _lastError;
        /// <summary>最後に成功したdamage軽減評価を取得します。</summary>
        public DamageMitigationEvaluation LastEvaluation => _lastEvaluation;
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
            _root.style.backgroundColor = new Color(0.055f, 0.025f, 0.02f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.16f, 0.065f, 0.045f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Damage Mitigation Evaluator Basics", 31f, new Color(1f, 0.96f, 0.91f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "元damageへ軽減層を入力順に適用し、各段階の要求量・実適用量・残damageを返します。", 15f, new Color(1f, 0.83f, 0.72f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "0–32 ORDERED LAYERS  ·  FLAT / RATIO  ·  CLAMP AT ZERO  ·  NO STATE", 12f, new Color(1f, 0.59f, 0.34f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(1f, 0.91f, 0.82f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(1f, 0.76f, 0.35f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(1f, 0.94f, 0.86f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.075f, 0.018f, 0.012f, 1f);
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
                CreateButton(FlatButtonElementName, "Flat · 100 − 25", () => Evaluate("FLAT", 100d, new[] { Flat(1, 25d) })),
                CreateButton(RatioButtonElementName, "Ratio · 100 × 75%", () => Evaluate("RATIO", 100d, new[] { Ratio(1, 0.25d) })),
                CreateButton(OrderedButtonElementName, "Ordered · flat → ratio", () => Evaluate("ORDERED", 100d, new[] { Flat(1, 20d), Ratio(2, 0.25d) })),
                CreateButton(ClampButtonElementName, "Clamp · 100 − 120", () => Evaluate("CLAMP", 100d, new[] { Flat(1, 120d) })),
                CreateButton(InvalidButtonElementName, "Invalid · duplicate ID", () => Evaluate("INVALID", 100d, new[] { Flat(1, 20d), Ratio(1, 0.25d) }))
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
            button.style.color = new Color(0.16f, 0.035f, 0.015f, 1f);
            button.style.backgroundColor = new Color(1f, 0.71f, 0.44f, 1f);
            return button;
        }

        private void Evaluate(string label, double damage, DamageMitigationLayer[] layers)
        {
            var before = (DamageMitigationLayer[])layers.Clone();
            _lastSucceeded = DamageMitigationEvaluator.TryEvaluate(damage, layers, out _lastEvaluation, out _lastError);
            _lastInputPreserved = ArraysEqual(layers, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   DAMAGE {Format(damage)}   ·   LAYERS {layers.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"FINAL {Format(_lastEvaluation.FinalDamage)}   ·   MITIGATED {Format(_lastEvaluation.MitigatedDamage)}   ·   STEPS {_lastEvaluation.StepCount}";
                _result.text = FormatSteps(_lastEvaluation);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "EVALUATION —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL RESULT";
            }
        }

        private void ResetStateCore()
        {
            _lastEvaluation = null;
            _lastError = DamageMitigationError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   DAMAGE —   ·   LAYERS —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose flat, ratio, ordered, clamp, or invalid input";
            _result.text = "INPUT DAMAGE  →  REQUESTED  →  APPLIED  →  OUTPUT DAMAGE";
        }

        private static DamageMitigationLayer Flat(int id, double value) => new DamageMitigationLayer(id, DamageMitigationKind.FlatReduction, value);
        private static DamageMitigationLayer Ratio(int id, double value) => new DamageMitigationLayer(id, DamageMitigationKind.RatioReduction, value);

        private static string FormatSteps(DamageMitigationEvaluation evaluation)
        {
            if (evaluation.StepCount == 0) return "NO LAYERS   ·   DAMAGE UNCHANGED";
            var builder = new StringBuilder();
            for (var index = 0; index < evaluation.StepCount; index++)
            {
                evaluation.TryGetStep(index, out var step);
                if (index > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(step.LayerId)
                    .Append(": ").Append(Format(step.InputDamage))
                    .Append(" − ").Append(Format(step.AppliedReduction))
                    .Append(" → ").Append(Format(step.OutputDamage));
            }

            return builder.ToString();
        }

        private static bool ArraysEqual(DamageMitigationLayer[] left, DamageMitigationLayer[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index].LayerId != right[index].LayerId || left[index].Kind != right[index].Kind || left[index].Value != right[index].Value) return false;
            }

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
            _title.style.fontSize = compact ? 22f : 31f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 10.5f : 15f;
            _description.style.marginBottom = compact ? 4f : 10f;
            _configuration.style.fontSize = compact ? 9f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _input.style.fontSize = compact ? 9.5f : 13f;
            _input.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 12.5f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 12f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
            _buttonRow.style.marginTop = compact ? 1f : 3f;
            for (var index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].style.flexBasis = compact ? 160f : 130f;
                _buttons[index].style.minWidth = compact ? 140f : 110f;
                _buttons[index].style.minHeight = compact ? 30f : 42f;
                _buttons[index].style.fontSize = compact ? 10f : 12f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
