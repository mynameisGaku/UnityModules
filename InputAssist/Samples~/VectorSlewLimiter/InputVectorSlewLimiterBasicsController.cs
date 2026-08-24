using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputSmoothing.Samples
{
    /// <summary>明示stepごとの2D slew制限、reset、入力拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputVectorSlewLimiterBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-vector-slew-limiter-basics-card";
        public const string TitleElementName = "input-vector-slew-limiter-basics-title";
        public const string DescriptionElementName = "input-vector-slew-limiter-basics-description";
        public const string ConfigurationElementName = "input-vector-slew-limiter-basics-configuration";
        public const string InputElementName = "input-vector-slew-limiter-basics-input";
        public const string StageElementName = "input-vector-slew-limiter-basics-stage";
        public const string ResultElementName = "input-vector-slew-limiter-basics-result";
        public const string ButtonRowElementName = "input-vector-slew-limiter-basics-buttons";
        public const string OneStepButtonElementName = "input-vector-slew-limiter-basics-one-step";
        public const string TwoStepsButtonElementName = "input-vector-slew-limiter-basics-two-steps";
        public const string DiagonalButtonElementName = "input-vector-slew-limiter-basics-diagonal";
        public const string ReachButtonElementName = "input-vector-slew-limiter-basics-reach";
        public const string RejectButtonElementName = "input-vector-slew-limiter-basics-reject";

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
        private InputVectorSlewLimiter _limiter;
        private InputVectorSlewResult _lastResult;
        private double _lastTargetHorizontal;
        private double _lastTargetVertical;
        private bool _rejectionPreserved;
        private int _buttonActionCount;

        public double CurrentHorizontal => _limiter?.CurrentHorizontal ?? 0d;
        public double CurrentVertical => _limiter?.CurrentVertical ?? 0d;
        public InputVectorSlewResult LastResult => _lastResult;
        public double LastTargetHorizontal => _lastTargetHorizontal;
        public double LastTargetVertical => _lastTargetVertical;
        public bool RejectionPreserved => _rejectionPreserved;
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
            _title = AddLabel(TitleElementName, "Input Vector Slew Limiter Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "2D targetへのvector差を、明示stepごとの最大magnitudeで制限します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "MAX DELTA / STEP 0.25  ·  RANGE -1..1  ·  EXPLICIT STATE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(OneStepButtonElementName, "1 Step → (1, 0)", () => RunSteps(1, 1d, 0d, "One step  ·  current (0.25, 0)")),
                CreateButton(TwoStepsButtonElementName, "2 Steps → (1, 0)", () => RunSteps(2, 1d, 0d, "Two steps  ·  current (0.50, 0)")),
                CreateButton(DiagonalButtonElementName, "Diagonal → (0.6, 0.8)", () => RunSteps(1, 0.6d, 0.8d, "Direction preserved  ·  current (0.15, 0.20)")),
                CreateButton(ReachButtonElementName, "Reach (0.1, 0.1)", () => RunSteps(1, 0.1d, 0.1d, "Inside limit  ·  target reached")),
                CreateButton(RejectButtonElementName, "Reject (2, 0)", RejectOutOfRange)
            };
            for (var index = 0; index < _buttons.Length; index++) _buttonRow.Add(_buttons[index]);
            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ApplyResponsiveLayout();
        }

        private Label AddLabel(string name, string text, float size, Color color)
        {
            var label = new Label(text) { name = name };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            _card.Add(label);
            return label;
        }

        private static Button CreateButton(string name, string text, Action action)
        {
            var button = new Button(action) { name = name, text = text };
            button.style.flexGrow = 1f;
            button.style.color = new Color(0.05f, 0.06f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.75f, 0.81f, 1f, 1f);
            return button;
        }

        private void RunSteps(int count, double targetHorizontal, double targetVertical, string stage)
        {
            _limiter.TryReset(0d, 0d, out _);
            for (var index = 0; index < count; index++) _lastResult = _limiter.Process(targetHorizontal, targetVertical);
            _lastTargetHorizontal = targetHorizontal;
            _lastTargetVertical = targetVertical;
            _rejectionPreserved = false;
            _buttonActionCount++;
            _stage.text = stage;
            RefreshLabels();
        }

        private void RejectOutOfRange()
        {
            var beforeHorizontal = CurrentHorizontal;
            var beforeVertical = CurrentVertical;
            _lastTargetHorizontal = 2d;
            _lastTargetVertical = 0d;
            _lastResult = _limiter.Process(2d, 0d);
            _rejectionPreserved = !_lastResult.Succeeded && _lastResult.Error == InputVectorSlewLimiterError.InputOutOfRange && CurrentHorizontal == beforeHorizontal && CurrentVertical == beforeVertical;
            _buttonActionCount++;
            _stage.text = _rejectionPreserved ? "Out-of-range rejected  ·  current state unchanged" : "Range guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputVectorSlewLimiter.TryCreate(0.25d, 0d, 0d, out _limiter, out var error)) throw new InvalidOperationException($"Input Vector Slew Limiter configuration failed: {error}");
            _lastResult = _limiter.Process(0d, 0d);
            _lastTargetHorizontal = 0d;
            _lastTargetVertical = 0d;
            _rejectionPreserved = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic step scenarios";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TARGET ({Format(_lastTargetHorizontal)}, {Format(_lastTargetVertical)})   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"CURRENT ({Format(CurrentHorizontal)}, {Format(CurrentVertical)})   ·   DELTA {Format(_lastResult.AppliedDeltaMagnitude)}   ·   REACHED {_lastResult.ReachedTarget}   ·   ERROR {_lastResult.Error}";
        }

        private static string Format(double value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        private void HandleGeometryChanged(GeometryChangedEvent _) => ApplyResponsiveLayout();

        private void ApplyResponsiveLayout()
        {
            if (_root == null || _buttons == null) return;
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
            _result.style.fontSize = compact ? 8.5f : 12f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
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
