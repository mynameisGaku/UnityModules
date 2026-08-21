using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputSmoothing.Samples
{
    /// <summary>反時計・時計回り、正反対tie-break、unit circle外拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputVectorDirectionLimiterBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-vector-direction-limiter-basics-card";
        public const string TitleElementName = "input-vector-direction-limiter-basics-title";
        public const string DescriptionElementName = "input-vector-direction-limiter-basics-description";
        public const string ConfigurationElementName = "input-vector-direction-limiter-basics-configuration";
        public const string InputElementName = "input-vector-direction-limiter-basics-input";
        public const string StageElementName = "input-vector-direction-limiter-basics-stage";
        public const string ResultElementName = "input-vector-direction-limiter-basics-result";
        public const string ButtonRowElementName = "input-vector-direction-limiter-basics-buttons";
        public const string TurnStepButtonElementName = "input-vector-direction-limiter-basics-turn-step";
        public const string TurnAgainButtonElementName = "input-vector-direction-limiter-basics-turn-again";
        public const string ClockwiseButtonElementName = "input-vector-direction-limiter-basics-clockwise";
        public const string OppositeButtonElementName = "input-vector-direction-limiter-basics-opposite";
        public const string RejectButtonElementName = "input-vector-direction-limiter-basics-reject";

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
        private InputVectorDirectionLimiter _limiter;
        private InputVectorDirectionLimitResult _lastResult;
        private double _lastTargetHorizontal;
        private double _lastTargetVertical;
        private bool _rejectionPreserved;
        private int _buttonActionCount;

        /// <summary>現在の方向制限済みhorizontal成分。</summary>
        public double CurrentHorizontal => _limiter?.CurrentHorizontal ?? 0d;

        /// <summary>現在の方向制限済みvertical成分。</summary>
        public double CurrentVertical => _limiter?.CurrentVertical ?? 0d;

        /// <summary>現在scenarioの1 step最大回転radian。</summary>
        public double CurrentMaximumTurnRadians => _limiter?.MaximumTurnRadians ?? 0d;

        /// <summary>最後に操作したtarget horizontal成分。</summary>
        public double LastTargetHorizontal => _lastTargetHorizontal;

        /// <summary>最後に操作したtarget vertical成分。</summary>
        public double LastTargetVertical => _lastTargetVertical;

        /// <summary>最後の処理結果。</summary>
        public InputVectorDirectionLimitResult LastResult => _lastResult;

        /// <summary>範囲外targetが現在状態を変えずに拒否されたか。</summary>
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

            _title = AddLabel(TitleElementName, "Input Vector Direction Limiter Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "target magnitudeを即時反映し、方向回転だけを明示stepごとに制限します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "MAX TURN PI/4  ·  UNIT CIRCLE  ·  NO TIME  ·  STATEFUL", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(TurnStepButtonElementName, "Turn 45°", () => ApplyScenario(Math.PI / 4d, 1d, 0d, 0d, 1d, 1, "Counter-clockwise 45°  ·  remaining 45°")),
                CreateButton(TurnAgainButtonElementName, "Turn twice", () => ApplyScenario(Math.PI / 4d, 1d, 0d, 0d, 1d, 2, "Second step  ·  exact target direction")),
                CreateButton(ClockwiseButtonElementName, "Clockwise", () => ApplyScenario(Math.PI / 4d, 1d, 0d, 0d, -0.5d, 1, "Clockwise 45°  ·  target magnitude 0.50")),
                CreateButton(OppositeButtonElementName, "Opposite", () => ApplyScenario(Math.PI / 2d, 1d, 0d, -1d, 0d, 1, "Opposite tie  ·  deterministic counter-clockwise")),
                CreateButton(RejectButtonElementName, "Reject (0.8, 0.8)", RejectOutOfRange)
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

        private void ApplyScenario(double maximumTurnRadians, double initialHorizontal, double initialVertical, double targetHorizontal, double targetVertical, int steps, string stage)
        {
            if (!InputVectorDirectionLimiter.TryCreate(maximumTurnRadians, initialHorizontal, initialVertical, out _limiter, out var error)) throw new InvalidOperationException($"Input Vector Direction Limiter configuration failed: {error}.");
            _lastTargetHorizontal = targetHorizontal;
            _lastTargetVertical = targetVertical;
            for (var index = 0; index < steps; index++) _lastResult = _limiter.Process(targetHorizontal, targetVertical);
            _rejectionPreserved = false;
            _buttonActionCount++;
            _stage.text = _lastResult.Succeeded ? stage : $"Processing failed  ·  {_lastResult.Error}";
            RefreshLabels();
        }

        private void RejectOutOfRange()
        {
            var beforeHorizontal = CurrentHorizontal;
            var beforeVertical = CurrentVertical;
            _lastTargetHorizontal = 0.8d;
            _lastTargetVertical = 0.8d;
            _lastResult = _limiter.Process(_lastTargetHorizontal, _lastTargetVertical);
            _rejectionPreserved = !_lastResult.Succeeded && _lastResult.Error == InputVectorDirectionLimiterError.InputOutsideUnitCircle && CurrentHorizontal == beforeHorizontal && CurrentVertical == beforeVertical;
            _buttonActionCount++;
            _stage.text = _rejectionPreserved ? "Out-of-range target rejected  ·  current state unchanged" : "Range guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputVectorDirectionLimiter.TryCreate(Math.PI / 4d, 1d, 0d, out _limiter, out var error)) throw new InvalidOperationException($"Input Vector Direction Limiter configuration failed: {error}.");
            _lastTargetHorizontal = 1d;
            _lastTargetVertical = 0d;
            _lastResult = _limiter.Process(1d, 0d);
            _rejectionPreserved = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  compare deterministic direction steps";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TARGET ({Format(_lastTargetHorizontal)}, {Format(_lastTargetVertical)})   ·   MAX TURN {Format(CurrentMaximumTurnRadians)} RAD   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"CURRENT ({Format(CurrentHorizontal)}, {Format(CurrentVertical)})   ·   MAG {Format(_lastResult.TargetMagnitude)}   ·   TURN {Format(_lastResult.AppliedTurnRadians)}   ·   REMAINING {Format(_lastResult.RemainingTurnRadians)}   ·   ERROR {_lastResult.Error}";
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
