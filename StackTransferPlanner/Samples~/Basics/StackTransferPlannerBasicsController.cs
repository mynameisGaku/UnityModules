using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayInventory.Samples
{
    /// <summary>sourceからdestinationへの整数unit移送計画を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StackTransferPlannerBasicsController : MonoBehaviour
    {
        public const string CardElementName = "stack-transfer-planner-basics-card";
        public const string TitleElementName = "stack-transfer-planner-basics-title";
        public const string DescriptionElementName = "stack-transfer-planner-basics-description";
        public const string ConfigurationElementName = "stack-transfer-planner-basics-configuration";
        public const string InputElementName = "stack-transfer-planner-basics-input";
        public const string StageElementName = "stack-transfer-planner-basics-stage";
        public const string ResultElementName = "stack-transfer-planner-basics-result";
        public const string ButtonRowElementName = "stack-transfer-planner-basics-buttons";
        public const string FullButtonElementName = "stack-transfer-planner-basics-full";
        public const string PartialButtonElementName = "stack-transfer-planner-basics-partial";
        public const string SourceLimitButtonElementName = "stack-transfer-planner-basics-source-limit";
        public const string DestinationLimitButtonElementName = "stack-transfer-planner-basics-destination-limit";
        public const string ZeroButtonElementName = "stack-transfer-planner-basics-zero";

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
        private StackTransferPlan _lastPlan;
        private StackTransferError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と計画構築が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public StackTransferError LastError => _lastError;

        /// <summary>最後に成功した移送計画を取得します。</summary>
        public StackTransferPlan LastPlan => _lastPlan;

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
            _root.style.backgroundColor = new Color(0.025f, 0.05f, 0.045f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.06f, 0.14f, 0.12f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Stack Transfer Planner Basics", 31f, new Color(0.96f, 1f, 0.96f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "sourceを入力順で減らし、destinationを入力順で満たす移送計画をstate変更なしで返します。", 15f, new Color(0.82f, 0.96f, 0.88f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 SOURCES  ·  1–32 DESTINATIONS  ·  INTEGER UNITS  ·  PARTIAL TRANSFER", 12f, new Color(0.55f, 0.95f, 0.68f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.9f, 0.98f, 0.92f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(1f, 0.88f, 0.42f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.92f, 1f, 0.94f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.015f, 0.075f, 0.06f, 1f);
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
                CreateButton(FullButtonElementName, "Full · request 9 → 9", () => Plan("FULL", 9, Sources((1, 5), (2, 5)), Destinations((11, 0, 5), (12, 1, 6)))),
                CreateButton(PartialButtonElementName, "Partial · request 8 → 7", () => Plan("PARTIAL", 8, Sources((1, 4), (2, 6)), Destinations((11, 8, 10), (12, 2, 7)))),
                CreateButton(SourceLimitButtonElementName, "Source limit · 8 → 3", () => Plan("SOURCE LIMIT", 8, Sources((1, 3)), Destinations((11, 0, 10)))),
                CreateButton(DestinationLimitButtonElementName, "Destination limit · 8 → 4", () => Plan("DEST LIMIT", 8, Sources((1, 10)), Destinations((11, 4, 8)))),
                CreateButton(ZeroButtonElementName, "Zero request · 0 → 0", () => Plan("ZERO", 0, Sources((1, 4)), Destinations((11, 1, 5))))
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
            button.style.color = new Color(0.04f, 0.14f, 0.08f, 1f);
            button.style.backgroundColor = new Color(0.68f, 0.95f, 0.72f, 1f);
            return button;
        }

        private void Plan(string label, int requestedUnits, StackTransferSource[] sources, StackTransferDestination[] destinations)
        {
            var sourceBefore = (StackTransferSource[])sources.Clone();
            var destinationBefore = (StackTransferDestination[])destinations.Clone();
            _lastSucceeded = StackTransferPlanner.TryPlan(sources, destinations, requestedUnits, out _lastPlan, out _lastError);
            _lastInputPreserved = SourcesEqual(sources, sourceBefore) && DestinationsEqual(destinations, destinationBefore);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   REQUEST {requestedUnits}   ·   S {sources.Length} / D {destinations.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"TRANSFERRED {_lastPlan.TransferredUnits}/{_lastPlan.RequestedUnits}   ·   UNFULFILLED {_lastPlan.UnfulfilledUnits}   ·   SOURCE {_lastPlan.AvailableSourceUnits}   ·   ROOM {_lastPlan.AvailableDestinationRoom}";
                _result.text = FormatLines(_lastPlan);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "TRANSFER —   ·   INPUT ARRAYS UNCHANGED   ·   NO PARTIAL LINES";
            }
        }

        private void ResetStateCore()
        {
            _lastPlan = null;
            _lastError = StackTransferError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   REQUEST —   ·   SOURCES / DESTINATIONS —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose full, partial, source limit, destination limit, or zero request";
            _result.text = "SOURCE BEFORE → AFTER   ·   DESTINATION BEFORE → AFTER / CAPACITY";
        }

        private static StackTransferSource[] Sources(params (int identifier, int units)[] values)
        {
            var sources = new StackTransferSource[values.Length];
            for (var index = 0; index < values.Length; index++) sources[index] = new StackTransferSource(values[index].identifier, values[index].units);
            return sources;
        }

        private static StackTransferDestination[] Destinations(params (int identifier, int current, int capacity)[] values)
        {
            var destinations = new StackTransferDestination[values.Length];
            for (var index = 0; index < values.Length; index++) destinations[index] = new StackTransferDestination(values[index].identifier, values[index].current, values[index].capacity);
            return destinations;
        }

        private static string FormatLines(StackTransferPlan plan)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < plan.SourceLineCount; index++)
            {
                plan.TryGetSourceLine(index, out var line);
                if (index > 0) builder.Append("   |   ");
                builder.Append("S").Append(line.Identifier).Append(" ").Append(line.BeforeUnits).Append("→").Append(line.AfterUnits);
            }

            builder.Append("   ||   ");
            for (var index = 0; index < plan.DestinationLineCount; index++)
            {
                plan.TryGetDestinationLine(index, out var line);
                if (index > 0) builder.Append("   |   ");
                builder.Append("D").Append(line.Identifier).Append(" ").Append(line.BeforeUnits).Append("→").Append(line.AfterUnits).Append("/").Append(line.Capacity);
            }
            return builder.ToString();
        }

        private static bool SourcesEqual(StackTransferSource[] left, StackTransferSource[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index].Identifier != right[index].Identifier || left[index].AvailableUnits != right[index].AvailableUnits) return false;
            return true;
        }

        private static bool DestinationsEqual(StackTransferDestination[] left, StackTransferDestination[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index].Identifier != right[index].Identifier || left[index].CurrentUnits != right[index].CurrentUnits || left[index].Capacity != right[index].Capacity) return false;
            return true;
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
                _buttons[index].style.fontSize = compact ? 10.5f : 12f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
