using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayResources.Samples
{
    /// <summary>複数resourceの支払可否と不足明細を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResourceCostEvaluatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "resource-cost-evaluator-basics-card";
        public const string TitleElementName = "resource-cost-evaluator-basics-title";
        public const string DescriptionElementName = "resource-cost-evaluator-basics-description";
        public const string ConfigurationElementName = "resource-cost-evaluator-basics-configuration";
        public const string InputElementName = "resource-cost-evaluator-basics-input";
        public const string StageElementName = "resource-cost-evaluator-basics-stage";
        public const string ResultElementName = "resource-cost-evaluator-basics-result";
        public const string ButtonRowElementName = "resource-cost-evaluator-basics-buttons";
        public const string PayableButtonElementName = "resource-cost-evaluator-basics-payable";
        public const string ShortageButtonElementName = "resource-cost-evaluator-basics-shortage";
        public const string MissingButtonElementName = "resource-cost-evaluator-basics-missing";
        public const string ZeroCostButtonElementName = "resource-cost-evaluator-basics-zero-cost";
        public const string InvalidButtonElementName = "resource-cost-evaluator-basics-invalid";

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
        private ResourceCostEvaluation _lastEvaluation;
        private ResourceCostError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と評価が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public ResourceCostError LastError => _lastError;
        /// <summary>最後の成功したresource cost評価を取得します。</summary>
        public ResourceCostEvaluation LastEvaluation => _lastEvaluation;
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
            _root.style.backgroundColor = new Color(0.055f, 0.03f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.15f, 0.075f, 0.18f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Resource Cost Evaluator Basics", 31f, new Color(1f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "複数resourceを変更せず、全支払可否とresource別のremaining・deficitを返します。", 15f, new Color(0.94f, 0.82f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "0–32 BALANCES  ·  1–32 COSTS  ·  ALL-OR-NOTHING DECISION  ·  NO STATE", 12f, new Color(1f, 0.66f, 0.9f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.98f, 0.92f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.67f, 0.9f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.98f, 0.93f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.075f, 0.025f, 0.095f, 1f);
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
                CreateButton(PayableButtonElementName, "Payable · 2 resources", () => Evaluate("PAYABLE", new[] { Amount(1, 100d), Amount(2, 40d) }, new[] { Amount(1, 25d), Amount(2, 10d) })),
                CreateButton(ShortageButtonElementName, "Shortage · mana −7", () => Evaluate("SHORTAGE", new[] { Amount(1, 100d), Amount(2, 3d) }, new[] { Amount(1, 25d), Amount(2, 10d) })),
                CreateButton(MissingButtonElementName, "Missing · ticket", () => Evaluate("MISSING", Array.Empty<ResourceAmount>(), new[] { Amount(9, 4d) })),
                CreateButton(ZeroCostButtonElementName, "Zero cost · payable", () => Evaluate("ZERO COST", Array.Empty<ResourceAmount>(), new[] { Amount(9, 0d) })),
                CreateButton(InvalidButtonElementName, "Invalid · duplicate ID", () => Evaluate("INVALID", new[] { Amount(1, 100d) }, new[] { Amount(1, 25d), Amount(1, 10d) }))
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
            button.style.color = new Color(0.13f, 0.035f, 0.15f, 1f);
            button.style.backgroundColor = new Color(0.95f, 0.7f, 0.93f, 1f);
            return button;
        }

        private void Evaluate(string label, ResourceAmount[] balances, ResourceAmount[] costs)
        {
            var balanceBefore = (ResourceAmount[])balances.Clone();
            var costBefore = (ResourceAmount[])costs.Clone();
            _lastSucceeded = ResourceCostEvaluator.TryEvaluate(balances, costs, out _lastEvaluation, out _lastError);
            _lastInputPreserved = ArraysEqual(balances, balanceBefore) && ArraysEqual(costs, costBefore);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   BALANCES {balances.Length}   ·   COSTS {costs.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = _lastEvaluation.CanPay ? "PAYABLE  ·  every resource has enough balance" : "NOT PAYABLE  ·  inspect explicit deficits";
                _result.text = FormatLines(_lastEvaluation);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "EVALUATION —   ·   INPUT ARRAYS UNCHANGED   ·   NO PARTIAL PLAN";
            }
        }

        private void ResetStateCore()
        {
            _lastEvaluation = null;
            _lastError = ResourceCostError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   BALANCES —   ·   COSTS —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose payable, shortage, missing, zero cost, or invalid input";
            _result.text = "AVAILABLE —   ·   REQUIRED —   ·   REMAINING —   ·   DEFICIT —";
        }

        private static ResourceAmount Amount(int resourceId, double amount) => new ResourceAmount(resourceId, amount);

        private static string FormatLines(ResourceCostEvaluation evaluation)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < evaluation.LineCount; index++)
            {
                evaluation.TryGetLine(index, out var line);
                if (index > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(line.ResourceId)
                    .Append(": ").Append(Format(line.AvailableAmount))
                    .Append(" − ").Append(Format(line.RequiredAmount))
                    .Append(" → ").Append(Format(line.RemainingAmount))
                    .Append("  DEF ").Append(Format(line.DeficitAmount));
            }

            return builder.ToString();
        }

        private static bool ArraysEqual(ResourceAmount[] left, ResourceAmount[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
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
