using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayDecision.Samples
{
    /// <summary>複数候補のutility score選択と全寄与明細を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UtilityScoreEvaluatorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "utility-score-evaluator-basics-card";
        public const string TitleElementName = "utility-score-evaluator-basics-title";
        public const string DescriptionElementName = "utility-score-evaluator-basics-description";
        public const string ConfigurationElementName = "utility-score-evaluator-basics-configuration";
        public const string InputElementName = "utility-score-evaluator-basics-input";
        public const string StageElementName = "utility-score-evaluator-basics-stage";
        public const string ResultElementName = "utility-score-evaluator-basics-result";
        public const string ButtonRowElementName = "utility-score-evaluator-basics-buttons";
        public const string HighestButtonElementName = "utility-score-evaluator-basics-highest";
        public const string WeightedButtonElementName = "utility-score-evaluator-basics-weighted";
        public const string TieButtonElementName = "utility-score-evaluator-basics-tie";
        public const string LinesButtonElementName = "utility-score-evaluator-basics-lines";
        public const string InvalidButtonElementName = "utility-score-evaluator-basics-invalid";

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
        private UtilityScoreEvaluation _lastEvaluation;
        private UtilityScoreError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と評価が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public UtilityScoreError LastError => _lastError;

        /// <summary>最後に成功した候補評価を取得します。</summary>
        public UtilityScoreEvaluation LastEvaluation => _lastEvaluation;

        /// <summary>最後の入力配列とfactor列が変更されなかったかを取得します。</summary>
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
            _root.style.backgroundColor = new Color(0.025f, 0.045f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.055f, 0.12f, 0.2f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Utility Score Evaluator Basics", 31f, new Color(0.95f, 0.98f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "候補ごとのutilityとweightから最高scoreを選び、全factorの寄与を入力順で返します。", 15f, new Color(0.8f, 0.92f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 CANDIDATES  ·  1–16 FACTORS  ·  WEIGHTED MEAN  ·  STABLE TIE", 12f, new Color(0.52f, 0.86f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.9f, 0.96f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.46f, 1f, 0.82f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.9f, 0.97f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.02f, 0.065f, 0.12f, 1f);
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
                CreateButton(HighestButtonElementName, "Highest · 0.9 wins", () => Evaluate("HIGHEST", new[] { Candidate(1, Factor(1, 0.3d, 1d)), Candidate(2, Factor(1, 0.9d, 1d)), Candidate(3, Factor(1, 0.6d, 1d)) })),
                CreateButton(WeightedButtonElementName, "Weighted · 0.75 wins", () => Evaluate("WEIGHTED", new[] { Candidate(1, Factor(1, 1d, 3d), Factor(2, 0d, 1d)), Candidate(2, Factor(1, 0.6d, 1d)) })),
                CreateButton(TieButtonElementName, "Tie · first stays", () => Evaluate("TIE", new[] { Candidate(20, Factor(1, 0.75d, 2d)), Candidate(10, Factor(1, 0.75d, 2d)) })),
                CreateButton(LinesButtonElementName, "Lines · all factors", () => Evaluate("LINES", new[] { Candidate(7, Factor(30, 0.2d, 4d), Factor(10, 0.5d, 2d), Factor(20, 1d, 1d)) })),
                CreateButton(InvalidButtonElementName, "Invalid · duplicate ID", () => Evaluate("INVALID", new[] { Candidate(1, Factor(1, 0.4d, 1d)), Candidate(1, Factor(2, 0.8d, 1d)) }))
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
            button.style.color = new Color(0.025f, 0.095f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.66f, 0.88f, 1f, 1f);
            return button;
        }

        private void Evaluate(string label, UtilityScoreCandidate[] candidates)
        {
            var before = CopyCandidates(candidates);
            _lastSucceeded = UtilityScoreEvaluator.TryEvaluate(candidates, out _lastEvaluation, out _lastError);
            _lastInputPreserved = InputsEqual(candidates, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   CANDIDATES {candidates.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"SELECTED ID {_lastEvaluation.SelectedCandidateIdentifier}   ·   SCORE {Format(_lastEvaluation.SelectedScore)}   ·   FIRST TIE-BREAK";
                _result.text = FormatLines(_lastEvaluation);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "SELECTION —   ·   INPUT ARRAYS UNCHANGED   ·   NO PARTIAL LINES";
            }
        }

        private void ResetStateCore()
        {
            _lastEvaluation = null;
            _lastError = UtilityScoreError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   CANDIDATES —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose highest, weighted, tie, lines, or invalid input";
            _result.text = "CANDIDATE —   ·   SCORE —   ·   FACTOR CONTRIBUTIONS —";
        }

        private static UtilityScoreFactor Factor(int identifier, double utility, double weight) => new UtilityScoreFactor(identifier, utility, weight);

        private static UtilityScoreCandidate Candidate(int identifier, params UtilityScoreFactor[] factors) => new UtilityScoreCandidate(identifier, factors);

        private static string FormatLines(UtilityScoreEvaluation evaluation)
        {
            var builder = new StringBuilder();
            for (var candidateIndex = 0; candidateIndex < evaluation.CandidateCount; candidateIndex++)
            {
                evaluation.TryGetCandidateLine(candidateIndex, out var candidate);
                if (candidateIndex > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(candidate.CandidateIdentifier).Append(": ").Append(Format(candidate.Score)).Append(" [");
                for (var factorIndex = 0; factorIndex < candidate.FactorCount; factorIndex++)
                {
                    candidate.TryGetFactorLine(factorIndex, out var factor);
                    if (factorIndex > 0) builder.Append(", ");
                    builder.Append(factor.FactorIdentifier).Append('=').Append(Format(factor.WeightedUtility));
                }

                builder.Append(']');
            }

            return builder.ToString();
        }

        private static UtilityScoreCandidate[] CopyCandidates(UtilityScoreCandidate[] candidates)
        {
            var copy = new UtilityScoreCandidate[candidates.Length];
            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var factors = new UtilityScoreFactor[candidates[candidateIndex].FactorCount];
                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++) candidates[candidateIndex].TryGetFactor(factorIndex, out factors[factorIndex]);
                copy[candidateIndex] = new UtilityScoreCandidate(candidates[candidateIndex].Identifier, factors);
            }

            return copy;
        }

        private static bool InputsEqual(UtilityScoreCandidate[] left, UtilityScoreCandidate[] right)
        {
            if (left.Length != right.Length) return false;
            for (var candidateIndex = 0; candidateIndex < left.Length; candidateIndex++)
            {
                if (left[candidateIndex].Identifier != right[candidateIndex].Identifier || left[candidateIndex].FactorCount != right[candidateIndex].FactorCount) return false;
                for (var factorIndex = 0; factorIndex < left[candidateIndex].FactorCount; factorIndex++)
                {
                    left[candidateIndex].TryGetFactor(factorIndex, out var leftFactor);
                    right[candidateIndex].TryGetFactor(factorIndex, out var rightFactor);
                    if (leftFactor.Identifier != rightFactor.Identifier || leftFactor.Utility != rightFactor.Utility || leftFactor.Weight != rightFactor.Weight) return false;
                }
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
