using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayThreat.Samples
{
    /// <summary>加算・減算・入力順・0下限・安定首位を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ThreatScoreResolverBasicsController : MonoBehaviour
    {
        public const string CardElementName = "threat-score-resolver-basics-card";
        public const string TitleElementName = "threat-score-resolver-basics-title";
        public const string DescriptionElementName = "threat-score-resolver-basics-description";
        public const string ConfigurationElementName = "threat-score-resolver-basics-configuration";
        public const string InputElementName = "threat-score-resolver-basics-input";
        public const string StageElementName = "threat-score-resolver-basics-stage";
        public const string ResultElementName = "threat-score-resolver-basics-result";
        public const string ButtonRowElementName = "threat-score-resolver-basics-buttons";
        public const string AddButtonElementName = "threat-score-resolver-basics-add";
        public const string ReduceButtonElementName = "threat-score-resolver-basics-reduce";
        public const string OrderedButtonElementName = "threat-score-resolver-basics-ordered";
        public const string ClampButtonElementName = "threat-score-resolver-basics-clamp";
        public const string InvalidButtonElementName = "threat-score-resolver-basics-invalid";

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
        private ThreatScoreResolution _lastResolution;
        private ThreatScoreError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の入力検証と解決が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public ThreatScoreError LastError => _lastError;
        /// <summary>最後に成功したthreat score解決を取得します。</summary>
        public ThreatScoreResolution LastResolution => _lastResolution;
        /// <summary>最後の入力配列が変更されなかったかを取得します。</summary>
        public bool LastInputPreserved => _lastInputPreserved;
        /// <summary>実Button操作数を取得します。</summary>
        public int ButtonActionCount => _buttonActionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            BuildUi();
            ResetState();
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
            _root.style.backgroundColor = new Color(0.035f, 0.02f, 0.06f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.11f, 0.06f, 0.18f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Threat Score Resolver Basics", 31f, new Color(0.97f, 0.94f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "対象別threatへ増減を入力順に適用し、0下限・全明細・安定した首位を返します。", 15f, new Color(0.84f, 0.78f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "1–32 TARGETS  ·  0–64 DELTAS  ·  CLAMP AT ZERO  ·  STABLE LEADER", 12f, new Color(0.69f, 0.53f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.92f, 0.88f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.75f, 0.65f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.95f, 0.92f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.045f, 0.018f, 0.08f, 1f);
            _result.style.borderTopLeftRadius = 10f;
            _result.style.borderTopRightRadius = 10f;
            _result.style.borderBottomLeftRadius = 10f;
            _result.style.borderBottomRightRadius = 10f;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);
            _buttons = new[]
            {
                CreateButton(AddButtonElementName, "Add · ID 1 +15", () => Resolve("ADD", new[] { Entry(1, 10d), Entry(2, 20d) }, new[] { Delta(1, 15d) })),
                CreateButton(ReduceButtonElementName, "Reduce · ID 1 −12", () => Resolve("REDUCE", new[] { Entry(1, 30d), Entry(2, 20d) }, new[] { Delta(1, -12d) })),
                CreateButton(OrderedButtonElementName, "Ordered · +20, +40, −8", () => Resolve("ORDERED", new[] { Entry(1, 10d), Entry(2, 5d) }, new[] { Delta(1, 20d), Delta(2, 40d), Delta(1, -8d) })),
                CreateButton(ClampButtonElementName, "Clamp · ID 1 −50", () => Resolve("CLAMP", new[] { Entry(1, 10d), Entry(2, 0d) }, new[] { Delta(1, -50d) })),
                CreateButton(InvalidButtonElementName, "Invalid · unknown ID", () => Resolve("INVALID", new[] { Entry(1, 10d) }, new[] { Delta(9, 5d) }))
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
            button.style.color = new Color(0.08f, 0.025f, 0.14f, 1f);
            button.style.backgroundColor = new Color(0.76f, 0.64f, 1f, 1f);
            return button;
        }

        private void Resolve(string label, ThreatScoreEntry[] entries, ThreatScoreAdjustment[] adjustments)
        {
            var entryCopy = (ThreatScoreEntry[])entries.Clone();
            var adjustmentCopy = (ThreatScoreAdjustment[])adjustments.Clone();
            _lastSucceeded = ThreatScoreResolver.TryResolve(entries, adjustments, out _lastResolution, out _lastError, out var failureIndex);
            _lastInputPreserved = EntriesEqual(entries, entryCopy) && AdjustmentsEqual(adjustments, adjustmentCopy);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   TARGETS {entries.Length}   ·   DELTAS {adjustments.Length}   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                _stage.text = $"LEADER ID {_lastResolution.LeaderTargetId}   ·   SCORE {Format(_lastResolution.LeaderScore)}   ·   STEPS {_lastResolution.StepCount}";
                _result.text = FormatSteps(_lastResolution);
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}  ·  INDEX {failureIndex}";
                _result.text = "RESOLUTION —   ·   INPUT ARRAYS UNCHANGED   ·   NO PARTIAL RESULT";
            }
        }

        private void ResetState()
        {
            _lastResolution = null;
            _lastError = ThreatScoreError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   TARGETS —   ·   DELTAS —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose add, reduce, ordered, clamp, or invalid input";
            _result.text = "TARGET  →  INPUT SCORE  →  APPLIED DELTA  →  OUTPUT SCORE";
        }

        private static ThreatScoreEntry Entry(int id, double score) => new ThreatScoreEntry(id, score);
        private static ThreatScoreAdjustment Delta(int id, double value) => new ThreatScoreAdjustment(id, value);

        private static string FormatSteps(ThreatScoreResolution resolution)
        {
            if (resolution.StepCount == 0) return "NO DELTAS   ·   SCORES UNCHANGED";
            var builder = new StringBuilder();
            for (var index = 0; index < resolution.StepCount; index++)
            {
                resolution.TryGetStep(index, out var step);
                if (index > 0) builder.Append("   |   ");
                builder.Append("ID ").Append(step.TargetId).Append(": ").Append(Format(step.InputScore)).Append(step.AppliedDelta < 0d ? " − " : " + ").Append(Format(Math.Abs(step.AppliedDelta))).Append(" → ").Append(Format(step.OutputScore));
            }
            return builder.ToString();
        }

        private static bool EntriesEqual(ThreatScoreEntry[] left, ThreatScoreEntry[] right)
        {
            for (var index = 0; index < left.Length; index++) if (left[index].TargetId != right[index].TargetId || left[index].Score != right[index].Score) return false;
            return true;
        }

        private static bool AdjustmentsEqual(ThreatScoreAdjustment[] left, ThreatScoreAdjustment[] right)
        {
            for (var index = 0; index < left.Length; index++) if (left[index].TargetId != right[index].TargetId || left[index].Delta != right[index].Delta) return false;
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
            _title.style.fontSize = compact ? 22f : 31f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 10.5f : 15f;
            _description.style.marginBottom = compact ? 4f : 10f;
            _configuration.style.fontSize = compact ? 9f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _input.style.fontSize = compact ? 9.5f : 13f;
            _input.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 12.5f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 12f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
            for (var index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].style.flexBasis = compact ? 160f : 130f;
                _buttons[index].style.minWidth = compact ? 140f : 110f;
                _buttons[index].style.minHeight = compact ? 30f : 42f;
                _buttons[index].style.fontSize = compact ? 10f : 12f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
