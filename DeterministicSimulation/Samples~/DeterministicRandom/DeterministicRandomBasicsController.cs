using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeterministicRandom.Samples
{
    /// <summary>seed、範囲値、保存状態Replayを実Buttonで確認するサンプル。</summary>
    [AddComponentMenu("StudioGaku/Deterministic Random Basics Controller")]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DeterministicRandomBasicsController : MonoBehaviour
    {
        /// <summary>sample構築完了を示すroot要素名。</summary>
        public const string ReadyElementName = "deterministic-random-basics-ready";
        /// <summary>全表示を囲むcard要素名。</summary>
        public const string CardElementName = "deterministic-random-basics-card";
        /// <summary>操作Buttonを含む行要素名。</summary>
        public const string ButtonRowElementName = "deterministic-random-basics-buttons";
        /// <summary>title要素名。</summary>
        public const string TitleElementName = "deterministic-random-basics-title";
        /// <summary>機能説明要素名。</summary>
        public const string DescriptionElementName = "deterministic-random-basics-description";
        /// <summary>固定設定を表示する要素名。</summary>
        public const string ConfigurationElementName = "deterministic-random-basics-configuration";
        /// <summary>現在状態を表示する要素名。</summary>
        public const string StatusElementName = "deterministic-random-basics-status";
        /// <summary>直近結果を表示する要素名。</summary>
        public const string StageElementName = "deterministic-random-basics-stage";
        /// <summary>操作進行を表示するtrack要素名。</summary>
        public const string TrackElementName = "deterministic-random-basics-track";
        /// <summary>状態wordと履歴を表示する要素名。</summary>
        public const string TraceElementName = "deterministic-random-basics-trace";
        /// <summary>64-bit値を引くButton名。</summary>
        public const string NextUInt64ButtonElementName = "deterministic-random-basics-next-u64";
        /// <summary>D20を引くButton名。</summary>
        public const string RollD20ButtonElementName = "deterministic-random-basics-roll-d20";
        /// <summary>unit doubleを引くButton名。</summary>
        public const string NextDoubleButtonElementName = "deterministic-random-basics-next-double";
        /// <summary>保存状態から同じ列を再生するButton名。</summary>
        public const string ReplayButtonElementName = "deterministic-random-basics-replay";
        /// <summary>固定seedへ戻すButton名。</summary>
        public const string ResetButtonElementName = "deterministic-random-basics-reset";
        /// <summary>sampleが使う固定seed。</summary>
        public const ulong DemoSeed = 0xC0FFEEUL;

        private readonly List<string> _history = new List<string>();
        private DeterministicRandomStream _stream;
        private DeterministicRandomState _initialState;
        private bool _hasResult;
        private bool _replayVerified;
        private ulong _lastUInt64;
        private int _lastInt32;
        private double _lastDouble;
        private string _lastOutputKind = string.Empty;
        private int _actionCount;
        private VisualElement _sampleRoot;
        private VisualElement _card;
        private VisualElement _track;
        private VisualElement _fill;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _status;
        private Label _stage;
        private Label _trace;
        private Button[] _buttons;

        /// <summary>直近に乱数結果がある場合にtrue。</summary>
        public bool HasResult => _hasResult;

        /// <summary>現在の保存・復元可能な乱数状態。</summary>
        public DeterministicRandomState State => _stream?.State ?? default;

        /// <summary>固定seedから作った初期状態。</summary>
        public DeterministicRandomState InitialState => _initialState;

        /// <summary>直近64-bit値。</summary>
        public ulong LastUInt64 => _lastUInt64;

        /// <summary>直近int値。</summary>
        public int LastInt32 => _lastInt32;

        /// <summary>直近double値。</summary>
        public double LastDouble => _lastDouble;

        /// <summary>直近出力種別。</summary>
        public string LastOutputKind => _lastOutputKind;

        /// <summary>保存状態から同じ後続列を再現できた場合にtrue。</summary>
        public bool ReplayVerified => _replayVerified;

        /// <summary>現在の操作履歴。</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>画面へ表示している直近説明。</summary>
        public string StageText => _stage?.text ?? string.Empty;

        private void OnEnable()
        {
            _stream = DeterministicRandomStream.Create(DemoSeed);
            _initialState = _stream.State;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            ResetView();
        }

        private void OnDisable()
        {
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
        }

        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                Debug.LogError("[Deterministic Random Basics] UIDocument rootを取得できません。", this);
                enabled = false;
                return;
            }

            _sampleRoot = new VisualElement { name = ReadyElementName, pickingMode = PickingMode.Position };
            _sampleRoot.style.position = Position.Absolute;
            _sampleRoot.style.left = 0f;
            _sampleRoot.style.top = 0f;
            _sampleRoot.style.right = 0f;
            _sampleRoot.style.bottom = 0f;
            _sampleRoot.style.alignItems = Align.Center;
            _sampleRoot.style.justifyContent = Justify.Center;
            _sampleRoot.style.overflow = Overflow.Hidden;
            _sampleRoot.style.backgroundColor = new Color(0.018f, 0.045f, 0.055f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.maxWidth = 940f;
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxHeight = 700f;
            _card.style.paddingLeft = 32f;
            _card.style.paddingRight = 32f;
            _card.style.paddingTop = 22f;
            _card.style.paddingBottom = 22f;
            _card.style.borderTopLeftRadius = 22f;
            _card.style.borderTopRightRadius = 22f;
            _card.style.borderBottomLeftRadius = 22f;
            _card.style.borderBottomRightRadius = 22f;
            _card.style.backgroundColor = new Color(0.035f, 0.12f, 0.13f, 0.99f);
            _card.style.color = new Color(0.94f, 1f, 0.98f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            _title = new Label("Deterministic Random Basics") { name = TitleElementName };
            _title.style.fontSize = 32f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 5f;
            _card.Add(_title);

            _description = new Label("同じseed・状態・操作列から、同じbit列と範囲値を再現します。") { name = DescriptionElementName };
            _description.style.fontSize = 15f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 8f;
            _card.Add(_description);

            _configuration = new Label("ALGORITHM v1 / xoshiro256**   ·   SEED 0x0000000000C0FFEE") { name = ConfigurationElementName };
            _configuration.style.fontSize = 12f;
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _configuration.style.color = new Color(0.48f, 1f, 0.72f, 1f);
            _configuration.style.marginBottom = 5f;
            _card.Add(_configuration);

            _status = new Label { name = StatusElementName };
            _status.style.fontSize = 14f;
            _status.style.marginBottom = 4f;
            _card.Add(_status);

            _stage = new Label { name = StageElementName };
            _stage.style.fontSize = 16f;
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stage.style.color = new Color(0.42f, 0.9f, 1f, 1f);
            _stage.style.marginBottom = 7f;
            _card.Add(_stage);

            _track = new VisualElement { name = TrackElementName };
            _track.style.height = 24f;
            _track.style.marginBottom = 8f;
            _track.style.borderTopLeftRadius = 12f;
            _track.style.borderTopRightRadius = 12f;
            _track.style.borderBottomLeftRadius = 12f;
            _track.style.borderBottomRightRadius = 12f;
            _track.style.overflow = Overflow.Hidden;
            _track.style.backgroundColor = new Color(0.018f, 0.045f, 0.055f, 1f);
            _card.Add(_track);

            _fill = new VisualElement { name = "deterministic-random-basics-fill" };
            _fill.style.height = new Length(100f, LengthUnit.Percent);
            _fill.style.width = 0f;
            _fill.style.backgroundColor = new Color(0.12f, 0.78f, 0.52f, 1f);
            _track.Add(_fill);

            _trace = new Label { name = TraceElementName };
            _trace.style.minHeight = 42f;
            _trace.style.paddingLeft = 12f;
            _trace.style.paddingRight = 12f;
            _trace.style.paddingTop = 9f;
            _trace.style.paddingBottom = 9f;
            _trace.style.marginBottom = 8f;
            _trace.style.whiteSpace = WhiteSpace.Normal;
            _trace.style.unityTextAlign = TextAnchor.MiddleCenter;
            _trace.style.fontSize = 12f;
            _trace.style.borderTopLeftRadius = 10f;
            _trace.style.borderTopRightRadius = 10f;
            _trace.style.borderBottomLeftRadius = 10f;
            _trace.style.borderBottomRightRadius = 10f;
            _trace.style.backgroundColor = new Color(0.015f, 0.07f, 0.075f, 1f);
            _card.Add(_trace);

            var row = new VisualElement { name = ButtonRowElementName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            _card.Add(row);
            _buttons = new[]
            {
                CreateButton(NextUInt64ButtonElementName, "Next UInt64", NextRaw),
                CreateButton(RollD20ButtonElementName, "Roll D20", RollD20),
                CreateButton(NextDoubleButtonElementName, "Next Double", NextUnitDouble),
                CreateButton(ReplayButtonElementName, "Replay State", VerifyReplay),
                CreateButton(ResetButtonElementName, "Reset Seed", ResetView)
            };
            foreach (var button in _buttons) row.Add(button);

            _sampleRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            documentRoot.Add(_sampleRoot);
        }

        private static Button CreateButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text };
            button.style.flexBasis = 116f;
            button.style.flexGrow = 1f;
            button.style.flexShrink = 1f;
            button.style.minWidth = 105f;
            button.style.maxWidth = 170f;
            button.style.height = 42f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.marginTop = 4f;
            button.style.marginBottom = 4f;
            button.style.fontSize = 14f;
            return button;
        }

        private void NextRaw()
        {
            _lastUInt64 = _stream.NextUInt64();
            _lastOutputKind = "UInt64";
            _hasResult = true;
            _replayVerified = false;
            _actionCount++;
            AddHistory($"U64 0x{_lastUInt64:X16}");
            _stage.text = $"UInt64 / 0x{_lastUInt64:X16}";
            RefreshView();
        }

        private void RollD20()
        {
            _stream.TryNextInt32(1, 21, out _lastInt32, out _);
            _lastOutputKind = "D20";
            _hasResult = true;
            _replayVerified = false;
            _actionCount++;
            AddHistory($"D20 {_lastInt32}");
            _stage.text = $"D20 / {_lastInt32}";
            RefreshView();
        }

        private void NextUnitDouble()
        {
            _lastDouble = _stream.NextDouble();
            _lastOutputKind = "Double";
            _hasResult = true;
            _replayVerified = false;
            _actionCount++;
            AddHistory($"F64 {_lastDouble:0.000000}");
            _stage.text = $"Double [0,1) / {_lastDouble:0.000000000000}";
            RefreshView();
        }

        private void VerifyReplay()
        {
            var saved = _stream.State;
            var first = Enumerable.Range(0, 6).Select(_ => _stream.NextUInt64()).ToArray();
            var expectedState = _stream.State;
            _stream.Reset(saved);
            var second = Enumerable.Range(0, 6).Select(_ => _stream.NextUInt64()).ToArray();
            _replayVerified = first.SequenceEqual(second) && _stream.State == expectedState;
            _lastOutputKind = "Replay";
            _hasResult = true;
            _actionCount += 6;
            AddHistory(_replayVerified ? "Replay ×6 exact" : "Replay mismatch");
            _stage.text = _replayVerified ? "Replay verified / state + 6 outputs match" : "Replay mismatch";
            RefreshView();
        }

        private void ResetView()
        {
            _stream = DeterministicRandomStream.Create(DemoSeed);
            _initialState = _stream.State;
            _hasResult = false;
            _replayVerified = false;
            _lastUInt64 = 0UL;
            _lastInt32 = 0;
            _lastDouble = 0d;
            _lastOutputKind = string.Empty;
            _actionCount = 0;
            _history.Clear();
            if (_stage != null) _stage.text = "Ready / explicit seed and state, no global random";
            RefreshView();
        }

        private void AddHistory(string text)
        {
            if (_history.Count == 3) _history.RemoveAt(0);
            _history.Add(text);
        }

        private void RefreshView()
        {
            if (_status != null) _status.text = $"Actions: {_actionCount}   Algorithm: v{State.AlgorithmVersion}   Saved state: 256 bit";
            if (_trace != null)
            {
                var words = $"STATE {State.Word0:X8} · {State.Word1:X8} · {State.Word2:X8} · {State.Word3:X8}";
                _trace.text = _history.Count == 0 ? words : words + "\n" + string.Join("  →  ", _history);
            }
            if (_fill != null) _fill.style.width = new Length((_actionCount % 20) * 5f, LengthUnit.Percent);
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            var compact = evt.newRect.width < 720f || evt.newRect.height < 500f;
            _card.style.paddingLeft = compact ? 12f : 32f;
            _card.style.paddingRight = compact ? 12f : 32f;
            _card.style.paddingTop = compact ? 6f : 22f;
            _card.style.paddingBottom = compact ? 6f : 22f;
            _title.style.fontSize = compact ? 22f : 32f;
            _title.style.marginBottom = compact ? 1f : 5f;
            _description.style.fontSize = compact ? 10f : 15f;
            _description.style.marginBottom = compact ? 2f : 8f;
            _configuration.style.fontSize = compact ? 9.5f : 12f;
            _configuration.style.marginBottom = compact ? 2f : 5f;
            _status.style.fontSize = compact ? 9.5f : 14f;
            _status.style.marginBottom = compact ? 1f : 4f;
            _stage.style.fontSize = compact ? 10.5f : 16f;
            _stage.style.marginBottom = compact ? 2f : 7f;
            _track.style.height = compact ? 16f : 24f;
            _track.style.marginBottom = compact ? 2f : 8f;
            _trace.style.minHeight = compact ? 28f : 42f;
            _trace.style.paddingTop = compact ? 4f : 9f;
            _trace.style.paddingBottom = compact ? 4f : 9f;
            _trace.style.marginBottom = compact ? 2f : 8f;
            _trace.style.fontSize = compact ? 8.5f : 12f;
            foreach (var button in _buttons)
            {
                button.style.flexBasis = compact ? 142f : 116f;
                button.style.minWidth = compact ? 130f : 105f;
                button.style.maxWidth = compact ? 190f : 170f;
                button.style.height = compact ? 30f : 42f;
                button.style.fontSize = compact ? 10f : 14f;
                button.style.marginTop = compact ? 2f : 4f;
                button.style.marginBottom = compact ? 2f : 4f;
            }
        }
    }
}
