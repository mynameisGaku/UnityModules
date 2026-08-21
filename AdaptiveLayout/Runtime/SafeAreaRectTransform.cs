using UnityEngine;

namespace AdaptiveLayout
{
    /// <summary>
    /// Constrains a screen-sized RectTransform to selected safe-area edges.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaRectTransform : MonoBehaviour
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private SafeAreaEdges _edges = SafeAreaEdges.All;
        [SerializeField] private bool _restoreOnDisable = true;

        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalOffsetMin;
        private Vector2 _originalOffsetMax;
        private bool _hasOriginalLayout;
        private bool _hasAppliedSnapshot;
        private SafeAreaSnapshot _appliedSnapshot;

        internal ISafeAreaSource Source { get; set; } = ScreenSafeAreaSource.Instance;

        /// <summary>Gets the RectTransform controlled by this component.</summary>
        public RectTransform Target => _target;

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
            EnsureTarget();
        }

        private void OnEnable()
        {
            EnsureTarget();
            CaptureOriginalLayout();
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
                RestoreOriginalLayout();
            }

            _hasAppliedSnapshot = false;
        }

        private void OnValidate()
        {
            _edges = SafeAreaMath.NormalizeEdges(_edges);
            if (_target == null)
            {
                _target = GetComponent<RectTransform>();
            }

            _hasAppliedSnapshot = false;
        }

        /// <summary>
        /// Reads the current screen safe area and updates the target when it changed.
        /// </summary>
        /// <returns>True when a valid snapshot is available and the target is updated.</returns>
        public bool Refresh()
        {
            EnsureTarget();
            if (_target == null || _target.parent is not RectTransform || Source == null
                || !Source.TryGetSnapshot(out var snapshot))
            {
                return false;
            }

            if (_hasAppliedSnapshot && _appliedSnapshot == snapshot)
            {
                return true;
            }

            var normalized = SafeAreaMath.GetNormalizedRect(snapshot, _edges);
            _target.anchorMin = normalized.min;
            _target.anchorMax = normalized.max;
            _target.offsetMin = Vector2.zero;
            _target.offsetMax = Vector2.zero;

            Current = snapshot;
            _appliedSnapshot = snapshot;
            _hasAppliedSnapshot = true;
            return true;
        }

        private void EnsureTarget()
        {
            if (_target == null)
            {
                _target = GetComponent<RectTransform>();
            }
        }

        private void CaptureOriginalLayout()
        {
            if (_target == null)
            {
                return;
            }

            _originalAnchorMin = _target.anchorMin;
            _originalAnchorMax = _target.anchorMax;
            _originalOffsetMin = _target.offsetMin;
            _originalOffsetMax = _target.offsetMax;
            _hasOriginalLayout = true;
        }

        private void RestoreOriginalLayout()
        {
            if (!_hasOriginalLayout || _target == null)
            {
                return;
            }

            _target.anchorMin = _originalAnchorMin;
            _target.anchorMax = _originalAnchorMax;
            _target.offsetMin = _originalOffsetMin;
            _target.offsetMax = _originalOffsetMax;
        }
    }
}
