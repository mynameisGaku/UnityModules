using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputResponse.Samples
{
    /// <summary>4種類のmagnitude curveと単位円外入力の拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputVectorResponseCurveBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-vector-response-curve-basics-card";
        public const string TitleElementName = "input-vector-response-curve-basics-title";
        public const string DescriptionElementName = "input-vector-response-curve-basics-description";
        public const string ConfigurationElementName = "input-vector-response-curve-basics-configuration";
        public const string InputElementName = "input-vector-response-curve-basics-input";
        public const string StageElementName = "input-vector-response-curve-basics-stage";
        public const string ResultElementName = "input-vector-response-curve-basics-result";
        public const string ButtonRowElementName = "input-vector-response-curve-basics-buttons";
        public const string LinearButtonElementName = "input-vector-response-curve-basics-linear";
        public const string SquaredButtonElementName = "input-vector-response-curve-basics-squared";
        public const string CubicButtonElementName = "input-vector-response-curve-basics-cubic";
        public const string SmoothStepButtonElementName = "input-vector-response-curve-basics-smooth-step";
        public const string RejectOverRangeButtonElementName = "input-vector-response-curve-basics-reject-over-range";

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
        private double _currentHorizontal;
        private double _currentVertical;
        private double _currentMagnitude;
        private double _lastHorizontal;
        private double _lastVertical;
        private InputVectorResponseMode _lastMode;
        private InputVectorResponseCurveError _lastError;
        private bool _overRangeRejected;
        private int _buttonActionCount;

        /// <summary>最後に成功した処理済みhorizontal成分。</summary>
        public double CurrentHorizontal => _currentHorizontal;

        /// <summary>最後に成功した処理済みvertical成分。</summary>
        public double CurrentVertical => _currentVertical;

        /// <summary>最後に成功した処理済みmagnitude。</summary>
        public double CurrentMagnitude => _currentMagnitude;

        /// <summary>最後に操作したhorizontal入力。</summary>
        public double LastHorizontal => _lastHorizontal;

        /// <summary>最後に操作したvertical入力。</summary>
        public double LastVertical => _lastVertical;

        /// <summary>最後の処理で使用したcurve mode。</summary>
        public InputVectorResponseMode LastMode => _lastMode;

        /// <summary>最後の処理error。</summary>
        public InputVectorResponseCurveError LastError => _lastError;

        /// <summary>単位円外入力が成功値を変えずに拒否されたか。</summary>
        public bool OverRangeRejected => _overRangeRejected;

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

            _title = AddLabel(TitleElementName, "Input Vector Response Curve Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "単位円内の2D入力方向を保ち、magnitudeだけへ選択したcurveを適用します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "INPUT (0.30, 0.40)  ·  MAGNITUDE 0.50  ·  STATELESS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(LinearButtonElementName, "Linear", () => Apply(InputVectorResponseMode.Linear, "Linear  ·  magnitude 0.50")),
                CreateButton(SquaredButtonElementName, "Squared", () => Apply(InputVectorResponseMode.Squared, "Squared  ·  magnitude 0.25")),
                CreateButton(CubicButtonElementName, "Cubic", () => Apply(InputVectorResponseMode.Cubic, "Cubic  ·  magnitude 0.125")),
                CreateButton(SmoothStepButtonElementName, "Smooth Step", () => Apply(InputVectorResponseMode.SmoothStep, "Smooth step  ·  midpoint unchanged")),
                CreateButton(RejectOverRangeButtonElementName, "Reject (1, 1)", RejectOverRange)
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

        private void Apply(InputVectorResponseMode mode, string stage)
        {
            if (!InputVectorResponseCurve.TryCreate(mode, out var curve, out _lastError)) throw new InvalidOperationException("Input Vector Response Curve configuration is invalid.");
            _lastHorizontal = 0.3d;
            _lastVertical = 0.4d;
            _lastMode = mode;
            var processed = curve.Process(_lastHorizontal, _lastVertical);
            _lastError = processed.Error;
            if (processed.Succeeded)
            {
                _currentHorizontal = processed.Horizontal;
                _currentVertical = processed.Vertical;
                _currentMagnitude = processed.Magnitude;
            }
            _overRangeRejected = false;
            _buttonActionCount++;
            _stage.text = processed.Succeeded ? stage : $"Processing failed  ·  {processed.Error}";
            RefreshLabels();
        }

        private void RejectOverRange()
        {
            if (!InputVectorResponseCurve.TryCreate(InputVectorResponseMode.Linear, out var curve, out _lastError)) throw new InvalidOperationException("Input Vector Response Curve configuration is invalid.");
            var beforeHorizontal = _currentHorizontal;
            var beforeVertical = _currentVertical;
            var beforeMagnitude = _currentMagnitude;
            _lastHorizontal = 1d;
            _lastVertical = 1d;
            _lastMode = InputVectorResponseMode.Linear;
            var processed = curve.Process(_lastHorizontal, _lastVertical);
            _lastError = processed.Error;
            _overRangeRejected = !processed.Succeeded && processed.Error == InputVectorResponseCurveError.InputOutOfRange && _currentHorizontal == beforeHorizontal && _currentVertical == beforeVertical && _currentMagnitude == beforeMagnitude;
            _buttonActionCount++;
            _stage.text = _overRangeRejected ? "Unit-circle overflow rejected  ·  last output unchanged" : "Range guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _currentHorizontal = 0d;
            _currentVertical = 0d;
            _currentMagnitude = 0d;
            _lastHorizontal = 0d;
            _lastVertical = 0d;
            _lastMode = InputVectorResponseMode.Linear;
            _lastError = InputVectorResponseCurveError.None;
            _overRangeRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  compare deterministic magnitude curves";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"INPUT ({Format(_lastHorizontal)}, {Format(_lastVertical)})   ·   MODE {_lastMode}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"OUTPUT ({Format(_currentHorizontal)}, {Format(_currentVertical)})   ·   MAGNITUDE {Format(_currentMagnitude)}   ·   ERROR {_lastError}";
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
