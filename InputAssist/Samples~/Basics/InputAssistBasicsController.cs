using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputAssist.Samples
{
    /// <summary>Shows vector filtering and button gesture recognition through real UI controls.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputAssistBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-assist-card";
        public const string TitleElementName = "input-assist-title";
        public const string DescriptionElementName = "input-assist-description";
        public const string VectorStageElementName = "input-assist-vector-stage";
        public const string RawMarkerElementName = "input-assist-raw-marker";
        public const string FilteredMarkerElementName = "input-assist-filtered-marker";
        public const string VectorResultElementName = "input-assist-vector-result";
        public const string ButtonResultElementName = "input-assist-button-result";
        public const string ButtonRowElementName = "input-assist-button-row";
        public const string NeutralButtonElementName = "input-assist-neutral";
        public const string SoftRightButtonElementName = "input-assist-soft-right";
        public const string DiagonalButtonElementName = "input-assist-diagonal";
        public const string TapButtonElementName = "input-assist-tap";
        public const string HoldRepeatButtonElementName = "input-assist-hold-repeat";
        public const string ResetButtonElementName = "input-assist-reset";

        [SerializeField] private InputVectorFilter _vectorFilter = new InputVectorFilter();
        [SerializeField] private InputButtonTracker _buttonTracker = new InputButtonTracker();

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _stage;
        private VisualElement _rawMarker;
        private VisualElement _filteredMarker;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _vectorResult;
        private Label _buttonResult;
        private Button[] _buttons;
        private Vector2 _lastRaw;
        private InputVectorFilterResult _lastVector;
        private InputButtonEvent _lastButtonEvents;
        private int _lastTapCount;
        private int _lastRepeatCount;
        private int _actionCount;

        /// <summary>Gets whether the UI and processors are ready.</summary>
        public bool IsReady => _root != null && _buttons != null;

        /// <summary>Gets the most recent raw vector.</summary>
        public Vector2 LastRawInput => _lastRaw;

        /// <summary>Gets the most recent processed vector.</summary>
        public Vector2 LastFilteredInput => _lastVector.Value;

        /// <summary>Gets the most recent classified direction.</summary>
        public InputDirection LastDirection => _lastVector.Direction;

        /// <summary>Gets the gesture events produced by the most recent gesture demo.</summary>
        public InputButtonEvent LastButtonEvents => _lastButtonEvents;

        /// <summary>Gets the completed tap count from the most recent gesture demo.</summary>
        public int LastTapCount => _lastTapCount;

        /// <summary>Gets the total repeat count from the most recent gesture demo.</summary>
        public int LastRepeatCount => _lastRepeatCount;

        /// <summary>Gets the number of real UI button callbacks handled by this controller.</summary>
        public int ActionCount => _actionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            BuildUi();
            ResetDemo(false);
        }

        private void OnDisable()
        {
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            if (_document != null && _document.rootVisualElement != null) _document.rootVisualElement.Clear();
            _buttons = null;
            _root = null;
            _card = null;
            _stage = null;
            _rawMarker = null;
            _filteredMarker = null;
            _buttonRow = null;
        }

        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.025f, 0.035f, 0.07f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(90f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 960f;
            _card.style.paddingLeft = 22f;
            _card.style.paddingRight = 22f;
            _card.style.paddingTop = 18f;
            _card.style.paddingBottom = 16f;
            _card.style.backgroundColor = new Color(0.075f, 0.095f, 0.16f, 0.98f);
            _card.style.borderTopLeftRadius = 18f;
            _card.style.borderTopRightRadius = 18f;
            _card.style.borderBottomLeftRadius = 18f;
            _card.style.borderBottomRightRadius = 18f;
            _root.Add(_card);

            _title = AddLabel(_card, TitleElementName, "Input Assist", 28, FontStyle.Bold, new Color(0.88f, 0.94f, 1f));
            _description = AddLabel(_card, DescriptionElementName, "One setup handles stick dead zones, response, smoothing, directions, tap, hold, repeat, and multi-tap.", 13, FontStyle.Normal, new Color(0.66f, 0.75f, 0.88f));
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 8f;

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.minHeight = 150f;
            _card.Add(body);

            _stage = new VisualElement { name = VectorStageElementName };
            _stage.style.width = new Length(42f, LengthUnit.Percent);
            _stage.style.minWidth = 210f;
            _stage.style.backgroundColor = new Color(0.035f, 0.05f, 0.09f, 1f);
            _stage.style.borderTopLeftRadius = 14f;
            _stage.style.borderTopRightRadius = 14f;
            _stage.style.borderBottomLeftRadius = 14f;
            _stage.style.borderBottomRightRadius = 14f;
            _stage.style.marginRight = 16f;
            body.Add(_stage);

            AddAxisLine(_stage, true);
            AddAxisLine(_stage, false);
            _rawMarker = CreateMarker(RawMarkerElementName, 16f, new Color(1f, 0.55f, 0.3f, 0.95f));
            _filteredMarker = CreateMarker(FilteredMarkerElementName, 20f, new Color(0.3f, 0.9f, 1f, 0.95f));
            _stage.Add(_rawMarker);
            _stage.Add(_filteredMarker);

            var details = new VisualElement();
            details.style.flexGrow = 1f;
            details.style.justifyContent = Justify.Center;
            body.Add(details);
            AddLabel(details, string.Empty, "VECTOR FILTER", 11, FontStyle.Bold, new Color(0.4f, 0.85f, 1f));
            _vectorResult = AddLabel(details, VectorResultElementName, string.Empty, 14, FontStyle.Normal, Color.white);
            _vectorResult.style.whiteSpace = WhiteSpace.Normal;
            _vectorResult.style.marginBottom = 12f;
            AddLabel(details, string.Empty, "BUTTON GESTURES", 11, FontStyle.Bold, new Color(0.55f, 0.95f, 0.6f));
            _buttonResult = AddLabel(details, ButtonResultElementName, string.Empty, 14, FontStyle.Normal, Color.white);
            _buttonResult.style.whiteSpace = WhiteSpace.Normal;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _buttonRow.style.marginTop = 10f;
            _card.Add(_buttonRow);

            _buttons = new[]
            {
                AddButton(NeutralButtonElementName, "Neutral", () => RunVector(Vector2.zero)),
                AddButton(SoftRightButtonElementName, "Soft Right", () => RunVector(new Vector2(0.55f, 0.05f))),
                AddButton(DiagonalButtonElementName, "Diagonal", () => RunVector(new Vector2(0.8f, 0.8f))),
                AddButton(TapButtonElementName, "Tap", RunTap),
                AddButton(HoldRepeatButtonElementName, "Hold + Repeat", RunHoldRepeat),
                AddButton(ResetButtonElementName, "Reset", () => ResetDemo(true))
            };

            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
        }

        private void RunVector(Vector2 rawInput)
        {
            _actionCount++;
            _lastRaw = rawInput;
            for (var i = 0; i < 4; i++) _lastVector = _vectorFilter.Process(rawInput, 0.05f);
            RefreshVectorUi();
        }

        private void RunTap()
        {
            _actionCount++;
            _buttonTracker.Reset();
            var events = InputButtonEvent.None;
            events |= _buttonTracker.Process(true, 0f).Events;
            events |= _buttonTracker.Process(false, 0.05f).Events;
            var completed = _buttonTracker.Process(false, 0.3f);
            events |= completed.Events;
            _lastButtonEvents = events;
            _lastTapCount = completed.TapCount;
            _lastRepeatCount = 0;
            RefreshButtonUi();
        }

        private void RunHoldRepeat()
        {
            _actionCount++;
            _buttonTracker.Reset();
            var events = InputButtonEvent.None;
            var repeatCount = 0;
            events |= _buttonTracker.Process(true, 0f).Events;
            var held = _buttonTracker.Process(true, 0.35f);
            events |= held.Events;
            repeatCount += held.RepeatCount;
            var repeated = _buttonTracker.Process(true, 0.2f);
            events |= repeated.Events;
            repeatCount += repeated.RepeatCount;
            events |= _buttonTracker.Process(false, 0f).Events;
            _lastButtonEvents = events;
            _lastTapCount = 0;
            _lastRepeatCount = repeatCount;
            RefreshButtonUi();
        }

        private void ResetDemo(bool countAction)
        {
            if (countAction) _actionCount++;
            _vectorFilter.Reset();
            _buttonTracker.Reset();
            _lastRaw = Vector2.zero;
            _lastVector = _vectorFilter.Process(Vector2.zero, 0f);
            _lastButtonEvents = InputButtonEvent.None;
            _lastTapCount = 0;
            _lastRepeatCount = 0;
            RefreshVectorUi();
            RefreshButtonUi();
        }

        private void RefreshVectorUi()
        {
            if (_vectorResult == null) return;
            _vectorResult.text = string.Format(
                CultureInfo.InvariantCulture,
                "Raw       {0,6:0.00}, {1,6:0.00}\nFiltered  {2,6:0.00}, {3,6:0.00}\nDirection {4}",
                _lastRaw.x,
                _lastRaw.y,
                _lastVector.Value.x,
                _lastVector.Value.y,
                _lastVector.Direction);
            PositionMarker(_rawMarker, _lastRaw);
            PositionMarker(_filteredMarker, _lastVector.Value);
        }

        private void RefreshButtonUi()
        {
            if (_buttonResult == null) return;
            _buttonResult.text = string.Format(
                CultureInfo.InvariantCulture,
                "Events  {0}\nTaps    {1}\nRepeats {2}",
                _lastButtonEvents,
                _lastTapCount,
                _lastRepeatCount);
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveLayout(evt.newRect.width, evt.newRect.height);
            RefreshVectorUi();
        }

        private void ApplyResponsiveLayout(float width, float height)
        {
            if (_card == null) return;
            var compact = width < 720f || height < 430f;
            _card.style.paddingLeft = compact ? 10f : 22f;
            _card.style.paddingRight = compact ? 10f : 22f;
            _card.style.paddingTop = compact ? 7f : 18f;
            _card.style.paddingBottom = compact ? 7f : 16f;
            _title.style.fontSize = compact ? 21 : 28;
            _description.style.fontSize = compact ? 10 : 13;
            _description.style.marginBottom = compact ? 3f : 8f;
            _stage.style.marginRight = compact ? 8f : 16f;
            _vectorResult.style.fontSize = compact ? 11 : 14;
            _buttonResult.style.fontSize = compact ? 11 : 14;
            _buttonRow.style.marginTop = compact ? 4f : 10f;
            foreach (var button in _buttons)
            {
                button.style.minWidth = compact ? 92f : 118f;
                button.style.height = compact ? 28f : 34f;
                button.style.fontSize = compact ? 10 : 12;
                button.style.marginLeft = 3f;
                button.style.marginRight = 3f;
                button.style.marginTop = 2f;
                button.style.marginBottom = 2f;
            }
        }

        private static Label AddLabel(VisualElement parent, string name, string text, int fontSize, FontStyle style, Color color)
        {
            var label = new Label(text) { name = name };
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = style;
            label.style.color = color;
            parent.Add(label);
            return label;
        }

        private Button AddButton(string name, string text, Action action)
        {
            var button = new Button(action) { name = name, text = text };
            button.style.minWidth = 118f;
            button.style.height = 34f;
            button.style.marginLeft = 3f;
            button.style.marginRight = 3f;
            _buttonRow.Add(button);
            return button;
        }

        private static VisualElement CreateMarker(string name, float size, Color color)
        {
            var marker = new VisualElement { name = name };
            marker.style.position = Position.Absolute;
            marker.style.width = size;
            marker.style.height = size;
            marker.style.marginLeft = -size * 0.5f;
            marker.style.marginTop = -size * 0.5f;
            marker.style.backgroundColor = color;
            marker.style.borderTopLeftRadius = size;
            marker.style.borderTopRightRadius = size;
            marker.style.borderBottomLeftRadius = size;
            marker.style.borderBottomRightRadius = size;
            return marker;
        }

        private static void AddAxisLine(VisualElement stage, bool horizontal)
        {
            var line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.backgroundColor = new Color(0.3f, 0.4f, 0.55f, 0.45f);
            if (horizontal)
            {
                line.style.left = 0f;
                line.style.right = 0f;
                line.style.top = new Length(50f, LengthUnit.Percent);
                line.style.height = 1f;
            }
            else
            {
                line.style.top = 0f;
                line.style.bottom = 0f;
                line.style.left = new Length(50f, LengthUnit.Percent);
                line.style.width = 1f;
            }
            stage.Add(line);
        }

        private static void PositionMarker(VisualElement marker, Vector2 value)
        {
            if (marker == null) return;
            var clamped = Vector2.ClampMagnitude(value, 1f);
            marker.style.left = new Length(50f + clamped.x * 42f, LengthUnit.Percent);
            marker.style.top = new Length(50f - clamped.y * 42f, LengthUnit.Percent);
        }
    }
}
