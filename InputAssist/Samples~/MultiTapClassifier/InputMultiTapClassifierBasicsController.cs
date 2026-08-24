using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputMultiTapping.Samples
{
    /// <summary>明示tickのsingle・double・triple tap分類を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputMultiTapClassifierBasicsController : MonoBehaviour
    {
        /// <summary>サンプルcardを取得する為のUI element名。</summary>
        public const string CardElementName = "input-multi-tap-classifier-basics-card";
        /// <summary>titleを取得する為のUI element名。</summary>
        public const string TitleElementName = "input-multi-tap-classifier-basics-title";
        /// <summary>説明文を取得する為のUI element名。</summary>
        public const string DescriptionElementName = "input-multi-tap-classifier-basics-description";
        /// <summary>設定表示を取得する為のUI element名。</summary>
        public const string ConfigurationElementName = "input-multi-tap-classifier-basics-configuration";
        /// <summary>入力状態表示を取得する為のUI element名。</summary>
        public const string InputElementName = "input-multi-tap-classifier-basics-input";
        /// <summary>進行状態表示を取得する為のUI element名。</summary>
        public const string StageElementName = "input-multi-tap-classifier-basics-stage";
        /// <summary>解決結果表示を取得する為のUI element名。</summary>
        public const string ResultElementName = "input-multi-tap-classifier-basics-result";
        /// <summary>button群を取得する為のUI element名。</summary>
        public const string ButtonRowElementName = "input-multi-tap-classifier-basics-buttons";
        /// <summary>最初のtap buttonを取得する為のUI element名。</summary>
        public const string FirstTapButtonElementName = "input-multi-tap-classifier-basics-first-tap";
        /// <summary>2回目のtap buttonを取得する為のUI element名。</summary>
        public const string SecondTapButtonElementName = "input-multi-tap-classifier-basics-second-tap";
        /// <summary>gap満了buttonを取得する為のUI element名。</summary>
        public const string ExpireButtonElementName = "input-multi-tap-classifier-basics-expire";
        /// <summary>新しいburst開始buttonを取得する為のUI element名。</summary>
        public const string NewTapButtonElementName = "input-multi-tap-classifier-basics-new-tap";
        /// <summary>triple tap完成buttonを取得する為のUI element名。</summary>
        public const string CompleteTripleButtonElementName = "input-multi-tap-classifier-basics-complete-triple";

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
        private InputMultiTapClassifier _classifier;
        private InputMultiTapStatus _lastStatus;
        private InputMultiTapError _lastError;
        private int _buttonActionCount;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _classifier?.CurrentTick ?? 0;
        /// <summary>確定待ちのtap数。</summary>
        public int PendingTapCount => _lastStatus.PendingTapCount;
        /// <summary>確定待ちのtapが存在するか。</summary>
        public bool HasPendingTaps => _lastStatus.HasPendingTaps;
        /// <summary>確定待ちburstが許容する最後のtick。</summary>
        public ulong PendingDeadlineTick => _lastStatus.PendingDeadlineTick;
        /// <summary>最後のsampleでtapを受理したか。</summary>
        public bool TapAcceptedThisSample => _lastStatus.TapAcceptedThisSample;
        /// <summary>最後のsampleで確定したtap数。</summary>
        public int CompletedTapCount => _lastStatus.CompletedTapCount;
        /// <summary>最後のsampleでburstを確定したか。</summary>
        public bool CompletedThisSample => _lastStatus.CompletedThisSample;
        /// <summary>最後のsampleでburstを確定した理由。</summary>
        public InputMultiTapCompletionReason CompletionReason => _lastStatus.CompletionReason;
        /// <summary>最後の操作error。</summary>
        public InputMultiTapError LastError => _lastError;
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
            _title = AddLabel(TitleElementName, "Input Multi Tap Classifier Basics", 30f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "tap edgeを明示tickのgap windowへ集約し、single・double・tripleを決定論的に確定します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "MAX GAP 3 TICKS  ·  MAXIMUM 3 TAPS  ·  INCLUSIVE WINDOW", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(FirstTapButtonElementName, "Tap @100", () => Sample(100, true, "Pending 1 tap")),
                CreateButton(SecondTapButtonElementName, "Tap @102", () => Sample(102, true, "Pending 2 taps")),
                CreateButton(ExpireButtonElementName, "Expire @106", () => Sample(106, false, "Double tap  ·  gap expired")),
                CreateButton(NewTapButtonElementName, "New Tap @107", () => Sample(107, true, "New burst  ·  pending 1")),
                CreateButton(CompleteTripleButtonElementName, "Tap x2 @108-109", () => SampleTwice(108, 109, "Triple tap  ·  maximum reached"))
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

        private void Sample(ulong tick, bool tapOccurred, string stage)
        {
            var succeeded = _classifier.TrySample(tick, tapOccurred, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Sample failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void SampleTwice(ulong firstTick, ulong secondTick, string stage)
        {
            var succeeded = _classifier.TrySample(firstTick, true, out _, out _lastError) && _classifier.TrySample(secondTick, true, out _lastStatus, out _lastError);
            _buttonActionCount++;
            _stage.text = succeeded ? stage : $"Sample failed  ·  {_lastError}";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputMultiTapClassifier.TryCreate(3, 3, 100, out _classifier, out var error)) throw new InvalidOperationException($"Input Multi Tap Classifier Basics configuration is invalid: {error}.");
            _lastStatus = _classifier.Snapshot();
            _lastError = InputMultiTapError.None;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  double timeout, then triple maximum";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"TICK {_classifier.CurrentTick}   ·   PENDING {_lastStatus.PendingTapCount}   ·   DEADLINE {_lastStatus.PendingDeadlineTick}";
            _result.text = $"TAP+ {_lastStatus.TapAcceptedThisSample}   ·   COMPLETED {_lastStatus.CompletedThisSample}   ·   COUNT {_lastStatus.CompletedTapCount}   ·   REASON {_lastStatus.CompletionReason}   ·   ACTIONS {_buttonActionCount}";
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
