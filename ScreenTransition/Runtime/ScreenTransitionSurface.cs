using UnityEngine;
using UnityEngine.UIElements;

namespace ScreenTransition
{
    /// <summary>UIDocumentのroot全体を覆うVisualElementを所有する。</summary>
    internal sealed class ScreenTransitionSurface
    {
        internal const string ElementName = "screen-transition-overlay";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _element;

        /// <summary>有効なUIDocumentとPanelSettingsを保ち、表示要素が同じrootへ接続されている場合はtrue。</summary>
        internal bool IsAvailable =>
            _document != null &&
            _document.isActiveAndEnabled &&
            _document.panelSettings != null &&
            _root != null &&
            ReferenceEquals(_document.rootVisualElement, _root) &&
            _element != null &&
            ReferenceEquals(_element.parent, _root);

        /// <summary>PanelSettingsを持つUIDocumentのrootへ表示要素を追加する。</summary>
        /// <param name="document">表示先となるUIDocument。</param>
        /// <returns>表示要素を利用できる場合はtrue。</returns>
        internal bool TryAttach(UIDocument document)
        {
            if (IsAvailable) return true;
            if (document == null || !document.isActiveAndEnabled || document.panelSettings == null) return false;

            var root = document.rootVisualElement;
            if (root == null) return false;

            Detach();
            _document = document;
            _root = root;
            _element = new VisualElement
            {
                name = ElementName,
                pickingMode = PickingMode.Ignore,
            };
            _element.style.position = Position.Absolute;
            _element.style.left = 0f;
            _element.style.top = 0f;
            _element.style.right = 0f;
            _element.style.bottom = 0f;
            _element.style.display = DisplayStyle.None;
            root.Add(_element);
            return IsAvailable;
        }

        /// <summary>要求色と計算済み不透明度を表示要素へ反映する。</summary>
        /// <param name="color">表示するRGB色。</param>
        /// <param name="opacity">0以上1以下の表示alpha。</param>
        internal void Apply(Color color, float opacity)
        {
            if (!IsAvailable) throw new System.InvalidOperationException("画面遷移の表示先を使用できません。");

            var displayColor = color;
            displayColor.a = Mathf.Clamp01(opacity);
            _element.style.backgroundColor = displayColor;
            _element.style.display = displayColor.a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (displayColor.a > 0f && !EnsureFront()) throw new System.InvalidOperationException("画面遷移の表示要素を最前面に維持できません。");
        }

        /// <summary>表示要素を同じrootの最後へ移し、後から追加された兄弟より前面へ戻す。</summary>
        /// <returns>表示要素を最前面へ置けた場合はtrue。</returns>
        internal bool EnsureFront()
        {
            if (!IsAvailable || _root.childCount == 0) return false;
            if (!ReferenceEquals(_root.ElementAt(_root.childCount - 1), _element)) _element.BringToFront();
            return IsAvailable && ReferenceEquals(_root.ElementAt(_root.childCount - 1), _element);
        }

        /// <summary>表示要素をUIDocumentから取り外す。</summary>
        internal void Detach()
        {
            _element?.RemoveFromHierarchy();
            _element = null;
            _root = null;
            _document = null;
        }
    }
}
