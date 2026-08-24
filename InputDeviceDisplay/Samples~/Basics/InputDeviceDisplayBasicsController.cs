using UnityEngine;
using UnityEngine.UIElements;

namespace InputDeviceDisplay.Samples
{
    /// <summary>実入力で切り替わる表示familyと元deviceを、外部assetなしのUIへ表示する。</summary>
    [AddComponentMenu("StudioGaku/Input Device Display Basics Controller")]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(InputDeviceDisplayController))]
    public sealed class InputDeviceDisplayBasicsController : MonoBehaviour
    {
        /// <summary>sample構築完了を外部testから確認するroot要素名。</summary>
        public const string ReadyElementName = "input-device-display-basics-ready";

        /// <summary>現在の表示familyを示す大見出しの要素名。</summary>
        public const string StyleElementName = "input-device-display-basics-style";

        /// <summary>追跡元deviceの情報を表示する要素名。</summary>
        public const string DeviceElementName = "input-device-display-basics-device";

        /// <summary>監視状態とfallback利用有無を表示する要素名。</summary>
        public const string StatusElementName = "input-device-display-basics-status";

        private InputDeviceDisplayController _controller;
        private VisualElement _sampleRoot;
        private Label _styleLabel;
        private Label _deviceLabel;
        private Label _statusLabel;

        /// <summary>現在画面へ表示しているfamily名。</summary>
        public string DisplayedStyle => _styleLabel?.text ?? string.Empty;

        /// <summary>同じGameObjectのControllerへ購読し、UIDocument上へsample画面を構築する。</summary>
        private void OnEnable()
        {
            _controller = GetComponent<InputDeviceDisplayController>();
            if (!BuildView(GetComponent<UIDocument>().rootVisualElement)) return;
            _controller.StateChanged += HandleStateChanged;
            HandleStateChanged(_controller.State);
        }

        /// <summary>状態購読とsampleが追加した表示だけを終了する。</summary>
        private void OnDisable()
        {
            if (_controller != null) _controller.StateChanged -= HandleStateChanged;
            _sampleRoot?.RemoveFromHierarchy();
            _sampleRoot = null;
            _styleLabel = null;
            _deviceLabel = null;
            _statusLabel = null;
        }

        /// <summary>背景、説明、現在family、device、監視状態をコードだけで構築する。</summary>
        /// <param name="documentRoot">UIDocumentが所有する実panelのroot。</param>
        /// <returns>表示を構築できた場合はtrue。</returns>
        private bool BuildView(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                Debug.LogError("[Input Device Display Basics] UIDocumentのrootを取得できません。", this);
                enabled = false;
                return false;
            }

            _sampleRoot = new VisualElement { name = ReadyElementName, pickingMode = PickingMode.Ignore };
            _sampleRoot.style.position = Position.Absolute;
            _sampleRoot.style.left = 0f;
            _sampleRoot.style.top = 0f;
            _sampleRoot.style.right = 0f;
            _sampleRoot.style.bottom = 0f;
            _sampleRoot.style.alignItems = Align.Center;
            _sampleRoot.style.justifyContent = Justify.Center;
            _sampleRoot.style.backgroundColor = new Color(0.018f, 0.035f, 0.065f, 1f);
            documentRoot.Add(_sampleRoot);

            var card = new VisualElement();
            card.style.width = new Length(86f, LengthUnit.Percent);
            card.style.maxWidth = 860f;
            card.style.paddingLeft = 34f;
            card.style.paddingRight = 34f;
            card.style.paddingTop = 28f;
            card.style.paddingBottom = 30f;
            card.style.borderTopLeftRadius = 24f;
            card.style.borderTopRightRadius = 24f;
            card.style.borderBottomLeftRadius = 24f;
            card.style.borderBottomRightRadius = 24f;
            card.style.backgroundColor = new Color(0.065f, 0.105f, 0.17f, 0.985f);
            card.style.color = new Color(0.94f, 0.975f, 1f, 1f);
            _sampleRoot.Add(card);

            var eyebrow = new Label("LIVE INPUT FAMILY");
            eyebrow.style.fontSize = 13f;
            eyebrow.style.letterSpacing = 2.2f;
            eyebrow.style.color = new Color(0.44f, 0.78f, 1f, 1f);
            eyebrow.style.marginBottom = 7f;
            card.Add(eyebrow);

            var title = new Label("Input Device Display");
            title.style.fontSize = 31f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8f;
            card.Add(title);

            var description = new Label(
                "Keyboard、Mouse、Gamepad、Touchを操作してください。接続だけでは表示を切り替えません。");
            description.style.fontSize = 15f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = new Color(0.74f, 0.82f, 0.9f, 1f);
            description.style.marginBottom = 24f;
            card.Add(description);

            _styleLabel = new Label { name = StyleElementName };
            _styleLabel.style.fontSize = 36f;
            _styleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _styleLabel.style.paddingLeft = 20f;
            _styleLabel.style.paddingRight = 20f;
            _styleLabel.style.paddingTop = 15f;
            _styleLabel.style.paddingBottom = 17f;
            _styleLabel.style.borderTopLeftRadius = 14f;
            _styleLabel.style.borderTopRightRadius = 14f;
            _styleLabel.style.borderBottomLeftRadius = 14f;
            _styleLabel.style.borderBottomRightRadius = 14f;
            _styleLabel.style.marginBottom = 16f;
            card.Add(_styleLabel);

            _deviceLabel = new Label { name = DeviceElementName };
            _deviceLabel.style.fontSize = 15f;
            _deviceLabel.style.whiteSpace = WhiteSpace.Normal;
            _deviceLabel.style.marginBottom = 7f;
            card.Add(_deviceLabel);

            _statusLabel = new Label { name = StatusElementName };
            _statusLabel.style.fontSize = 14f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.color = new Color(0.63f, 0.72f, 0.82f, 1f);
            card.Add(_statusLabel);
            return true;
        }

        /// <summary>最新stateをfamily色、device情報、監視状態へ反映する。</summary>
        /// <param name="state">Controllerが公開した不変スナップショット。</param>
        private void HandleStateChanged(InputDeviceDisplayState state)
        {
            if (_styleLabel == null) return;
            _styleLabel.text = state.Style.ToString();
            _styleLabel.style.backgroundColor = GetStyleColor(state.Style);
            _deviceLabel.text = state.HasDeviceActivity
                ? $"Device #{state.DeviceId}  •  Layout {state.LayoutName}"
                : "Fallback display  •  Waiting for input activity";
            _statusLabel.text = state.IsReady
                ? "Monitoring Input System events without consuming input"
                : $"Unavailable  •  {state.Error}";
        }

        /// <summary>familyごとに区別しやすい暗色背景を返す。</summary>
        /// <param name="style">現在の表示family。</param>
        /// <returns>family cardへ使用する背景色。</returns>
        private static Color GetStyleColor(InputDeviceDisplayStyle style)
        {
            switch (style)
            {
                case InputDeviceDisplayStyle.KeyboardMouse: return new Color(0.05f, 0.34f, 0.46f, 1f);
                case InputDeviceDisplayStyle.XboxStyleGamepad: return new Color(0.12f, 0.38f, 0.2f, 1f);
                case InputDeviceDisplayStyle.PlayStationStyleGamepad: return new Color(0.13f, 0.25f, 0.52f, 1f);
                case InputDeviceDisplayStyle.SwitchStyleGamepad: return new Color(0.48f, 0.15f, 0.18f, 1f);
                case InputDeviceDisplayStyle.GenericGamepad: return new Color(0.38f, 0.29f, 0.11f, 1f);
                case InputDeviceDisplayStyle.Touch: return new Color(0.34f, 0.18f, 0.48f, 1f);
                default: return new Color(0.2f, 0.24f, 0.3f, 1f);
            }
        }
    }
}
