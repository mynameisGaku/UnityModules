using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputChording.Samples
{
    /// <summary>明示tickでのrequired command押下edgeとchord span判定を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputChordMatcherBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-chord-matcher-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-chord-matcher-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-chord-matcher-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-chord-matcher-basics-configuration";

        /// <summary>repeat状態要素名。</summary>
        public const string InputElementName = "input-chord-matcher-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-chord-matcher-basics-stage";

        /// <summary>最終結果要素名。</summary>
        public const string ResultElementName = "input-chord-matcher-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-chord-matcher-basics-buttons";

        /// <summary>Guard押下Button要素名。</summary>
        public const string GuardButtonElementName = "input-chord-matcher-basics-guard";

        /// <summary>Light追加Button要素名。</summary>
        public const string LightButtonElementName = "input-chord-matcher-basics-light";

        /// <summary>Heavy追加Button要素名。</summary>
        public const string HeavyButtonElementName = "input-chord-matcher-basics-heavy";

        /// <summary>Guard解放Button要素名。</summary>
        public const string ReleaseGuardButtonElementName = "input-chord-matcher-basics-release-guard";

        /// <summary>遅延Guard再押下Button要素名。</summary>
        public const string LateGuardButtonElementName = "input-chord-matcher-basics-late-guard";

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
        private InputChordMatcher _matcher;
        private InputChordStatus _lastStatus;
        private InputChordError _lastError;
        private int _buttonActionCount;

        /// <summary>現在の明示simulation tick。</summary>
        public ulong CurrentTick => _matcher?.CurrentTick ?? 0;

        /// <summary>押下中のrequired command数。</summary>
        public int PressedRequiredCommandCount => _lastStatus.PressedRequiredCommandCount;

        /// <summary>required commandがすべて押下中か。</summary>
        public bool IsComplete => _lastStatus.IsComplete;

        /// <summary>最後のsampleでchordが成立したか。</summary>
        public bool LastTriggered => _lastStatus.Triggered;

        /// <summary>最後のsampleでspan上限を超えたか。</summary>
        public bool LastSpanExceeded => _lastStatus.SpanExceeded;

        /// <summary>最後のsampleで次のchordへ再armしたか。</summary>
        public bool LastRearmed => _lastStatus.Rearmed;

        /// <summary>最後のcomplete chordの押下edge間tick差。</summary>
        public ulong LastPressSpanTicks => _lastStatus.PressSpanTicks;

        /// <summary>最後のAPI error。</summary>
        public InputChordError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Chord Matcher Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "required command 1・2・3の押下edgeが明示tickの許容span内に揃った時だけchordを発火します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "REQUIRED 1 + 2 + 3  ·  MAXIMUM SPAN 2 TICKS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(GuardButtonElementName, "Guard 1 @100", () => Sample(100, "Guard pressed  ·  1 / 3", 1)),
                CreateButton(LightButtonElementName, "Light 2 @101", () => Sample(101, "Light added  ·  2 / 3", 1, 2)),
                CreateButton(HeavyButtonElementName, "Heavy 3 @102  ·  Match", () => Sample(102, "Chord matched  ·  span 2", 1, 2, 3)),
                CreateButton(ReleaseGuardButtonElementName, "Release Guard @103", () => Sample(103, "Guard released  ·  rearmed", 2, 3)),
                CreateButton(LateGuardButtonElementName, "Guard 1 @106  ·  Late", () => Sample(106, "Chord rejected  ·  span 5", 1, 2, 3))
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

        private void Sample(ulong tick, string stage, params int[] pressedCommandIds)
        {
            var succeeded = _matcher.TrySample(tick, pressedCommandIds, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Sample failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputChordMatcher.TryCreate(new[] { 1, 2, 3 }, 2, 100, out _matcher, out var error)) throw new InvalidOperationException($"Input Chord Matcher Basics configuration is invalid: {error}.");
            _lastStatus = _matcher.Snapshot();
            _lastError = InputChordError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  guard, light, heavy, release, late guard";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_matcher.CurrentTick}   ·   PRESSED {_lastStatus.PressedRequiredCommandCount} / {_lastStatus.RequiredCommandCount}   ·   COMPLETE {_lastStatus.IsComplete}";
            _result.text = $"TRIGGERED {_lastStatus.Triggered}   ·   SPAN {_lastStatus.PressSpanTicks}   ·   EXCEEDED {_lastStatus.SpanExceeded}   ·   REARMED {_lastStatus.Rearmed}   ·   ACTIONS {_buttonActionCount}";
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
