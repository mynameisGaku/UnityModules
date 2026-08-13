using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimeControl.Samples
{
    /// <summary>時間倍率のlease操作と、scale有無による時間経過の差を同じ画面へ表示する。</summary>
    [AddComponentMenu("StudioGaku/Time Control Basics Controller")]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(TimeControlController))]
    public sealed class TimeControlBasicsController : MonoBehaviour
    {
        /// <summary>外部の視覚gateがsample構築完了を確認する要素名。</summary>
        public const string ReadyElementName = "time-control-basics-ready";

        /// <summary>画面captureの背景色を確認する要素名。</summary>
        public const string EvidenceElementName = "time-control-basics-evidence";

        /// <summary>状態、lane、操作Buttonを囲むcard名。</summary>
        public const string CardElementName = "time-control-basics-card";

        /// <summary>5つの操作Buttonを表示順に含む行名。</summary>
        public const string ButtonRowElementName = "time-control-basics-buttons";

        /// <summary>Controllerの状態snapshotを表示するLabel名。</summary>
        public const string StatusElementName = "time-control-basics-status";

        /// <summary>手動操作または入れ子デモの段階を表示するLabel名。</summary>
        public const string StageElementName = "time-control-basics-stage";

        /// <summary>最後の取得または解放結果を表示するLabel名。</summary>
        public const string ResultElementName = "time-control-basics-result";

        /// <summary>スケール時間の累積秒数を表示するLabel名。</summary>
        public const string ScaledCounterElementName = "time-control-basics-scaled-counter";

        /// <summary>非スケール時間の累積秒数を表示するLabel名。</summary>
        public const string UnscaledCounterElementName = "time-control-basics-unscaled-counter";

        /// <summary>スケール時間の移動marker名。</summary>
        public const string ScaledLaneMarkerElementName = "time-control-basics-scaled-marker";

        /// <summary>非スケール時間の移動marker名。</summary>
        public const string UnscaledLaneMarkerElementName = "time-control-basics-unscaled-marker";

        /// <summary>pause leaseを取得するButton名。</summary>
        public const string PauseButtonElementName = "time-control-basics-pause";

        /// <summary>slow leaseを取得するButton名。</summary>
        public const string SlowButtonElementName = "time-control-basics-slow";

        /// <summary>fast leaseを取得するButton名。</summary>
        public const string FastButtonElementName = "time-control-basics-fast";

        /// <summary>入れ子leaseの順序を自動実行するButton名。</summary>
        public const string NestedDemoButtonElementName = "time-control-basics-nested-demo";

        /// <summary>sampleが所有するleaseだけを解放するButton名。</summary>
        public const string ReleaseOwnedButtonElementName = "time-control-basics-release-owned";

        /// <summary>入れ子デモがfast leaseだけを所有する段階文字列。</summary>
        public const string NestedFastStageText = "Nested: Fast x2";

        /// <summary>入れ子デモでslow leaseがfastより優先された段階文字列。</summary>
        public const string NestedSlowStageText = "Nested: Slow x0.25 wins";

        /// <summary>入れ子デモでpause leaseが最優先された段階文字列。</summary>
        public const string NestedPauseStageText = "Nested: Pause x0 wins";

        /// <summary>入れ子デモでpauseを解放しslowへ戻った段階文字列。</summary>
        public const string NestedReleasePauseStageText = "Nested: Release Pause -> x0.25";

        /// <summary>入れ子デモでslowを解放しfastへ戻った段階文字列。</summary>
        public const string NestedReleaseSlowStageText = "Nested: Release Slow -> x2";

        /// <summary>入れ子デモでfastを解放し基準値へ戻った段階文字列。</summary>
        public const string NestedReleaseFastStageText = "Nested: Release Fast -> Baseline";

        /// <summary>入れ子デモの各段階を視認できる実時間秒数。</summary>
        private const double NestedStageDurationSeconds = 0.35d;

        /// <summary>sample自身が取得し、終了時に解放するlease。</summary>
        private readonly List<TimeScaleLease> _ownedLeases = new List<TimeScaleLease>();

        /// <summary>同じGameObjectで`Time.timeScale`を所有するController。</summary>
        private TimeControlController _timeControl;

        /// <summary>UIDocumentへ追加したsample画面のroot。</summary>
        private VisualElement _sampleRoot;

        /// <summary>現在のController snapshotを表示するLabel。</summary>
        private Label _statusLabel;

        /// <summary>現在の手動操作または入れ子段階を表示するLabel。</summary>
        private Label _stageLabel;

        /// <summary>直近の取得失敗を含む操作結果を表示するLabel。</summary>
        private Label _resultLabel;

        /// <summary>`Time.deltaTime`の累積値を表示するLabel。</summary>
        private Label _scaledCounterLabel;

        /// <summary>`Time.unscaledDeltaTime`の累積値を表示するLabel。</summary>
        private Label _unscaledCounterLabel;

        /// <summary>スケール時間の進行位置を示すmarker。</summary>
        private VisualElement _scaledMarker;

        /// <summary>非スケール時間の進行位置を示すmarker。</summary>
        private VisualElement _unscaledMarker;

        /// <summary>pause leaseを追加するButton。</summary>
        private Button _pauseButton;

        /// <summary>slow leaseを追加するButton。</summary>
        private Button _slowButton;

        /// <summary>fast leaseを追加するButton。</summary>
        private Button _fastButton;

        /// <summary>入れ子demoを開始するButton。</summary>
        private Button _nestedDemoButton;

        /// <summary>sample所有leaseを解放するButton。</summary>
        private Button _releaseOwnedButton;

        /// <summary>入れ子demoが実行中かを示す。</summary>
        private bool _demoRunning;

        /// <summary>停止対象を特定する現在の入れ子demo coroutine。</summary>
        private Coroutine _nestedDemoRoutine;

        /// <summary>`Time.deltaTime`だけを加えた経過秒数。</summary>
        private double _scaledElapsed;

        /// <summary>`Time.unscaledDeltaTime`だけを加えた経過秒数。</summary>
        private double _unscaledElapsed;

        /// <summary>PlayMode testが画面表示と同じスケール時間を確認する値。</summary>
        public double ScaledElapsed => _scaledElapsed;

        /// <summary>PlayMode testが画面表示と同じ非スケール時間を確認する値。</summary>
        public double UnscaledElapsed => _unscaledElapsed;

        /// <summary>PlayMode testが実Button callback後の表示段階を確認する値。</summary>
        public string StageText => _stageLabel?.text ?? string.Empty;

        /// <summary>同じGameObjectの所有物を取得し、実panelへsample画面を追加する。</summary>
        private void OnEnable()
        {
            _scaledElapsed = 0d;
            _unscaledElapsed = 0d;
            _timeControl = GetComponent<TimeControlController>();
            _timeControl.StatusChanged += HandleStatusChanged;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            SetStage("Ready");
            HandleStatusChanged(_timeControl.Status);
        }

        /// <summary>sampleの購読、coroutine、所有lease、表示要素を終了する。</summary>
        private void OnDisable()
        {
            StopNestedDemo();

            if (_timeControl != null) _timeControl.StatusChanged -= HandleStatusChanged;

            DisposeOwnedLeases();
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
            _statusLabel = null;
            _stageLabel = null;
            _resultLabel = null;
            _scaledCounterLabel = null;
            _unscaledCounterLabel = null;
            _scaledMarker = null;
            _unscaledMarker = null;
            _pauseButton = null;
            _slowButton = null;
            _fastButton = null;
            _nestedDemoButton = null;
            _releaseOwnedButton = null;
        }

        /// <summary>2種類の時間を累積し、pause中の差をLabelとlaneへ反映する。</summary>
        private void Update()
        {
            _scaledElapsed += Time.deltaTime;
            _unscaledElapsed += Time.unscaledDeltaTime;
            RefreshTimeLanes();
        }

        /// <summary>累積時間をLabelと移動markerへ反映する。</summary>
        private void RefreshTimeLanes()
        {
            if (_scaledCounterLabel != null)
            {
                _scaledCounterLabel.text = string.Format(CultureInfo.InvariantCulture, "Scaled Time: {0:F3} s", _scaledElapsed);
            }

            if (_unscaledCounterLabel != null)
            {
                _unscaledCounterLabel.text = string.Format(CultureInfo.InvariantCulture, "Unscaled Time: {0:F3} s", _unscaledElapsed);
            }

            SetMarkerPosition(_scaledMarker, _scaledElapsed);
            SetMarkerPosition(_unscaledMarker, _unscaledElapsed);
        }

        /// <summary>背景、状態、2本のlane、5つの操作Buttonをコードだけで構築する。</summary>
        /// <param name="documentRoot">UIDocumentが所有する実panelのroot。</param>
        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                enabled = false;
                Debug.LogError("[Time Control Basics] UIDocumentのrootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.035f, 0.055f, 0.095f, 1f);

            var evidence = new VisualElement { name = EvidenceElementName, pickingMode = PickingMode.Ignore };
            evidence.style.position = Position.Absolute;
            evidence.style.left = 0f;
            evidence.style.top = 0f;
            evidence.style.right = 0f;
            evidence.style.bottom = 0f;
            evidence.style.backgroundColor = new Color(0.035f, 0.055f, 0.095f, 1f);
            _sampleRoot.Add(evidence);

            var card = new VisualElement { name = CardElementName };
            card.style.width = new Length(84f, LengthUnit.Percent);
            card.style.maxWidth = 980f;
            card.style.height = new Length(96f, LengthUnit.Percent);
            card.style.maxHeight = 840f;
            card.style.paddingLeft = 36f;
            card.style.paddingRight = 36f;
            card.style.paddingTop = 28f;
            card.style.paddingBottom = 28f;
            card.style.borderTopLeftRadius = 24f;
            card.style.borderTopRightRadius = 24f;
            card.style.borderBottomLeftRadius = 24f;
            card.style.borderBottomRightRadius = 24f;
            card.style.backgroundColor = new Color(0.09f, 0.13f, 0.22f, 0.98f);
            card.style.color = new Color(0.94f, 0.97f, 1f, 1f);
            card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(card);

            var title = new Label("Time Control Basics");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8f;
            card.Add(title);

            var description = new Label("複数leaseでは最小倍率が優先されます。Pause中もUIとUnscaled Laneは動き続けます。");
            description.style.fontSize = 17f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 18f;
            card.Add(description);

            _statusLabel = new Label { name = StatusElementName };
            _statusLabel.style.fontSize = 16f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.minHeight = 48f;
            _statusLabel.style.marginBottom = 8f;
            card.Add(_statusLabel);

            _stageLabel = new Label { name = StageElementName };
            _stageLabel.style.fontSize = 17f;
            _stageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stageLabel.style.color = new Color(0.38f, 0.86f, 1f, 1f);
            _stageLabel.style.marginBottom = 6f;
            card.Add(_stageLabel);

            _resultLabel = new Label("Result: 操作待ち");
            _resultLabel.name = ResultElementName;
            _resultLabel.style.fontSize = 14f;
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.minHeight = 32f;
            _resultLabel.style.marginBottom = 14f;
            card.Add(_resultLabel);

            CreateLane(card, "Scaled Lane / Time.deltaTime", ScaledCounterElementName, ScaledLaneMarkerElementName, new Color(1f, 0.55f, 0.27f, 1f), out _scaledCounterLabel, out _scaledMarker);
            CreateLane(card, "Unscaled Lane / Time.unscaledDeltaTime", UnscaledCounterElementName, UnscaledLaneMarkerElementName, new Color(0.24f, 0.87f, 0.67f, 1f), out _unscaledCounterLabel, out _unscaledMarker);

            var buttons = new VisualElement { name = ButtonRowElementName };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.justifyContent = Justify.Center;
            buttons.style.marginTop = 10f;
            card.Add(buttons);

            _pauseButton = CreateButton(PauseButtonElementName, "Pause x0", () => AcquireManual(0f, "Manual: Pause x0"));
            _slowButton = CreateButton(SlowButtonElementName, "Slow x0.25", () => AcquireManual(0.25f, "Manual: Slow x0.25"));
            _fastButton = CreateButton(FastButtonElementName, "Fast x2", () => AcquireManual(2f, "Manual: Fast x2"));
            _nestedDemoButton = CreateButton(NestedDemoButtonElementName, "Nested Demo", StartNestedDemo);
            _releaseOwnedButton = CreateButton(ReleaseOwnedButtonElementName, "Release Owned", ReleaseOwned);
            buttons.Add(_pauseButton);
            buttons.Add(_slowButton);
            buttons.Add(_fastButton);
            buttons.Add(_nestedDemoButton);
            buttons.Add(_releaseOwnedButton);

            documentRoot.Add(_sampleRoot);
            RefreshTimeLanes();
        }

        /// <summary>時間の種類、累積値、移動markerを1本のlaneとして追加する。</summary>
        /// <param name="parent">laneを追加するcard。</param>
        /// <param name="title">時間の種類とUnity API名。</param>
        /// <param name="counterName">累積Labelの安定した要素名。</param>
        /// <param name="markerName">移動markerの安定した要素名。</param>
        /// <param name="accent">laneとmarkerを区別する色。</param>
        /// <param name="counter">作成した累積Label。</param>
        /// <param name="marker">作成した移動marker。</param>
        private static void CreateLane(VisualElement parent, string title, string counterName, string markerName, Color accent, out Label counter, out VisualElement marker)
        {
            var laneCard = new VisualElement();
            laneCard.style.marginTop = 6f;
            laneCard.style.marginBottom = 6f;
            laneCard.style.paddingLeft = 16f;
            laneCard.style.paddingRight = 16f;
            laneCard.style.paddingTop = 12f;
            laneCard.style.paddingBottom = 12f;
            laneCard.style.borderTopLeftRadius = 12f;
            laneCard.style.borderTopRightRadius = 12f;
            laneCard.style.borderBottomLeftRadius = 12f;
            laneCard.style.borderBottomRightRadius = 12f;
            laneCard.style.backgroundColor = new Color(0.055f, 0.08f, 0.14f, 1f);
            parent.Add(laneCard);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            laneCard.Add(header);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 15f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLabel);

            counter = new Label("0.000 s") { name = counterName };
            counter.style.fontSize = 15f;
            counter.style.color = accent;
            header.Add(counter);

            var track = new VisualElement();
            track.style.height = 18f;
            track.style.marginTop = 8f;
            track.style.borderTopLeftRadius = 9f;
            track.style.borderTopRightRadius = 9f;
            track.style.borderBottomLeftRadius = 9f;
            track.style.borderBottomRightRadius = 9f;
            track.style.backgroundColor = new Color(0.14f, 0.18f, 0.27f, 1f);
            laneCard.Add(track);

            marker = new VisualElement { name = markerName, pickingMode = PickingMode.Ignore };
            marker.style.position = Position.Absolute;
            marker.style.left = new Length(4f, LengthUnit.Percent);
            marker.style.top = 2f;
            marker.style.width = 14f;
            marker.style.height = 14f;
            marker.style.borderTopLeftRadius = 7f;
            marker.style.borderTopRightRadius = 7f;
            marker.style.borderBottomLeftRadius = 7f;
            marker.style.borderBottomRightRadius = 7f;
            marker.style.backgroundColor = accent;
            track.Add(marker);
        }

        /// <summary>共通寸法の操作Buttonを作る。</summary>
        /// <param name="name">testと視覚gateが特定する安定した名前。</param>
        /// <param name="text">Buttonへ表示する操作名。</param>
        /// <param name="clicked">実際のButton callbackとして実行する処理。</param>
        /// <returns>sample画面へ追加できるButton。</returns>
        private static Button CreateButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text };
            button.style.flexBasis = 100f;
            button.style.flexGrow = 1f;
            button.style.flexShrink = 1f;
            button.style.minWidth = 100f;
            button.style.maxWidth = 164f;
            button.style.height = 44f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.marginTop = 3f;
            button.style.marginBottom = 3f;
            button.style.fontSize = 15f;
            return button;
        }

        /// <summary>手動Buttonに対応する相対倍率を取得し、sample所有一覧へ加える。</summary>
        /// <param name="multiplier">0以上100以下の相対倍率。</param>
        /// <param name="stage">取得成功時に表示する手動段階。</param>
        private void AcquireManual(float multiplier, string stage)
        {
            if (_demoRunning) return;

            if (TryAcquireOwned(multiplier, out _, out var error))
            {
                SetStage(stage);
                _resultLabel.text = string.Format(CultureInfo.InvariantCulture, "Result: x{0:0.##} leaseを取得", multiplier);
            }
            else
            {
                SetStage("Manual: Acquire Failed");
                _resultLabel.text = $"Result: 取得失敗 / {error}";
            }

            RefreshButtonState();
        }

        /// <summary>sample自身が所有するleaseをすべて解放し、実行中demoも止める。</summary>
        private void ReleaseOwned()
        {
            var releasedCount = _ownedLeases.Count;
            StopNestedDemo();
            DisposeOwnedLeases();
            SetStage("Manual: Release Owned");
            _resultLabel.text = $"Result: sample所有leaseを{releasedCount}件解放";
            HandleStatusChanged(_timeControl.Status);
        }

        /// <summary>sample所有leaseを初期化してから、入れ子優先順位demoを開始する。</summary>
        private void StartNestedDemo()
        {
            if (_demoRunning || !_timeControl.IsControlling) return;

            DisposeOwnedLeases();
            _demoRunning = true;
            _nestedDemoRoutine = StartCoroutine(RunNestedDemo());
            RefreshButtonState();
        }

        /// <summary>2、0.25、0の取得と逆順解放を非スケール時間で実行する。</summary>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private IEnumerator RunNestedDemo()
        {
            TimeScaleLease fastLease = null;
            TimeScaleLease slowLease = null;
            TimeScaleLease pauseLease = null;

            try
            {
                if (!TryAcquireDemoLease(2f, NestedFastStageText, out fastLease)) yield break;
                yield return WaitForRealtime(NestedStageDurationSeconds);

                if (!TryAcquireDemoLease(0.25f, NestedSlowStageText, out slowLease)) yield break;
                yield return WaitForRealtime(NestedStageDurationSeconds);

                if (!TryAcquireDemoLease(0f, NestedPauseStageText, out pauseLease)) yield break;
                yield return WaitForRealtime(NestedStageDurationSeconds);

                DisposeOwnedLease(pauseLease);
                pauseLease = null;
                SetStage(NestedReleasePauseStageText);
                yield return WaitForRealtime(NestedStageDurationSeconds);

                DisposeOwnedLease(slowLease);
                slowLease = null;
                SetStage(NestedReleaseSlowStageText);
                yield return WaitForRealtime(NestedStageDurationSeconds);

                DisposeOwnedLease(fastLease);
                fastLease = null;
                SetStage(NestedReleaseFastStageText);
                _resultLabel.text = "Result: Nested Demo完了 / 基準値へ復元";
            }
            finally
            {
                DisposeOwnedLease(pauseLease);
                DisposeOwnedLease(slowLease);
                DisposeOwnedLease(fastLease);
                _demoRunning = false;
                _nestedDemoRoutine = null;
                RefreshButtonState();
            }
        }

        /// <summary>入れ子demo用leaseを取得し、段階または失敗理由を表示する。</summary>
        /// <param name="multiplier">取得する相対倍率。</param>
        /// <param name="stage">成功時に表示する段階。</param>
        /// <param name="lease">成功時にsampleが所有するlease。</param>
        /// <returns>取得できた場合はtrue。</returns>
        private bool TryAcquireDemoLease(float multiplier, string stage, out TimeScaleLease lease)
        {
            if (TryAcquireOwned(multiplier, out lease, out var error))
            {
                SetStage(stage);
                _resultLabel.text = string.Format(CultureInfo.InvariantCulture, "Result: Nested x{0:0.##} leaseを取得", multiplier);
                return true;
            }

            SetStage("Nested: Acquire Failed");
            _resultLabel.text = $"Result: Nested取得失敗 / {error}";
            return false;
        }

        /// <summary>Controllerからleaseを取得し、成功したleaseだけをsample所有一覧へ加える。</summary>
        /// <param name="multiplier">取得する相対倍率。</param>
        /// <param name="lease">成功時に取得したlease。</param>
        /// <param name="error">失敗時の理由。</param>
        /// <returns>leaseを取得できた場合はtrue。</returns>
        private bool TryAcquireOwned(float multiplier, out TimeScaleLease lease, out TimeControlError error)
        {
            if (_timeControl.TryAcquire(multiplier, out lease, out error))
            {
                _ownedLeases.Add(lease);
                return true;
            }

            return false;
        }

        /// <summary>指定したsample所有leaseを重複解放可能な契約で破棄する。</summary>
        /// <param name="lease">解放するlease。nullまたはstaleでも安全。</param>
        private void DisposeOwnedLease(TimeScaleLease lease)
        {
            if (lease == null) return;

            lease.Dispose();
            _ownedLeases.Remove(lease);
        }

        /// <summary>sampleが保持するleaseを逆順に全て解放する。</summary>
        private void DisposeOwnedLeases()
        {
            var leases = _ownedLeases.ToArray();
            _ownedLeases.Clear();
            for (var index = leases.Length - 1; index >= 0; index--)
            {
                leases[index]?.Dispose();
            }
        }

        /// <summary>実行中の入れ子demoだけを止め、lease解放は呼出元へ委ねる。</summary>
        private void StopNestedDemo()
        {
            if (_nestedDemoRoutine != null)
            {
                StopCoroutine(_nestedDemoRoutine);
                _nestedDemoRoutine = null;
            }

            _demoRunning = false;
        }

        /// <summary>`Time.timeScale`に影響されない実時間deadlineまで待つ。</summary>
        /// <param name="seconds">0以上の待機秒数。</param>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitForRealtime(double seconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        }

        /// <summary>状態snapshotを基準値、倍率、実効値、lease数、errorとして表示する。</summary>
        /// <param name="status">Controllerが通知した不変snapshot。</param>
        private void HandleStatusChanged(TimeControlStatus status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Status: Controlling={0}  Baseline={1:0.###}  Multiplier={2:0.###}\nEffective={3:0.###}  Leases={4}  Error={5}",
                    status.IsControlling,
                    status.BaselineTimeScale,
                    status.EffectiveMultiplier,
                    status.EffectiveTimeScale,
                    status.ActiveLeaseCount,
                    status.Error);
            }

            RemoveInactiveOwnedLeases();
            if (_demoRunning && !status.IsControlling)
            {
                StopNestedDemo();
                DisposeOwnedLeases();
                SetStage("Control Stopped");
                if (_resultLabel != null) _resultLabel.text = $"Result: 制御停止 / {status.Error}";
            }

            RefreshButtonState();
        }

        /// <summary>Controller終了で無効化されたstale leaseをsample一覧から外す。</summary>
        private void RemoveInactiveOwnedLeases()
        {
            for (var index = _ownedLeases.Count - 1; index >= 0; index--)
            {
                if (_ownedLeases[index] == null || !_ownedLeases[index].IsActive) _ownedLeases.RemoveAt(index);
            }
        }

        /// <summary>現在の手動操作または入れ子demo段階を安定した文字列で表示する。</summary>
        /// <param name="stage">testと画面captureが共有する段階文字列。</param>
        private void SetStage(string stage)
        {
            if (_stageLabel != null) _stageLabel.text = stage;
        }

        /// <summary>制御可否とdemo実行状態に合わせてButtonを有効化する。</summary>
        private void RefreshButtonState()
        {
            if (_timeControl == null) return;

            var canAcquire = _timeControl.IsControlling && !_demoRunning;
            _pauseButton?.SetEnabled(canAcquire);
            _slowButton?.SetEnabled(canAcquire);
            _fastButton?.SetEnabled(canAcquire);
            _nestedDemoButton?.SetEnabled(canAcquire);
            _releaseOwnedButton?.SetEnabled(_timeControl.IsControlling && _ownedLeases.Count > 0);
        }

        /// <summary>累積秒数を周期化し、lane内のmarker位置へ反映する。</summary>
        /// <param name="marker">位置を更新するmarker。</param>
        /// <param name="elapsed">対応する時間APIの累積秒数。</param>
        private static void SetMarkerPosition(VisualElement marker, double elapsed)
        {
            if (marker == null) return;

            var normalized = (elapsed * 0.28d) % 1d;
            marker.style.left = new Length(4f + (float)(normalized * 90d), LengthUnit.Percent);
        }
    }
}
