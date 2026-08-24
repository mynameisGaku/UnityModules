using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputArbitration.Samples
{
    /// <summary>同時に成立したcommand候補のpriority仲裁を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputCommandArbiterBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-command-arbiter-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-command-arbiter-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-command-arbiter-basics-description";

        /// <summary>規則表示要素名。</summary>
        public const string RuleElementName = "input-command-arbiter-basics-rule";

        /// <summary>操作数表示要素名。</summary>
        public const string InputElementName = "input-command-arbiter-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-command-arbiter-basics-stage";

        /// <summary>仲裁結果要素名。</summary>
        public const string ResultElementName = "input-command-arbiter-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-command-arbiter-basics-buttons";

        /// <summary>eligible候補なしButton要素名。</summary>
        public const string NoneButtonElementName = "input-command-arbiter-basics-none";

        /// <summary>単独Attack候補Button要素名。</summary>
        public const string AttackButtonElementName = "input-command-arbiter-basics-attack";

        /// <summary>Interact優先Button要素名。</summary>
        public const string InteractButtonElementName = "input-command-arbiter-basics-interact";

        /// <summary>同priority先頭勝利Button要素名。</summary>
        public const string TieButtonElementName = "input-command-arbiter-basics-tie";

        /// <summary>重複id拒否Button要素名。</summary>
        public const string RejectDuplicateButtonElementName = "input-command-arbiter-basics-reject-duplicate";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _rule;
        private Label _input;
        private Label _stage;
        private Label _result;
        private Button[] _buttons;

        /// <summary>最後の仲裁結果。</summary>
        public InputCommandArbitrationResult LastResult { get; private set; }

        /// <summary>実Button操作数。</summary>
        public int ButtonActionCount { get; private set; }

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

            _title = AddLabel(TitleElementName, "Input Command Arbiter Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "同時に成立したcommand候補からpriority最大の1件を決定論的に選びます。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _rule = AddLabel(RuleElementName, "HIGHER PRIORITY WINS  ·  EQUAL PRIORITY KEEPS FIRST INPUT", 12f, new Color(0.55f, 1f, 0.82f, 1f));
            _rule.style.unityFontStyleAndWeight = FontStyle.Bold;
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
                CreateButton(NoneButtonElementName, "No eligible", ApplyNoEligible),
                CreateButton(AttackButtonElementName, "Attack only", ApplyAttackOnly),
                CreateButton(InteractButtonElementName, "Interact wins", ApplyInteractWins),
                CreateButton(TieButtonElementName, "Tie keeps first", ApplyTieKeepsFirst),
                CreateButton(RejectDuplicateButtonElementName, "Reject duplicate", ApplyDuplicate)
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

        private void ApplyNoEligible() => Apply(new[]
        {
            new InputCommandCandidate(10, 100, false),
            new InputCommandCandidate(20, 200, false)
        }, "No command selected  ·  zero eligible candidates");

        private void ApplyAttackOnly() => Apply(new[]
        {
            new InputCommandCandidate(10, 100, true),
            new InputCommandCandidate(20, 200, false)
        }, "Attack selected  ·  sole eligible command");

        private void ApplyInteractWins() => Apply(new[]
        {
            new InputCommandCandidate(10, 100, true),
            new InputCommandCandidate(20, 200, true),
            new InputCommandCandidate(30, 300, false)
        }, "Interact selected  ·  higher priority wins");

        private void ApplyTieKeepsFirst() => Apply(new[]
        {
            new InputCommandCandidate(10, 300, true),
            new InputCommandCandidate(30, 300, true)
        }, "Attack selected  ·  equal priority keeps first input");

        private void ApplyDuplicate() => Apply(new[]
        {
            new InputCommandCandidate(10, 100, true),
            new InputCommandCandidate(10, 500, false)
        }, "Duplicate command id rejected before selection");

        private void Apply(InputCommandCandidate[] candidates, string stage)
        {
            LastResult = InputCommandArbiter.Select(candidates);
            ButtonActionCount++;
            _stage.text = LastResult.Succeeded || LastResult.Error == InputCommandArbitrationError.DuplicateCommandId ? stage : $"Arbitration failed  ·  {LastResult.Error}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            LastResult = InputCommandArbiter.Select(new InputCommandCandidate[0]);
            ButtonActionCount = 0;
            _stage.text = "Ready  ·  run deterministic arbitration scenarios";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"ACTIONS {ButtonActionCount}   ·   ELIGIBLE {LastResult.EligibleCandidateCount}";
            _result.text = $"SUCCESS {LastResult.Succeeded}   ·   SELECTED {LastResult.HasSelection}   ·   INDEX {LastResult.SelectedIndex}   ·   COMMAND {LastResult.CommandId}   ·   PRIORITY {LastResult.Priority}   ·   ERROR {LastResult.Error}";
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
            _rule.style.fontSize = compact ? 9.5f : 12f;
            _rule.style.marginBottom = compact ? 3f : 8f;
            _input.style.fontSize = compact ? 10f : 13f;
            _input.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 11.5f;
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
