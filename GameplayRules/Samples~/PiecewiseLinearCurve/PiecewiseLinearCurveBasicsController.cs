using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayMath.Samples
{
    /// <summary>3つのpoint追加と2つのquery補間を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PiecewiseLinearCurveBasicsController : MonoBehaviour
    {
        public const string CardElementName = "piecewise-linear-curve-basics-card";
        public const string TitleElementName = "piecewise-linear-curve-basics-title";
        public const string DescriptionElementName = "piecewise-linear-curve-basics-description";
        public const string ConfigurationElementName = "piecewise-linear-curve-basics-configuration";
        public const string InputElementName = "piecewise-linear-curve-basics-input";
        public const string StageElementName = "piecewise-linear-curve-basics-stage";
        public const string ResultElementName = "piecewise-linear-curve-basics-result";
        public const string ButtonRowElementName = "piecewise-linear-curve-basics-buttons";
        public const string AddStartButtonElementName = "piecewise-linear-curve-basics-add-start";
        public const string AddPeakButtonElementName = "piecewise-linear-curve-basics-add-peak";
        public const string AddEndButtonElementName = "piecewise-linear-curve-basics-add-end";
        public const string EvaluateFiveButtonElementName = "piecewise-linear-curve-basics-evaluate-five";
        public const string EvaluateFifteenButtonElementName = "piecewise-linear-curve-basics-evaluate-fifteen";

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
        private PiecewiseLinearCurve _curve;
        private CurveChangeResult _lastChange;
        private CurveEvaluationResult _lastEvaluation;
        private int _buttonActionCount;

        /// <summary>現在のpoint件数。</summary>
        public int PointCount => _curve?.PointCount ?? 0;

        /// <summary>最後のpoint変更結果。</summary>
        public CurveChangeResult LastChange => _lastChange;

        /// <summary>最後のquery評価結果。</summary>
        public CurveEvaluationResult LastEvaluation => _lastEvaluation;

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

            _title = AddLabel(TitleElementName, "Piecewise Linear Curve Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "有限pointをX昇順へ並べ、queryを隣接2点から線形補間します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "MAX 32  ·  UNIQUE X  ·  FINITE X/Y  ·  CLAMP ENDS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(AddStartButtonElementName, "Add (0, 0)", () => ApplyChange(_curve.Add(0d, 0d), "Start point  ·  X 0  ·  Y 0")),
                CreateButton(AddPeakButtonElementName, "Add (10, 100)", () => ApplyChange(_curve.Add(10d, 100d), "Peak point  ·  X 10  ·  Y 100")),
                CreateButton(AddEndButtonElementName, "Add (20, 50)", () => ApplyChange(_curve.Add(20d, 50d), "End point  ·  X 20  ·  Y 50")),
                CreateButton(EvaluateFiveButtonElementName, "Evaluate X 5 → 50", () => ApplyEvaluation(5d, "query 5  ·  segment 0→10  ·  t 0.5  ·  value 50")),
                CreateButton(EvaluateFifteenButtonElementName, "Evaluate X 15 → 75", () => ApplyEvaluation(15d, "query 15  ·  segment 10→20  ·  t 0.5  ·  value 75"))
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

        private void ApplyChange(CurveChangeResult result, string successStage)
        {
            _lastChange = result;
            _buttonActionCount++;
            _stage.text = result.Succeeded ? successStage : $"Change rejected  ·  {result.Error}";
            RefreshLabels();
        }

        private void ApplyEvaluation(double query, string successStage)
        {
            _lastEvaluation = _curve.Evaluate(query);
            _buttonActionCount++;
            _stage.text = _lastEvaluation.Succeeded ? successStage : $"Evaluation rejected  ·  {_lastEvaluation.Error}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _curve = new PiecewiseLinearCurve();
            _lastChange = _curve.Clear();
            _lastEvaluation = default;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  add three points, then evaluate two queries";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"POINTS {PointCount}   ·   LAST X {Format(_lastChange.AffectedX)}   ·   LAST CHANGE {_lastChange.Error}   ·   ACTIONS {_buttonActionCount}";
            _result.text = _lastEvaluation.Succeeded
                ? $"QUERY {Format(_lastEvaluation.Query)}   ·   VALUE {Format(_lastEvaluation.Value)}   ·   SEGMENT {_lastEvaluation.LowerIndex}→{_lastEvaluation.UpperIndex}   ·   T {Format(_lastEvaluation.Interpolation)}   ·   CLAMP {_lastEvaluation.Clamped}"
                : "QUERY —   ·   VALUE —   ·   SEGMENT —   ·   T —   ·   CLAMP —";
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
