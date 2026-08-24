using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayAllocation.Samples
{
    /// <summary>integer totalをweight比で配分し、端数unitまで保つ契約を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WeightedIntegerAllocatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "weighted-integer-allocator-basics-card";
        public const string TitleElementName = "weighted-integer-allocator-basics-title";
        public const string DescriptionElementName = "weighted-integer-allocator-basics-description";
        public const string ConfigurationElementName = "weighted-integer-allocator-basics-configuration";
        public const string InputElementName = "weighted-integer-allocator-basics-input";
        public const string StageElementName = "weighted-integer-allocator-basics-stage";
        public const string ResultElementName = "weighted-integer-allocator-basics-result";
        public const string ButtonRowElementName = "weighted-integer-allocator-basics-buttons";
        public const string EqualButtonElementName = "weighted-integer-allocator-basics-equal";
        public const string WeightedButtonElementName = "weighted-integer-allocator-basics-weighted";
        public const string RemainderButtonElementName = "weighted-integer-allocator-basics-remainder";
        public const string ZeroWeightButtonElementName = "weighted-integer-allocator-basics-zero-weight";
        public const string ZeroTotalButtonElementName = "weighted-integer-allocator-basics-zero-total";

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
        private WeightedIntegerAllocation _lastAllocation;
        private WeightedIntegerError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と配分が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public WeightedIntegerError LastError => _lastError;

        /// <summary>最後に成功した配分結果を取得します。</summary>
        public WeightedIntegerAllocation LastAllocation => _lastAllocation;

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

            _title = AddLabel(TitleElementName, "Weighted Integer Allocator Basics", 31f, new Color(0.96f, 1f, 0.96f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "integer totalをweight比で切り捨て配分し、largest remainderで残りunitを失わず配ります。", 15f, new Color(0.82f, 0.96f, 0.88f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 ENTRIES  ·  INTEGER WEIGHT  ·  EXACT TOTAL  ·  STABLE REMAINDER", 12f, new Color(0.55f, 0.95f, 0.68f, 1f));
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
                CreateButton(EqualButtonElementName, "Equal · 10 → 4/3/3", () => Allocate("EQUAL", 10, Entries((1, 1), (2, 1), (3, 1)))),
                CreateButton(WeightedButtonElementName, "Weighted · 12 → 2/4/6", () => Allocate("WEIGHTED", 12, Entries((1, 1), (2, 2), (3, 3)))),
                CreateButton(RemainderButtonElementName, "Remainder · 8 → 4/2/2", () => Allocate("REMAINDER", 8, Entries((1, 5), (2, 3), (3, 2)))),
                CreateButton(ZeroWeightButtonElementName, "Zero weight · 5 → 0/5", () => Allocate("ZERO WEIGHT", 5, Entries((1, 0), (2, 4)))),
                CreateButton(ZeroTotalButtonElementName, "Zero total · 0 → 0/0", () => Allocate("ZERO TOTAL", 0, Entries((1, 0), (2, 0))))
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

        private void Allocate(string label, int totalUnits, WeightedIntegerEntry[] entries)
        {
            var before = (WeightedIntegerEntry[])entries.Clone();
            _lastSucceeded = WeightedIntegerAllocator.TryAllocate(entries, totalUnits, out _lastAllocation, out _lastError);
            _lastInputPreserved = InputsEqual(entries, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   TOTAL {totalUnits}   ·   ENTRIES {entries.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"ALLOCATED {_lastAllocation.TotalAllocatedUnits}/{_lastAllocation.TotalUnits}   ·   WEIGHT {_lastAllocation.TotalWeight}   ·   REMAINDER UNITS {_lastAllocation.RemainderUnitCount}";
                _result.text = FormatLines(_lastAllocation);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "ALLOCATION —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL LINES";
            }
        }

        private void ResetStateCore()
        {
            _lastAllocation = null;
            _lastError = WeightedIntegerError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   TOTAL —   ·   ENTRIES —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose equal, weighted, remainder, zero weight, or zero total";
            _result.text = "ENTRY —   ·   BASE —   ·   REMAINDER —   ·   ALLOCATED —";
        }

        private static WeightedIntegerEntry[] Entries(params (int identifier, int weight)[] values)
        {
            var entries = new WeightedIntegerEntry[values.Length];
            for (var index = 0; index < values.Length; index++) entries[index] = new WeightedIntegerEntry(values[index].identifier, values[index].weight);
            return entries;
        }

        private static string FormatLines(WeightedIntegerAllocation allocation)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < allocation.EntryCount; index++)
            {
                allocation.TryGetLine(index, out var line);
                if (index > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(line.EntryIdentifier)
                    .Append("  W ").Append(line.Weight)
                    .Append("  BASE ").Append(line.BaseUnits)
                    .Append("  REM ").Append(line.RemainderNumerator);
                if (line.ReceivedRemainderUnit) builder.Append(" +1");
                builder.Append("  = ").Append(line.AllocatedUnits);
            }

            return builder.ToString();
        }

        private static bool InputsEqual(WeightedIntegerEntry[] left, WeightedIntegerEntry[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index].Identifier != right[index].Identifier || left[index].Weight != right[index].Weight) return false;
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
