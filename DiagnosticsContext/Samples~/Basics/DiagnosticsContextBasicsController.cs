using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiagnosticsContext.Samples
{
    /// <summary>明示的な診断情報、live log capture、手動reportの境界を実画面で操作する。</summary>
    [AddComponentMenu("StudioGaku/Diagnostics Context Basics Controller")]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DiagnosticsContextBasicsController : MonoBehaviour
    {
        /// <summary>外部の視覚gateがsample構築完了を確認する要素名。</summary>
        public const string ReadyElementName = "diagnostics-context-basics-ready";

        /// <summary>画面captureの背景色を確認する要素名。</summary>
        public const string EvidenceElementName = "diagnostics-context-basics-evidence";

        /// <summary>全表示と操作Buttonを囲むcard名。</summary>
        public const string CardElementName = "diagnostics-context-basics-card";

        /// <summary>sample名を表示するtitle Label名。</summary>
        public const string TitleElementName = "diagnostics-context-basics-title";

        /// <summary>手動reportであることを表示するbadge Label名。</summary>
        public const string BadgeElementName = "diagnostics-context-basics-badge";

        /// <summary>有界保持する情報を説明するLabel名。</summary>
        public const string DescriptionElementName = "diagnostics-context-basics-description";

        /// <summary>5つの操作Buttonを表示順に含む行名。</summary>
        public const string ButtonRowElementName = "diagnostics-context-basics-buttons";

        /// <summary>Service ownerとdrop件数を表示するLabel名。</summary>
        public const string StatusElementName = "diagnostics-context-basics-status";

        /// <summary>context件数を表示するLabel名。</summary>
        public const string ContextCountElementName = "diagnostics-context-basics-context-count";

        /// <summary>breadcrumb件数を表示するLabel名。</summary>
        public const string BreadcrumbCountElementName = "diagnostics-context-basics-breadcrumb-count";

        /// <summary>captured log件数を表示するLabel名。</summary>
        public const string LogCountElementName = "diagnostics-context-basics-log-count";

        /// <summary>直近のAPI結果を表示するLabel名。</summary>
        public const string ResultElementName = "diagnostics-context-basics-result";

        /// <summary>直近に成功したreport pathを表示するLabel名。</summary>
        public const string ReportPathElementName = "diagnostics-context-basics-report-path";

        /// <summary>自動識別情報収集を行わない境界を表示するLabel名。</summary>
        public const string PrivacyElementName = "diagnostics-context-basics-privacy";

        /// <summary>crash survivalとuploadを保証しない境界を表示するLabel名。</summary>
        public const string ManualBoundaryElementName = "diagnostics-context-basics-manual-boundary";

        /// <summary>明示的なcontextを1件追加するButton名。</summary>
        public const string AddContextButtonElementName = "diagnostics-context-basics-add-context";

        /// <summary>時系列breadcrumbを1件追加するButton名。</summary>
        public const string AddBreadcrumbButtonElementName = "diagnostics-context-basics-add-breadcrumb";

        /// <summary>live subscriptionへWarningを1件送るButton名。</summary>
        public const string EmitWarningButtonElementName = "diagnostics-context-basics-emit-warning";

        /// <summary>現在のsnapshotを手動reportへ保存するButton名。</summary>
        public const string WriteReportButtonElementName = "diagnostics-context-basics-write-report";

        /// <summary>現在のServiceを終了して新しく作り直すButton名。</summary>
        public const string RecreateButtonElementName = "diagnostics-context-basics-recreate";

        /// <summary>sampleが明示的にreportへ渡す固定reason。</summary>
        public const string SampleReportReason = "Diagnostics Context Basics manual report [reason-only-7f3b]";

        /// <summary>sampleが発生させるWarningを外部testが識別する接頭辞。</summary>
        public const string SampleWarningPrefix = "[Diagnostics Context Basics] Sample warning #";

        /// <summary>狭い画面向け配置へ切り替えるroot幅。</summary>
        private const float CompactWidthThreshold = 720f;

        /// <summary>低い画面向け配置へ切り替えるroot高さ。</summary>
        private const float CompactHeightThreshold = 480f;

        /// <summary>wide画面で表示する機能説明。</summary>
        private const string WideDescriptionText = "明示したcontextとbreadcrumb、実行中のUnity log payloadを有界にまとめます。";

        /// <summary>compact画面で1行に収める機能説明。</summary>
        private const string CompactDescriptionText = "明示context・breadcrumb・対象logを有界保持します。";

        /// <summary>wide画面で表示するprivacy注意。</summary>
        private const string WidePrivacyText = "Privacy: contextはすべてopt-in。識別fieldを自動追加しません。Log本文・stack内のpath・user名・tokenは共有前に確認してください。";

        /// <summary>compact画面で要点を2行以内へ収めるprivacy注意。</summary>
        private const string CompactPrivacyText = "Privacy: 識別fieldは自動追加なし。Log内のpath・user名・tokenは共有前に確認。";

        /// <summary>このsample componentが明示的に所有する診断Service。</summary>
        private DiagnosticsContextService _service;

        /// <summary>UIDocumentへ追加したsample画面のroot。</summary>
        private VisualElement _sampleRoot;

        /// <summary>全表示とButtonを安全余白内へ収めるcard。</summary>
        private VisualElement _card;

        /// <summary>3種類の件数を横並びにする領域。</summary>
        private VisualElement _metricsRow;

        /// <summary>5つの操作Buttonを折返し可能に並べる領域。</summary>
        private VisualElement _buttonRow;

        /// <summary>sample名を表示するtitle Label。</summary>
        private Label _titleLabel;

        /// <summary>手動reportであることを表示するbadge Label。</summary>
        private Label _badgeLabel;

        /// <summary>有界保持する情報を説明するLabel。</summary>
        private Label _descriptionLabel;

        /// <summary>Service ownerとdrop件数を表示するLabel。</summary>
        private Label _statusLabel;

        /// <summary>現在のcontext件数を表示するLabel。</summary>
        private Label _contextCountLabel;

        /// <summary>現在のbreadcrumb件数を表示するLabel。</summary>
        private Label _breadcrumbCountLabel;

        /// <summary>現在のcaptured log件数を表示するLabel。</summary>
        private Label _logCountLabel;

        /// <summary>直近のAPI結果を表示するLabel。</summary>
        private Label _resultLabel;

        /// <summary>直近に成功したreport pathを表示するLabel。</summary>
        private Label _reportPathLabel;

        /// <summary>自動収集しないprivacy境界を表示するLabel。</summary>
        private Label _privacyLabel;

        /// <summary>手動reportの限界を表示するLabel。</summary>
        private Label _manualBoundaryLabel;

        /// <summary>明示的なcontextを追加するButton。</summary>
        private Button _addContextButton;

        /// <summary>時系列breadcrumbを追加するButton。</summary>
        private Button _addBreadcrumbButton;

        /// <summary>Unity Warningを発生させるButton。</summary>
        private Button _emitWarningButton;

        /// <summary>現在のsnapshotを書き出すButton。</summary>
        private Button _writeReportButton;

        /// <summary>Serviceを終了して再作成するButton。</summary>
        private Button _recreateButton;

        /// <summary>context keyを重複させないsample内連番。</summary>
        private int _contextRequestNumber;

        /// <summary>breadcrumb messageへ表示するsample内連番。</summary>
        private int _breadcrumbRequestNumber;

        /// <summary>Warning messageへ表示するsample内連番。</summary>
        private int _warningRequestNumber;

        /// <summary>直近に成功したreportの絶対path。</summary>
        private string _lastReportPath = string.Empty;

        /// <summary>現在適用中のresponsive配置。初回適用前はnull。</summary>
        private bool? _compactLayout;

        /// <summary>PlayMode testがService作成状態を確認する。</summary>
        public bool ServiceAvailable => _service != null;

        /// <summary>PlayMode testがServiceの実context件数を確認する。</summary>
        public int ContextEntryCount => _service?.ContextEntryCount ?? 0;

        /// <summary>PlayMode testがServiceの実breadcrumb件数を確認する。</summary>
        public int BreadcrumbCount => _service?.BreadcrumbCount ?? 0;

        /// <summary>PlayMode testがServiceの実captured log件数を確認する。</summary>
        public int CapturedLogCount => _service?.CapturedLogCount ?? 0;

        /// <summary>PlayMode testが直近に成功したreport pathを確認する。</summary>
        public string LastReportPath => _lastReportPath;

        /// <summary>Serviceを明示作成し、実panelへsample画面を追加する。</summary>
        private void OnEnable()
        {
            _contextRequestNumber = 0;
            _breadcrumbRequestNumber = 0;
            _warningRequestNumber = 0;
            _lastReportPath = string.Empty;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            CreateService("Owner: TryCreate");
        }

        /// <summary>Serviceのlog購読と状態を終了し、sample画面を除去する。</summary>
        private void OnDisable()
        {
            _service?.Dispose();
            _service = null;
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
            _card = null;
            _metricsRow = null;
            _buttonRow = null;
            _titleLabel = null;
            _badgeLabel = null;
            _descriptionLabel = null;
            _statusLabel = null;
            _contextCountLabel = null;
            _breadcrumbCountLabel = null;
            _logCountLabel = null;
            _resultLabel = null;
            _reportPathLabel = null;
            _privacyLabel = null;
            _manualBoundaryLabel = null;
            _addContextButton = null;
            _addBreadcrumbButton = null;
            _emitWarningButton = null;
            _writeReportButton = null;
            _recreateButton = null;
            _compactLayout = null;
        }

        /// <summary>背景、privacy表示、件数、結果、5つのButtonをcodeだけで構築する。</summary>
        /// <param name="documentRoot">UIDocumentが所有する実panelのroot。</param>
        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                enabled = false;
                Debug.LogError("[Diagnostics Context Basics] UIDocumentのrootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.025f, 0.04f, 0.07f, 1f);

            var evidence = new VisualElement { name = EvidenceElementName, pickingMode = PickingMode.Ignore };
            evidence.style.position = Position.Absolute;
            evidence.style.left = 0f;
            evidence.style.top = 0f;
            evidence.style.right = 0f;
            evidence.style.bottom = 0f;
            evidence.style.backgroundColor = new Color(0.025f, 0.04f, 0.07f, 1f);
            _sampleRoot.Add(evidence);

            var upperGlow = CreateGlow(new Color(0.08f, 0.55f, 0.72f, 0.18f), -80f, -110f, 330f);
            _sampleRoot.Add(upperGlow);
            var lowerGlow = CreateGlow(new Color(0.42f, 0.22f, 0.72f, 0.14f), 760f, 430f, 280f);
            _sampleRoot.Add(lowerGlow);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(90f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.height = new Length(90f, LengthUnit.Percent);
            _card.style.maxHeight = 540f;
            _card.style.paddingLeft = 28f;
            _card.style.paddingRight = 28f;
            _card.style.paddingTop = 22f;
            _card.style.paddingBottom = 20f;
            SetRoundedCorners(_card, 22f);
            SetBorder(_card, new Color(0.18f, 0.42f, 0.55f, 0.72f), 1f);
            _card.style.backgroundColor = new Color(0.065f, 0.095f, 0.15f, 0.985f);
            _card.style.color = new Color(0.94f, 0.97f, 1f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.flexShrink = 0f;
            _card.Add(titleRow);

            _titleLabel = new Label("Diagnostics Context") { name = TitleElementName };
            _titleLabel.style.fontSize = 31f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.94f, 0.98f, 1f, 1f);
            titleRow.Add(_titleLabel);

            _badgeLabel = new Label("MANUAL REPORT") { name = BadgeElementName };
            _badgeLabel.style.fontSize = 11f;
            _badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _badgeLabel.style.color = new Color(0.37f, 0.9f, 0.78f, 1f);
            _badgeLabel.style.backgroundColor = new Color(0.08f, 0.25f, 0.25f, 1f);
            _badgeLabel.style.paddingLeft = 10f;
            _badgeLabel.style.paddingRight = 10f;
            _badgeLabel.style.paddingTop = 5f;
            _badgeLabel.style.paddingBottom = 5f;
            SetRoundedCorners(_badgeLabel, 10f);
            titleRow.Add(_badgeLabel);

            _descriptionLabel = new Label(WideDescriptionText) { name = DescriptionElementName };
            _descriptionLabel.style.fontSize = 15f;
            _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _descriptionLabel.style.marginTop = 4f;
            _descriptionLabel.style.marginBottom = 9f;
            _descriptionLabel.style.flexShrink = 0f;
            _descriptionLabel.style.color = new Color(0.75f, 0.82f, 0.9f, 1f);
            _card.Add(_descriptionLabel);

            _privacyLabel = new Label(WidePrivacyText) { name = PrivacyElementName };
            _privacyLabel.style.fontSize = 13f;
            _privacyLabel.style.whiteSpace = WhiteSpace.Normal;
            _privacyLabel.style.paddingLeft = 12f;
            _privacyLabel.style.paddingRight = 12f;
            _privacyLabel.style.paddingTop = 8f;
            _privacyLabel.style.paddingBottom = 8f;
            _privacyLabel.style.marginBottom = 9f;
            _privacyLabel.style.flexShrink = 0f;
            _privacyLabel.style.backgroundColor = new Color(0.08f, 0.16f, 0.22f, 1f);
            _privacyLabel.style.color = new Color(0.57f, 0.9f, 0.95f, 1f);
            SetRoundedCorners(_privacyLabel, 9f);
            _card.Add(_privacyLabel);

            _metricsRow = new VisualElement();
            _metricsRow.style.flexDirection = FlexDirection.Row;
            _metricsRow.style.marginBottom = 8f;
            _metricsRow.style.flexShrink = 0f;
            _card.Add(_metricsRow);

            _contextCountLabel = CreateMetric(ContextCountElementName, "CONTEXT", new Color(0.34f, 0.82f, 1f, 1f));
            _breadcrumbCountLabel = CreateMetric(BreadcrumbCountElementName, "BREADCRUMBS", new Color(0.55f, 0.91f, 0.66f, 1f));
            _logCountLabel = CreateMetric(LogCountElementName, "CAPTURED LOGS", new Color(1f, 0.69f, 0.35f, 1f));
            _metricsRow.Add(_contextCountLabel);
            _metricsRow.Add(_breadcrumbCountLabel);
            _metricsRow.Add(_logCountLabel);

            _statusLabel = new Label("Owner: 未作成") { name = StatusElementName };
            ConfigureSingleLine(_statusLabel, 13f, new Color(0.7f, 0.78f, 0.88f, 1f));
            _statusLabel.style.marginBottom = 4f;
            _card.Add(_statusLabel);

            _resultLabel = new Label("Last Result: 操作待ち") { name = ResultElementName };
            ConfigureSingleLine(_resultLabel, 14f, new Color(0.9f, 0.94f, 1f, 1f));
            _resultLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _resultLabel.style.marginBottom = 3f;
            _card.Add(_resultLabel);

            _reportPathLabel = new Label("Report Path: 未作成") { name = ReportPathElementName };
            ConfigureSingleLine(_reportPathLabel, 12f, new Color(0.56f, 0.72f, 0.86f, 1f));
            _reportPathLabel.style.marginBottom = 8f;
            _card.Add(_reportPathLabel);

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _buttonRow.style.flexShrink = 0f;
            _card.Add(_buttonRow);

            _addContextButton = CreateButton(AddContextButtonElementName, "Add Context", HandleAddContext, new Color(0.1f, 0.38f, 0.53f, 1f));
            _addBreadcrumbButton = CreateButton(AddBreadcrumbButtonElementName, "Add Breadcrumb", HandleAddBreadcrumb, new Color(0.12f, 0.4f, 0.3f, 1f));
            _emitWarningButton = CreateButton(EmitWarningButtonElementName, "Emit Warning", HandleEmitWarning, new Color(0.5f, 0.3f, 0.12f, 1f));
            _writeReportButton = CreateButton(WriteReportButtonElementName, "Write Report", HandleWriteReport, new Color(0.32f, 0.2f, 0.52f, 1f));
            _recreateButton = CreateButton(RecreateButtonElementName, "Dispose / Recreate", HandleRecreate, new Color(0.2f, 0.25f, 0.34f, 1f));
            _buttonRow.Add(_addContextButton);
            _buttonRow.Add(_addBreadcrumbButton);
            _buttonRow.Add(_emitWarningButton);
            _buttonRow.Add(_writeReportButton);
            _buttonRow.Add(_recreateButton);

            _manualBoundaryLabel = new Label("手動snapshotのみ / crash後の生存保証なし / uploadなし") { name = ManualBoundaryElementName };
            _manualBoundaryLabel.style.fontSize = 12f;
            _manualBoundaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _manualBoundaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _manualBoundaryLabel.style.marginTop = 7f;
            _manualBoundaryLabel.style.flexShrink = 0f;
            _manualBoundaryLabel.style.color = new Color(0.67f, 0.69f, 0.8f, 1f);
            _card.Add(_manualBoundaryLabel);

            _sampleRoot.RegisterCallback<GeometryChangedEvent>(HandleRootGeometryChanged);
            documentRoot.Add(_sampleRoot);
            RefreshStatus();
        }

        /// <summary>sample rootの確定寸法に応じてwideまたはcompact配置を適用する。</summary>
        /// <param name="geometryEvent">変更前後のroot寸法。</param>
        private void HandleRootGeometryChanged(GeometryChangedEvent geometryEvent)
        {
            var compact = geometryEvent.newRect.width < CompactWidthThreshold || geometryEvent.newRect.height < CompactHeightThreshold;
            ApplyResponsiveLayout(compact);
        }

        /// <summary>640x360でも全要素がcard内へ収まる寸法を切り替える。</summary>
        /// <param name="compact">狭いまたは低い画面向けならtrue。</param>
        private void ApplyResponsiveLayout(bool compact)
        {
            if (_compactLayout == compact || _card == null) return;

            _compactLayout = compact;
            _card.style.width = new Length(compact ? 94f : 90f, LengthUnit.Percent);
            _card.style.height = new Length(compact ? 94f : 90f, LengthUnit.Percent);
            _card.style.paddingLeft = compact ? 14f : 28f;
            _card.style.paddingRight = compact ? 14f : 28f;
            _card.style.paddingTop = compact ? 6f : 22f;
            _card.style.paddingBottom = compact ? 6f : 20f;
            _titleLabel.style.fontSize = compact ? 24f : 31f;
            _badgeLabel.style.fontSize = compact ? 9f : 11f;
            _badgeLabel.style.paddingLeft = compact ? 7f : 10f;
            _badgeLabel.style.paddingRight = compact ? 7f : 10f;
            _badgeLabel.style.paddingTop = compact ? 3f : 5f;
            _badgeLabel.style.paddingBottom = compact ? 3f : 5f;
            _descriptionLabel.text = compact ? CompactDescriptionText : WideDescriptionText;
            _descriptionLabel.style.fontSize = compact ? 11f : 15f;
            _descriptionLabel.style.marginTop = compact ? 1f : 4f;
            _descriptionLabel.style.marginBottom = compact ? 3f : 9f;
            _privacyLabel.text = compact ? CompactPrivacyText : WidePrivacyText;
            _privacyLabel.style.fontSize = compact ? 10.5f : 13f;
            _privacyLabel.style.paddingLeft = compact ? 8f : 12f;
            _privacyLabel.style.paddingRight = compact ? 8f : 12f;
            _privacyLabel.style.paddingTop = compact ? 3f : 8f;
            _privacyLabel.style.paddingBottom = compact ? 3f : 8f;
            _privacyLabel.style.marginBottom = compact ? 2f : 9f;
            _metricsRow.style.marginBottom = compact ? 2f : 8f;
            _statusLabel.style.fontSize = compact ? 10.5f : 13f;
            _statusLabel.style.marginBottom = compact ? 1f : 4f;
            _resultLabel.style.fontSize = compact ? 11.5f : 14f;
            _resultLabel.style.marginBottom = compact ? 1f : 3f;
            _reportPathLabel.style.fontSize = compact ? 10.5f : 12f;
            _reportPathLabel.style.marginBottom = compact ? 3f : 8f;
            _manualBoundaryLabel.style.fontSize = compact ? 9.5f : 12f;
            _manualBoundaryLabel.style.marginTop = compact ? 1f : 7f;

            var metricLabels = new[] { _contextCountLabel, _breadcrumbCountLabel, _logCountLabel };
            for (var index = 0; index < metricLabels.Length; index++)
            {
                metricLabels[index].style.fontSize = compact ? 10.5f : 14f;
                metricLabels[index].style.paddingTop = compact ? 2f : 9f;
                metricLabels[index].style.paddingBottom = compact ? 2f : 9f;
                metricLabels[index].style.marginLeft = compact ? 1.5f : 4f;
                metricLabels[index].style.marginRight = compact ? 1.5f : 4f;
            }

            var buttons = new[] { _addContextButton, _addBreadcrumbButton, _emitWarningButton, _writeReportButton, _recreateButton };
            for (var index = 0; index < buttons.Length; index++)
            {
                buttons[index].style.flexBasis = compact ? 160f : 135f;
                buttons[index].style.minHeight = compact ? 26f : 38f;
                buttons[index].style.fontSize = compact ? 10.5f : 13f;
                buttons[index].style.marginLeft = compact ? 1f : 4f;
                buttons[index].style.marginRight = compact ? 1f : 4f;
                buttons[index].style.marginTop = compact ? 1f : 4f;
                buttons[index].style.marginBottom = compact ? 1f : 4f;
            }
        }

        /// <summary>利用側が選んだ文字列だけを明示的なcontextとして追加する。</summary>
        private void HandleAddContext()
        {
            if (_service == null)
            {
                SetResult("Add Context: Service unavailable", true);
                return;
            }

            var nextNumber = _contextRequestNumber + 1;
            var contextKey = string.Format(CultureInfo.InvariantCulture, "sample.context.{0:D2}", nextNumber);
            var contextValue = string.Format(CultureInfo.InvariantCulture, "opt-in-value-{0:D2}", nextNumber);
            var error = _service.SetContext(contextKey, contextValue);
            if (error == DiagnosticsError.None) _contextRequestNumber = nextNumber;
            SetResult($"Add Context: {error}", error != DiagnosticsError.None);
            RefreshStatus();
        }

        /// <summary>sampleの操作順を表す短いbreadcrumbを明示追加する。</summary>
        private void HandleAddBreadcrumb()
        {
            if (_service == null)
            {
                SetResult("Add Breadcrumb: Service unavailable", true);
                return;
            }

            var nextNumber = _breadcrumbRequestNumber + 1;
            var message = string.Format(CultureInfo.InvariantCulture, "Sample action {0:D2}", nextNumber);
            var error = _service.AddBreadcrumb(message);
            if (error == DiagnosticsError.None) _breadcrumbRequestNumber = nextNumber;
            SetResult($"Add Breadcrumb: {error}", error != DiagnosticsError.None);
            RefreshStatus();
        }

        /// <summary>Serviceのlive subscriptionへ安全なsample Warningを1件送る。</summary>
        private void HandleEmitWarning()
        {
            if (_service == null)
            {
                SetResult("Emit Warning: Service unavailable", true);
                return;
            }

            _warningRequestNumber++;
            var warningMessage = string.Format(CultureInfo.InvariantCulture, "{0}{1:D2}", SampleWarningPrefix, _warningRequestNumber);
            Debug.LogWarning(warningMessage, this);
            SetResult("Emit Warning: callback delivered", false);
            RefreshStatus();
        }

        /// <summary>現在までのsnapshotを明示的な手動reportとして保存する。</summary>
        private void HandleWriteReport()
        {
            if (_service == null)
            {
                SetResult("Write Report: Service unavailable", true);
                return;
            }

            var result = _service.WriteReport(SampleReportReason);
            if (result.Succeeded)
            {
                _lastReportPath = result.ReportPath ?? string.Empty;
                SetResult(string.Format(CultureInfo.InvariantCulture, "Write Report: {0} bytes", result.ReportByteCount), false);
            }
            else
            {
                SetResult($"Write Report: {result.Error}", true);
            }

            RefreshStatus();
        }

        /// <summary>現在のServiceを終了し、同じsample ownerが新しいServiceを作り直す。</summary>
        private void HandleRecreate()
        {
            _service?.Dispose();
            _service = null;
            CreateService("Owner: Dispose -> TryCreate");
        }

        /// <summary>Diagnostics Context Serviceを明示作成して操作可否を更新する。</summary>
        /// <param name="operation">結果表示へ含めるowner操作。</param>
        private void CreateService(string operation)
        {
            if (DiagnosticsContextService.TryCreate(out var createdService, out var error))
            {
                _service = createdService;
                SetResult($"{operation}: None", false);
            }
            else
            {
                _service = null;
                SetResult($"{operation}: {error}", true);
            }

            SetOperationButtonsEnabled(_service != null);
            RefreshStatus();
        }

        /// <summary>Serviceが必要な4つのButtonだけをまとめて切り替える。</summary>
        /// <param name="enabledState">Serviceを利用できる場合はtrue。</param>
        private void SetOperationButtonsEnabled(bool enabledState)
        {
            _addContextButton?.SetEnabled(enabledState);
            _addBreadcrumbButton?.SetEnabled(enabledState);
            _emitWarningButton?.SetEnabled(enabledState);
            _writeReportButton?.SetEnabled(enabledState);
            _recreateButton?.SetEnabled(true);
        }

        /// <summary>Serviceの実件数、drop件数、直近pathを表示へ反映する。</summary>
        private void RefreshStatus()
        {
            if (_contextCountLabel != null) _contextCountLabel.text = string.Format(CultureInfo.InvariantCulture, "CONTEXT\n{0}", _service?.ContextEntryCount ?? 0);
            if (_breadcrumbCountLabel != null) _breadcrumbCountLabel.text = string.Format(CultureInfo.InvariantCulture, "BREADCRUMBS\n{0}", _service?.BreadcrumbCount ?? 0);
            if (_logCountLabel != null) _logCountLabel.text = string.Format(CultureInfo.InvariantCulture, "CAPTURED LOGS\n{0}", _service?.CapturedLogCount ?? 0);
            if (_statusLabel != null)
            {
                _statusLabel.text = _service == null
                    ? "Owner: unavailable"
                    : string.Format(CultureInfo.InvariantCulture, "Owner: active  |  Dropped: breadcrumbs {0}, logs {1}", _service.DroppedBreadcrumbCount, _service.DroppedLogCount);
            }

            if (_reportPathLabel != null)
            {
                _reportPathLabel.text = string.IsNullOrEmpty(_lastReportPath) ? "Report Path: 未作成" : $"Report Path: {_lastReportPath}";
                _reportPathLabel.tooltip = _lastReportPath;
            }
        }

        /// <summary>直近操作の成功または失敗を色と文字列で表示する。</summary>
        /// <param name="message">利用者が判断できる短い結果。</param>
        /// <param name="failed">失敗色を使う場合はtrue。</param>
        private void SetResult(string message, bool failed)
        {
            if (_resultLabel == null) return;
            _resultLabel.text = $"Last Result: {message}";
            _resultLabel.style.color = failed ? new Color(1f, 0.55f, 0.55f, 1f) : new Color(0.72f, 0.95f, 0.84f, 1f);
        }

        /// <summary>背景の奥行きを作る非操作の円形要素を作る。</summary>
        /// <param name="color">半透明の発光色。</param>
        /// <param name="left">root左端からの位置。</param>
        /// <param name="top">root上端からの位置。</param>
        /// <param name="size">正方形の一辺。</param>
        /// <returns>絶対配置した円形要素。</returns>
        private static VisualElement CreateGlow(Color color, float left, float top, float size)
        {
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.left = left;
            glow.style.top = top;
            glow.style.width = size;
            glow.style.height = size;
            glow.style.backgroundColor = color;
            SetRoundedCorners(glow, size * 0.5f);
            return glow;
        }

        /// <summary>件数の種類と値を2行で表示する均等幅tileを作る。</summary>
        /// <param name="elementName">testから取得する安定した名前。</param>
        /// <param name="title">件数の種類。</param>
        /// <param name="accent">値を識別する色。</param>
        /// <returns>初期値0の件数Label。</returns>
        private static Label CreateMetric(string elementName, string title, Color accent)
        {
            var metric = new Label($"{title}\n0") { name = elementName };
            metric.style.flexGrow = 1f;
            metric.style.flexBasis = 0f;
            metric.style.marginLeft = 4f;
            metric.style.marginRight = 4f;
            metric.style.paddingTop = 9f;
            metric.style.paddingBottom = 9f;
            metric.style.unityTextAlign = TextAnchor.MiddleCenter;
            metric.style.fontSize = 14f;
            metric.style.unityFontStyleAndWeight = FontStyle.Bold;
            metric.style.color = accent;
            metric.style.backgroundColor = new Color(0.035f, 0.065f, 0.11f, 1f);
            SetRoundedCorners(metric, 10f);
            return metric;
        }

        /// <summary>実callbackを持つ折返し可能な操作Buttonを作る。</summary>
        /// <param name="elementName">testから取得する安定した名前。</param>
        /// <param name="text">画面に表示する操作名。</param>
        /// <param name="clicked">click時に実行するsample操作。</param>
        /// <param name="background">操作の種類を区別する背景色。</param>
        /// <returns>操作行へ追加できるButton。</returns>
        private static Button CreateButton(string elementName, string text, Action clicked, Color background)
        {
            var button = new Button(clicked) { name = elementName, text = text };
            button.style.flexGrow = 1f;
            button.style.flexBasis = 135f;
            button.style.minHeight = 38f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.marginTop = 4f;
            button.style.marginBottom = 4f;
            button.style.fontSize = 13f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = new Color(0.96f, 0.98f, 1f, 1f);
            button.style.backgroundColor = background;
            SetBorder(button, new Color(0.45f, 0.68f, 0.78f, 0.55f), 1f);
            SetRoundedCorners(button, 9f);
            return button;
        }

        /// <summary>長いpathを1行省略表示できるLabelへ整える。</summary>
        /// <param name="label">表示styleを設定するLabel。</param>
        /// <param name="fontSize">画面上の文字size。</param>
        /// <param name="color">通常表示色。</param>
        private static void ConfigureSingleLine(Label label, float fontSize, Color color)
        {
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.flexShrink = 0f;
        }

        /// <summary>VisualElementの4辺へ同じborderを設定する。</summary>
        /// <param name="element">対象要素。</param>
        /// <param name="color">4辺の色。</param>
        /// <param name="width">4辺のpixel幅。</param>
        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
        }

        /// <summary>VisualElementの4角へ同じ丸みを設定する。</summary>
        /// <param name="element">対象要素。</param>
        /// <param name="radius">4角のpixel半径。</param>
        private static void SetRoundedCorners(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
