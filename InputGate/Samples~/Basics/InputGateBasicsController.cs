using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace InputGate.Samples
{
    /// <summary>Gameplay mapだけをleaseで停止し、UI mapと操作画面が継続することを表示する。</summary>
    [AddComponentMenu("StudioGaku/Input Gate Basics Controller")]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(InputGateController))]
    public sealed class InputGateBasicsController : MonoBehaviour
    {
        /// <summary>外部の視覚gateがsample構築完了を確認する要素名。</summary>
        public const string ReadyElementName = "input-gate-basics-ready";

        /// <summary>状態と操作を囲むcard名。</summary>
        public const string CardElementName = "input-gate-basics-card";

        /// <summary>5つの操作Buttonを表示順に含む行名。</summary>
        public const string ButtonRowElementName = "input-gate-basics-buttons";

        /// <summary>sample名を表示するTitle Label名。</summary>
        public const string TitleElementName = "input-gate-basics-title";

        /// <summary>停止対象と対象外を説明するLabel名。</summary>
        public const string DescriptionElementName = "input-gate-basics-description";

        /// <summary>Controller状態を表示するLabel名。</summary>
        public const string StatusElementName = "input-gate-basics-status";

        /// <summary>直近の操作段階を表示するLabel名。</summary>
        public const string StageElementName = "input-gate-basics-stage";

        /// <summary>Gameplay Actionのperformed回数を表示するLabel名。</summary>
        public const string GameplayCountElementName = "input-gate-basics-gameplay-count";

        /// <summary>停止対象外UI Actionのperformed回数を表示するLabel名。</summary>
        public const string UiCountElementName = "input-gate-basics-ui-count";

        /// <summary>1件のleaseを取得するButton名。</summary>
        public const string AcquireButtonElementName = "input-gate-basics-acquire";

        /// <summary>2件のleaseを追加するButton名。</summary>
        public const string NestedButtonElementName = "input-gate-basics-nested";

        /// <summary>最後に取得した1件を解放するButton名。</summary>
        public const string ReleaseOneButtonElementName = "input-gate-basics-release-one";

        /// <summary>sample所有leaseを全て解放するButton名。</summary>
        public const string ReleaseAllButtonElementName = "input-gate-basics-release-all";

        /// <summary>2種類のAction counterを0へ戻すButton名。</summary>
        public const string ResetButtonElementName = "input-gate-basics-reset";

        private readonly List<InputGateLease> _ownedLeases = new List<InputGateLease>();
        private InputGateController _inputGate;
        private InputAction _gameplayPulse;
        private InputAction _uiPulse;
        private VisualElement _sampleRoot;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _statusLabel;
        private Label _stageLabel;
        private Label _gameplayCountLabel;
        private Label _uiCountLabel;
        private Button _acquireButton;
        private Button _nestedButton;
        private Button _releaseOneButton;
        private Button _releaseAllButton;
        private Button _resetButton;
        private int _gameplayCount;
        private int _uiCount;
        private bool _uiWasEnabled;

        /// <summary>Gameplay mapのPulse Actionがperformedになった回数。</summary>
        public int GameplayCount => _gameplayCount;

        /// <summary>停止対象外UI mapのPulse Actionがperformedになった回数。</summary>
        public int UiCount => _uiCount;

        /// <summary>sampleが現在所有する有効なlease数。</summary>
        public int OwnedLeaseCount => _ownedLeases.Count;

        /// <summary>画面へ表示している直近の操作段階。</summary>
        public string StageText => _stageLabel?.text ?? string.Empty;

        /// <summary>同じGameObjectの実行中ActionとControllerを取得し、実panelへsample画面を追加する。</summary>
        private void OnEnable()
        {
            var playerInput = GetComponent<PlayerInput>();
            _inputGate = GetComponent<InputGateController>();
            var actions = playerInput.actions;
            _gameplayPulse = actions?.FindAction("Gameplay/Pulse", false);
            _uiPulse = actions?.FindAction("UI/Pulse", false);
            if (_gameplayPulse == null || _uiPulse == null)
            {
                Debug.LogError("[Input Gate Basics] Gameplay/PulseまたはUI/Pulseを解決できません。", this);
                enabled = false;
                return;
            }

            _uiWasEnabled = _uiPulse.enabled;
            if (!_uiWasEnabled) _uiPulse.actionMap.Enable();
            _gameplayPulse.performed += HandleGameplayPulse;
            _uiPulse.performed += HandleUiPulse;
            _inputGate.StatusChanged += HandleStatusChanged;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            SetStage("Ready / Space=Gameplay, Enter=UI");
            RefreshCounters();
            HandleStatusChanged(_inputGate.Status);
        }

        /// <summary>Action購読、所有lease、UI map baseline、表示要素を終了する。</summary>
        private void OnDisable()
        {
            if (_inputGate != null) _inputGate.StatusChanged -= HandleStatusChanged;
            if (_gameplayPulse != null) _gameplayPulse.performed -= HandleGameplayPulse;
            if (_uiPulse != null) _uiPulse.performed -= HandleUiPulse;
            DisposeOwnedLeases();
            if (!_uiWasEnabled && _uiPulse?.actionMap != null) _uiPulse.actionMap.Disable();
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
        }

        /// <summary>Gameplay Actionのperformedを1件として画面へ反映する。</summary>
        /// <param name="context">Input System callback中だけ有効なAction情報。</param>
        private void HandleGameplayPulse(InputAction.CallbackContext context)
        {
            _gameplayCount++;
            RefreshCounters();
            SetStage("Gameplay Pulse accepted");
        }

        /// <summary>停止対象外UI Actionのperformedを1件として画面へ反映する。</summary>
        /// <param name="context">Input System callback中だけ有効なAction情報。</param>
        private void HandleUiPulse(InputAction.CallbackContext context)
        {
            _uiCount++;
            RefreshCounters();
            SetStage("UI Pulse accepted while Gameplay may be gated");
        }

        /// <summary>背景、状態、2つのAction counter、5つの操作Buttonをコードだけで構築する。</summary>
        /// <param name="documentRoot">UIDocumentが所有する実panelのroot。</param>
        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                Debug.LogError("[Input Gate Basics] UIDocumentのrootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.025f, 0.045f, 0.075f, 1f);

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
            _card.style.backgroundColor = new Color(0.075f, 0.12f, 0.18f, 0.99f);
            _card.style.color = new Color(0.94f, 0.98f, 1f, 1f);
            _card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(_card);

            _title = new Label("Input Gate Basics") { name = TitleElementName };
            _title.style.fontSize = 32f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 6f;
            _card.Add(_title);

            _description = new Label("Gameplay mapだけをleaseで停止します。UI mapとこの操作画面は動き続けます。")
            {
                name = DescriptionElementName,
            };
            _description.style.fontSize = 16f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginBottom = 14f;
            _card.Add(_description);

            _statusLabel = new Label { name = StatusElementName };
            _statusLabel.style.fontSize = 15f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8f;
            _card.Add(_statusLabel);

            _stageLabel = new Label { name = StageElementName };
            _stageLabel.style.fontSize = 16f;
            _stageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stageLabel.style.color = new Color(0.35f, 0.88f, 0.75f, 1f);
            _stageLabel.style.marginBottom = 12f;
            _card.Add(_stageLabel);

            var counters = new VisualElement();
            counters.style.flexDirection = FlexDirection.Row;
            counters.style.flexWrap = Wrap.Wrap;
            counters.style.justifyContent = Justify.Center;
            counters.style.marginBottom = 12f;
            _card.Add(counters);
            _gameplayCountLabel = CreateCounter(GameplayCountElementName, "Gameplay / Space / South", new Color(1f, 0.58f, 0.28f, 1f));
            _uiCountLabel = CreateCounter(UiCountElementName, "UI / Enter / North", new Color(0.35f, 0.88f, 0.75f, 1f));
            counters.Add(_gameplayCountLabel);
            counters.Add(_uiCountLabel);

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);
            _acquireButton = CreateButton(AcquireButtonElementName, "Acquire Gate", AcquireOne);
            _nestedButton = CreateButton(NestedButtonElementName, "Acquire Nested x2", AcquireNested);
            _releaseOneButton = CreateButton(ReleaseOneButtonElementName, "Release One", ReleaseOne);
            _releaseAllButton = CreateButton(ReleaseAllButtonElementName, "Release All", ReleaseAll);
            _resetButton = CreateButton(ResetButtonElementName, "Reset Counters", ResetCounters);
            _buttonRow.Add(_acquireButton);
            _buttonRow.Add(_nestedButton);
            _buttonRow.Add(_releaseOneButton);
            _buttonRow.Add(_releaseAllButton);
            _buttonRow.Add(_resetButton);

            _sampleRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            documentRoot.Add(_sampleRoot);
        }

        /// <summary>Action名、キー、実行回数を表示するcounter cardを作る。</summary>
        /// <param name="name">安定した要素名。</param>
        /// <param name="title">mapとbindingを示す文字列。</param>
        /// <param name="accent">counterの強調色。</param>
        /// <returns>作成したLabel。</returns>
        private static Label CreateCounter(string name, string title, Color accent)
        {
            var label = new Label(title + "\nCount: 0") { name = name };
            label.style.flexBasis = 240f;
            label.style.flexGrow = 1f;
            label.style.minWidth = 210f;
            label.style.maxWidth = 420f;
            label.style.marginLeft = 5f;
            label.style.marginRight = 5f;
            label.style.marginTop = 4f;
            label.style.marginBottom = 4f;
            label.style.paddingLeft = 16f;
            label.style.paddingRight = 16f;
            label.style.paddingTop = 12f;
            label.style.paddingBottom = 12f;
            label.style.borderTopLeftRadius = 12f;
            label.style.borderTopRightRadius = 12f;
            label.style.borderBottomLeftRadius = 12f;
            label.style.borderBottomRightRadius = 12f;
            label.style.backgroundColor = new Color(0.035f, 0.065f, 0.105f, 1f);
            label.style.color = accent;
            label.style.fontSize = 15f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        /// <summary>操作callbackを持つresponsive Buttonを作る。</summary>
        /// <param name="name">安定した要素名。</param>
        /// <param name="text">画面へ表示する操作名。</param>
        /// <param name="clicked">押下時に実行する操作。</param>
        /// <returns>作成したButton。</returns>
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

        /// <summary>1件のleaseを取得してsample所有一覧へ追加する。</summary>
        private void AcquireOne()
        {
            if (_inputGate.TryAcquire(out var lease, out var error))
            {
                _ownedLeases.Add(lease);
                SetStage("Acquire Gate / Gameplay blocked");
            }
            else
            {
                SetStage("Acquire failed / " + error);
            }

            RefreshButtons();
        }

        /// <summary>2件のleaseを連続取得し、入れ子要求数を表示する。</summary>
        private void AcquireNested()
        {
            AcquireOne();
            AcquireOne();
            SetStage("Acquire Nested / owned=" + _ownedLeases.Count);
        }

        /// <summary>sampleが最後に取得した1件だけを解放する。</summary>
        private void ReleaseOne()
        {
            RemoveInactiveLeases();
            if (_ownedLeases.Count == 0)
            {
                SetStage("Release One / no owned lease");
                return;
            }

            var index = _ownedLeases.Count - 1;
            var lease = _ownedLeases[index];
            _ownedLeases.RemoveAt(index);
            lease.Dispose();
            SetStage("Release One / owned=" + _ownedLeases.Count);
            RefreshButtons();
        }

        /// <summary>sampleが所有するleaseだけを逆順に全て解放する。</summary>
        private void ReleaseAll()
        {
            var count = _ownedLeases.Count;
            DisposeOwnedLeases();
            SetStage("Release All / released=" + count);
            RefreshButtons();
        }

        /// <summary>GameplayとUIのperformed回数だけを0へ戻す。</summary>
        private void ResetCounters()
        {
            _gameplayCount = 0;
            _uiCount = 0;
            RefreshCounters();
            SetStage("Counters reset");
        }

        /// <summary>Controller snapshotを状態、Map数、lease数、errorとして表示する。</summary>
        /// <param name="status">Controllerが通知した不変snapshot。</param>
        private void HandleStatusChanged(InputGateStatus status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = $"Status: Ready={status.IsReady}  Blocking={status.IsBlocking}  Maps={status.ControlledMapCount}  Leases={status.ActiveLeaseCount}  Error={status.Error}";
            }

            RemoveInactiveLeases();
            RefreshButtons();
        }

        /// <summary>2種類のAction performed回数を対応するLabelへ反映する。</summary>
        private void RefreshCounters()
        {
            if (_gameplayCountLabel != null) _gameplayCountLabel.text = "Gameplay / Space / South\nCount: " + _gameplayCount;
            if (_uiCountLabel != null) _uiCountLabel.text = "UI / Enter / North\nCount: " + _uiCount;
        }

        /// <summary>現在のController状態とsample所有数に合わせてButton有効状態を更新する。</summary>
        private void RefreshButtons()
        {
            if (_inputGate == null) return;
            var ready = _inputGate.Status.IsReady;
            _acquireButton?.SetEnabled(ready);
            _nestedButton?.SetEnabled(ready);
            _releaseOneButton?.SetEnabled(_ownedLeases.Count > 0);
            _releaseAllButton?.SetEnabled(_ownedLeases.Count > 0);
            _resetButton?.SetEnabled(true);
        }

        /// <summary>終了したstale leaseをsample所有一覧から除く。</summary>
        private void RemoveInactiveLeases()
        {
            for (var index = _ownedLeases.Count - 1; index >= 0; index--)
            {
                if (_ownedLeases[index] == null || !_ownedLeases[index].IsActive) _ownedLeases.RemoveAt(index);
            }
        }

        /// <summary>sample所有leaseを逆順に全て解放する。</summary>
        private void DisposeOwnedLeases()
        {
            var leases = _ownedLeases.ToArray();
            _ownedLeases.Clear();
            for (var index = leases.Length - 1; index >= 0; index--) leases[index]?.Dispose();
        }

        /// <summary>直近の操作またはAction受付段階を表示する。</summary>
        /// <param name="stage">画面とtestが共有する安定した段階文字列。</param>
        private void SetStage(string stage)
        {
            if (_stageLabel != null) _stageLabel.text = stage;
        }

        /// <summary>実panel寸法へ合わせ、wideとnarrowの余白、文字、Button幅を切り替える。</summary>
        /// <param name="evt">変更後の実panel geometry。</param>
        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            var compact = evt.newRect.width < 720f || evt.newRect.height < 500f;
            _card.style.paddingLeft = compact ? 12f : 32f;
            _card.style.paddingRight = compact ? 12f : 32f;
            _card.style.paddingTop = compact ? 8f : 22f;
            _card.style.paddingBottom = compact ? 8f : 22f;
            _title.style.fontSize = compact ? 23f : 32f;
            _title.style.marginBottom = compact ? 2f : 6f;
            _description.style.fontSize = compact ? 11f : 16f;
            _description.style.marginBottom = compact ? 5f : 14f;
            _statusLabel.style.fontSize = compact ? 10f : 15f;
            _statusLabel.style.marginBottom = compact ? 3f : 8f;
            _stageLabel.style.fontSize = compact ? 11f : 16f;
            _stageLabel.style.marginBottom = compact ? 4f : 12f;
            _gameplayCountLabel.style.fontSize = compact ? 11f : 15f;
            _uiCountLabel.style.fontSize = compact ? 11f : 15f;
            _gameplayCountLabel.style.paddingTop = compact ? 5f : 12f;
            _gameplayCountLabel.style.paddingBottom = compact ? 5f : 12f;
            _uiCountLabel.style.paddingTop = compact ? 5f : 12f;
            _uiCountLabel.style.paddingBottom = compact ? 5f : 12f;
            var buttons = new[] { _acquireButton, _nestedButton, _releaseOneButton, _releaseAllButton, _resetButton };
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].style.height = compact ? 29f : 42f;
                buttons[i].style.fontSize = compact ? 10f : 14f;
                buttons[i].style.flexBasis = compact ? 150f : 116f;
            }
        }
    }
}
