using System;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="SerializableGuid"/> を、読み取り専用の 16 進表示＋生成／コピーのボタンとして描く。
    /// <para>
    /// 手で書き換えられないようにしてあるのは、ID は<b>参照される側の同一性そのもの</b>で、
    /// うっかり書き換えるとセーブデータや他アセットからの参照が静かに切れるため。
    /// 意図的に振り直すときだけボタンを押す。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableGuid))]
    public sealed class SerializableGuidDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 26f;
        private const float Gap = 2f;

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var a = property.FindPropertyRelative("_a");
            var b = property.FindPropertyRelative("_b");
            var c = property.FindPropertyRelative("_c");
            var d = property.FindPropertyRelative("_d");

            if (a == null || b == null || c == null || d == null)
            {
                EditorGUI.LabelField(position, label.text, "SerializableGuid の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, label);
            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var isEmpty = (a.intValue | b.intValue | c.intValue | d.intValue) == 0;
            var text = isEmpty ? "(未設定)" : ToGuid(a, b, c, d).ToString("N");

            var fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - (ButtonWidth + Gap) * 2f, contentRect.height);
            var newRect = new Rect(fieldRect.xMax + Gap, contentRect.y, ButtonWidth, contentRect.height);
            var copyRect = new Rect(newRect.xMax + Gap, contentRect.y, ButtonWidth, contentRect.height);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(fieldRect, text);
            }

            if (GUI.Button(newRect, new GUIContent("↻", "新しい ID を振り直す。既存の参照は切れる。"), EditorStyles.miniButton))
            {
                Assign(a, b, c, d, Guid.NewGuid());
            }

            using (new EditorGUI.DisabledScope(isEmpty))
            {
                if (GUI.Button(copyRect, new GUIContent("⧉", "16 進表記をクリップボードにコピーする。"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = text;
                }
            }

            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        private static Guid ToGuid(SerializedProperty a, SerializedProperty b, SerializedProperty c, SerializedProperty d)
        {
            var bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(a.intValue), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(b.intValue), 0, bytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(c.intValue), 0, bytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(d.intValue), 0, bytes, 12, 4);
            return new Guid(bytes);
        }

        private static void Assign(SerializedProperty a, SerializedProperty b, SerializedProperty c, SerializedProperty d, Guid guid)
        {
            var bytes = guid.ToByteArray();
            a.intValue = BitConverter.ToInt32(bytes, 0);
            b.intValue = BitConverter.ToInt32(bytes, 4);
            c.intValue = BitConverter.ToInt32(bytes, 8);
            d.intValue = BitConverter.ToInt32(bytes, 12);
        }
    }
}
