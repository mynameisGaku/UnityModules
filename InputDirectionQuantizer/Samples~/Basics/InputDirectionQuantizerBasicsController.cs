using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputDirectionQuantization.Samples
{
    /// <summary>radial dead zone、4-way・8-way方向、非有限値拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputDirectionQuantizerBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-direction-quantizer-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-direction-quantizer-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-direction-quantizer-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-direction-quantizer-basics-configuration";

        /// <summary>入力状態要素名。</summary>
        public const string InputElementName = "input-direction-quantizer-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-direction-quantizer-basics-stage";

        /// <summary>量子化結果要素名。</summary>
        public const string ResultElementName = "input-direction-quantizer-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-direction-quantizer-basics-buttons";

        /// <summary>dead zone確認Button要素名。</summary>
        public const string DeadZoneButtonElementName = "input-direction-quantizer-basics-dead-zone";

        /// <summary>right方向Button要素名。</summary>
        public const string RightButtonElementName = "input-direction-quantizer-basics-right";

        /// <summary>diagonal方向Button要素名。</summary>
        public const string DiagonalButtonElementName = "input-direction-quantizer-basics-diagonal";

        /// <summary>4-way tie規則Button要素名。</summary>
        public const string FourWayTieButtonElementName = "input-direction-quantizer-basics-four-way-tie";

        /// <summary>非有限値確認Button要素名。</summary>
        public const string RejectNonFiniteButtonElementName = "input-direction-quantizer-basics-reject-non-finite";

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
        private InputDirectionQuantizer _eightWay;
        private InputDirectionQuantizer _fourWay;
        private sbyte _currentHorizontal;
        private sbyte _currentVertical;
        private double _lastHorizontal;
        private double _lastVertical;
        private InputDirectionMode _lastMode;
        private InputDirectionQuantizationError _lastError;
        private bool _nonFiniteRejected;
        private int _buttonActionCount;

        /// <summary>最後に成功したhorizontal方向。</summary>
        public sbyte CurrentHorizontal => _currentHorizontal;

        /// <summary>最後に成功したvertical方向。</summary>
        public sbyte CurrentVertical => _currentVertical;

        /// <summary>最後に操作したhorizontal入力。NaNもそのまま保持する。</summary>
        public double LastHorizontal => _lastHorizontal;

        /// <summary>最後に操作したvertical入力。</summary>
        public double LastVertical => _lastVertical;

        /// <summary>最後に操作した方向mode。</summary>
        public InputDirectionMode LastMode => _lastMode;

        /// <summary>最後の量子化error。</summary>
        public InputDirectionQuantizationError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Direction Quantizer Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "2D analog入力をradial dead zone付きの4-way・8-way方向へ決定論的に変換します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "RADIAL DEAD ZONE 0.10  ·  4-WAY / 8-WAY  ·  INCLUSIVE BOUNDARIES", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(DeadZoneButtonElementName, "Radial (0.06, 0.08)", () => Apply(_eightWay, 0.06d, 0.08d, "Inclusive radial boundary  ·  neutral")),
                CreateButton(RightButtonElementName, "8-way (0.9, 0.1)", () => Apply(_eightWay, 0.9d, 0.1d, "8-way cardinal  ·  right (1, 0)")),
                CreateButton(DiagonalButtonElementName, "8-way (-0.7, 0.7)", () => Apply(_eightWay, -0.7d, 0.7d, "8-way diagonal  ·  up-left (-1, 1)")),
                CreateButton(FourWayTieButtonElementName, "4-way tie (0.5, -0.5)", () => Apply(_fourWay, 0.5d, -0.5d, "4-way exact tie  ·  vertical (0, -1)")),
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

        private void Apply(InputDirectionQuantizer quantizer, double horizontal, double vertical, string stage)
        {
            _lastHorizontal = horizontal;
            _lastVertical = vertical;
            _lastMode = quantizer.Mode;
            var quantized = quantizer.Quantize(horizontal, vertical);
            _lastError = quantized.Error;
            if (quantized.Succeeded)
            {
                _currentHorizontal = quantized.Horizontal;
                _currentVertical = quantized.Vertical;
            }
            _nonFiniteRejected = false;
            _buttonActionCount++;
            _stage.text = quantized.Succeeded ? stage : $"Quantization failed  ·  {quantized.Error}";
            RefreshLabels();
        }

        private void RejectNonFinite()
        {
            var beforeHorizontal = _currentHorizontal;
            var beforeVertical = _currentVertical;
            _lastHorizontal = double.NaN;
            _lastVertical = 0d;
            _lastMode = InputDirectionMode.EightWay;
            var quantized = _eightWay.Quantize(_lastHorizontal, _lastVertical);
            _lastError = quantized.Error;
            _nonFiniteRejected = !quantized.Succeeded && quantized.Error == InputDirectionQuantizationError.NonFiniteInput && _currentHorizontal == beforeHorizontal && _currentVertical == beforeVertical;
            _buttonActionCount++;
            _stage.text = _nonFiniteRejected ? "NaN rejected  ·  last successful command unchanged" : "Non-finite guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputDirectionQuantizer.TryCreate(0.1d, InputDirectionMode.EightWay, out _eightWay, out _lastError)) throw new InvalidOperationException("Input Direction Quantizer eight-way configuration is invalid.");
            if (!InputDirectionQuantizer.TryCreate(0.1d, InputDirectionMode.FourWay, out _fourWay, out _lastError)) throw new InvalidOperationException("Input Direction Quantizer four-way configuration is invalid.");
            _currentHorizontal = 0;
            _currentVertical = 0;
            _lastHorizontal = 0d;
            _lastVertical = 0d;
            _lastMode = InputDirectionMode.EightWay;
            _nonFiniteRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic input sequence";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            var horizontal = double.IsNaN(_lastHorizontal) ? "NaN" : _lastHorizontal.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
            var vertical = _lastVertical.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
            _input.text = $"INPUT ({horizontal}, {vertical})   ·   MODE {_lastMode}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"DIRECTION ({_currentHorizontal}, {_currentVertical})   ·   ERROR {_lastError}   ·   NON-FINITE PRESERVED {_nonFiniteRejected}";
        }

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
