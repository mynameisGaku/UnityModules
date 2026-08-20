using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputThresholding.Samples
{
    /// <summary>2つのinclusive thresholdによるpressed状態とedgeを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputThresholdClassifierBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-threshold-classifier-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-threshold-classifier-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-threshold-classifier-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-threshold-classifier-basics-configuration";

        /// <summary>入力状態要素名。</summary>
        public const string InputElementName = "input-threshold-classifier-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-threshold-classifier-basics-stage";

        /// <summary>分類結果要素名。</summary>
        public const string ResultElementName = "input-threshold-classifier-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-threshold-classifier-basics-buttons";

        /// <summary>press threshold未到達Button要素名。</summary>
        public const string BelowPressButtonElementName = "input-threshold-classifier-basics-below-press";

        /// <summary>Pressed edge確認Button要素名。</summary>
        public const string PressButtonElementName = "input-threshold-classifier-basics-press";

        /// <summary>hysteresis保持Button要素名。</summary>
        public const string HoldButtonElementName = "input-threshold-classifier-basics-hold";

        /// <summary>Released edge確認Button要素名。</summary>
        public const string ReleaseButtonElementName = "input-threshold-classifier-basics-release";

        /// <summary>非有限値確認Button要素名。</summary>
        public const string RejectNonFiniteButtonElementName = "input-threshold-classifier-basics-reject-non-finite";

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
        private InputThresholdClassifier _classifier;
        private double _lastValue;
        private InputThresholdEvent _lastEvent;
        private InputThresholdClassificationError _lastError;
        private bool _nonFiniteRejected;
        private int _buttonActionCount;

        /// <summary>classifierが現在保持するpressed状態。</summary>
        public bool IsPressed => _classifier.IsPressed;

        /// <summary>最後に操作したscalar sample。NaNもそのまま保持する。</summary>
        public double LastValue => _lastValue;

        /// <summary>最後に成功したsampleで発生したedge。失敗時はNone。</summary>
        public InputThresholdEvent LastEvent => _lastEvent;

        /// <summary>最後の分類error。</summary>
        public InputThresholdClassificationError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Threshold Classifier Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "analog sampleをrelease・press thresholdのhysteresisで安定したpressed状態へ分類します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "RELEASE ≤ 0.25  ·  PRESS ≥ 0.75  ·  BETWEEN RETAINS STATE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(BelowPressButtonElementName, "Below press 0.10", () => Apply(0.10d, "Released retained  ·  no edge")),
                CreateButton(PressButtonElementName, "Press exact 0.75", () => Apply(0.75d, "Pressed edge  ·  inclusive press boundary")),
                CreateButton(HoldButtonElementName, "Hysteresis 0.50", () => Apply(0.50d, "Pressed retained  ·  between thresholds")),
                CreateButton(ReleaseButtonElementName, "Release exact 0.25", () => Apply(0.25d, "Released edge  ·  inclusive release boundary")),
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

        private void Apply(double value, string stage)
        {
            _lastValue = value;
            var classified = _classifier.Sample(value);
            _lastEvent = classified.Event;
            _lastError = classified.Error;
            _nonFiniteRejected = false;
            _buttonActionCount++;
            _stage.text = classified.Succeeded ? stage : $"Classification failed  ·  {classified.Error}";
            RefreshLabels();
        }

        private void RejectNonFinite()
        {
            var before = _classifier.IsPressed;
            _lastValue = double.NaN;
            var classified = _classifier.Sample(_lastValue);
            _lastEvent = classified.Event;
            _lastError = classified.Error;
            _nonFiniteRejected = !classified.Succeeded && classified.Error == InputThresholdClassificationError.NonFiniteInput && _classifier.IsPressed == before;
            _buttonActionCount++;
            _stage.text = _nonFiniteRejected ? "NaN rejected  ·  pressed state unchanged" : "Non-finite guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputThresholdClassifier.TryCreate(0.25d, 0.75d, false, out _classifier, out _lastError)) throw new InvalidOperationException("Input Threshold Classifier configuration is invalid.");
            _lastValue = 0d;
            _lastEvent = InputThresholdEvent.None;
            _nonFiniteRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic input sequence";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            var value = double.IsNaN(_lastValue) ? "NaN" : _lastValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            _input.text = $"SAMPLE {value}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"PRESSED {_classifier.IsPressed}   ·   EVENT {_lastEvent}   ·   ERROR {_lastError}   ·   NON-FINITE PRESERVED {_nonFiniteRejected}";
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
