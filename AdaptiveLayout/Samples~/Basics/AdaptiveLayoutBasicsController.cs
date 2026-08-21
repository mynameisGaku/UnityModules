using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdaptiveLayout.Samples
{
    /// <summary>Builds a live safe-area visualization for Device Simulator.</summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(SafeAreaVisualElement))]
    public sealed class AdaptiveLayoutBasicsController : MonoBehaviour
    {
        public const string SafeContentElementName = "adaptive-layout-safe-content";
        public const string CardElementName = "adaptive-layout-card";
        public const string StatusElementName = "adaptive-layout-status";
        public const string ScreenElementName = "adaptive-layout-screen";
        public const string SafeAreaElementName = "adaptive-layout-safe-area";
        public const string InsetsElementName = "adaptive-layout-insets";

        private Label _status;
        private Label _screen;
        private Label _safeArea;
        private Label _insets;
        private SafeAreaVisualElement _safeAreaComponent;

        private void OnEnable()
        {
            _safeAreaComponent = GetComponent<SafeAreaVisualElement>();
            _safeAreaComponent.TargetElementName = SafeContentElementName;
            BuildUi(GetComponent<UIDocument>().rootVisualElement);
            UpdateMetrics();
        }

        private void Update()
        {
            UpdateMetrics();
        }

        private void BuildUi(VisualElement root)
        {
            root.Clear();
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            root.style.backgroundColor = new Color(0.035f, 0.055f, 0.085f, 1f);

            var backdrop = new VisualElement { name = "adaptive-layout-backdrop" };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0f;
            backdrop.style.top = 0f;
            backdrop.style.right = 0f;
            backdrop.style.bottom = 0f;
            backdrop.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f, 1f);
            root.Add(backdrop);

            var safeContent = new VisualElement { name = SafeContentElementName };
            safeContent.style.alignItems = Align.Center;
            safeContent.style.justifyContent = Justify.Center;
            safeContent.style.borderLeftWidth = 3f;
            safeContent.style.borderTopWidth = 3f;
            safeContent.style.borderRightWidth = 3f;
            safeContent.style.borderBottomWidth = 3f;
            var safeColor = new Color(0.22f, 0.90f, 0.58f, 0.95f);
            safeContent.style.borderLeftColor = safeColor;
            safeContent.style.borderTopColor = safeColor;
            safeContent.style.borderRightColor = safeColor;
            safeContent.style.borderBottomColor = safeColor;
            safeContent.style.paddingLeft = 24f;
            safeContent.style.paddingTop = 24f;
            safeContent.style.paddingRight = 24f;
            safeContent.style.paddingBottom = 24f;
            root.Add(safeContent);

            var card = new VisualElement { name = CardElementName };
            card.style.width = new Length(82f, LengthUnit.Percent);
            card.style.maxWidth = 760f;
            card.style.paddingLeft = 28f;
            card.style.paddingTop = 24f;
            card.style.paddingRight = 28f;
            card.style.paddingBottom = 24f;
            card.style.backgroundColor = new Color(0.09f, 0.14f, 0.22f, 0.98f);
            card.style.borderTopLeftRadius = 18f;
            card.style.borderTopRightRadius = 18f;
            card.style.borderBottomLeftRadius = 18f;
            card.style.borderBottomRightRadius = 18f;
            safeContent.Add(card);

            var title = CreateLabel("Adaptive Layout", 30, FontStyle.Bold, new Color(0.93f, 0.98f, 1f, 1f));
            card.Add(title);

            var description = CreateLabel(
                "Open Device Simulator, select a notched device, and rotate it. The green content boundary follows Screen.safeArea.",
                15,
                FontStyle.Normal,
                new Color(0.72f, 0.80f, 0.90f, 1f));
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 8f;
            description.style.marginBottom = 18f;
            card.Add(description);

            _status = CreateMetric(StatusElementName);
            _screen = CreateMetric(ScreenElementName);
            _safeArea = CreateMetric(SafeAreaElementName);
            _insets = CreateMetric(InsetsElementName);
            card.Add(_status);
            card.Add(_screen);
            card.Add(_safeArea);
            card.Add(_insets);

            var footer = CreateLabel(
                "Green border: safe content. Dark background: full viewport.",
                13,
                FontStyle.Italic,
                new Color(0.48f, 0.88f, 0.70f, 1f));
            footer.style.marginTop = 16f;
            card.Add(footer);
        }

        private void UpdateMetrics()
        {
            if (_status == null)
            {
                return;
            }

            var snapshot = _safeAreaComponent.Current;
            var culture = CultureInfo.InvariantCulture;
            var available = snapshot.ScreenSize.x > 0 && snapshot.ScreenSize.y > 0;
            _status.text = available && snapshot.IsFullViewport ? "Status: full viewport" : "Status: safe-area insets active";
            _screen.text = $"Screen: {snapshot.ScreenSize.x} x {snapshot.ScreenSize.y}";
            _safeArea.text = string.Format(
                culture,
                "Safe area: x {0:0}, y {1:0}, width {2:0}, height {3:0}",
                snapshot.SafeArea.x,
                snapshot.SafeArea.y,
                snapshot.SafeArea.width,
                snapshot.SafeArea.height);
            _insets.text = string.Format(
                culture,
                "Insets: left {0:0}, top {1:0}, right {2:0}, bottom {3:0}",
                snapshot.LeftInset,
                snapshot.TopInset,
                snapshot.RightInset,
                snapshot.BottomInset);
        }

        private static Label CreateMetric(string name)
        {
            var label = CreateLabel(string.Empty, 15, FontStyle.Normal, new Color(0.84f, 0.90f, 0.97f, 1f));
            label.name = name;
            label.style.marginTop = 5f;
            return label;
        }

        private static Label CreateLabel(string text, int size, FontStyle style, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.color = color;
            return label;
        }
    }
}
