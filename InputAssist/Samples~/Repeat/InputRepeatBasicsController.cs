using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputRepeating.Samples
{
    /// <summary>明示tickでの押下edge、保持delay、repeat catch-up、解放edgeを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputRepeatBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-repeat-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-repeat-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-repeat-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-repeat-basics-configuration";

        /// <summary>repeat状態要素名。</summary>
        public const string InputElementName = "input-repeat-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-repeat-basics-stage";

        /// <summary>最終結果要素名。</summary>
        public const string ResultElementName = "input-repeat-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-repeat-basics-buttons";

        /// <summary>押下edge Button要素名。</summary>
        public const string PressButtonElementName = "input-repeat-basics-press";

        /// <summary>delay前保持Button要素名。</summary>
        public const string HoldBeforeDelayButtonElementName = "input-repeat-basics-hold-before-delay";

        /// <summary>初回repeat Button要素名。</summary>
        public const string FirstRepeatButtonElementName = "input-repeat-basics-first-repeat";

        /// <summary>catch-up Button要素名。</summary>
        public const string CatchUpButtonElementName = "input-repeat-basics-catch-up";

        /// <summary>解放edge Button要素名。</summary>
        public const string ReleaseButtonElementName = "input-repeat-basics-release";

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
        private InputRepeatTracker _tracker;
        private InputRepeatStatus _lastStatus;
        private InputRepeatError _lastError;
        private int _buttonActionCount;

        /// <summary>現在の明示simulation tick。</summary>
        public ulong CurrentTick => _tracker?.CurrentTick ?? 0;

        /// <summary>現在押下中か。</summary>
        public bool IsPressed => _tracker?.IsPressed ?? false;

        /// <summary>最後のsampleが初回triggerを発行したか。</summary>
        public bool LastInitialTriggered => _lastStatus.InitialTriggered;

        /// <summary>最後のsampleで新しく期限へ到達したrepeat数。</summary>
        public ulong LastRepeatTriggerCount => _lastStatus.RepeatTriggerCount;

        /// <summary>最後のsampleで発行すべき全trigger数。</summary>
        public ulong LastTriggerCount => _lastStatus.TriggerCount;

        /// <summary>最後のsampleで解放edgeを検出したか。</summary>
        public bool LastReleased => _lastStatus.Released;

        /// <summary>最後のAPI error。</summary>
        public InputRepeatError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Repeat Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "押下edgeを即時triggerし、保持中は明示tickのdelayとintervalからrepeat件数を再現します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "INITIAL DELAY 3 TICKS  ·  REPEAT INTERVAL 2 TICKS", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(PressButtonElementName, "Press @100  ·  Initial", () => Push(100, true, "Press edge  ·  trigger 1")),
                CreateButton(HoldBeforeDelayButtonElementName, "Hold @102  ·  Wait", () => Push(102, true, "Before delay  ·  trigger 0")),
                CreateButton(FirstRepeatButtonElementName, "Hold @103  ·  Repeat", () => Push(103, true, "First repeat due  ·  trigger 1")),
                CreateButton(CatchUpButtonElementName, "Hold @110  ·  Catch Up", () => Push(110, true, "Tick jump  ·  3 new repeats")),
                CreateButton(ReleaseButtonElementName, "Release @111", () => Push(111, false, "Release edge  ·  repeat state cleared"))
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

        private void Push(ulong tick, bool pressed, string stage)
        {
            var succeeded = _tracker.TryPush(tick, pressed, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Push failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputRepeatTracker.TryCreate(3, 2, 100, out _tracker, out var error)) throw new InvalidOperationException($"Input Repeat Basics configuration is invalid: {error}.");
            _lastStatus = _tracker.Snapshot();
            _lastError = InputRepeatError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  press, hold, catch up, release";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_tracker.CurrentTick}   ·   PRESSED {_tracker.IsPressed}   ·   DELAY {_tracker.InitialDelayTicks}   ·   INTERVAL {_tracker.RepeatIntervalTicks}";
            _result.text = $"INITIAL {_lastStatus.InitialTriggered}   ·   REPEATS {_lastStatus.RepeatTriggerCount}   ·   TRIGGERS {_lastStatus.TriggerCount}   ·   RELEASED {_lastStatus.Released}   ·   ACTIONS {_buttonActionCount}";
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
