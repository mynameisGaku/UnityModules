using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayResources.Samples
{
    /// <summary>回復、部分消費、全量必須消費、不正amount拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResourceMeterBasicsController : MonoBehaviour
    {
        public const string CardElementName = "resource-meter-basics-card";
        public const string TitleElementName = "resource-meter-basics-title";
        public const string DescriptionElementName = "resource-meter-basics-description";
        public const string ConfigurationElementName = "resource-meter-basics-configuration";
        public const string InputElementName = "resource-meter-basics-input";
        public const string StageElementName = "resource-meter-basics-stage";
        public const string ResultElementName = "resource-meter-basics-result";
        public const string ButtonRowElementName = "resource-meter-basics-buttons";
        public const string RestoreButtonElementName = "resource-meter-basics-restore";
        public const string PartialSpendButtonElementName = "resource-meter-basics-partial-spend";
        public const string RequireSpendButtonElementName = "resource-meter-basics-require-spend";
        public const string ExactSpendButtonElementName = "resource-meter-basics-exact-spend";
        public const string RejectButtonElementName = "resource-meter-basics-reject";

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
        private ResourceMeter _meter;
        private ResourceChangeResult _lastResult;
        private double _lastRequestedAmount;
        private ResourceSpendPolicy _lastSpendPolicy;
        private bool _rejectionPreserved;
        private int _buttonActionCount;

        /// <summary>現在のresource値。</summary>
        public double Current => _meter?.Current ?? 0d;

        /// <summary>現在scenarioのimmutable capacity。</summary>
        public double Capacity => _meter?.Capacity ?? 0d;

        /// <summary>現在値の0以上1以下の正規化値。</summary>
        public double Normalized => _meter?.Normalized ?? 0d;

        /// <summary>最後に操作した非負要求amount。不正拒否scenarioでは負値。</summary>
        public double LastRequestedAmount => _lastRequestedAmount;

        /// <summary>最後の消費policy。回復scenarioではAllowPartial。</summary>
        public ResourceSpendPolicy LastSpendPolicy => _lastSpendPolicy;

        /// <summary>最後の処理結果。</summary>
        public ResourceChangeResult LastResult => _lastResult;

        /// <summary>負amountが現在stateを変えずに拒否されたか。</summary>
        public bool RejectionPreserved => _rejectionPreserved;

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

            _title = AddLabel(TitleElementName, "Resource Meter Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "immutable capacityの中で回復・部分消費・全量必須消費を明示結果で処理します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "CAPACITY 100  ·  INITIAL 40  ·  NO TIME  ·  STATEFUL", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(RestoreButtonElementName, "Restore +30", () => ApplyRestore(30d, "Restore 30  ·  current 70  ·  fully applied")),
                CreateButton(PartialSpendButtonElementName, "Partial spend 50", () => ApplySpend(50d, ResourceSpendPolicy.AllowPartial, "Partial spend 50  ·  applied 40  ·  empty")),
                CreateButton(RequireSpendButtonElementName, "Require spend 50", () => ApplySpend(50d, ResourceSpendPolicy.RequireFull, "Require full 50  ·  insufficient  ·  unchanged")),
                CreateButton(ExactSpendButtonElementName, "Exact spend 40", () => ApplySpend(40d, ResourceSpendPolicy.RequireFull, "Require full 40  ·  exact empty transition")),
                CreateButton(RejectButtonElementName, "Reject -1", RejectNegativeAmount)
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

        private void ApplyRestore(double amount, string stage)
        {
            CreateScenarioMeter();
            _lastRequestedAmount = amount;
            _lastSpendPolicy = ResourceSpendPolicy.AllowPartial;
            _lastResult = _meter.Restore(amount);
            _rejectionPreserved = false;
            _buttonActionCount++;
            _stage.text = _lastResult.Succeeded ? stage : $"Processing failed  ·  {_lastResult.Error}";
            RefreshLabels();
        }

        private void ApplySpend(double amount, ResourceSpendPolicy policy, string stage)
        {
            CreateScenarioMeter();
            _lastRequestedAmount = amount;
            _lastSpendPolicy = policy;
            _lastResult = _meter.Spend(amount, policy);
            _rejectionPreserved = false;
            _buttonActionCount++;
            _stage.text = _lastResult.Succeeded ? stage : $"Processing failed  ·  {_lastResult.Error}";
            RefreshLabels();
        }

        private void RejectNegativeAmount()
        {
            var before = Current;
            _lastRequestedAmount = -1d;
            _lastSpendPolicy = ResourceSpendPolicy.AllowPartial;
            _lastResult = _meter.Restore(-1d);
            _rejectionPreserved = !_lastResult.Succeeded && _lastResult.Error == ResourceMeterError.NegativeAmount && Current == before;
            _buttonActionCount++;
            _stage.text = _rejectionPreserved ? "Negative amount rejected  ·  current state unchanged" : "Amount guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            CreateScenarioMeter();
            _lastRequestedAmount = 0d;
            _lastSpendPolicy = ResourceSpendPolicy.AllowPartial;
            _lastResult = _meter.Restore(0d);
            _rejectionPreserved = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  compare deterministic resource changes";
            RefreshLabels();
        }

        private void CreateScenarioMeter()
        {
            if (!ResourceMeter.TryCreate(100d, 40d, out _meter, out var error)) throw new InvalidOperationException($"Resource Meter configuration failed: {error}.");
        }

        private void RefreshLabels()
        {
            _input.text = $"REQUEST {Format(_lastRequestedAmount)}   ·   POLICY {_lastSpendPolicy}   ·   CAPACITY {Format(Capacity)}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"CURRENT {Format(Current)}   ·   NORMALIZED {Format(Normalized)}   ·   APPLIED {Format(_lastResult.AppliedDelta)}   ·   UNAPPLIED {Format(_lastResult.UnappliedDelta)}   ·   ERROR {_lastResult.Error}";
        }

        private static string Format(double value) => value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

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
                _buttons[index].style.fontSize = compact ? 11f : 13f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
