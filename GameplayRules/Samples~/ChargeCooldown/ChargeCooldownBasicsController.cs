using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayTiming.Samples
{
    /// <summary>3 charge・10 tick回復の消費、待機、catch-upを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ChargeCooldownBasicsController : MonoBehaviour
    {
        public const string CardElementName = "charge-cooldown-basics-card";
        public const string TitleElementName = "charge-cooldown-basics-title";
        public const string DescriptionElementName = "charge-cooldown-basics-description";
        public const string ConfigurationElementName = "charge-cooldown-basics-configuration";
        public const string InputElementName = "charge-cooldown-basics-input";
        public const string StageElementName = "charge-cooldown-basics-stage";
        public const string ResultElementName = "charge-cooldown-basics-result";
        public const string ButtonRowElementName = "charge-cooldown-basics-buttons";
        public const string ResetButtonElementName = "charge-cooldown-basics-reset";
        public const string SpendButtonElementName = "charge-cooldown-basics-spend";
        public const string AdvanceNineButtonElementName = "charge-cooldown-basics-advance-nine";
        public const string AdvanceOneButtonElementName = "charge-cooldown-basics-advance-one";
        public const string AdvanceTwentyFiveButtonElementName = "charge-cooldown-basics-advance-twenty-five";

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
        private ChargeCooldownRules _rules;
        private ChargeCooldownState _state;
        private ChargeCooldownResult _lastResult;
        private ChargeCooldownError _lastError;
        private bool _lastSucceeded;
        private long _currentTick;
        private int _buttonActionCount;

        /// <summary>サンプルで使用するrulesを取得します。</summary>
        public ChargeCooldownRules Rules => _rules;
        /// <summary>現在のcooldown stateを取得します。</summary>
        public ChargeCooldownState State => _state;
        /// <summary>最後のadvanceまたはspend結果を取得します。</summary>
        public ChargeCooldownResult LastResult => _lastResult;
        /// <summary>最後の要求が成功したかを取得します。</summary>
        public bool LastSucceeded => _lastSucceeded;
        /// <summary>最後の失敗理由を取得します。</summary>
        public ChargeCooldownError LastError => _lastError;
        /// <summary>現在の明示simulation tickを取得します。</summary>
        public long CurrentTick => _currentTick;
        /// <summary>実Button操作数を取得します。</summary>
        public int ButtonActionCount => _buttonActionCount;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            BuildUi();
            ResetStateCore(false);
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

            _title = AddLabel(TitleElementName, "Charge Cooldown Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "明示tickだけでcharge消費・逐次回復・tick jump catch-upを再現します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "3 CHARGES  ·  10 TICKS EACH  ·  EXPLICIT TIME  ·  RESTORABLE STATE", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(ResetButtonElementName, "Reset · 3/3 @ 100", () => ResetStateCore(true)),
                CreateButton(SpendButtonElementName, "Spend one charge", Spend),
                CreateButton(AdvanceNineButtonElementName, "Advance +9 ticks", () => Advance(9)),
                CreateButton(AdvanceOneButtonElementName, "Advance +1 tick", () => Advance(1)),
                CreateButton(AdvanceTwentyFiveButtonElementName, "Advance +25 ticks", () => Advance(25))
            };
            foreach (var button in _buttons) _buttonRow.Add(button);
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

        private void ResetStateCore(bool countAction)
        {
            ChargeCooldown.TryCreateRules(3, 10, out _rules, out _);
            ChargeCooldown.TryCreateState(_rules, 100, 3, out _state, out _);
            _currentTick = 100;
            _lastResult = default;
            _lastError = ChargeCooldownError.None;
            _lastSucceeded = true;
            if (countAction) _buttonActionCount++;
            else _buttonActionCount = 0;
            Refresh("RESET", 0, false);
        }

        private void Spend()
        {
            _lastSucceeded = ChargeCooldown.TrySpend(_state, _rules, _currentTick, out _lastResult, out _lastError);
            _buttonActionCount++;
            if (_lastSucceeded) _state = _lastResult.State;
            Refresh("SPEND", _lastSucceeded ? _lastResult.ChargesRestored : 0, _lastSucceeded && _lastResult.ChargeSpent);
        }

        private void Advance(long ticks)
        {
            _currentTick += ticks;
            _lastSucceeded = ChargeCooldown.TryAdvance(_state, _rules, _currentTick, out _lastResult, out _lastError);
            _buttonActionCount++;
            if (_lastSucceeded) _state = _lastResult.State;
            Refresh($"ADVANCE +{ticks}", _lastSucceeded ? _lastResult.ChargesRestored : 0, false);
        }

        private void Refresh(string operation, int restored, bool spent)
        {
            _input.text = $"TICK {_currentTick}   ·   {operation}   ·   ACTIONS {_buttonActionCount}";
            _stage.text = _lastSucceeded ? $"{_state.AvailableCharges} / {_rules.MaximumCharges} charges ready" : $"Rejected explicitly  ·  {_lastError}";
            var next = _state.IsRecharging ? _state.NextRechargeTick.ToString() : "—";
            _result.text = $"RESTORED {restored}   ·   SPENT {(spent ? "YES" : "NO")}   ·   NEXT RECHARGE {next}";
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
            foreach (var button in _buttons)
            {
                button.style.flexBasis = compact ? 160f : 130f;
                button.style.minWidth = compact ? 140f : 110f;
                button.style.minHeight = compact ? 30f : 42f;
                button.style.fontSize = compact ? 10.5f : 12f;
                button.style.marginLeft = 4f;
                button.style.marginRight = 4f;
                button.style.marginTop = 2f;
                button.style.marginBottom = 2f;
            }
        }
    }
}
