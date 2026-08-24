using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayDecision.Samples
{
    /// <summary>current候補を小さなscore差では維持する選択契約を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StableScoreSelectorBasicsController : MonoBehaviour
    {
        public const string CardElementName = "stable-score-selector-basics-card";
        public const string TitleElementName = "stable-score-selector-basics-title";
        public const string DescriptionElementName = "stable-score-selector-basics-description";
        public const string ConfigurationElementName = "stable-score-selector-basics-configuration";
        public const string InputElementName = "stable-score-selector-basics-input";
        public const string StageElementName = "stable-score-selector-basics-stage";
        public const string ResultElementName = "stable-score-selector-basics-result";
        public const string ButtonRowElementName = "stable-score-selector-basics-buttons";
        public const string SelectButtonElementName = "stable-score-selector-basics-select";
        public const string KeepButtonElementName = "stable-score-selector-basics-keep";
        public const string SwitchButtonElementName = "stable-score-selector-basics-switch";
        public const string TieButtonElementName = "stable-score-selector-basics-tie";
        public const string MissingButtonElementName = "stable-score-selector-basics-missing";

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
        private StableScoreSelection _lastSelection;
        private StableScoreError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と選択が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public StableScoreError LastError => _lastError;

        /// <summary>最後に成功した選択結果を取得します。</summary>
        public StableScoreSelection LastSelection => _lastSelection;

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
            _root.style.backgroundColor = new Color(0.03f, 0.035f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.09f, 0.075f, 0.18f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Stable Score Selector Basics", 31f, new Color(0.98f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "current候補を微差では維持し、指定したscore優位差を満たすchallengerへだけ切り替えます。", 15f, new Color(0.9f, 0.84f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 CANDIDATES  ·  SCORE 0–1  ·  EXPLICIT MARGIN  ·  STABLE TIE", 12f, new Color(0.78f, 0.63f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.95f, 0.92f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.55f, 1f, 0.84f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.95f, 0.93f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.035f, 0.025f, 0.09f, 1f);
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
                CreateButton(SelectButtonElementName, "Select · no current", () => Evaluate("SELECT", 0, 0.1d, Candidates((1, 0.3d), (2, 0.9d), (3, 0.6d)))),
                CreateButton(KeepButtonElementName, "Keep · +0.06 < 0.10", () => Evaluate("KEEP", 1, 0.1d, Candidates((1, 0.62d), (2, 0.68d), (3, 0.55d)))),
                CreateButton(SwitchButtonElementName, "Switch · +0.16 ≥ 0.10", () => Evaluate("SWITCH", 1, 0.1d, Candidates((1, 0.62d), (2, 0.78d), (3, 0.5d)))),
                CreateButton(TieButtonElementName, "Tie · current stays", () => Evaluate("TIE", 20, 0d, Candidates((10, 0.75d), (20, 0.75d)))),
                CreateButton(MissingButtonElementName, "Missing · recover best", () => Evaluate("MISSING", 99, 0.5d, Candidates((7, 0.4d), (8, 0.8d))))
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
            button.style.color = new Color(0.12f, 0.05f, 0.22f, 1f);
            button.style.backgroundColor = new Color(0.84f, 0.74f, 1f, 1f);
            return button;
        }

        private void Evaluate(string label, int currentIdentifier, double minimumAdvantage, StableScoreCandidate[] candidates)
        {
            var before = (StableScoreCandidate[])candidates.Clone();
            _lastSucceeded = StableScoreSelector.TrySelect(candidates, currentIdentifier, minimumAdvantage, out _lastSelection, out _lastError);
            _lastInputPreserved = InputsEqual(candidates, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   CURRENT {currentIdentifier}   ·   MARGIN {Format(minimumAdvantage)}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"SELECTED ID {_lastSelection.SelectedCandidateIdentifier}   ·   SCORE {Format(_lastSelection.SelectedScore)}   ·   {_lastSelection.Reason}";
                _result.text = FormatLines(_lastSelection);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "SELECTION —   ·   INPUT ARRAY UNCHANGED   ·   NO PARTIAL LINES";
            }
        }

        private void ResetStateCore()
        {
            _lastSelection = null;
            _lastError = StableScoreError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   CURRENT —   ·   MARGIN —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  select, keep, switch, tie, or recover a missing current";
            _result.text = "CURRENT —   ·   CHALLENGER —   ·   SELECTED —   ·   REASON —";
        }

        private static StableScoreCandidate[] Candidates(params (int identifier, double score)[] values)
        {
            var candidates = new StableScoreCandidate[values.Length];
            for (var index = 0; index < values.Length; index++) candidates[index] = new StableScoreCandidate(values[index].identifier, values[index].score);
            return candidates;
        }

        private static string FormatLines(StableScoreSelection selection)
        {
            var builder = new StringBuilder();
            builder.Append("BEST ").Append(selection.BestCandidateIdentifier).Append("  ·  ");
            builder.Append("CHALLENGER ").Append(selection.HasChallenger ? selection.ChallengerCandidateIdentifier.ToString(CultureInfo.InvariantCulture) : "—").Append("  ·  ");
            for (var index = 0; index < selection.CandidateCount; index++)
            {
                selection.TryGetCandidateLine(index, out var line);
                if (index > 0) builder.Append("  |  ");
                builder.Append("ID ").Append(line.CandidateIdentifier).Append('=').Append(Format(line.Score));
                if (line.IsCurrent) builder.Append(" C");
                if (line.IsBestCandidate) builder.Append(" B");
                if (line.IsSelected) builder.Append(" S");
            }

            return builder.ToString();
        }

        private static bool InputsEqual(StableScoreCandidate[] left, StableScoreCandidate[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
                if (left[index].Identifier != right[index].Identifier || left[index].Score != right[index].Score) return false;
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
