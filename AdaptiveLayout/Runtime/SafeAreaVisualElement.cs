using UnityEngine;
using UnityEngine.UIElements;

namespace AdaptiveLayout
{
    /// <summary>
    /// Constrains a UI Toolkit element to selected safe-area edges.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class SafeAreaVisualElement : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private string _targetElementName = string.Empty;
        [SerializeField] private SafeAreaEdges _edges = SafeAreaEdges.All;
        [SerializeField] private bool _restoreOnDisable = true;

        private VisualElement _target;
        private StyleEnum<Position> _originalPosition;
        private StyleLength _originalLeft;
        private StyleLength _originalTop;
        private StyleLength _originalRight;
        private StyleLength _originalBottom;
        private bool _hasOriginalStyle;
        private bool _hasAppliedSnapshot;
        private SafeAreaSnapshot _appliedSnapshot;
        private Rect _appliedParentBounds;

        internal ISafeAreaSource Source { get; set; } = ScreenSafeAreaSource.Instance;

        /// <summary>Gets the UI document that owns the target element.</summary>
        public UIDocument Document => _document;

        /// <summary>Gets or sets the target element name. An empty value selects the document root.</summary>
        public string TargetElementName
        {
            get => _targetElementName;
            set
            {
                RestoreOriginalStyle();
                _targetElementName = value ?? string.Empty;
                _target = null;
                _hasOriginalStyle = false;
                _hasAppliedSnapshot = false;
            }
        }

        /// <summary>Gets or sets the safe-area edges applied to the target.</summary>
        public SafeAreaEdges Edges
        {
            get => _edges;
            set
            {
                _edges = SafeAreaMath.NormalizeEdges(value);
                _hasAppliedSnapshot = false;
            }
        }

        /// <summary>Gets the last safe-area snapshot applied successfully.</summary>
        public SafeAreaSnapshot Current { get; private set; }

        private void Awake()
        {
            EnsureDocument();
        }

        private void OnEnable()
        {
            EnsureDocument();
            _hasAppliedSnapshot = false;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (_restoreOnDisable)
            {
                RestoreOriginalStyle();
            }

            _target = null;
            _hasOriginalStyle = false;
            _hasAppliedSnapshot = false;
        }

        private void OnValidate()
        {
            _edges = SafeAreaMath.NormalizeEdges(_edges);
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            _hasAppliedSnapshot = false;
        }

        /// <summary>
        /// Reads the current screen safe area and updates the target when it changed.
        /// </summary>
        /// <returns>True when the document, target, panel, and safe-area snapshot are valid.</returns>
        public bool Refresh()
        {
            EnsureDocument();
            if (!TryResolveTarget(out var target) || Source == null
                || !Source.TryGetSnapshot(out var snapshot))
            {
                return false;
            }

            var parent = target.parent;
            if (parent == null || target.panel == null)
            {
                return false;
            }

            if (_target != target)
            {
                RestoreOriginalStyle();
                _target = target;
                CaptureOriginalStyle();
                _hasAppliedSnapshot = false;
            }

            var parentBounds = parent.worldBound;
            if (parentBounds.width <= 0f || parentBounds.height <= 0f)
            {
                return false;
            }

            if (_hasAppliedSnapshot && _appliedSnapshot == snapshot && _appliedParentBounds == parentBounds)
            {
                return true;
            }

            var safeTopLeftScreen = new Vector2(snapshot.SafeArea.xMin, snapshot.ScreenSize.y - snapshot.SafeArea.yMax);
            var safeBottomRightScreen = new Vector2(snapshot.SafeArea.xMax, snapshot.ScreenSize.y - snapshot.SafeArea.yMin);
            var safeTopLeftPanel = RuntimePanelUtils.ScreenToPanel(target.panel, safeTopLeftScreen);
            var safeBottomRightPanel = RuntimePanelUtils.ScreenToPanel(target.panel, safeBottomRightScreen);

            var left = Mathf.Max(0f, safeTopLeftPanel.x - parentBounds.xMin);
            var top = Mathf.Max(0f, safeTopLeftPanel.y - parentBounds.yMin);
            var right = Mathf.Max(0f, parentBounds.xMax - safeBottomRightPanel.x);
            var bottom = Mathf.Max(0f, parentBounds.yMax - safeBottomRightPanel.y);
            var appliedLeft = SafeAreaMath.Includes(_edges, SafeAreaEdges.Left) ? left : 0f;
            var appliedTop = SafeAreaMath.Includes(_edges, SafeAreaEdges.Top) ? top : 0f;
            var appliedRight = SafeAreaMath.Includes(_edges, SafeAreaEdges.Right) ? right : 0f;
            var appliedBottom = SafeAreaMath.Includes(_edges, SafeAreaEdges.Bottom) ? bottom : 0f;
            if (appliedLeft + appliedRight >= parentBounds.width || appliedTop + appliedBottom >= parentBounds.height)
            {
                return false;
            }

            target.style.position = Position.Absolute;
            target.style.left = appliedLeft;
            target.style.top = appliedTop;
            target.style.right = appliedRight;
            target.style.bottom = appliedBottom;

            Current = snapshot;
            _appliedSnapshot = snapshot;
            _appliedParentBounds = parentBounds;
            _hasAppliedSnapshot = true;
            return true;
        }

        private void EnsureDocument()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }
        }

        private bool TryResolveTarget(out VisualElement target)
        {
            target = null;
            if (_document == null || !_document.isActiveAndEnabled || _document.panelSettings == null)
            {
                return false;
            }

            var root = _document.rootVisualElement;
            if (root == null || root.panel == null)
            {
                return false;
            }

            target = string.IsNullOrWhiteSpace(_targetElementName)
                ? root
                : root.Q<VisualElement>(_targetElementName);
            return target != null;
        }

        private void CaptureOriginalStyle()
        {
            if (_target == null)
            {
                return;
            }

            _originalPosition = _target.style.position;
            _originalLeft = _target.style.left;
            _originalTop = _target.style.top;
            _originalRight = _target.style.right;
            _originalBottom = _target.style.bottom;
            _hasOriginalStyle = true;
        }

        private void RestoreOriginalStyle()
        {
            if (!_hasOriginalStyle || _target == null)
            {
                return;
            }

            _target.style.position = _originalPosition;
            _target.style.left = _originalLeft;
            _target.style.top = _originalTop;
            _target.style.right = _originalRight;
            _target.style.bottom = _originalBottom;
        }
    }
}
