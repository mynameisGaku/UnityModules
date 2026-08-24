using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputBuffering.Samples
{
    /// <summary>明示tickでのcommand記録、期限内消費、期限切れを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputCommandBufferBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-command-buffer-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-command-buffer-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-command-buffer-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-command-buffer-basics-configuration";

        /// <summary>buffer状態要素名。</summary>
        public const string InputElementName = "input-command-buffer-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-command-buffer-basics-stage";

        /// <summary>最終結果要素名。</summary>
        public const string ResultElementName = "input-command-buffer-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-command-buffer-basics-buttons";

        /// <summary>Jump記録Button要素名。</summary>
        public const string BufferJumpButtonElementName = "input-command-buffer-basics-buffer-jump";

        /// <summary>1 tick前進Button要素名。</summary>
        public const string AdvanceOneButtonElementName = "input-command-buffer-basics-advance-one";

        /// <summary>Jump消費Button要素名。</summary>
        public const string ConsumeJumpButtonElementName = "input-command-buffer-basics-consume-jump";

        /// <summary>Dash記録Button要素名。</summary>
        public const string BufferDashButtonElementName = "input-command-buffer-basics-buffer-dash";

        /// <summary>期限切れButton要素名。</summary>
        public const string ExpireButtonElementName = "input-command-buffer-basics-expire";

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
        private InputCommandBuffer _buffer;
        private BufferedInputCommand _lastCommand;
        private InputCommandBufferError _lastError;
        private bool _lastConsumed;
        private int _lastExpiredCount;
        private int _buttonActionCount;

        /// <summary>現在の明示simulation tick。</summary>
        public ulong CurrentTick => _buffer?.CurrentTick ?? 0;

        /// <summary>現在保持しているcommand数。</summary>
        public int BufferedCount => _buffer?.Count ?? 0;

        /// <summary>最後に記録または消費したcommand id。</summary>
        public int LastCommandId => _lastCommand.CommandId;

        /// <summary>最後に扱ったcommandの記録tick。</summary>
        public ulong LastRecordedTick => _lastCommand.RecordedTick;

        /// <summary>最後の操作がcommand消費だったか。</summary>
        public bool LastConsumed => _lastConsumed;

        /// <summary>最後のtick前進で期限切れになったcommand数。</summary>
        public int LastExpiredCount => _lastExpiredCount;

        /// <summary>最後のAPI error。</summary>
        public InputCommandBufferError LastError => _lastError;

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

            _title = AddLabel(TitleElementName, "Input Command Buffer Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "早押しcommandを明示tickの短い有効期間だけ保持し、利用可能になった時にFIFOで消費します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "CAPACITY 3  ·  RETENTION +2 TICKS  ·  INITIAL TICK 100", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(BufferJumpButtonElementName, "Buffer Jump @100", BufferJump),
                CreateButton(AdvanceOneButtonElementName, "Advance @101", AdvanceOne),
                CreateButton(ConsumeJumpButtonElementName, "Consume Jump", ConsumeJump),
                CreateButton(BufferDashButtonElementName, "Buffer Dash @101", BufferDash),
                CreateButton(ExpireButtonElementName, "Expire @104", ExpireDash)
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

        private void BufferJump()
        {
            Record(1, "Jump buffered at tick 100");
        }

        private void AdvanceOne()
        {
            Advance(101, "Advanced to tick 101  ·  Jump remains valid");
        }

        private void ConsumeJump()
        {
            _lastConsumed = _buffer.TryConsume(1, out _lastCommand, out _lastError);
            _lastExpiredCount = 0;
            _buttonActionCount++;
            _stage.text = _lastConsumed ? "Jump consumed  ·  recorded at tick 100" : $"Jump consume failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void BufferDash()
        {
            Record(2, "Dash buffered at tick 101");
        }

        private void ExpireDash()
        {
            Advance(104, "Advanced to tick 104  ·  Dash expired");
        }

        private void Record(int commandId, string stage)
        {
            var succeeded = _buffer.TryRecord(commandId, out _lastCommand, out _lastError);
            _lastConsumed = false;
            _lastExpiredCount = 0;
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Record failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void Advance(ulong tick, string stage)
        {
            var succeeded = _buffer.TryAdvanceTo(tick, out _lastExpiredCount, out _lastError);
            _lastConsumed = false;
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Advance failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputCommandBuffer.TryCreate(3, 2, 100, out _buffer, out var error)) throw new InvalidOperationException($"Input Command Buffer Basics configuration is invalid: {error}.");
            _lastCommand = default;
            _lastError = InputCommandBufferError.None;
            _lastConsumed = false;
            _lastExpiredCount = 0;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  buffer Jump before it can be consumed";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_buffer.CurrentTick}   ·   BUFFERED {_buffer.Count} / {_buffer.Capacity}   ·   RETENTION +{_buffer.RetentionTicks}";
            _result.text = $"LAST ID {_lastCommand.CommandId}   ·   CONSUMED {_lastConsumed}   ·   EXPIRED {_lastExpiredCount}   ·   ACTIONS {_buttonActionCount}";
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
