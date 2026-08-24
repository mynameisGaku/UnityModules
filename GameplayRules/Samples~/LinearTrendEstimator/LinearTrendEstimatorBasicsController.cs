using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayAnalysis.Samples
{
    /// <summary>4 sampleの上昇・横ばい・下降・noiseと表現範囲外を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LinearTrendEstimatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "linear-trend-estimator-basics-card";
        public const string TitleElementName = "linear-trend-estimator-basics-title";
        public const string DescriptionElementName = "linear-trend-estimator-basics-description";
        public const string ConfigurationElementName = "linear-trend-estimator-basics-configuration";
        public const string InputElementName = "linear-trend-estimator-basics-input";
        public const string StageElementName = "linear-trend-estimator-basics-stage";
        public const string ResultElementName = "linear-trend-estimator-basics-result";
        public const string ButtonRowElementName = "linear-trend-estimator-basics-buttons";
        public const string RisingButtonElementName = "linear-trend-estimator-basics-rising";
        public const string FlatButtonElementName = "linear-trend-estimator-basics-flat";
        public const string FallingButtonElementName = "linear-trend-estimator-basics-falling";
        public const string NoisyButtonElementName = "linear-trend-estimator-basics-noisy";
        public const string ExtremeButtonElementName = "linear-trend-estimator-basics-extreme";

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
        private LinearTrendEstimate _lastEstimate;
        private LinearTrendError _lastError;
        private bool _lastSucceeded;
        private int _buttonActionCount;

        /// <summary>最後の推定が成功したか。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由。</summary>
        public LinearTrendError LastError => _lastError;
        /// <summary>最後の成功した推定結果。</summary>
        public LinearTrendEstimate LastEstimate => _lastEstimate;
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

            _title = AddLabel(TitleElementName, "Linear Trend Estimator Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "等間隔の有限sampleから傾き・切片・次sample予測を最小二乗で再構築します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "2–32 SAMPLES  ·  FINITE INPUT  ·  LEAST SQUARES  ·  NO STATE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(RisingButtonElementName, "Rising 10·20·30·40", () => Evaluate("RISING", new[] { 10d, 20d, 30d, 40d })),
                CreateButton(FlatButtonElementName, "Flat 20·20·20·20", () => Evaluate("FLAT", new[] { 20d, 20d, 20d, 20d })),
                CreateButton(FallingButtonElementName, "Falling 40·30·20·10", () => Evaluate("FALLING", new[] { 40d, 30d, 20d, 10d })),
                CreateButton(NoisyButtonElementName, "Noisy 10·30·20·40", () => Evaluate("NOISY", new[] { 10d, 30d, 20d, 40d })),
                CreateButton(ExtremeButtonElementName, "Extreme · explicit error", () => Evaluate("EXTREME", new[] { -double.MaxValue, double.MaxValue }))
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

        private void Evaluate(string label, double[] samples)
        {
            _lastSucceeded = LinearTrendEstimator.TryEstimate(samples, out _lastEstimate, out _lastError);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   COUNT {samples.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"Slope {Format(_lastEstimate.SlopePerSample)} per sample  ·  mean {Format(_lastEstimate.Mean)}";
                _result.text = $"INTERCEPT {Format(_lastEstimate.InterceptAtIndexZero)}   ·   NEXT {Format(_lastEstimate.PredictedNextSample)}   ·   FIRST/LAST {Format(_lastEstimate.FirstSample)} / {Format(_lastEstimate.LastSample)}";
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "ESTIMATE —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL RESULT";
            }
        }

        private void ResetStateCore()
        {
            _lastEstimate = default;
            _lastError = LinearTrendError.None;
            _lastSucceeded = false;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   COUNT —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose rising, flat, falling, noisy, or extreme samples";
            _result.text = "SLOPE —   ·   INTERCEPT —   ·   NEXT —";
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
