using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputMixing.Samples
{
    /// <summary>weighted average、zero weight、empty入力、失敗indexを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputVectorWeightedMixerBasicsController : MonoBehaviour
    {
        public const string CardElementName = "input-vector-weighted-mixer-basics-card";
        public const string TitleElementName = "input-vector-weighted-mixer-basics-title";
        public const string DescriptionElementName = "input-vector-weighted-mixer-basics-description";
        public const string ConfigurationElementName = "input-vector-weighted-mixer-basics-configuration";
        public const string InputElementName = "input-vector-weighted-mixer-basics-input";
        public const string StageElementName = "input-vector-weighted-mixer-basics-stage";
        public const string ResultElementName = "input-vector-weighted-mixer-basics-result";
        public const string ButtonRowElementName = "input-vector-weighted-mixer-basics-buttons";
        public const string EqualButtonElementName = "input-vector-weighted-mixer-basics-equal";
        public const string PlayerHeavyButtonElementName = "input-vector-weighted-mixer-basics-player-heavy";
        public const string ZeroWeightButtonElementName = "input-vector-weighted-mixer-basics-zero-weight";
        public const string EmptyButtonElementName = "input-vector-weighted-mixer-basics-empty";
        public const string RejectButtonElementName = "input-vector-weighted-mixer-basics-reject";

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
        private InputVectorMixResult _lastResult;
        private string _lastInputSummary;
        private bool _rejectionObserved;
        private int _buttonActionCount;

        /// <summary>最後の合成結果。</summary>
        public InputVectorMixResult LastResult => _lastResult;

        /// <summary>不正weightが失敗index付きで拒否されたか。</summary>
        public bool RejectionObserved => _rejectionObserved;

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

            _title = AddLabel(TitleElementName, "Input Vector Weighted Mixer Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "複数の2D sourceを明示weightで合成し、結果・件数・失敗位置を直接観測します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "NORMALIZED WEIGHT  ·  ORDERED INPUT  ·  STATELESS  ·  NO ENGINE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(EqualButtonElementName, "Equal blend", MixEqual),
                CreateButton(PlayerHeavyButtonElementName, "Player 75%", MixPlayerHeavy),
                CreateButton(ZeroWeightButtonElementName, "Zero ignored", MixWithZeroWeight),
                CreateButton(EmptyButtonElementName, "Empty", MixEmpty),
                CreateButton(RejectButtonElementName, "Reject weight", RejectWeight)
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

        private void MixEqual() => Apply(new[] { new InputVectorContribution(1d, 0d, 1d), new InputVectorContribution(0d, 1d, 1d) }, "P(1, 0) × 1  +  AI(0, 1) × 1", "Equal blend  ·  output (0.50, 0.50)");

        private void MixPlayerHeavy() => Apply(new[] { new InputVectorContribution(1d, 0d, 0.75d), new InputVectorContribution(0d, 1d, 0.25d) }, "P(1, 0) × .75  +  AI(0, 1) × .25", "Player-heavy blend  ·  output (0.75, 0.25)");

        private void MixWithZeroWeight() => Apply(new[] { new InputVectorContribution(0.4d, -0.2d, 1d), new InputVectorContribution(-1d, 1d, 0d) }, "ACTIVE(.4, -.2) × 1  +  MUTED(-1, 1) × 0", "Zero weight ignored  ·  active count 1");

        private void MixEmpty() => Apply(Array.Empty<InputVectorContribution>(), "EMPTY CONTRIBUTION ARRAY", "Empty input  ·  neutral success");

        private void RejectWeight()
        {
            var input = new[] { new InputVectorContribution(0.25d, 0.5d, 1d), new InputVectorContribution(-0.5d, 0.25d, 1.5d) };
            _lastInputSummary = "2 SOURCES  ·  SECOND WEIGHT 1.50";
            _lastResult = InputVectorWeightedMixer.Mix(input);
            _rejectionObserved = !_lastResult.Succeeded && _lastResult.Error == InputVectorWeightedMixerError.WeightOutOfRange && _lastResult.InvalidContributionIndex == 1;
            _buttonActionCount++;
            _stage.text = _rejectionObserved ? "Invalid weight rejected  ·  index 1 reported" : "Weight guard failed";
            RefreshLabels();
        }

        private void Apply(InputVectorContribution[] input, string summary, string stage)
        {
            _lastInputSummary = summary;
            _lastResult = InputVectorWeightedMixer.Mix(input);
            _rejectionObserved = false;
            _buttonActionCount++;
            _stage.text = _lastResult.Succeeded ? stage : $"Mix failed  ·  {_lastResult.Error}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _lastInputSummary = "NO SOURCES YET";
            _lastResult = InputVectorWeightedMixer.Mix(Array.Empty<InputVectorContribution>());
            _rejectionObserved = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  compare explicit source weights";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"{_lastInputSummary}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"OUTPUT ({Format(_lastResult.Horizontal)}, {Format(_lastResult.Vertical)})   ·   TOTAL {Format(_lastResult.TotalWeight)}   ·   ACTIVE {_lastResult.ActiveContributionCount}/{_lastResult.ContributionCount}   ·   INVALID {_lastResult.InvalidContributionIndex}   ·   ERROR {_lastResult.Error}";
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
            _result.style.fontSize = compact ? 8.5f : 12f;
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
