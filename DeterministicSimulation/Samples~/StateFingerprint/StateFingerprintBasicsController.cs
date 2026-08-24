using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateFingerprint.Samples
{
    /// <summary>型付きfield列の一致・差分・snapshot再現を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StateFingerprintBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "state-fingerprint-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "state-fingerprint-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "state-fingerprint-basics-description";

        /// <summary>形式表示要素名。</summary>
        public const string ConfigurationElementName = "state-fingerprint-basics-configuration";

        /// <summary>model表示要素名。</summary>
        public const string ModelElementName = "state-fingerprint-basics-model";

        /// <summary>結果表示要素名。</summary>
        public const string StageElementName = "state-fingerprint-basics-stage";

        /// <summary>fingerprint表示要素名。</summary>
        public const string FingerprintElementName = "state-fingerprint-basics-fingerprint";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "state-fingerprint-basics-buttons";

        /// <summary>Build Button要素名。</summary>
        public const string BuildButtonElementName = "state-fingerprint-basics-build";

        /// <summary>Damage Button要素名。</summary>
        public const string DamageButtonElementName = "state-fingerprint-basics-damage";

        /// <summary>Move Button要素名。</summary>
        public const string MoveButtonElementName = "state-fingerprint-basics-move";

        /// <summary>Replay Button要素名。</summary>
        public const string ReplayButtonElementName = "state-fingerprint-basics-replay";

        /// <summary>Reset Button要素名。</summary>
        public const string ResetButtonElementName = "state-fingerprint-basics-reset";

        private const ulong InitialTick = 42UL;
        private const int InitialHealth = 100;
        private const double InitialPositionX = 12.5d;
        private const string InitialPlayerName = "Player One";

        private UIDocument _document;
        private StateFingerprintBuilder _builder;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _model;
        private Label _stage;
        private Label _fingerprint;
        private Button[] _buttons;
        private ulong _tick;
        private int _health;
        private double _positionX;
        private string _playerName;
        private bool _active;
        private StateFingerprintValue _baselineFingerprint;
        private StateFingerprintValue _lastFingerprint;
        private bool _replayVerified;
        private int _actionCount;

        /// <summary>最後に表示したfingerprint。</summary>
        public StateFingerprintValue LastFingerprint => _lastFingerprint;

        /// <summary>初期modelのfingerprint。</summary>
        public StateFingerprintValue BaselineFingerprint => _baselineFingerprint;

        /// <summary>Replayで復元後の一致を確認できたか。</summary>
        public bool ReplayVerified => _replayVerified;

        /// <summary>現在のhealth。</summary>
        public int Health => _health;

        /// <summary>現在のposition X。</summary>
        public double PositionX => _positionX;

        /// <summary>実行済み操作数。</summary>
        public int ActionCount => _actionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _builder = new StateFingerprintBuilder(4096);
            BuildUi();
            ResetModel();
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

            _title = AddLabel(TitleElementName, "State Fingerprint Basics", 32f, new Color(0.94f, 1f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "同じ型・field id・値・順序から、同じ256-bit fingerprintを再現します。", 15f, new Color(0.78f, 0.88f, 0.88f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "FORMAT v1  ·  SHA-256  ·  canonical little-endian fields", 12f, new Color(0.45f, 1f, 0.72f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _model = AddLabel(ModelElementName, string.Empty, 13f, new Color(0.9f, 0.95f, 0.96f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.35f, 0.9f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _fingerprint = AddLabel(FingerprintElementName, string.Empty, 11f, new Color(0.82f, 0.92f, 0.92f, 1f));
            _fingerprint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _fingerprint.style.backgroundColor = new Color(0.01f, 0.055f, 0.06f, 1f);
            _fingerprint.style.borderTopLeftRadius = 10f;
            _fingerprint.style.borderTopRightRadius = 10f;
            _fingerprint.style.borderBottomLeftRadius = 10f;
            _fingerprint.style.borderBottomRightRadius = 10f;
            _fingerprint.style.paddingTop = 8f;
            _fingerprint.style.paddingBottom = 8f;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);

            _buttons = new[]
            {
                CreateButton(BuildButtonElementName, "Build Fingerprint", BuildCurrent),
                CreateButton(DamageButtonElementName, "Damage -10", Damage),
                CreateButton(MoveButtonElementName, "Move +0.25", Move),
                CreateButton(ReplayButtonElementName, "Replay Snapshot", ReplaySnapshot),
                CreateButton(ResetButtonElementName, "Reset State", ResetModel)
            };
            for (var i = 0; i < _buttons.Length; i++) _buttonRow.Add(_buttons[i]);

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

        private void BuildCurrent()
        {
            _actionCount++;
            _replayVerified = false;
            _lastFingerprint = ComputeFingerprint();
            _stage.text = _lastFingerprint == _baselineFingerprint ? "Exact baseline / same state" : "Fingerprint built / current state";
            RefreshLabels();
        }

        private void Damage()
        {
            _health = Math.Max(0, _health - 10);
            _tick++;
            _actionCount++;
            _replayVerified = false;
            _lastFingerprint = ComputeFingerprint();
            _stage.text = "Changed / Health and Tick altered the digest";
            RefreshLabels();
        }

        private void Move()
        {
            _positionX += 0.25d;
            _tick++;
            _actionCount++;
            _replayVerified = false;
            _lastFingerprint = ComputeFingerprint();
            _stage.text = "Changed / Position and Tick altered the digest";
            RefreshLabels();
        }

        private void ReplaySnapshot()
        {
            var tick = _tick;
            var health = _health;
            var positionX = _positionX;
            var playerName = _playerName;
            var active = _active;
            var expected = ComputeFingerprint();

            _tick += 99;
            _health = Math.Max(0, _health - 7);
            _positionX += 3.5d;
            _active = !_active;
            var divergent = ComputeFingerprint();

            _tick = tick;
            _health = health;
            _positionX = positionX;
            _playerName = playerName;
            _active = active;
            _lastFingerprint = ComputeFingerprint();
            _replayVerified = _lastFingerprint == expected && divergent != expected;
            _actionCount++;
            _stage.text = _replayVerified ? "Replay verified / restored fields match exactly" : "Replay mismatch";
            RefreshLabels();
        }

        private void ResetModel()
        {
            _tick = InitialTick;
            _health = InitialHealth;
            _positionX = InitialPositionX;
            _playerName = InitialPlayerName;
            _active = true;
            _actionCount = 0;
            _replayVerified = false;
            _baselineFingerprint = ComputeFingerprint();
            _lastFingerprint = _baselineFingerprint;
            _stage.text = "Ready / explicit fields, no reflection or global state";
            RefreshLabels();
        }

        private StateFingerprintValue ComputeFingerprint()
        {
            RequireSuccess(_builder.Reset());
            RequireSuccess(_builder.WriteUInt64(1, _tick));
            RequireSuccess(_builder.WriteInt32(2, _health));
            RequireSuccess(_builder.WriteDouble(3, _positionX));
            RequireSuccess(_builder.WriteString(4, _playerName));
            RequireSuccess(_builder.WriteBoolean(5, _active));
            if (!_builder.TryBuild(out var value, out var error)) throw new InvalidOperationException(error.ToString());
            return value;
        }

        private static void RequireSuccess(StateFingerprintError error)
        {
            if (error != StateFingerprintError.None) throw new InvalidOperationException(error.ToString());
        }

        private void RefreshLabels()
        {
            _model.text = $"Tick {_tick}   ·   Health {_health}   ·   X {_positionX:0.00}   ·   Active {_active}   ·   Actions {_actionCount}";
            var hex = _lastFingerprint.ToString();
            _fingerprint.text = $"{hex.Substring(0, 32)}\n{hex.Substring(32, 32)}";
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
            _fingerprint.style.fontSize = compact ? 9f : 11f;
            _fingerprint.style.paddingTop = compact ? 4f : 8f;
            _fingerprint.style.paddingBottom = compact ? 4f : 8f;
            _fingerprint.style.marginBottom = compact ? 4f : 9f;
            _buttonRow.style.marginTop = compact ? 1f : 3f;
            for (var i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].style.flexBasis = compact ? 160f : 130f;
                _buttons[i].style.minWidth = compact ? 140f : 110f;
                _buttons[i].style.minHeight = compact ? 30f : 42f;
                _buttons[i].style.fontSize = compact ? 11f : 13f;
                _buttons[i].style.marginLeft = 4f;
                _buttons[i].style.marginRight = 4f;
                _buttons[i].style.marginTop = 2f;
                _buttons[i].style.marginBottom = 2f;
            }
        }
    }
}
