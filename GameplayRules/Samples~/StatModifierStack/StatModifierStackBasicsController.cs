using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayStats.Samples
{
    /// <summary>Flat、加算percent、乗算factor、更新、重複拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StatModifierStackBasicsController : MonoBehaviour
    {
        public const string CardElementName = "stat-modifier-stack-basics-card";
        public const string TitleElementName = "stat-modifier-stack-basics-title";
        public const string DescriptionElementName = "stat-modifier-stack-basics-description";
        public const string ConfigurationElementName = "stat-modifier-stack-basics-configuration";
        public const string InputElementName = "stat-modifier-stack-basics-input";
        public const string StageElementName = "stat-modifier-stack-basics-stage";
        public const string ResultElementName = "stat-modifier-stack-basics-result";
        public const string ButtonRowElementName = "stat-modifier-stack-basics-buttons";
        public const string AddFlatButtonElementName = "stat-modifier-stack-basics-add-flat";
        public const string AddPercentButtonElementName = "stat-modifier-stack-basics-add-percent";
        public const string AddFactorButtonElementName = "stat-modifier-stack-basics-add-factor";
        public const string UpdateFactorButtonElementName = "stat-modifier-stack-basics-update-factor";
        public const string RejectDuplicateButtonElementName = "stat-modifier-stack-basics-reject-duplicate";

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
        private StatModifierStack _stack;
        private StatModifierEvaluationResult _lastResult;
        private bool _rejectionPreserved;
        private int _buttonActionCount;

        /// <summary>現在の有限base値。</summary>
        public double BaseValue => _stack?.BaseValue ?? 0d;

        /// <summary>3 stage適用後の有限stat値。</summary>
        public double CurrentValue => _stack?.CurrentValue ?? 0d;

        /// <summary>現在のFlat合計。</summary>
        public double FlatTotal => _stack?.FlatTotal ?? 0d;

        /// <summary>現在のAdditivePercent合計。</summary>
        public double AdditivePercentTotal => _stack?.AdditivePercentTotal ?? 0d;

        /// <summary>現在のMultiplicativeFactor積。</summary>
        public double MultiplicativeFactor => _stack?.MultiplicativeFactor ?? 0d;

        /// <summary>現在のmodifier件数。</summary>
        public int ModifierCount => _stack?.ModifierCount ?? 0;

        /// <summary>最後のmodifier変更結果。</summary>
        public StatModifierEvaluationResult LastResult => _lastResult;

        /// <summary>重複IDが現在stateを変えずに拒否されたか。</summary>
        public bool RejectionPreserved => _rejectionPreserved;

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

            _title = AddLabel(TitleElementName, "Stat Modifier Stack Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "ID昇順のmodifierをFlat・加算percent・乗算factorの3 stageで合成します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "BASE 100  ·  MAX 32  ·  ORDER BY ID  ·  NO TIME", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(AddFlatButtonElementName, "Add Flat +15", () => Apply(_stack.Add(10, StatModifierKind.Flat, 15d), "ID 10  ·  Flat +15  ·  value 115")),
                CreateButton(AddPercentButtonElementName, "Add Percent +20%", () => Apply(_stack.Add(20, StatModifierKind.AdditivePercent, 0.2d), "ID 20  ·  Additive +20%  ·  value 138")),
                CreateButton(AddFactorButtonElementName, "Add Factor ×1.5", () => Apply(_stack.Add(30, StatModifierKind.MultiplicativeFactor, 1.5d), "ID 30  ·  Factor ×1.5  ·  value 207")),
                CreateButton(UpdateFactorButtonElementName, "Update Factor ×0.5", () => Apply(_stack.Update(30, StatModifierKind.MultiplicativeFactor, 0.5d), "ID 30 updated  ·  Factor ×0.5  ·  value 69")),
                CreateButton(RejectDuplicateButtonElementName, "Reject Duplicate 10", RejectDuplicate)
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

        private void Apply(StatModifierEvaluationResult result, string successStage)
        {
            _lastResult = result;
            _rejectionPreserved = false;
            _buttonActionCount++;
            _stage.text = result.Succeeded ? successStage : $"Change rejected  ·  {result.Error}";
            RefreshLabels();
        }

        private void RejectDuplicate()
        {
            var before = CurrentValue;
            _lastResult = _stack.Add(10, StatModifierKind.Flat, 99d);
            _rejectionPreserved = !_lastResult.Succeeded && _lastResult.Error == StatModifierError.DuplicateModifierId && CurrentValue == before;
            _buttonActionCount++;
            _stage.text = _rejectionPreserved ? "Duplicate ID 10 rejected  ·  current state unchanged" : "Duplicate guard failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!StatModifierStack.TryCreate(100d, out _stack, out var error)) throw new InvalidOperationException($"Stat Modifier Stack configuration failed: {error}.");
            _lastResult = _stack.Clear();
            _rejectionPreserved = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  press the five operations from left to right";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"BASE {Format(BaseValue)}   ·   MODIFIERS {ModifierCount}   ·   LAST ID {_lastResult.AffectedModifierId}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"VALUE {Format(CurrentValue)}   ·   FLAT {Format(FlatTotal)}   ·   ADD% {Format(AdditivePercentTotal)}   ·   FACTOR {Format(MultiplicativeFactor)}   ·   ERROR {_lastResult.Error}";
        }

        private static string Format(double value) => value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

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
