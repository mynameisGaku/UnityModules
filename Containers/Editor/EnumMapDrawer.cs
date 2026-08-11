using System;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="EnumMap{TEnum,TValue}"/> を、enum のメンバー名をラベルにした値の並びとして描く。
    /// <para>
    /// 配列としてそのまま描くと <c>Element 0</c>, <c>Element 1</c>, … になり、
    /// どれがどのメンバーか分からない。名前を出せば<b>設定漏れがその場で見える</b>。
    /// 要素数は enum 側に従うので、Inspector からの増減はさせない。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(EnumMap<,>), true)]
    public sealed class EnumMapDrawer : PropertyDrawer
    {
        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var values = property.FindPropertyRelative("_values");
            if (values == null || !property.isExpanded) return EditorGUIUtility.singleLineHeight;

            var height = EditorGUIUtility.singleLineHeight;
            for (var i = 0; i < values.arraySize; i++)
            {
                height += ContainersEditorUtility.Spacing;
                height += EditorGUI.GetPropertyHeight(values.GetArrayElementAtIndex(i), GUIContent.none, true);
            }

            return height;
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var values = property.FindPropertyRelative("_values");
            if (values == null)
            {
                EditorGUI.LabelField(position, label.text, "EnumMap の中身を読めない");
                return;
            }

            var names = ResolveEnumNames();

            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            var y = headerRect.yMax;

            for (var i = 0; i < values.arraySize; i++)
            {
                var element = values.GetArrayElementAtIndex(i);
                var elementHeight = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);

                y += ContainersEditorUtility.Spacing;
                var rowRect = new Rect(position.x, y, position.width, elementHeight);

                var name = names != null && i < names.Length ? ObjectNames.NicifyVariableName(names[i]) : $"[{i}]";
                EditorGUI.PropertyField(rowRect, element, new GUIContent(name), true);

                y += elementHeight;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        /// <summary>フィールドの型から enum 型を辿ってメンバー名を取り出す。</summary>
        private string[] ResolveEnumNames()
        {
            var type = fieldInfo?.FieldType;

            // 具体型で継承されているのが通常なので、EnumMap<,> に行き当たるまで基底を遡る。
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EnumMap<,>))
                {
                    var enumType = type.GetGenericArguments()[0];
                    return enumType.IsEnum ? Enum.GetNames(enumType) : null;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
