using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayEffects.Samples
{
    /// <summary>時限stackの代表的な再適用方針を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TimedStackResolverBasicsController : MonoBehaviour
    {
        public const string CardElementName = "timed-stack-resolver-basics-card";
        public const string TitleElementName = "timed-stack-resolver-basics-title";
        public const string DescriptionElementName = "timed-stack-resolver-basics-description";
        public const string ConfigurationElementName = "timed-stack-resolver-basics-configuration";
        public const string InputElementName = "timed-stack-resolver-basics-input";
        public const string StageElementName = "timed-stack-resolver-basics-stage";
        public const string ResultElementName = "timed-stack-resolver-basics-result";
        public const string ButtonRowElementName = "timed-stack-resolver-basics-buttons";
        public const string AddRefreshButtonElementName = "timed-stack-resolver-basics-add-refresh";
        public const string AddExtendButtonElementName = "timed-stack-resolver-basics-add-extend";
        public const string MaximumButtonElementName = "timed-stack-resolver-basics-maximum";
        public const string ReplaceButtonElementName = "timed-stack-resolver-basics-replace";
        public const string InactiveButtonElementName = "timed-stack-resolver-basics-inactive";

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
        private TimedStackResolution _lastResolution;
        private TimedStackError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の解決要求が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public TimedStackError LastError => _lastError;

        /// <summary>最後に成功した解決結果を取得します。</summary>
        public TimedStackResolution LastResolution => _lastResolution;

        /// <summary>最後の入力値が変更されなかったかを取得します。</summary>
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
            _root.style.backgroundColor = new Color(0.035f, 0.035f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.095f, 0.075f, 0.18f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Timed Stack Resolver Basics", 31f, new Color(0.98f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "現在値と追加値からstack数・残りtick数を決定論的に解決し、入力値を変更しません。", 15f, new Color(0.88f, 0.84f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "STACK: ADD / REPLACE / MAX   ·   DURATION: REFRESH / ADD / MAX   ·   INTEGER TICKS", 12f, new Color(0.72f, 0.62f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.94f, 0.92f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(1f, 0.78f, 0.38f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.96f, 0.94f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.045f, 0.035f, 0.105f, 1f);
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
                CreateButton(AddRefreshButtonElementName, "Add + Refresh", () => Resolve("ADD + REFRESH", State(2, 50), State(2, 30), Policy(3, 100, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped))),
                CreateButton(AddExtendButtonElementName, "Add + Extend", () => Resolve("ADD + EXTEND", State(1, 20), State(1, 15), Policy(4, 30, TimedStackCountMode.AddClamped, TimedStackDurationMode.AddClamped))),
                CreateButton(MaximumButtonElementName, "Maximum", () => Resolve("MAXIMUM", State(3, 40), State(2, 60), Policy(5, 100, TimedStackCountMode.MaximumClamped, TimedStackDurationMode.MaximumClamped))),
                CreateButton(ReplaceButtonElementName, "Replace", () => Resolve("REPLACE", State(3, 40), State(1, 10), Policy(5, 100, TimedStackCountMode.ReplaceClamped, TimedStackDurationMode.RefreshClamped))),
                CreateButton(InactiveButtonElementName, "Inactive → Active", () => Resolve("INACTIVE", State(0, 0), State(2, 25), Policy(4, 100, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped)))
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
            button.style.color = new Color(0.11f, 0.055f, 0.19f, 1f);
            button.style.backgroundColor = new Color(0.78f, 0.68f, 1f, 1f);
            return button;
        }

        private void Resolve(string label, TimedStackState current, TimedStackState incoming, TimedStackPolicy policy)
        {
            var currentBefore = current;
            var incomingBefore = incoming;
            var policyBefore = policy;
            _lastSucceeded = TimedStackResolver.TryResolve(current, incoming, policy, out _lastResolution, out _lastError);
            _lastInputPreserved = Same(current, currentBefore) && Same(incoming, incomingBefore) && Same(policy, policyBefore);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   CURRENT {current.StackCount} / {current.RemainingTicks}t   ·   INCOMING {incoming.StackCount} / {incoming.RemainingTicks}t   ·   ACTIONS {_buttonActionCount}";
            if (_lastSucceeded)
            {
                var result = _lastResolution.ResultState;
                _stage.text = $"RESULT {result.StackCount} STACKS   ·   {result.RemainingTicks} TICKS   ·   WAS INACTIVE {_lastResolution.WasInactive}";
                _result.text = $"STACK CLAMPED {_lastResolution.StackClamped}   ·   DURATION CLAMPED {_lastResolution.DurationClamped}   ·   STACK CHANGED {_lastResolution.StackCountChanged}   ·   DURATION CHANGED {_lastResolution.DurationChanged}";
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "RESULT —   ·   INPUT VALUES UNCHANGED   ·   NO PARTIAL RESOLUTION";
            }
        }

        private void ResetStateCore()
        {
            _lastResolution = default;
            _lastError = TimedStackError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   CURRENT —   ·   INCOMING —   ·   ACTIONS 0";
            _stage.text = "Ready  ·  choose add, replace, maximum, or inactive application";
            _result.text = "STACK COUNT + REMAINING TICKS   ·   POLICY + CLAMP FLAGS";
        }

        private static TimedStackState State(int stacks, int ticks) => new TimedStackState(stacks, ticks);

        private static TimedStackPolicy Policy(int stacks, int ticks, TimedStackCountMode stackMode, TimedStackDurationMode durationMode) => new TimedStackPolicy(stacks, ticks, stackMode, durationMode);

        private static bool Same(TimedStackState left, TimedStackState right) => left.StackCount == right.StackCount && left.RemainingTicks == right.RemainingTicks;

        private static bool Same(TimedStackPolicy left, TimedStackPolicy right) => left.MaximumStackCount == right.MaximumStackCount && left.MaximumDurationTicks == right.MaximumDurationTicks && left.StackMode == right.StackMode && left.DurationMode == right.DurationMode;

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
