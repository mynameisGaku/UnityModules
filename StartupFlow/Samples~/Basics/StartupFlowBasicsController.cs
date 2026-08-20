using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace StartupFlow.Samples
{
    /// <summary>成功、失敗、cancelの直列startup flowを実Buttonと進捗表示で確認するサンプル。</summary>
    [AddComponentMenu("StudioGaku/Startup Flow Basics Controller")]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StartupFlowBasicsController : MonoBehaviour
    {
        /// <summary>sample構築完了を示すroot要素名。</summary>
        public const string ReadyElementName = "startup-flow-basics-ready";
        /// <summary>全表示を囲むcard要素名。</summary>
        public const string CardElementName = "startup-flow-basics-card";
        /// <summary>操作Buttonを表示順に含む行要素名。</summary>
        public const string ButtonRowElementName = "startup-flow-basics-buttons";
        /// <summary>sample title要素名。</summary>
        public const string TitleElementName = "startup-flow-basics-title";
        /// <summary>機能説明要素名。</summary>
        public const string DescriptionElementName = "startup-flow-basics-description";
        /// <summary>flow状態を表示する要素名。</summary>
        public const string StatusElementName = "startup-flow-basics-status";
        /// <summary>直近結果を表示する要素名。</summary>
        public const string StageElementName = "startup-flow-basics-stage";
        /// <summary>全体進捗の背景要素名。</summary>
        public const string ProgressTrackElementName = "startup-flow-basics-progress-track";
        /// <summary>全体進捗の塗り要素名。</summary>
        public const string ProgressFillElementName = "startup-flow-basics-progress-fill";
        /// <summary>実行済みstepを表示する要素名。</summary>
        public const string TraceElementName = "startup-flow-basics-trace";
        /// <summary>成功flowを開始するButton名。</summary>
        public const string RunSuccessButtonElementName = "startup-flow-basics-run-success";
        /// <summary>途中失敗flowを開始するButton名。</summary>
        public const string RunFailureButtonElementName = "startup-flow-basics-run-failure";
        /// <summary>cancel確認用の長いflowを開始するButton名。</summary>
        public const string RunSlowButtonElementName = "startup-flow-basics-run-slow";
        /// <summary>現在flowへcancelを要求するButton名。</summary>
        public const string CancelButtonElementName = "startup-flow-basics-cancel";
        /// <summary>表示とtraceを初期状態へ戻すButton名。</summary>
        public const string ResetButtonElementName = "startup-flow-basics-reset";

        private readonly List<string> _executionTrace = new List<string>();
        private StartupFlowService _service;
        private CancellationTokenSource _cancellation;
        private StartupFlowResult _lastResult;
        private bool _hasResult;
        private VisualElement _sampleRoot;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private VisualElement _progressTrack;
        private VisualElement _progressFill;
        private Label _title;
        private Label _description;
        private Label _status;
        private Label _stage;
        private Label _trace;
        private Button _runSuccess;
        private Button _runFailure;
        private Button _runSlow;
        private Button _cancel;
        private Button _reset;

        /// <summary>直近に完了したflowがある場合にtrue。</summary>
        public bool HasResult => _hasResult;

        /// <summary>直近に完了したflowの結果。HasResultがfalseの場合はdefault値。</summary>
        public StartupFlowResult LastResult => _lastResult;

        /// <summary>現在flowを実行中または通知中ならtrue。</summary>
        public bool IsRunning => _service?.IsBusy ?? false;

        /// <summary>現在のflowで実行開始したstep識別子を順番どおり返す。</summary>
        public IReadOnlyList<string> ExecutionTrace => _executionTrace;

        /// <summary>画面へ表示している直近結果。</summary>
        public string StageText => _stage?.text ?? string.Empty;

        private void OnEnable()
        {
            _service = new StartupFlowService();
            _service.StatusChanged += HandleStatusChanged;
            _service.Finished += HandleFinished;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            ResetView();
        }

        private void OnDisable()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            if (_service != null)
            {
                _service.StatusChanged -= HandleStatusChanged;
                _service.Finished -= HandleFinished;
            }

            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
        }

        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                Debug.LogError("[Startup Flow Basics] UIDocument rootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.018f, 0.045f, 0.075f, 1f);

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
            _card.style.backgroundColor = new Color(0.04f, 0.12f, 0.17f, 0.99f);
            _card.style.color = new Color(0.94f, 0.99f, 1f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            _title = new Label("Startup Flow Basics") { name = TitleElementName };
            _title.style.fontSize = 32f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 6f;
            _card.Add(_title);

            _description = new Label("明示したstepをOrder → Idで直列実行し、進捗・失敗位置・cancelを確認します。") { name = DescriptionElementName };
            _description.style.fontSize = 15f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 10f;
            _card.Add(_description);

            _status = new Label { name = StatusElementName };
            _status.style.fontSize = 14f;
            _status.style.marginBottom = 5f;
            _card.Add(_status);

            _stage = new Label { name = StageElementName };
            _stage.style.fontSize = 16f;
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stage.style.color = new Color(0.35f, 0.92f, 0.88f, 1f);
            _stage.style.marginBottom = 8f;
            _card.Add(_stage);

            _progressTrack = new VisualElement { name = ProgressTrackElementName };
            _progressTrack.style.height = 24f;
            _progressTrack.style.marginBottom = 9f;
            _progressTrack.style.borderTopLeftRadius = 12f;
            _progressTrack.style.borderTopRightRadius = 12f;
            _progressTrack.style.borderBottomLeftRadius = 12f;
            _progressTrack.style.borderBottomRightRadius = 12f;
            _progressTrack.style.overflow = Overflow.Hidden;
            _progressTrack.style.backgroundColor = new Color(0.018f, 0.055f, 0.085f, 1f);
            _card.Add(_progressTrack);

            _progressFill = new VisualElement { name = ProgressFillElementName };
            _progressFill.style.height = new Length(100f, LengthUnit.Percent);
            _progressFill.style.width = 0f;
            _progressFill.style.backgroundColor = new Color(0.14f, 0.78f, 0.72f, 1f);
            _progressTrack.Add(_progressFill);

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
            _trace.style.backgroundColor = new Color(0.02f, 0.075f, 0.105f, 1f);
            _card.Add(_trace);

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);
            _runSuccess = CreateButton(RunSuccessButtonElementName, "Run Success", () => StartFlow(false, false));
            _runFailure = CreateButton(RunFailureButtonElementName, "Run Failure", () => StartFlow(true, false));
            _runSlow = CreateButton(RunSlowButtonElementName, "Run Slow", () => StartFlow(false, true));
            _cancel = CreateButton(CancelButtonElementName, "Cancel", CancelFlow);
            _reset = CreateButton(ResetButtonElementName, "Reset", ResetView);
            _buttonRow.Add(_runSuccess);
            _buttonRow.Add(_runFailure);
            _buttonRow.Add(_runSlow);
            _buttonRow.Add(_cancel);
            _buttonRow.Add(_reset);

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

        private async void StartFlow(bool shouldFail, bool slow)
        {
            if (_service == null || _service.IsBusy) return;
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            _executionTrace.Clear();
            _hasResult = false;
            _lastResult = default;
            SetStage(slow ? "Slow flow / Cancelで中止できます" : shouldFail ? "Failure flow / 2番目で停止します" : "Success flow / 3 stepを直列実行します");
            RefreshButtons();

            IStartupStep[] steps = slow
                ? new IStartupStep[] { new DemoStep("10-long-warmup", 10, 120, false, _executionTrace) }
                : new IStartupStep[]
                {
                    new DemoStep("10-check-config", 10, 4, false, _executionTrace),
                    new DemoStep(shouldFail ? "20-load-profile" : "20-warm-cache", 20, 5, shouldFail, _executionTrace),
                    new DemoStep("30-ready-gameplay", 30, 3, false, _executionTrace)
                };
            var result = await _service.RunAsync(steps, _cancellation.Token);
            if (!isActiveAndEnabled) return;
            _lastResult = result;
            _hasResult = true;
            SetStage(result.IsSuccess ? $"Completed / {result.CompletedStepCount}/{result.TotalStepCount}" : $"{result.Error} / {result.FailedStepId}");
            RefreshTrace();
            RefreshButtons();
        }

        private void CancelFlow()
        {
            if (_service?.IsBusy != true) return;
            _cancellation?.Cancel();
            SetStage("Cancel requested / cooperative stepを待機中");
        }

        private void ResetView()
        {
            if (_service?.IsBusy == true) return;
            _executionTrace.Clear();
            _lastResult = default;
            _hasResult = false;
            SetStage("Ready / explicit steps, no auto-run");
            if (_status != null) _status.text = "Phase: Idle  Step: -  Progress: 0%";
            if (_progressFill != null) _progressFill.style.width = 0f;
            RefreshTrace();
            RefreshButtons();
        }

        private void HandleStatusChanged(StartupFlowStatus status)
        {
            if (_status != null)
            {
                _status.text = $"Phase: {status.Phase}  Step: {(string.IsNullOrEmpty(status.StepId) ? "-" : status.StepId)}  Progress: {status.OverallProgress * 100f:0}%";
            }

            if (_progressFill != null) _progressFill.style.width = new Length(status.OverallProgress * 100f, LengthUnit.Percent);
            RefreshTrace();
            RefreshButtons();
        }

        private void HandleFinished(StartupFlowResult result)
        {
            if (_stage != null) _stage.text = result.IsSuccess ? "Flow terminal: success" : $"Flow terminal: {result.Error}";
        }

        private void RefreshTrace()
        {
            if (_trace != null) _trace.text = _executionTrace.Count == 0 ? "EXECUTION ORDER  —" : "EXECUTION ORDER  " + string.Join("  →  ", _executionTrace);
        }

        private void RefreshButtons()
        {
            var busy = _service?.IsBusy ?? false;
            _runSuccess?.SetEnabled(!busy);
            _runFailure?.SetEnabled(!busy);
            _runSlow?.SetEnabled(!busy);
            _cancel?.SetEnabled(busy);
            _reset?.SetEnabled(!busy);
        }

        private void SetStage(string value)
        {
            if (_stage != null) _stage.text = value;
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            var compact = evt.newRect.width < 720f || evt.newRect.height < 500f;
            _card.style.paddingLeft = compact ? 12f : 32f;
            _card.style.paddingRight = compact ? 12f : 32f;
            _card.style.paddingTop = compact ? 7f : 22f;
            _card.style.paddingBottom = compact ? 7f : 22f;
            _title.style.fontSize = compact ? 23f : 32f;
            _title.style.marginBottom = compact ? 2f : 6f;
            _description.style.fontSize = compact ? 10.5f : 15f;
            _description.style.marginBottom = compact ? 3f : 10f;
            _status.style.fontSize = compact ? 10.5f : 14f;
            _status.style.marginBottom = compact ? 2f : 5f;
            _stage.style.fontSize = compact ? 11.5f : 16f;
            _stage.style.marginBottom = compact ? 3f : 8f;
            _progressTrack.style.height = compact ? 17f : 24f;
            _progressTrack.style.marginBottom = compact ? 3f : 9f;
            _trace.style.minHeight = compact ? 30f : 42f;
            _trace.style.paddingTop = compact ? 5f : 9f;
            _trace.style.paddingBottom = compact ? 5f : 9f;
            _trace.style.marginBottom = compact ? 3f : 8f;
            _trace.style.fontSize = compact ? 9.5f : 13f;
            foreach (var button in new[] { _runSuccess, _runFailure, _runSlow, _cancel, _reset })
            {
                button.style.flexBasis = compact ? 142f : 116f;
                button.style.minWidth = compact ? 130f : 105f;
                button.style.maxWidth = compact ? 190f : 170f;
                button.style.height = compact ? 31f : 42f;
                button.style.fontSize = compact ? 10.5f : 14f;
                button.style.marginTop = compact ? 2f : 4f;
                button.style.marginBottom = compact ? 2f : 4f;
            }
        }

        private sealed class DemoStep : IStartupStep
        {
            private readonly int _frameCount;
            private readonly bool _shouldFail;
            private readonly List<string> _trace;

            internal DemoStep(string id, int order, int frameCount, bool shouldFail, List<string> trace)
            {
                Id = id;
                Order = order;
                _frameCount = frameCount;
                _shouldFail = shouldFail;
                _trace = trace;
            }

            public string Id { get; }
            public int Order { get; }

            public async Awaitable ExecuteAsync(StartupStepContext context)
            {
                _trace.Add(Id);
                for (var index = 0; index < _frameCount; index++)
                {
                    await Awaitable.NextFrameAsync(context.CancellationToken);
                    var error = context.ReportProgress((index + 1f) / _frameCount);
                    if (error != StartupFlowError.None) throw new InvalidOperationException($"progress report failed: {error}");
                }

                if (_shouldFail) throw new InvalidOperationException("sample requested failure");
            }
        }
    }
}
