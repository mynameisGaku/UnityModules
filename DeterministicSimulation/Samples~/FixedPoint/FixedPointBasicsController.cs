using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FixedPoint.Samples
{
    /// <summary>Q16.16の生成、四則演算、overflow拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FixedPointBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "fixed-point-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "fixed-point-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "fixed-point-basics-description";

        /// <summary>形式表示要素名。</summary>
        public const string ConfigurationElementName = "fixed-point-basics-configuration";

        /// <summary>値状態要素名。</summary>
        public const string ValueElementName = "fixed-point-basics-value";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "fixed-point-basics-stage";

        /// <summary>raw値表示要素名。</summary>
        public const string ResultElementName = "fixed-point-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "fixed-point-basics-buttons";

        /// <summary>1.5生成Button要素名。</summary>
        public const string SetButtonElementName = "fixed-point-basics-set";

        /// <summary>-0.25加算Button要素名。</summary>
        public const string AddButtonElementName = "fixed-point-basics-add";

        /// <summary>2倍Button要素名。</summary>
        public const string MultiplyButtonElementName = "fixed-point-basics-multiply";

        /// <summary>4除算Button要素名。</summary>
        public const string DivideButtonElementName = "fixed-point-basics-divide";

        /// <summary>overflow検証Button要素名。</summary>
        public const string OverflowButtonElementName = "fixed-point-basics-overflow";

        /// <summary>golden操作列の最終raw値。</summary>
        public const int ExpectedGoldenRawValue = 40960;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _value;
        private Label _stage;
        private Label _result;
        private Button[] _buttons;
        private Fixed32 _current;
        private Fixed32Error _lastError;
        private bool _overflowRejected;
        private int _buttonActionCount;

        /// <summary>現在のQ16.16値。</summary>
        public Fixed32 Current => _current;

        /// <summary>最後の操作error。</summary>
        public Fixed32Error LastError => _lastError;

        /// <summary>overflowが現在値を変えずに拒否されたか。</summary>
        public bool OverflowRejected => _overflowRejected;

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
            _root.style.backgroundColor = new Color(0.035f, 0.025f, 0.07f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.10f, 0.055f, 0.20f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Fixed Point Basics", 32f, new Color(0.97f, 0.94f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "整数raw値だけで小数の生成・演算・overflow拒否を再現します。", 15f, new Color(0.86f, 0.80f, 0.94f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "SIGNED Q16.16  ·  SCALE 65536  ·  TOWARD ZERO  ·  CHECKED", 12f, new Color(0.60f, 1f, 0.82f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _value = AddLabel(ValueElementName, string.Empty, 13f, new Color(0.94f, 0.91f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.45f, 0.9f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.90f, 0.86f, 0.98f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.035f, 0.02f, 0.075f, 1f);
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
                CreateButton(SetButtonElementName, "Set 1.5", SetOneAndHalf),
                CreateButton(AddButtonElementName, "Add -0.25", AddNegativeQuarter),
                CreateButton(MultiplyButtonElementName, "Multiply 2", MultiplyTwo),
                CreateButton(DivideButtonElementName, "Divide 4", DivideFour),
                CreateButton(OverflowButtonElementName, "Guard Overflow", GuardOverflow)
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
            button.style.color = new Color(0.10f, 0.055f, 0.20f, 1f);
            button.style.backgroundColor = new Color(0.84f, 0.80f, 0.94f, 1f);
            return button;
        }

        private void SetOneAndHalf()
        {
            Apply(Fixed32.FromRatio(3, 2), "Set 3 / 2  ·  exact raw 98304");
        }

        private void AddNegativeQuarter()
        {
            Apply(Fixed32.Add(_current, Fixed32.FromRatio(-1, 4).Value), "Add -1 / 4  ·  checked addition");
        }

        private void MultiplyTwo()
        {
            Apply(Fixed32.Multiply(_current, Fixed32.FromInt32(2).Value), "Multiply 2  ·  64-bit intermediate");
        }

        private void DivideFour()
        {
            Apply(Fixed32.Divide(_current, Fixed32.FromInt32(4).Value), "Divide 4  ·  toward-zero rounding");
        }

        private void GuardOverflow()
        {
            var before = _current;
            var attempted = Fixed32.Add(Fixed32.MaxValue, Fixed32.FromRaw(1));
            _lastError = attempted.Error;
            _overflowRejected = !attempted.Succeeded && attempted.Error == Fixed32Error.Overflow && _current == before;
            _buttonActionCount++;
            _stage.text = _overflowRejected ? "Overflow rejected  ·  current value unchanged" : "Overflow guard failed";
            RefreshLabels();
        }

        private void Apply(Fixed32Result result, string stage)
        {
            _lastError = result.Error;
            if (result.Succeeded) _current = result.Value;
            _overflowRejected = false;
            _buttonActionCount++;
            _stage.text = result.Succeeded ? stage : $"Operation failed  ·  {result.Error}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _current = Fixed32.Zero;
            _lastError = Fixed32Error.None;
            _overflowRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic operation sequence";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _value.text = $"Value {_current}   ·   Raw {_current.RawValue}   ·   Actions {_buttonActionCount}";
            _result.text = $"ERROR {_lastError}   ·   OVERFLOW PRESERVED {_overflowRejected}";
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
            _title.style.fontSize = compact ? 23f : 32f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 11f : 15f;
            _description.style.marginBottom = compact ? 5f : 10f;
            _configuration.style.fontSize = compact ? 9.5f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _value.style.fontSize = compact ? 10f : 13f;
            _value.style.marginBottom = compact ? 3f : 7f;
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
