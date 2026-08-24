using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimulationClock.Samples
{
    /// <summary>明示した整数時間、catch-up上限、状態復元、同一入力再生を実Buttonで確認するサンプル。</summary>
    [AddComponentMenu("StudioGaku/Simulation Clock Basics Controller")]
    [RequireComponent(typeof(UIDocument))]
    public sealed class SimulationClockBasicsController : MonoBehaviour
    {
        /// <summary>sample構築完了を示すroot要素名。</summary>
        public const string ReadyElementName = "simulation-clock-basics-ready";
        /// <summary>全表示を囲むcard要素名。</summary>
        public const string CardElementName = "simulation-clock-basics-card";
        /// <summary>操作Buttonを含む行要素名。</summary>
        public const string ButtonRowElementName = "simulation-clock-basics-buttons";
        /// <summary>title要素名。</summary>
        public const string TitleElementName = "simulation-clock-basics-title";
        /// <summary>機能説明要素名。</summary>
        public const string DescriptionElementName = "simulation-clock-basics-description";
        /// <summary>固定設定を表示する要素名。</summary>
        public const string ConfigurationElementName = "simulation-clock-basics-configuration";
        /// <summary>現在状態を表示する要素名。</summary>
        public const string StatusElementName = "simulation-clock-basics-status";
        /// <summary>直近結果を表示する要素名。</summary>
        public const string StageElementName = "simulation-clock-basics-stage";
        /// <summary>step進行を表示するtrack要素名。</summary>
        public const string TrackElementName = "simulation-clock-basics-track";
        /// <summary>入力履歴を表示する要素名。</summary>
        public const string TraceElementName = "simulation-clock-basics-trace";
        /// <summary>16msを進めるButton名。</summary>
        public const string Advance16ButtonElementName = "simulation-clock-basics-advance-16";
        /// <summary>33msを進めるButton名。</summary>
        public const string Advance33ButtonElementName = "simulation-clock-basics-advance-33";
        /// <summary>500ms hitchを進めるButton名。</summary>
        public const string HitchButtonElementName = "simulation-clock-basics-hitch";
        /// <summary>同一入力列の再現性を確認するButton名。</summary>
        public const string ReplayButtonElementName = "simulation-clock-basics-replay";
        /// <summary>時計と表示を初期状態へ戻すButton名。</summary>
        public const string ResetButtonElementName = "simulation-clock-basics-reset";

        private static readonly FixedStepClockSettings DemoSettings = new FixedStepClockSettings(TimeSpan.TicksPerMillisecond * 20L, 4);
        private readonly List<string> _history = new List<string>();
        private FixedStepClock _clock;
        private FixedStepAdvanceResult _lastResult;
        private bool _hasResult;
        private bool _replayVerified;
        private VisualElement _sampleRoot;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private VisualElement _track;
        private VisualElement _fill;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _status;
        private Label _stage;
        private Label _trace;
        private Button[] _buttons;

        /// <summary>直近にAdvance結果がある場合にtrue。</summary>
        public bool HasResult => _hasResult;

        /// <summary>直近のAdvance結果。HasResultがfalseの場合はdefault値。</summary>
        public FixedStepAdvanceResult LastResult => _lastResult;

        /// <summary>現在の保存・復元可能な時計状態。</summary>
        public FixedStepClockState State => _clock?.State ?? default;

        /// <summary>同じ設定、初期状態、入力列の再生結果が一致した場合にtrue。</summary>
        public bool ReplayVerified => _replayVerified;

        /// <summary>現在の時計へ渡した入力履歴。</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>画面へ表示している直近説明。</summary>
        public string StageText => _stage?.text ?? string.Empty;

        private void OnEnable()
        {
            if (!FixedStepClock.TryCreate(DemoSettings, out _clock, out var error))
            {
                Debug.LogError($"[Simulation Clock Basics] clock creation failed: {error}", this);
                enabled = false;
                return;
            }

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
                Debug.LogError("[Simulation Clock Basics] UIDocument rootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.025f, 0.025f, 0.075f, 1f);

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
            _card.style.backgroundColor = new Color(0.075f, 0.07f, 0.19f, 0.99f);
            _card.style.color = new Color(0.97f, 0.97f, 1f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            _title = new Label("Simulation Clock Basics") { name = TitleElementName };
            _title.style.fontSize = 32f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 5f;
            _card.Add(_title);

            _description = new Label("明示した整数時間を固定step範囲へ変換し、端数・drop・同一入力再生を確認します。") { name = DescriptionElementName };
            _description.style.fontSize = 15f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 8f;
            _card.Add(_description);

            _configuration = new Label("STEP 20 ms   /   MAX CATCH-UP 4") { name = ConfigurationElementName };
            _configuration.style.fontSize = 12f;
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _configuration.style.color = new Color(0.72f, 0.67f, 1f, 1f);
            _configuration.style.marginBottom = 5f;
            _card.Add(_configuration);

            _status = new Label { name = StatusElementName };
            _status.style.fontSize = 14f;
            _status.style.marginBottom = 4f;
            _card.Add(_status);

            _stage = new Label { name = StageElementName };
            _stage.style.fontSize = 16f;
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stage.style.color = new Color(0.45f, 0.9f, 1f, 1f);
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
            _track.style.backgroundColor = new Color(0.025f, 0.025f, 0.075f, 1f);
            _card.Add(_track);

            _fill = new VisualElement { name = "simulation-clock-basics-fill" };
            _fill.style.height = new Length(100f, LengthUnit.Percent);
            _fill.style.width = 0f;
            _fill.style.backgroundColor = new Color(0.38f, 0.31f, 0.95f, 1f);
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
            _trace.style.fontSize = 13f;
            _trace.style.borderTopLeftRadius = 10f;
            _trace.style.borderTopRightRadius = 10f;
            _trace.style.borderBottomLeftRadius = 10f;
            _trace.style.borderBottomRightRadius = 10f;
            _trace.style.backgroundColor = new Color(0.04f, 0.035f, 0.12f, 1f);
            _card.Add(_trace);

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);
            _buttons = new[]
            {
                CreateButton(Advance16ButtonElementName, "Advance 16 ms", () => Advance(TimeSpan.TicksPerMillisecond * 16L, "16 ms")),
                CreateButton(Advance33ButtonElementName, "Advance 33 ms", () => Advance(TimeSpan.TicksPerMillisecond * 33L, "33 ms")),
                CreateButton(HitchButtonElementName, "Hitch 500 ms", () => Advance(TimeSpan.TicksPerMillisecond * 500L, "500 ms hitch")),
                CreateButton(ReplayButtonElementName, "Replay Pattern", VerifyReplay),
                CreateButton(ResetButtonElementName, "Reset", ResetView)
            };
            foreach (var button in _buttons) _buttonRow.Add(button);

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

        private void Advance(long elapsedTicks, string label)
        {
            _lastResult = _clock.AdvanceTicks(elapsedTicks);
            _hasResult = true;
            _replayVerified = false;
            _history.Add(label);
            _stage.text = _lastResult.IsSuccess
                ? $"Advance / steps {_lastResult.StepCount} / dropped {_lastResult.DroppedStepCount}"
                : $"Advance failed / {_lastResult.Error}";
            RefreshView();
        }

        private void VerifyReplay()
        {
            FixedStepClock.TryCreate(DemoSettings, out var first, out _);
            FixedStepClock.TryCreate(DemoSettings, out var second, out _);
            var sequence = new[] { 70_000L, 130_000L, 450_000L, 1_400_000L, 10_000L };
            _replayVerified = true;
            foreach (var elapsed in sequence) _replayVerified &= first.AdvanceTicks(elapsed) == second.AdvanceTicks(elapsed);
            _replayVerified &= first.State == second.State;
            _stage.text = _replayVerified ? "Replay verified / same inputs, same results" : "Replay mismatch";
            RefreshView();
        }

        private void ResetView()
        {
            _clock.Reset(default);
            _lastResult = default;
            _hasResult = false;
            _replayVerified = false;
            _history.Clear();
            if (_stage != null) _stage.text = "Ready / explicit integer time, no hidden clock";
            RefreshView();
        }

        private void RefreshView()
        {
            if (_status != null) _status.text = $"Completed: {State.CompletedStepCount}   Remainder: {State.RemainderTicks / 10_000d:0.###} ms   Total dropped: {State.TotalDroppedTicks / 10_000d:0.###} ms";
            if (_trace != null) _trace.text = _history.Count == 0 ? "INPUT TICKS  —" : "INPUT TICKS  " + string.Join("  →  ", _history);
            if (_fill != null)
            {
                var stepPosition = (State.CompletedStepCount % 20L + (double)State.RemainderTicks / DemoSettings.StepDurationTicks) / 20d;
                _fill.style.width = new Length((float)(stepPosition * 100d), LengthUnit.Percent);
            }
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
            _trace.style.fontSize = compact ? 9f : 13f;
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
