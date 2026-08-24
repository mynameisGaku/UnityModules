using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputPressing.Samples
{
    /// <summary>明示tickの短押しと長押し分類を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputPressClassifierBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-press-classifier-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-press-classifier-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-press-classifier-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-press-classifier-basics-configuration";

        /// <summary>押下状態要素名。</summary>
        public const string InputElementName = "input-press-classifier-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-press-classifier-basics-stage";

        /// <summary>分類結果要素名。</summary>
        public const string ResultElementName = "input-press-classifier-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-press-classifier-basics-buttons";

        /// <summary>tap押下Button要素名。</summary>
        public const string TapPressButtonElementName = "input-press-classifier-basics-tap-press";

        /// <summary>tap解放Button要素名。</summary>
        public const string TapReleaseButtonElementName = "input-press-classifier-basics-tap-release";

        /// <summary>hold押下Button要素名。</summary>
        public const string HoldPressButtonElementName = "input-press-classifier-basics-hold-press";

        /// <summary>hold閾値到達Button要素名。</summary>
        public const string HoldThresholdButtonElementName = "input-press-classifier-basics-hold-threshold";

        /// <summary>hold解放Button要素名。</summary>
        public const string HoldReleaseButtonElementName = "input-press-classifier-basics-hold-release";

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
        private InputPressClassifier _classifier;
        private InputPressStatus _lastStatus;
        private InputPressError _lastError;
        private int _buttonActionCount;

        /// <summary>現在の明示simulation tick。</summary>
        public ulong CurrentTick => _classifier?.CurrentTick ?? 0;

        /// <summary>入力が押下中か。</summary>
        public bool IsPressed => _lastStatus.IsPressed;

        /// <summary>現在の押下がhold判定済みか。</summary>
        public bool IsHolding => _lastStatus.IsHolding;

        /// <summary>最後のsampleで押下edgeが始まったか。</summary>
        public bool LastPressStarted => _lastStatus.PressStarted;

        /// <summary>最後のsampleでholdが始まったか。</summary>
        public bool LastHoldStarted => _lastStatus.HoldStarted;

        /// <summary>最後のsampleで解放edgeが発生したか。</summary>
        public bool LastReleased => _lastStatus.Released;

        /// <summary>最後の解放がtapへ分類されたか。</summary>
        public bool LastTapped => _lastStatus.Tapped;

        /// <summary>最後の解放がhold完了へ分類されたか。</summary>
        public bool LastHoldCompleted => _lastStatus.HoldCompleted;

        /// <summary>最後のsampleが示す押下継続tick数。</summary>
        public ulong LastPressDurationTicks => _lastStatus.PressDurationTicks;

        /// <summary>最後のAPI error。</summary>
        public InputPressError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Press Classifier Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "押下状態と明示simulation tickから、短押しtapと長押しholdを一度だけ分類します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "HOLD THRESHOLD 3 TICKS  ·  INITIAL TICK 100", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(TapPressButtonElementName, "Tap Press @100", () => Sample(100, true, "Tap press started")),
                CreateButton(TapReleaseButtonElementName, "Tap Release @102", () => Sample(102, false, "Tap classified  ·  duration 2")),
                CreateButton(HoldPressButtonElementName, "Hold Press @103", () => Sample(103, true, "Hold press started")),
                CreateButton(HoldThresholdButtonElementName, "Hold Check @106", () => Sample(106, true, "Hold started  ·  duration 3")),
                CreateButton(HoldReleaseButtonElementName, "Hold Release @108", () => Sample(108, false, "Hold completed  ·  duration 5"))
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

        private void Sample(ulong tick, bool isPressed, string stage)
        {
            var succeeded = _classifier.TrySample(tick, isPressed, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Sample failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputPressClassifier.TryCreate(3, 100, out _classifier, out var error)) throw new InvalidOperationException($"Input Press Classifier Basics configuration is invalid: {error}.");
            _lastStatus = _classifier.Snapshot();
            _lastError = InputPressError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  tap press, tap release, hold press, hold check, hold release";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_classifier.CurrentTick}   ·   PRESSED {_lastStatus.IsPressed}   ·   HOLDING {_lastStatus.IsHolding}";
            _result.text = $"START {_lastStatus.PressStarted}   ·   HOLD START {_lastStatus.HoldStarted}   ·   RELEASE {_lastStatus.Released}   ·   TAP {_lastStatus.Tapped}   ·   HOLD COMPLETE {_lastStatus.HoldCompleted}   ·   DURATION {_lastStatus.PressDurationTicks}   ·   ACTIONS {_buttonActionCount}";
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
            _result.style.fontSize = compact ? 8.5f : 11f;
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
