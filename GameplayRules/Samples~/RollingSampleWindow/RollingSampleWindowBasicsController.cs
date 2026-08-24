using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayMetrics.Samples
{
    /// <summary>容量3のFIFO窓への4追加とclearを実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RollingSampleWindowBasicsController : MonoBehaviour
    {
        public const string CardElementName = "rolling-sample-window-basics-card";
        public const string TitleElementName = "rolling-sample-window-basics-title";
        public const string DescriptionElementName = "rolling-sample-window-basics-description";
        public const string ConfigurationElementName = "rolling-sample-window-basics-configuration";
        public const string InputElementName = "rolling-sample-window-basics-input";
        public const string StageElementName = "rolling-sample-window-basics-stage";
        public const string ResultElementName = "rolling-sample-window-basics-result";
        public const string ButtonRowElementName = "rolling-sample-window-basics-buttons";
        public const string AddTenButtonElementName = "rolling-sample-window-basics-add-ten";
        public const string AddTwentyButtonElementName = "rolling-sample-window-basics-add-twenty";
        public const string AddThirtyButtonElementName = "rolling-sample-window-basics-add-thirty";
        public const string AddFortyButtonElementName = "rolling-sample-window-basics-add-forty";
        public const string ClearButtonElementName = "rolling-sample-window-basics-clear";

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
        private RollingSampleWindow _window;
        private SampleWindowAddResult _lastAdd;
        private int _buttonActionCount;

        /// <summary>現在のsample件数。</summary>
        public int SampleCount => _window?.Count ?? 0;

        /// <summary>現在のwindow snapshot。</summary>
        public SampleWindowSnapshot Snapshot => _window?.Snapshot ?? default;

        /// <summary>最後のsample追加結果。</summary>
        public SampleWindowAddResult LastAdd => _lastAdd;

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

            _title = AddLabel(TitleElementName, "Rolling Sample Window Basics", 31f, new Color(0.96f, 0.96f, 1f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "有限sampleをFIFO窓へ追加し、min・max・meanを毎回再構築します。", 15f, new Color(0.82f, 0.84f, 0.98f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "CAPACITY 3  ·  FIFO  ·  FINITE SAMPLES  ·  OLDEST EVICTION", 12f, new Color(0.55f, 1f, 0.82f, 1f));
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
                CreateButton(AddTenButtonElementName, "Add 10", () => ApplyAdd(10d)),
                CreateButton(AddTwentyButtonElementName, "Add 20", () => ApplyAdd(20d)),
                CreateButton(AddThirtyButtonElementName, "Add 30", () => ApplyAdd(30d)),
                CreateButton(AddFortyButtonElementName, "Add 40 · evict 10", () => ApplyAdd(40d)),
                CreateButton(ClearButtonElementName, "Clear window", ClearWindow)
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

        private void ApplyAdd(double sample)
        {
            _lastAdd = _window.Add(sample);
            _buttonActionCount++;
            _stage.text = _lastAdd.Succeeded
                ? _lastAdd.HadEviction ? $"Added {Format(sample)}  ·  evicted oldest {Format(_lastAdd.EvictedSample)}" : $"Added {Format(sample)}  ·  no eviction"
                : $"Add rejected  ·  {_lastAdd.Error}";
            RefreshLabels();
        }

        private void ClearWindow()
        {
            _window.Clear();
            _lastAdd = default;
            _buttonActionCount++;
            _stage.text = "Cleared  ·  capacity remains 3";
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            if (!RollingSampleWindow.TryCreate(3, out _window, out var error)) throw new InvalidOperationException($"Sample window creation failed: {error}");
            _lastAdd = default;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  add 10, 20, 30, then 40 to evict 10";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            var snapshot = Snapshot;
            _input.text = snapshot.HasSamples
                ? $"COUNT {snapshot.Count}/{snapshot.Capacity}   ·   OLDEST {Format(snapshot.Oldest)}   ·   NEWEST {Format(snapshot.Newest)}   ·   ACTIONS {_buttonActionCount}"
                : $"COUNT 0/{snapshot.Capacity}   ·   OLDEST —   ·   NEWEST —   ·   ACTIONS {_buttonActionCount}";
            _result.text = snapshot.HasSamples
                ? $"MIN {Format(snapshot.Minimum)}   ·   MAX {Format(snapshot.Maximum)}   ·   MEAN {Format(snapshot.Mean)}   ·   LAST EVICTION {(_lastAdd.HadEviction ? Format(_lastAdd.EvictedSample) : "—")}"
                : "MIN —   ·   MAX —   ·   MEAN —   ·   LAST EVICTION —";
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

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
