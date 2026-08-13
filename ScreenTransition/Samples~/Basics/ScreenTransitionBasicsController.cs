using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScreenTransition.Samples
{
    /// <summary>明るい背景、操作ボタン、進捗表示を組み立て、画面遷移を実行する。</summary>
    [AddComponentMenu("StudioGaku/Screen Transition Basics Controller")]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(ScreenTransitionController))]
    public sealed class ScreenTransitionBasicsController : MonoBehaviour
    {
        private static readonly Color TransitionColor = new Color(0.025f, 0.055f, 0.13f, 1f);

        private ScreenTransitionController _transition;
        private VisualElement _sampleRoot;
        private Label _statusLabel;
        private Label _resultLabel;
        private Button _coverButton;
        private Button _revealButton;
        private Button _demoButton;
        private bool _demoRunning;

        /// <summary>外部の視覚ゲートがサンプルの構築完了を確認する要素名。</summary>
        public const string ReadyElementName = "screen-transition-basics-ready";

        /// <summary>画面読取の証拠点として使う、中央の単色背景要素名。</summary>
        public const string EvidenceElementName = "screen-transition-basics-evidence";

        /// <summary>外部の視覚ゲートがCover操作と有効状態を確認するボタン名。</summary>
        public const string CoverButtonElementName = "screen-transition-basics-cover";

        /// <summary>外部の視覚ゲートがReveal操作と有効状態を確認するボタン名。</summary>
        public const string RevealButtonElementName = "screen-transition-basics-reveal";

        /// <summary>外部の視覚ゲートが自動デモ操作と有効状態を確認するボタン名。</summary>
        public const string DemoButtonElementName = "screen-transition-basics-demo";

        /// <summary>同じGameObjectの所有物を取得し、実panelへサンプル画面を追加する。</summary>
        private void OnEnable()
        {
            _transition = GetComponent<ScreenTransitionController>();
            _transition.StatusChanged += HandleStatusChanged;
            _transition.Finished += HandleFinished;
            BuildView(GetComponent<UIDocument>().rootVisualElement);
            HandleStatusChanged(_transition.Status);
        }

        /// <summary>通知購読とサンプル画面を外し、遷移surfaceの寿命には触れない。</summary>
        private void OnDisable()
        {
            if (_transition != null)
            {
                _transition.StatusChanged -= HandleStatusChanged;
                _transition.Finished -= HandleFinished;
            }

            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
        }

        /// <summary>背景、説明、状態、操作ボタンをコードだけで構築し、既存overlayより背面へ置く。</summary>
        /// <param name="documentRoot">UIDocumentが所有する実panelのroot。</param>
        private void BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                enabled = false;
                Debug.LogError("[Screen Transition Basics] UIDocumentのrootを取得できません。", this);
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
            _sampleRoot.style.backgroundColor = new Color(0.88f, 0.94f, 1f, 1f);

            var evidence = new VisualElement { name = EvidenceElementName, pickingMode = PickingMode.Ignore };
            evidence.style.position = Position.Absolute;
            evidence.style.left = 0f;
            evidence.style.top = 0f;
            evidence.style.right = 0f;
            evidence.style.bottom = 0f;
            evidence.style.backgroundColor = new Color(0.88f, 0.94f, 1f, 1f);
            _sampleRoot.Add(evidence);

            var card = new VisualElement { name = "screen-transition-basics-card" };
            card.style.width = new Length(78f, LengthUnit.Percent);
            card.style.maxWidth = 760f;
            card.style.minHeight = 420f;
            card.style.paddingLeft = 36f;
            card.style.paddingRight = 36f;
            card.style.paddingTop = 30f;
            card.style.paddingBottom = 30f;
            card.style.borderTopLeftRadius = 24f;
            card.style.borderTopRightRadius = 24f;
            card.style.borderBottomLeftRadius = 24f;
            card.style.borderBottomRightRadius = 24f;
            card.style.backgroundColor = new Color(0.11f, 0.18f, 0.31f, 0.97f);
            card.style.color = new Color(0.94f, 0.97f, 1f, 1f);
            card.style.justifyContent = Justify.Center;
            _sampleRoot.Add(card);

            var title = new Label("Screen Transition Basics");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12f;
            card.Add(title);

            var description = new Label("Coverで画面を覆い、Revealで背景を戻します。\n遷移はTime.timeScaleに依存しません。");
            description.style.fontSize = 18f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 24f;
            card.Add(description);

            _statusLabel = new Label();
            _statusLabel.name = "screen-transition-basics-status";
            _statusLabel.style.fontSize = 17f;
            _statusLabel.style.marginBottom = 8f;
            card.Add(_statusLabel);

            _resultLabel = new Label("準備完了。Cover、Reveal、Auto Demoを操作してください。");
            _resultLabel.name = "screen-transition-basics-result";
            _resultLabel.style.fontSize = 15f;
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.minHeight = 44f;
            _resultLabel.style.marginBottom = 20f;
            card.Add(_resultLabel);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.SpaceBetween;
            card.Add(buttons);

            _coverButton = CreateButton(CoverButtonElementName, "Cover", RunCover);
            _revealButton = CreateButton(RevealButtonElementName, "Reveal", RunReveal);
            _demoButton = CreateButton(DemoButtonElementName, "Auto Demo", RunDemo);
            buttons.Add(_coverButton);
            buttons.Add(_revealButton);
            buttons.Add(_demoButton);

            documentRoot.Insert(0, _sampleRoot);
        }

        /// <summary>視認しやすい共通寸法の操作ボタンを作る。</summary>
        /// <param name="name">視覚ゲートが要素を特定する安定した名前。</param>
        /// <param name="text">ボタンに表示する操作名。</param>
        /// <param name="clicked">クリック時に実行する処理。</param>
        /// <returns>サンプル画面へ追加できるボタン。</returns>
        private static Button CreateButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text };
            button.style.width = new Length(31f, LengthUnit.Percent);
            button.style.height = 54f;
            button.style.fontSize = 17f;
            return button;
        }

        /// <summary>青黒色で0.6秒かけて画面を覆う。</summary>
        private async void RunCover()
        {
            await ExecuteAsync(ScreenTransitionRequest.Cover(TransitionColor, 0.6f, ScreenTransitionEasing.EaseInOut));
        }

        /// <summary>青黒色から0.6秒かけて背景を表示する。</summary>
        private async void RunReveal()
        {
            await ExecuteAsync(ScreenTransitionRequest.Reveal(TransitionColor, 0.6f, ScreenTransitionEasing.EaseInOut));
        }

        /// <summary>CoverとRevealを続けて実行し、1操作で動きを確認できるようにする。</summary>
        private async void RunDemo()
        {
            if (_demoRunning) return;

            _demoRunning = true;
            RefreshButtonState();
            try
            {
                var cover = await _transition.CoverAsync(TransitionColor, 0.8f, ScreenTransitionEasing.EaseInOut);
                ShowResult("Auto Demo / Cover", cover);
                if (!cover.IsSuccess) return;

                var reveal = await _transition.RevealAsync(TransitionColor, 0.8f, ScreenTransitionEasing.EaseInOut);
                ShowResult("Auto Demo / Reveal", reveal);
            }
            catch (Exception exception)
            {
                _resultLabel.text = $"Auto Demo / 予期しない失敗: {exception.Message}";
                Debug.LogException(exception, this);
            }
            finally
            {
                _demoRunning = false;
                RefreshButtonState();
            }
        }

        /// <summary>1件の要求を実行し、例外をGame Viewへ残す。</summary>
        /// <param name="request">操作、色、時間、補間方法。</param>
        private async Awaitable ExecuteAsync(ScreenTransitionRequest request)
        {
            try
            {
                var result = await _transition.ExecuteAsync(request);
                ShowResult(request.Operation.ToString(), result);
            }
            catch (Exception exception)
            {
                _resultLabel.text = $"{request.Operation} / 予期しない失敗: {exception.Message}";
                Debug.LogException(exception, this);
            }
            finally
            {
                RefreshButtonState();
            }
        }

        /// <summary>状態通知を段階、進捗、不透明度として表示する。</summary>
        /// <param name="status">現在の画面遷移状態。</param>
        private void HandleStatusChanged(ScreenTransitionStatus status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = $"段階: {status.Phase}   進捗: {status.Progress:P0}   不透明度: {status.Opacity:P0}";
            }

            RefreshButtonState();
        }

        /// <summary>完了通知をConsoleへ残し、視覚ゲートから終端を追跡できるようにする。</summary>
        /// <param name="result">完了した画面遷移の結果。</param>
        private void HandleFinished(ScreenTransitionResult result)
        {
            var outcome = result.IsSuccess ? "成功" : $"失敗: {result.Error}";
            Debug.Log($"[Screen Transition Basics] {result.Request.Operation} / {outcome} / {result.Message}", this);
        }

        /// <summary>結果型の成功可否と説明を画面へ表示する。</summary>
        /// <param name="label">手動操作または自動デモ内の段階名。</param>
        /// <param name="result">Controllerから返った完了結果。</param>
        private void ShowResult(string label, ScreenTransitionResult result)
        {
            var outcome = result.IsSuccess ? "成功" : $"失敗: {result.Error}";
            _resultLabel.text = $"{label} / {outcome}\n{result.Message}";
        }

        /// <summary>処理中の二重操作を避け、自動デモの入口だけを明示的に閉じる。</summary>
        private void RefreshButtonState()
        {
            if (_transition == null) return;

            var enabled = !_transition.IsBusy && !_demoRunning;
            _coverButton?.SetEnabled(enabled);
            _revealButton?.SetEnabled(enabled);
            _demoButton?.SetEnabled(enabled);
        }
    }
}
