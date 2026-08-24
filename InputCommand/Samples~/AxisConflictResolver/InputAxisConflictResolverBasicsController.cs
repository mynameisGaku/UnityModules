using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputAxisConflict.Samples
{
    /// <summary>相反する2入力のLastPressedWins解決を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputAxisConflictResolverBasicsController : MonoBehaviour
    {
        /// <summary>サンプルcardを取得する為のUI element名。</summary>
        public const string CardElementName = "input-axis-conflict-resolver-basics-card";
        /// <summary>titleを取得する為のUI element名。</summary>
        public const string TitleElementName = "input-axis-conflict-resolver-basics-title";
        /// <summary>説明文を取得する為のUI element名。</summary>
        public const string DescriptionElementName = "input-axis-conflict-resolver-basics-description";
        /// <summary>設定表示を取得する為のUI element名。</summary>
        public const string ConfigurationElementName = "input-axis-conflict-resolver-basics-configuration";
        /// <summary>入力状態表示を取得する為のUI element名。</summary>
        public const string InputElementName = "input-axis-conflict-resolver-basics-input";
        /// <summary>進行状態表示を取得する為のUI element名。</summary>
        public const string StageElementName = "input-axis-conflict-resolver-basics-stage";
        /// <summary>解決結果表示を取得する為のUI element名。</summary>
        public const string ResultElementName = "input-axis-conflict-resolver-basics-result";
        /// <summary>button群を取得する為のUI element名。</summary>
        public const string ButtonRowElementName = "input-axis-conflict-resolver-basics-buttons";
        /// <summary>negative押下buttonを取得する為のUI element名。</summary>
        public const string NegativeButtonElementName = "input-axis-conflict-resolver-basics-negative";
        /// <summary>positive押下buttonを取得する為のUI element名。</summary>
        public const string PositiveButtonElementName = "input-axis-conflict-resolver-basics-positive";
        /// <summary>positive解放buttonを取得する為のUI element名。</summary>
        public const string ReleasePositiveButtonElementName = "input-axis-conflict-resolver-basics-release-positive";
        /// <summary>全入力解放buttonを取得する為のUI element名。</summary>
        public const string ReleaseAllButtonElementName = "input-axis-conflict-resolver-basics-release-all";
        /// <summary>同一tick両押下buttonを取得する為のUI element名。</summary>
        public const string SimultaneousButtonElementName = "input-axis-conflict-resolver-basics-simultaneous";

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
        private InputAxisConflictResolver _resolver;
        private InputAxisConflictStatus _lastStatus;
        private InputAxisConflictError _lastError;
        private int _buttonActionCount;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _resolver?.CurrentTick ?? 0;
        /// <summary>negative入力が押されているか。</summary>
        public bool NegativePressed => _lastStatus.NegativePressed;
        /// <summary>positive入力が押されているか。</summary>
        public bool PositivePressed => _lastStatus.PositivePressed;
        /// <summary>両入力が同時に押されているか。</summary>
        public bool HasConflict => _lastStatus.HasConflict;
        /// <summary>現在の解決値。</summary>
        public int ResolvedValue => _lastStatus.ResolvedValue;
        /// <summary>最後のsampleでnegative押下edgeが発生したか。</summary>
        public bool NegativePressedThisSample => _lastStatus.NegativePressedThisSample;
        /// <summary>最後のsampleでpositive押下edgeが発生したか。</summary>
        public bool PositivePressedThisSample => _lastStatus.PositivePressedThisSample;
        /// <summary>最後のsampleでnegative解放edgeが発生したか。</summary>
        public bool NegativeReleasedThisSample => _lastStatus.NegativeReleasedThisSample;
        /// <summary>最後のsampleでpositive解放edgeが発生したか。</summary>
        public bool PositiveReleasedThisSample => _lastStatus.PositiveReleasedThisSample;
        /// <summary>最後のsampleで解決値が変化したか。</summary>
        public bool ResolutionChanged => _lastStatus.ResolutionChanged;
        /// <summary>最後の操作error。</summary>
        public InputAxisConflictError LastError => _lastError;
        /// <summary>受理したbutton操作数。</summary>
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
            _title = AddLabel(TitleElementName, "Input Axis Conflict Resolver Basics", 30f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "negativeとpositiveの同時押下を、明示tickの押下edgeとpolicyから決定論的に解決します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "POLICY LAST PRESSED WINS  ·  SAME-TICK TIE IS NEUTRAL", 12f, new Color(0.55f, 1f, 0.82f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.92f, 0.93f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.48f, 0.90f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _result = AddLabel(ResultElementName, string.Empty, 11f, new Color(0.89f, 0.90f, 1f, 1f));
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
                CreateButton(NegativeButtonElementName, "Negative @100", () => Sample(100, true, false, "Negative selected  ·  -1")),
                CreateButton(PositiveButtonElementName, "Positive @101", () => Sample(101, true, true, "Positive edge wins  ·  +1")),
                CreateButton(ReleasePositiveButtonElementName, "Release + @102", () => Sample(102, true, false, "Fallback negative  ·  -1")),
                CreateButton(ReleaseAllButtonElementName, "Release All @103", () => Sample(103, false, false, "Released  ·  neutral")),
                CreateButton(SimultaneousButtonElementName, "Both @104  ·  Tie", () => Sample(104, true, true, "Same-tick tie  ·  neutral"))
            };
            for (var index = 0; index < _buttons.Length; index++) _buttonRow.Add(_buttons[index]);
            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ApplyResponsiveLayout();
        }

        private Label AddLabel(string name, string text, float size, Color color)
        {
            var label = new Label(text) { name = name };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            _card.Add(label);
            return label;
        }

        private static Button CreateButton(string name, string text, Action callback)
        {
            var button = new Button(callback) { name = name, text = text };
            button.style.flexGrow = 1f;
            button.style.color = new Color(0.05f, 0.06f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.75f, 0.81f, 1f, 1f);
            return button;
        }

        private void Sample(ulong tick, bool negative, bool positive, string stage)
        {
            var succeeded = _resolver.TrySample(tick, negative, positive, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Sample failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputAxisConflictResolver.TryCreate(InputAxisConflictPolicy.LastPressedWins, 100, out _resolver, out var error)) throw new InvalidOperationException($"Input Axis Conflict Resolver Basics configuration is invalid: {error}.");
            _lastStatus = _resolver.Snapshot();
            _lastError = InputAxisConflictError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  negative, positive, release positive, release all, simultaneous";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_resolver.CurrentTick}   ·   NEGATIVE {_lastStatus.NegativePressed}   ·   POSITIVE {_lastStatus.PositivePressed}   ·   CONFLICT {_lastStatus.HasConflict}";
            _result.text = $"VALUE {_lastStatus.ResolvedValue}   ·   N+ {_lastStatus.NegativePressedThisSample}   ·   P+ {_lastStatus.PositivePressedThisSample}   ·   N- {_lastStatus.NegativeReleasedThisSample}   ·   P- {_lastStatus.PositiveReleasedThisSample}   ·   CHANGED {_lastStatus.ResolutionChanged}   ·   ACTIONS {_buttonActionCount}";
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
            _title.style.fontSize = compact ? 22f : 30f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 10.5f : 15f;
            _description.style.marginBottom = compact ? 5f : 10f;
            _configuration.style.fontSize = compact ? 9f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _input.style.fontSize = compact ? 9.5f : 13f;
            _input.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 11f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
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
