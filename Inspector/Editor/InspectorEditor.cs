using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Inspector.Editor
{
    /// <summary>
    /// このモジュールの属性を解釈する Inspector。
    /// <para>
    /// <c>isFallback = true</c> を指定した全 Object 向けの予備 Editor なので、
    /// 専用のエディタを持つ型（Transform、Rigidbody、利用側の <c>CustomEditor</c> など）では
    /// そちらが優先され、ここは呼ばれない。行き先の無い型だけを受け持つ。
    /// </para>
    /// <para>
    /// 自作の <c>CustomEditor</c> でも属性を効かせたい場合は、このクラスを継承すればよい。
    /// </para>
    /// <code>
    /// [CustomEditor(typeof(Spawner))]
    /// public sealed class SpawnerEditor : Inspector.Editor.InspectorEditor
    /// {
    ///     public override void OnInspectorGUI()
    ///     {
    ///         base.OnInspectorGUI();          // 属性つきの描画
    ///         if (GUILayout.Button("プレビュー")) { ... }
    ///     }
    /// }
    /// </code>
    /// </summary>
    [CustomEditor(typeof(UnityEngine.Object), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class InspectorEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 属性を使っていない型では、Unity の既定のインスペクタをそのまま返す。
        /// <para>
        /// このモジュールを入れただけで、何も書いていないクラスの見た目が
        /// IMGUI 版に切り替わってしまうのを避けるため。
        /// 属性を使っている型では <c>null</c> を返し、<see cref="OnInspectorGUI"/> の経路に落とす。
        /// </para>
        /// </summary>
        /// <returns>既定表示を構築した要素。属性を使う型または派生 Editor では <c>null</c>。</returns>
        public override VisualElement CreateInspectorGUI()
        {
            // 派生クラスは自前で描くつもりで書かれている。既定のインスペクタを返すと
            // OnInspectorGUI が一度も呼ばれなくなるので、ここは素通りさせる。
            if (GetType() != typeof(InspectorEditor)) return null;

            if (target == null) return null;
            if (InspectorMemberScanner.UsesInspectorAttributes(target.GetType())) return null;

            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            InspectorGUILayout.Draw(serializedObject, targets);
        }
    }
}
