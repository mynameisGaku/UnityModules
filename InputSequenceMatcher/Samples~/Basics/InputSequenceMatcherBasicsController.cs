using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputSequencing.Samples
{
    /// <summary>明示tickでの順序一致、match、timeout、restartを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputSequenceMatcherBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-sequence-matcher-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-sequence-matcher-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-sequence-matcher-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-sequence-matcher-basics-configuration";

        /// <summary>sequence進捗要素名。</summary>
        public const string InputElementName = "input-sequence-matcher-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-sequence-matcher-basics-stage";

        /// <summary>最終結果要素名。</summary>
        public const string ResultElementName = "input-sequence-matcher-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-sequence-matcher-basics-buttons";

        /// <summary>1回目Light Button要素名。</summary>
        public const string FirstLightButtonElementName = "input-sequence-matcher-basics-first-light";

        /// <summary>2回目Light Button要素名。</summary>
        public const string SecondLightButtonElementName = "input-sequence-matcher-basics-second-light";

        /// <summary>Heavy match Button要素名。</summary>
        public const string HeavyButtonElementName = "input-sequence-matcher-basics-heavy";

        /// <summary>再開始Light Button要素名。</summary>
        public const string RestartLightButtonElementName = "input-sequence-matcher-basics-restart-light";

        /// <summary>timeout後Light Button要素名。</summary>
        public const string LateLightButtonElementName = "input-sequence-matcher-basics-late-light";

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
        private InputSequenceMatcher _matcher;
        private InputSequenceStatus _lastStatus;
        private InputSequenceError _lastError;
        private int _buttonActionCount;

        /// <summary>現在の明示simulation tick。</summary>
        public ulong CurrentTick => _matcher?.CurrentTick ?? 0;

        /// <summary>現在のpattern一致数。</summary>
        public int Progress => _matcher?.Progress ?? 0;

        /// <summary>次に期待するcommand id。</summary>
        public int ExpectedCommandId => _matcher?.ExpectedCommandId ?? 0;

        /// <summary>最後の入力でpattern全体が一致したか。</summary>
        public bool LastMatched => _lastStatus.Matched;

        /// <summary>最後の入力前にtick間隔が上限を超えたか。</summary>
        public bool LastTimedOut => _lastStatus.TimedOut;

        /// <summary>最後の不一致入力で進捗を破棄したか。</summary>
        public bool LastRestarted => _lastStatus.Restarted;

        /// <summary>最後のAPI error。</summary>
        public InputSequenceError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Sequence Matcher Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "Light・Light・Heavyを明示tickで照合し、間隔超過をtimeoutとして再現可能に判定します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "PATTERN LIGHT · LIGHT · HEAVY  ·  MAX GAP 2 TICKS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(FirstLightButtonElementName, "Light @100  ·  1/3", () => Push(100, 1, "Light accepted  ·  progress 1 / 3")),
                CreateButton(SecondLightButtonElementName, "Light @101  ·  2/3", () => Push(101, 1, "Light accepted  ·  progress 2 / 3")),
                CreateButton(HeavyButtonElementName, "Heavy @102  ·  Match", () => Push(102, 2, "Sequence matched  ·  progress reset")),
                CreateButton(RestartLightButtonElementName, "Light @103  ·  Restart", () => Push(103, 1, "New sequence started  ·  progress 1 / 3")),
                CreateButton(LateLightButtonElementName, "Late Light @106", () => Push(106, 1, "Previous progress timed out  ·  restarted at 1 / 3"))
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

        private void Push(ulong tick, int commandId, string stage)
        {
            var succeeded = _matcher.TryPush(tick, commandId, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Push failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputSequenceMatcher.TryCreate(new[] { 1, 1, 2 }, 2, 100, out _matcher, out var error)) throw new InvalidOperationException($"Input Sequence Matcher Basics configuration is invalid: {error}.");
            _lastStatus = _matcher.Snapshot();
            _lastError = InputSequenceError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  enter Light, Light, Heavy";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_matcher.CurrentTick}   ·   PROGRESS {_matcher.Progress} / {_matcher.PatternLength}   ·   EXPECT {_matcher.ExpectedCommandId}";
            _result.text = $"MATCHED {_lastStatus.Matched}   ·   TIMEOUT {_lastStatus.TimedOut}   ·   RESTARTED {_lastStatus.Restarted}   ·   ACTIONS {_buttonActionCount}";
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
                _buttons[index].style.fontSize = compact ? 11f : 13f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
