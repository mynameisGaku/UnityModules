using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CanonicalPayload.Samples
{
    /// <summary>明示schemaのencode、decode、破損拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CanonicalPayloadBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "canonical-payload-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "canonical-payload-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "canonical-payload-basics-description";

        /// <summary>形式表示要素名。</summary>
        public const string ConfigurationElementName = "canonical-payload-basics-configuration";

        /// <summary>payload状態要素名。</summary>
        public const string PayloadElementName = "canonical-payload-basics-payload";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "canonical-payload-basics-stage";

        /// <summary>byte列表示要素名。</summary>
        public const string ResultElementName = "canonical-payload-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "canonical-payload-basics-buttons";

        /// <summary>Encode Button要素名。</summary>
        public const string EncodeButtonElementName = "canonical-payload-basics-encode";

        /// <summary>Decode Button要素名。</summary>
        public const string DecodeButtonElementName = "canonical-payload-basics-decode";

        /// <summary>Corrupt Copy Button要素名。</summary>
        public const string CorruptButtonElementName = "canonical-payload-basics-corrupt";

        /// <summary>Rebuild Button要素名。</summary>
        public const string RebuildButtonElementName = "canonical-payload-basics-rebuild";

        /// <summary>Reset Button要素名。</summary>
        public const string ResetButtonElementName = "canonical-payload-basics-reset";

        /// <summary>サンプルschemaから得る固定payload hex。</summary>
        public const string ExpectedPayloadHex = "F6FFFFFF0000A03F0A000000E7A7BBE58B95F09F9A800300000000FF7F01";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _payload;
        private Label _stage;
        private Label _result;
        private Button[] _buttons;
        private CanonicalPayloadValue _lastPayload;
        private bool _decodeVerified;
        private bool _corruptionRejected;
        private bool _rebuildMatched;
        private int _buttonActionCount;

        /// <summary>最後にEncodeまたはResetしたimmutable payload。</summary>
        public CanonicalPayloadValue LastPayload => _lastPayload;

        /// <summary>schema順のDecodeが全fieldと末尾を確認したか。</summary>
        public bool DecodeVerified => _decodeVerified;

        /// <summary>破損copyがoriginalを変えずに拒否されたか。</summary>
        public bool CorruptionRejected => _corruptionRejected;

        /// <summary>同じschema入力の再Buildが完全一致したか。</summary>
        public bool RebuildMatched => _rebuildMatched;

        /// <summary>最後のpayload byte数。</summary>
        public int PayloadByteCount => _lastPayload.ByteCount;

        /// <summary>最後のpayloadを大文字hexで表した値。</summary>
        public string PayloadHex => ToHex(_lastPayload);

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

            _title = AddLabel(TitleElementName, "Canonical Payload Basics", 32f, new Color(0.94f, 1f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "明示schema順の値をportable bytesへEncodeし、同じ順序でDecodeします。", 15f, new Color(0.78f, 0.88f, 0.88f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "LITTLE-ENDIAN  ·  IEEE 754 BITS  ·  STRICT UTF-8  ·  UINT32 LENGTH", 12f, new Color(0.45f, 1f, 0.72f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _payload = AddLabel(PayloadElementName, string.Empty, 13f, new Color(0.9f, 0.95f, 0.96f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.35f, 0.9f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 11f, new Color(0.82f, 0.92f, 0.92f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.01f, 0.055f, 0.06f, 1f);
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
                CreateButton(EncodeButtonElementName, "Encode Schema", EncodeSchema),
                CreateButton(DecodeButtonElementName, "Decode Payload", DecodePayload),
                CreateButton(CorruptButtonElementName, "Corrupt Copy", CorruptCopy),
                CreateButton(RebuildButtonElementName, "Rebuild Same", RebuildSame),
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

        private void EncodeSchema()
        {
            _lastPayload = BuildSamplePayload();
            _decodeVerified = false;
            _corruptionRejected = false;
            _rebuildMatched = false;
            _buttonActionCount++;
            _stage.text = "Encoded explicit schema / immutable payload ready";
            RefreshLabels();
        }

        private void DecodePayload()
        {
            EnsureSamplePayload();
            if (!_lastPayload.TryCreateReader(out var reader, out var createError)) throw new InvalidOperationException(createError.ToString());
            var passed = reader.TryReadInt32(out var damage, out _)
                && reader.TryReadSingle(out var speed, out _)
                && reader.TryReadString(out var label, out _)
                && reader.TryReadBytes(out var marker, out _)
                && reader.TryReadBoolean(out var enabled, out _)
                && damage == -10
                && BitConverter.SingleToInt32Bits(speed) == BitConverter.SingleToInt32Bits(1.25f)
                && string.Equals(label, "移動🚀", StringComparison.Ordinal)
                && marker.AsSpan().SequenceEqual(new byte[] { 0x00, 0xFF, 0x7F })
                && enabled
                && reader.IsAtEnd;
            _decodeVerified = passed;
            _buttonActionCount++;
            _stage.text = passed ? "Decode verified / every field and payload end match" : "Decode mismatch";
            RefreshLabels();
        }

        private void CorruptCopy()
        {
            EnsureSamplePayload();
            var bytes = _lastPayload.ToByteArray();
            bytes[8] = 0xFF;
            bytes[9] = 0xFF;
            bytes[10] = 0xFF;
            bytes[11] = 0x7F;
            if (!CanonicalPayloadValue.TryCreate(bytes, out var corrupted, out var createError)) throw new InvalidOperationException(createError.ToString());
            if (!corrupted.TryCreateReader(out var reader, out var readerError)) throw new InvalidOperationException(readerError.ToString());
            var prefixRead = reader.TryReadInt32(out _, out _) && reader.TryReadSingle(out _, out _);
            var rejected = !reader.TryReadString(out _, out var error) && error == CanonicalPayloadError.InvalidLength && reader.Position == 8;
            _corruptionRejected = prefixRead && rejected && string.Equals(PayloadHex, ExpectedPayloadHex, StringComparison.Ordinal);
            _buttonActionCount++;
            _stage.text = _corruptionRejected ? "Corrupted copy rejected / original payload unchanged" : "Corruption check failed";
            RefreshLabels();
        }

        private void RebuildSame()
        {
            EnsureSamplePayload();
            var rebuilt = BuildSamplePayload();
            _rebuildMatched = rebuilt == _lastPayload;
            _buttonActionCount++;
            _stage.text = _rebuildMatched ? "Rebuild matched / canonical bytes are stable" : "Rebuild mismatch";
            RefreshLabels();
        }

        private void ResetState() => ResetStateCore();

        private void ResetStateCore()
        {
            using var writer = new CanonicalPayloadWriter();
            if (!writer.TryBuild(out _lastPayload, out var error)) throw new InvalidOperationException(error.ToString());
            _decodeVerified = false;
            _corruptionRejected = false;
            _rebuildMatched = false;
            _buttonActionCount = 0;
            _stage.text = "Ready / encode explicit schema values";
            RefreshLabels();
        }

        private void EnsureSamplePayload()
        {
            if (_lastPayload.ByteCount == 0) _lastPayload = BuildSamplePayload();
        }

        private static CanonicalPayloadValue BuildSamplePayload()
        {
            using var writer = new CanonicalPayloadWriter(256);
            if (!writer.TryWriteInt32(-10, out var error)
                || !writer.TryWriteSingle(1.25f, out error)
                || !writer.TryWriteString("移動🚀", out error)
                || !writer.TryWriteBytes(new byte[] { 0x00, 0xFF, 0x7F }, out error)
                || !writer.TryWriteBoolean(true, out error)
                || !writer.TryBuild(out var value, out error)) throw new InvalidOperationException(error.ToString());
            return value;
        }

        private void RefreshLabels()
        {
            _payload.text = $"Payload {PayloadByteCount} bytes   ·   Actions {_buttonActionCount}   ·   Decode {_decodeVerified}   ·   Corrupt {_corruptionRejected}   ·   Rebuild {_rebuildMatched}";
            _result.text = PayloadByteCount == 0 ? "EMPTY PAYLOAD" : PayloadHex;
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
            _payload.style.fontSize = compact ? 10f : 13f;
            _payload.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 10f;
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

        private static string ToHex(CanonicalPayloadValue value) => BitConverter.ToString(value.ToByteArray()).Replace("-", string.Empty);
    }
}
