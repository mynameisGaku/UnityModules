using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayTape.Samples
{
    /// <summary>tick付きcommandの記録、build、replayを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ReplayTapeBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "replay-tape-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "replay-tape-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "replay-tape-basics-description";

        /// <summary>形式表示要素名。</summary>
        public const string ConfigurationElementName = "replay-tape-basics-configuration";

        /// <summary>model表示要素名。</summary>
        public const string ModelElementName = "replay-tape-basics-model";

        /// <summary>結果表示要素名。</summary>
        public const string StageElementName = "replay-tape-basics-stage";

        /// <summary>tape表示要素名。</summary>
        public const string TapeElementName = "replay-tape-basics-tape";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "replay-tape-basics-buttons";

        /// <summary>Move記録Button要素名。</summary>
        public const string RecordMoveButtonElementName = "replay-tape-basics-record-move";

        /// <summary>Damage記録Button要素名。</summary>
        public const string RecordDamageButtonElementName = "replay-tape-basics-record-damage";

        /// <summary>Build Button要素名。</summary>
        public const string BuildButtonElementName = "replay-tape-basics-build";

        /// <summary>Replay Button要素名。</summary>
        public const string ReplayButtonElementName = "replay-tape-basics-replay";

        /// <summary>Reset Button要素名。</summary>
        public const string ResetButtonElementName = "replay-tape-basics-reset";

        private const uint MoveCommandId = 1;
        private const uint DamageCommandId = 2;
        private const int InitialHealth = 100;
        private const int InitialPositionX = 0;

        private UIDocument _document;
        private ReplayTapeBuilder _builder;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _model;
        private Label _stage;
        private Label _tape;
        private Button[] _buttons;
        private ReplayTapeValue _lastTape;
        private ulong _nextTick;
        private ulong _lastRecordedTick;
        private int _health;
        private int _positionX;
        private int _buttonActionCount;
        private bool _hasRecordedTick;
        private bool _replayVerified;

        /// <summary>最後にBuildまたはReplayしたimmutable tape。</summary>
        public ReplayTapeValue LastTape => _lastTape;

        /// <summary>Replay後のmodelが記録時と一致したか。</summary>
        public bool ReplayVerified => _replayVerified;

        /// <summary>現在のhealth。</summary>
        public int Health => _health;

        /// <summary>現在のposition X。</summary>
        public int PositionX => _positionX;

        /// <summary>記録済みentry数。</summary>
        public int EntryCount => _builder?.EntryCount ?? 0;

        /// <summary>headerを含む現在のbuilder byte数。</summary>
        public int TapeByteCount => _builder?.ByteCount ?? 0;

        /// <summary>次の記録に使うtick。</summary>
        public ulong NextTick => _nextTick;

        /// <summary>実Button操作数。</summary>
        public int ButtonActionCount => _buttonActionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _builder = new ReplayTapeBuilder(4096, 64);
            BuildUi();
            ResetStateCore();
        }

        private void OnDisable()
        {
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            if (_document != null && _document.rootVisualElement != null) _document.rootVisualElement.Clear();
            _builder?.Dispose();
            _builder = null;
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
            _root.style.backgroundColor = new Color(0.015f, 0.035f, 0.045f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.025f, 0.12f, 0.13f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Replay Tape Basics", 32f, new Color(0.94f, 1f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "tick・command id・opaque payloadを記録し、同じ順序でmodelへReplayします。", 15f, new Color(0.78f, 0.88f, 0.88f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "FORMAT v1  ·  nondecreasing tick  ·  canonical little-endian records", 12f, new Color(0.45f, 1f, 0.72f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _model = AddLabel(ModelElementName, string.Empty, 13f, new Color(0.9f, 0.95f, 0.96f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.35f, 0.9f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _tape = AddLabel(TapeElementName, string.Empty, 11f, new Color(0.82f, 0.92f, 0.92f, 1f));
            _tape.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tape.style.backgroundColor = new Color(0.01f, 0.055f, 0.06f, 1f);
            _tape.style.borderTopLeftRadius = 10f;
            _tape.style.borderTopRightRadius = 10f;
            _tape.style.borderBottomLeftRadius = 10f;
            _tape.style.borderBottomRightRadius = 10f;
            _tape.style.paddingTop = 8f;
            _tape.style.paddingBottom = 8f;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);

            _buttons = new[]
            {
                CreateButton(RecordMoveButtonElementName, "Record Move +1", RecordMove),
                CreateButton(RecordDamageButtonElementName, "Record Damage -10", RecordDamage),
                CreateButton(BuildButtonElementName, "Build Tape", BuildTape),
                CreateButton(ReplayButtonElementName, "Replay Tape", ReplayTape),
                CreateButton(ResetButtonElementName, "Reset", ResetState)
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
            button.style.color = new Color(0.05f, 0.12f, 0.13f, 1f);
            button.style.backgroundColor = new Color(0.78f, 0.86f, 0.84f, 1f);
            return button;
        }

        private void RecordMove()
        {
            Record(MoveCommandId, 1);
            _stage.text = "Recorded Move +1 / model applied immediately";
            RefreshLabels();
        }

        private void RecordDamage()
        {
            Record(DamageCommandId, -10);
            _stage.text = "Recorded Damage -10 / model applied immediately";
            RefreshLabels();
        }

        private void Record(uint commandId, int value)
        {
            Span<byte> payload = stackalloc byte[4];
            WriteInt32LittleEndian(payload, value);
            if (!_builder.TryAppend(_nextTick, commandId, payload, out var error)) throw new InvalidOperationException(error.ToString());
            ApplyCommand(commandId, value);
            _lastRecordedTick = _nextTick;
            _hasRecordedTick = true;
            _nextTick += 10;
            _buttonActionCount++;
            _replayVerified = false;
        }

        private void BuildTape()
        {
            if (!_builder.TryBuild(out _lastTape, out var error)) throw new InvalidOperationException(error.ToString());
            _buttonActionCount++;
            _replayVerified = false;
            _stage.text = $"Built immutable tape / {_lastTape.EntryCount} entries";
            RefreshLabels();
        }

        private void ReplayTape()
        {
            var expectedHealth = _health;
            var expectedPosition = _positionX;
            if (!_builder.TryBuild(out _lastTape, out var buildError)) throw new InvalidOperationException(buildError.ToString());
            if (!_lastTape.TryCreateReader(out var reader, out var readerError)) throw new InvalidOperationException(readerError.ToString());
            _health = InitialHealth;
            _positionX = InitialPositionX;
            while (reader.TryRead(out var entry, out var readError))
            {
                var payload = entry.ToPayloadArray();
                if (payload.Length != 4) throw new InvalidOperationException("Sample command payload length mismatch.");
                ApplyCommand(entry.CommandId, ReadInt32LittleEndian(payload));
            }

            if (reader.RemainingCount != 0) throw new InvalidOperationException("Reader did not reach tape end.");
            _replayVerified = _health == expectedHealth && _positionX == expectedPosition;
            _buttonActionCount++;
            _stage.text = _replayVerified ? "Replay verified / recorded model matches" : "Replay mismatch";
            RefreshLabels();
        }

        private void ResetState() => ResetStateCore();

        private void ResetStateCore()
        {
            var resetError = _builder.Reset();
            if (resetError != ReplayTapeError.None) throw new InvalidOperationException(resetError.ToString());
            _health = InitialHealth;
            _positionX = InitialPositionX;
            _nextTick = 10;
            _lastRecordedTick = 0;
            _hasRecordedTick = false;
            _replayVerified = false;
            _buttonActionCount = 0;
            if (!_builder.TryBuild(out _lastTape, out var buildError)) throw new InvalidOperationException(buildError.ToString());
            _stage.text = "Ready / record explicit commands, then replay";
            RefreshLabels();
        }

        private void ApplyCommand(uint commandId, int value)
        {
            switch (commandId)
            {
                case MoveCommandId:
                    _positionX += value;
                    break;
                case DamageCommandId:
                    _health = Math.Max(0, _health + value);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown sample command id: {commandId}.");
            }
        }

        private void RefreshLabels()
        {
            _model.text = $"Next Tick {_nextTick}   ·   Health {_health}   ·   X {_positionX}   ·   Entries {EntryCount}   ·   Actions {_buttonActionCount}";
            var lastTick = _hasRecordedTick ? _lastRecordedTick.ToString() : "-";
            _tape.text = $"Tape {TapeByteCount} bytes   ·   Entries {EntryCount}   ·   Last Tick {lastTick}";
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
            _title.style.fontSize = compact ? 23f : 32f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 11f : 15f;
            _description.style.marginBottom = compact ? 5f : 10f;
            _configuration.style.fontSize = compact ? 9.5f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _model.style.fontSize = compact ? 10f : 13f;
            _model.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _tape.style.fontSize = compact ? 9f : 11f;
            _tape.style.paddingTop = compact ? 4f : 8f;
            _tape.style.paddingBottom = compact ? 4f : 8f;
            _tape.style.marginBottom = compact ? 4f : 9f;
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

        private static void WriteInt32LittleEndian(Span<byte> destination, int value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
        {
            return source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24);
        }
    }
}
