using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayAnalysis.Samples
{
    /// <summary>有限sample列の平均・range・母分散・母標準偏差を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SampleStatisticsBasicsController : MonoBehaviour
    {
        public const string CardElementName = "sample-statistics-basics-card";
        public const string TitleElementName = "sample-statistics-basics-title";
        public const string DescriptionElementName = "sample-statistics-basics-description";
        public const string ConfigurationElementName = "sample-statistics-basics-configuration";
        public const string InputElementName = "sample-statistics-basics-input";
        public const string StageElementName = "sample-statistics-basics-stage";
        public const string ResultElementName = "sample-statistics-basics-result";
        public const string ButtonRowElementName = "sample-statistics-basics-buttons";
        public const string BalancedButtonElementName = "sample-statistics-basics-balanced";
        public const string ConstantButtonElementName = "sample-statistics-basics-constant";
        public const string SpreadButtonElementName = "sample-statistics-basics-spread";
        public const string SubrangeButtonElementName = "sample-statistics-basics-subrange";
        public const string ExtremeButtonElementName = "sample-statistics-basics-extreme";

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
        private SampleStatisticsResult _lastResult;
        private SampleStatisticsError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の要約が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public SampleStatisticsError LastError => _lastError;
        /// <summary>最後の成功した要約統計を取得します。</summary>
        public SampleStatisticsResult LastResult => _lastResult;
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
            _root.style.backgroundColor = new Color(0.025f, 0.045f, 0.06f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.055f, 0.13f, 0.15f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Sample Statistics Basics", 31f, new Color(0.94f, 1f, 0.98f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "有限sample列の散らばりを、入力順と明示範囲から再現可能に要約します。", 15f, new Color(0.78f, 0.95f, 0.93f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 SAMPLES  ·  POPULATION MOMENTS  ·  WELFORD  ·  NO STATE", 12f, new Color(0.52f, 1f, 0.78f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.91f, 0.98f, 0.96f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.43f, 0.93f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.88f, 0.98f, 0.95f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.02f, 0.075f, 0.085f, 1f);
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
                CreateButton(BalancedButtonElementName, "Balanced 1·2·3·4", () => Analyze("BALANCED", new[] { 1d, 2d, 3d, 4d }, 0, 4)),
                CreateButton(ConstantButtonElementName, "Constant 7·7·7·7", () => Analyze("CONSTANT", new[] { 7d, 7d, 7d, 7d }, 0, 4)),
                CreateButton(SpreadButtonElementName, "Spread −10·0·10", () => Analyze("SPREAD", new[] { -10d, 0d, 10d }, 0, 3)),
                CreateButton(SubrangeButtonElementName, "Subrange 2·4·6", () => Analyze("SUBRANGE", new[] { 999d, 2d, 4d, 6d, -999d }, 1, 3)),
                CreateButton(ExtremeButtonElementName, "Extreme · explicit error", () => Analyze("EXTREME", new[] { -double.MaxValue, double.MaxValue }, 0, 2))
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
            button.style.color = new Color(0.025f, 0.11f, 0.12f, 1f);
            button.style.backgroundColor = new Color(0.63f, 0.95f, 0.86f, 1f);
            return button;
        }

        private void Analyze(string label, double[] samples, int startIndex, int count)
        {
            var before = (double[])samples.Clone();
            _lastSucceeded = SampleStatistics.TryAnalyze(samples, startIndex, count, out _lastResult, out _lastError);
            _lastInputPreserved = ArraysEqual(samples, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   RANGE {startIndex}..{startIndex + count - 1}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"Mean {Format(_lastResult.Mean)}  ·  σ {Format(_lastResult.PopulationStandardDeviation)}";
                _result.text = $"MIN/MAX {Format(_lastResult.Minimum)} / {Format(_lastResult.Maximum)}   ·   RANGE {Format(_lastResult.Range)}   ·   VAR {Format(_lastResult.PopulationVariance)}   ·   COUNT {_lastResult.SampleCount}";
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "STATISTICS —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL RESULT";
            }
        }

        private void ResetStateCore()
        {
            _lastResult = default;
            _lastError = SampleStatisticsError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   RANGE —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose balanced, constant, spread, subrange, or extreme samples";
            _result.text = "MEAN —   ·   VARIANCE —   ·   STANDARD DEVIATION —";
        }

        private static bool ArraysEqual(double[] left, double[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (!left[index].Equals(right[index])) return false;
            }

            return true;
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
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
