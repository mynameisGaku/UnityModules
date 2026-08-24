using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayTiming.Samples
{
    /// <summary>定期発火cursorの代表的な計画結果を実Buttonで確認するサンプルです。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PeriodicTickPlannerBasicsController : MonoBehaviour
    {
        public const string CardElementName = "periodic-tick-planner-basics-card";
        public const string TitleElementName = "periodic-tick-planner-basics-title";
        public const string DescriptionElementName = "periodic-tick-planner-basics-description";
        public const string ConfigurationElementName = "periodic-tick-planner-basics-configuration";
        public const string InputElementName = "periodic-tick-planner-basics-input";
        public const string StageElementName = "periodic-tick-planner-basics-stage";
        public const string ResultElementName = "periodic-tick-planner-basics-result";
        public const string ButtonRowElementName = "periodic-tick-planner-basics-buttons";
        public const string FutureButtonElementName = "periodic-tick-planner-basics-future";
        public const string ExactButtonElementName = "periodic-tick-planner-basics-exact";
        public const string CatchUpButtonElementName = "periodic-tick-planner-basics-catch-up";
        public const string LimitedButtonElementName = "periodic-tick-planner-basics-limited";
        public const string CompleteButtonElementName = "periodic-tick-planner-basics-complete";

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
        private PeriodicTickPlan _lastPlan;
        private PeriodicTickError _lastError;
        private bool _lastSucceeded;
        private bool _lastInputPreserved;
        private int _buttonActionCount;

        /// <summary>最後の計画要求が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;

        /// <summary>最後の失敗理由を取得します。</summary>
        public PeriodicTickError LastError => _lastError;

        /// <summary>最後に成功した発火計画を取得します。</summary>
        public PeriodicTickPlan LastPlan => _lastPlan;

        /// <summary>最後の入力cursorが変更されなかったかを取得します。</summary>
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
            _root.style.backgroundColor = new Color(0.02f, 0.045f, 0.075f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.055f, 0.12f, 0.19f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Periodic Tick Planner Basics", 31f, new Color(0.94f, 0.98f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "次回tick・間隔・残り回数から、指定tickまでの発火範囲と次cursorを計画します。", 15f, new Color(0.8f, 0.92f, 1f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "INTEGER TICKS   ·   INCLUSIVE THROUGH TICK   ·   BOUNDED CATCH-UP   ·   NO CLOCK ACCESS", 12f, new Color(0.42f, 0.82f, 1f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _input = AddLabel(InputElementName, string.Empty, 13f, new Color(0.9f, 0.96f, 1f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(1f, 0.8f, 0.36f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.9f, 0.97f, 1f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.015f, 0.06f, 0.105f, 1f);
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
                CreateButton(FutureButtonElementName, "Future · emit 0", () => Plan("FUTURE", State(10, 4, 5), 9, 10)),
                CreateButton(ExactButtonElementName, "Exact · emit 1", () => Plan("EXACT", State(10, 4, 5), 10, 10)),
                CreateButton(CatchUpButtonElementName, "Catch-up · emit 4", () => Plan("CATCH-UP", State(10, 4, 5), 22, 10)),
                CreateButton(LimitedButtonElementName, "Limited · 10 → 3", () => Plan("LIMITED", State(10, 2, 10), 100, 3)),
                CreateButton(CompleteButtonElementName, "Complete · emit 3", () => Plan("COMPLETE", State(5, 5, 3), 100, 10))
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
            button.style.color = new Color(0.02f, 0.1f, 0.16f, 1f);
            button.style.backgroundColor = new Color(0.62f, 0.86f, 1f, 1f);
            return button;
        }

        private void Plan(string label, PeriodicTickState state, long throughTick, int maximumEmissionCount)
        {
            var before = state;
            _lastSucceeded = PeriodicTickPlanner.TryPlan(state, throughTick, maximumEmissionCount, out _lastPlan, out _lastError);
            _lastInputPreserved = Same(state, before);
            _buttonActionCount++;
            _input.text = $"INPUT {label}   ·   NEXT {state.NextTick}   ·   INTERVAL {state.IntervalTicks}   ·   REMAINING {state.RemainingCount}   ·   THROUGH {throughTick}";
            if (_lastSucceeded)
            {
                _stage.text = $"DUE {_lastPlan.DueCount}   ·   EMIT {_lastPlan.EmittedCount}/{maximumEmissionCount}   ·   LIMITED {_lastPlan.WasLimited}   ·   COMPLETE {_lastPlan.IsCompleted}";
                _result.text = $"RANGE {_lastPlan.FirstEmittedTick} → {_lastPlan.LastEmittedTick}   ·   NEXT {_lastPlan.NextState.NextTick}   ·   REMAINING {_lastPlan.NextState.RemainingCount}   ·   ACTIONS {_buttonActionCount}";
            }
            else
            {
                _stage.text = $"Rejected explicitly  ·  {_lastError}";
                _result.text = "RANGE —   ·   INPUT CURSOR UNCHANGED   ·   NO PARTIAL PLAN";
            }
        }

        private void ResetStateCore()
        {
            _lastPlan = default;
            _lastError = PeriodicTickError.None;
            _lastSucceeded = false;
            _lastInputPreserved = true;
            _buttonActionCount = 0;
            _input.text = "INPUT —   ·   NEXT —   ·   INTERVAL —   ·   REMAINING —   ·   THROUGH —";
            _stage.text = "Ready  ·  choose future, exact, catch-up, limited, or complete";
            _result.text = "FIRST → LAST EMITTED TICK   ·   NEXT CURSOR   ·   REMAINING COUNT";
        }

        private static PeriodicTickState State(long nextTick, int interval, int remaining) => new PeriodicTickState(nextTick, interval, remaining);

        private static bool Same(PeriodicTickState left, PeriodicTickState right) => left.NextTick == right.NextTick && left.IntervalTicks == right.IntervalTicks && left.RemainingCount == right.RemainingCount;

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
