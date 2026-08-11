using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="Optional{T}"/> を「チェックボックス＋値」の 1 行として描く。
    /// <para>
    /// チェックが外れている間も値のフィールドは表示したまま無効化する。消してしまうと
    /// 「一度切って、また戻す」ときに前の値が失われ、上書き設定として使いにくくなるため。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(Optional<>))]
    public sealed class OptionalDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("_value");
            return value == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(value, label, true);
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var hasValue = property.FindPropertyRelative("_hasValue");
            var value = property.FindPropertyRelative("_value");

            if (hasValue == null || value == null)
            {
                EditorGUI.LabelField(position, label.text, "Optional<T> の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // ラベルの直後にチェックボックスを置き、残りを値のフィールドに使う。
            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            var toggleRect = new Rect(labelRect.xMax, position.y, ToggleWidth, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(toggleRect.xMax, position.y, position.width - EditorGUIUtility.labelWidth - ToggleWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            hasValue.boolValue = EditorGUI.Toggle(toggleRect, hasValue.boolValue);

            using (new EditorGUI.DisabledScope(!hasValue.boolValue))
            {
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                EditorGUI.PropertyField(valueRect, value, GUIContent.none, true);

                EditorGUI.indentLevel = previousIndent;
            }

            EditorGUI.EndProperty();
        }
    }
}
