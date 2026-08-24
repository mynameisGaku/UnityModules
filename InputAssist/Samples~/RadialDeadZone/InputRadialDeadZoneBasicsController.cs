using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputDeadZones.Samples
{
    /// <summary>inner・mid・outer・over-range・非有限値の2D radial補正を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputRadialDeadZoneBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-radial-dead-zone-basics-card";
        public const string TitleElementName = "input-radial-dead-zone-basics-title";
        public const string DescriptionElementName = "input-radial-dead-zone-basics-description";
        public const string ConfigurationElementName = "input-radial-dead-zone-basics-configuration";
        public const string InputElementName = "input-radial-dead-zone-basics-input";
        public const string StageElementName = "input-radial-dead-zone-basics-stage";
        public const string ResultElementName = "input-radial-dead-zone-basics-result";
        public const string ButtonRowElementName = "input-radial-dead-zone-basics-buttons";
        public const string InnerButtonElementName = "input-radial-dead-zone-basics-inner";
        public const string MidButtonElementName = "input-radial-dead-zone-basics-mid";
        public const string OuterButtonElementName = "input-radial-dead-zone-basics-outer";
        public const string OverRangeButtonElementName = "input-radial-dead-zone-basics-over-range";
        public const string RejectNonFiniteButtonElementName = "input-radial-dead-zone-basics-reject-non-finite";

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
        private InputRadialDeadZone _deadZone;
        private double _currentHorizontal;
        private double _currentVertical;
        private double _currentMagnitude;
        private double _lastHorizontal;
        private double _lastVertical;
        private InputRadialDeadZoneError _lastError;
        private bool _nonFiniteRejected;
        private int _buttonActionCount;

        /// <summary>最後に成功した補正済みhorizontal成分。</summary>
        public double CurrentHorizontal => _currentHorizontal;

        /// <summary>最後に成功した補正済みvertical成分。</summary>
        public double CurrentVertical => _currentVertical;

        /// <summary>最後に成功した補正済みmagnitude。</summary>
        public double CurrentMagnitude => _currentMagnitude;

        /// <summary>最後に操作したhorizontal入力。NaNもそのまま保持する。</summary>
        public double LastHorizontal => _lastHorizontal;

        /// <summary>最後に操作したvertical入力。</summary>
        public double LastVertical => _lastVertical;

        /// <summary>最後の補正error。</summary>
        public InputRadialDeadZoneError LastError => _lastError;

        /// <summary>非有限入力が成功値を変えずに拒否されたか。</summary>
        public bool NonFiniteRejected => _nonFiniteRejected;

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

            _title = AddLabel(TitleElementName, "Input Radial Dead Zone Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "2D analog入力の方向を保ち、innerからouterを0..1へ連続補正します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "INNER 0.10  ·  OUTER 1.00  ·  RADIAL  ·  STATELESS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(InnerButtonElementName, "Inner (0.06, 0.08)", () => Apply(0.06d, 0.08d, "Inclusive inner boundary  ·  zero")),
                CreateButton(MidButtonElementName, "Mid (0.55, 0)", () => Apply(0.55d, 0d, "Linear remap  ·  magnitude 0.50")),
                CreateButton(OuterButtonElementName, "Outer (0, 1)", () => Apply(0d, 1d, "Inclusive outer boundary  ·  unit")),
                CreateButton(OverRangeButtonElementName, "Over-range (3, 4)", () => Apply(3d, 4d, "Over-range normalized  ·  direction preserved")),
                CreateButton(RejectNonFiniteButtonElementName, "Reject NaN", RejectNonFinite)
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

        private void Apply(double horizontal, double vertical, string stage)
        {
            _lastHorizontal = horizontal;
            _lastVertical = vertical;
            var processed = _deadZone.Process(horizontal, vertical);
            _lastError = processed.Error;
            if (processed.Succeeded)
            {
                _currentHorizontal = processed.Horizontal;
                _currentVertical = processed.Vertical;
                _currentMagnitude = processed.Magnitude;
            }
            _nonFiniteRejected = false;
            _buttonActionCount++;
            _stage.text = processed.Succeeded ? stage : $"Processing failed  ·  {processed.Error}";
            RefreshLabels();
        }

        private void RejectNonFinite()
        {
            var beforeHorizontal = _currentHorizontal;
            var beforeVertical = _currentVertical;
            var beforeMagnitude = _currentMagnitude;
            _lastHorizontal = double.NaN;
            _lastVertical = 0d;
            var processed = _deadZone.Process(_lastHorizontal, _lastVertical);
            _lastError = processed.Error;
            _nonFiniteRejected = !processed.Succeeded && processed.Error == InputRadialDeadZoneError.NonFiniteInput && _currentHorizontal == beforeHorizontal && _currentVertical == beforeVertical && _currentMagnitude == beforeMagnitude;
            _buttonActionCount++;
            _stage.text = _nonFiniteRejected ? "NaN rejected  ·  last successful output unchanged" : "Non-finite guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputRadialDeadZone.TryCreate(0.1d, 1d, out _deadZone, out _lastError)) throw new InvalidOperationException("Input Radial Dead Zone configuration is invalid.");
            _currentHorizontal = 0d;
            _currentVertical = 0d;
            _currentMagnitude = 0d;
            _lastHorizontal = 0d;
            _lastVertical = 0d;
            _nonFiniteRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic input sequence";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            var horizontal = double.IsNaN(_lastHorizontal) ? "NaN" : Format(_lastHorizontal);
            _input.text = $"INPUT ({horizontal}, {Format(_lastVertical)})   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"OUTPUT ({Format(_currentHorizontal)}, {Format(_currentVertical)})   ·   MAGNITUDE {Format(_currentMagnitude)}   ·   ERROR {_lastError}";
        }

        private static string Format(double value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

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
