using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace InputStabilization.Samples
{
    /// <summary>候補commandの連続確認、確定、noise取消を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InputStabilizerBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "input-stabilizer-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "input-stabilizer-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "input-stabilizer-basics-description";

        /// <summary>設定表示要素名。</summary>
        public const string ConfigurationElementName = "input-stabilizer-basics-configuration";

        /// <summary>入力状態要素名。</summary>
        public const string InputElementName = "input-stabilizer-basics-input";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "input-stabilizer-basics-stage";

        /// <summary>量子化結果要素名。</summary>
        public const string ResultElementName = "input-stabilizer-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "input-stabilizer-basics-buttons";

        /// <summary>候補1回目Button要素名。</summary>
        public const string FirstSampleButtonElementName = "input-stabilizer-basics-first-sample";

        /// <summary>候補2回目Button要素名。</summary>
        public const string SecondSampleButtonElementName = "input-stabilizer-basics-second-sample";

        /// <summary>候補確定Button要素名。</summary>
        public const string CommitSampleButtonElementName = "input-stabilizer-basics-commit-sample";

        /// <summary>noise候補Button要素名。</summary>
        public const string NoiseButtonElementName = "input-stabilizer-basics-noise";

        /// <summary>noise取消Button要素名。</summary>
        public const string CancelNoiseButtonElementName = "input-stabilizer-basics-cancel-noise";

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
        private InputCommandStabilizer _stabilizer;
        private InputCommandStatus _lastStatus;
        private bool _noiseCancelled;
        private int _buttonActionCount;

        /// <summary>現在確定しているcommand。</summary>
        public short CurrentCommand => _stabilizer?.CurrentCommand ?? 0;

        /// <summary>確定待ちのcommand。</summary>
        public short CandidateCommand => _stabilizer?.CandidateCommand ?? 0;

        /// <summary>候補が連続したsample数。</summary>
        public int CandidateSampleCount => _stabilizer?.CandidateSampleCount ?? 0;

        /// <summary>最後のsampleで確定commandが変わったか。</summary>
        public bool LastChanged => _lastStatus.Changed;

        /// <summary>一時noiseが現在値へ戻るsampleで取り消されたか。</summary>
        public bool NoiseCancelled => _noiseCancelled;

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

            _title = AddLabel(TitleElementName, "Input Stabilizer Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "同じ整数commandが3 sample連続した時だけ確定し、一時noiseを無視します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "REQUIRE 3 CONSECUTIVE SAMPLES  ·  INITIAL 0  ·  NO CLOCK", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(FirstSampleButtonElementName, "Sample +4  ·  1/3", () => Push(4, "Candidate +4  ·  sample 1 / 3")),
                CreateButton(SecondSampleButtonElementName, "Confirm +4  ·  2/3", () => Push(4, "Candidate +4  ·  sample 2 / 3")),
                CreateButton(CommitSampleButtonElementName, "Commit +4  ·  3/3", () => Push(4, "Command +4 committed  ·  3 / 3")),
                CreateButton(NoiseButtonElementName, "Noise -4", () => Push(-4, "Noise candidate -4  ·  current remains +4")),
                CreateButton(CancelNoiseButtonElementName, "Return +4", CancelNoise)
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

        private void Push(short command, string stage)
        {
            _lastStatus = _stabilizer.Push(command);
            _noiseCancelled = false;
            _buttonActionCount++;
            _stage.text = stage;
            RefreshLabels();
        }

        private void CancelNoise()
        {
            var before = _stabilizer.CurrentCommand;
            var hadNoise = _stabilizer.HasPendingCandidate && _stabilizer.CandidateCommand == -4;
            _lastStatus = _stabilizer.Push(4);
            _noiseCancelled = hadNoise && _stabilizer.CurrentCommand == before && !_stabilizer.HasPendingCandidate;
            _buttonActionCount++;
            _stage.text = _noiseCancelled ? "Noise cancelled  ·  current +4 preserved" : "Noise cancellation failed";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!InputCommandStabilizer.TryCreate(3, 0, out _stabilizer, out var error)) throw new InvalidOperationException($"Input Stabilizer Basics configuration is invalid: {error}.");
            _lastStatus = _stabilizer.Snapshot();
            _noiseCancelled = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  repeat a candidate three times";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _input.text = $"CURRENT {_stabilizer.CurrentCommand}   ·   CANDIDATE {_stabilizer.CandidateCommand}   ·   PROGRESS {_stabilizer.CandidateSampleCount} / 3";
            _result.text = $"CHANGED {_lastStatus.Changed}   ·   NOISE CANCELLED {_noiseCancelled}   ·   ACTIONS {_buttonActionCount}";
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
