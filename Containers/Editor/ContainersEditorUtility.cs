using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// このパッケージのドロワーが共有する小道具。
    /// </summary>
    internal static class ContainersEditorUtility
    {
        /// <summary>警告の帯に使う淡い赤。ライト／ダーク両方で読める濃さにしてある。</summary>
        internal static Color WarningTint => EditorGUIUtility.isProSkin
            ? new Color(0.62f, 0.24f, 0.24f, 0.35f)
            : new Color(1f, 0.55f, 0.55f, 0.45f);

        /// <summary>行の高さ＋行間。</summary>
        internal static float LineHeight => EditorGUIUtility.singleLineHeight;

        /// <summary>行間の余白。</summary>
        internal static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        /// <summary>
        /// <see cref="SerializedProperty"/> の値を、辞書の重複判定に使える文字列にする。
        /// <para>
        /// 型ごとに個別に読むのは、<c>boxedValue</c> が値型で確保を伴ううえ、
        /// オブジェクト参照の同一性を instanceID で見たいため。
        /// </para>
        /// </summary>
        internal static string KeySignature(SerializedProperty property)
        {
            if (property == null) return string.Empty;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    return property.longValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "1" : "0";
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("R");
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex.ToString();
                case SerializedPropertyType.Character:
                    return property.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    // Unity 6.5 で instanceID 系は EntityId に置き換わった。
                    return property.objectReferenceEntityIdValue.ToString();
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString("R");
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.ToString();
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString("R");
                default:
                    // 構造体などは子フィールドを連結して署名にする。
                    return CompositeSignature(property);
            }
        }

        private static string CompositeSignature(SerializedProperty property)
        {
            var copy = property.Copy();
            var end = copy.GetEndProperty();
            var builder = new System.Text.StringBuilder();

            if (!copy.NextVisible(true)) return copy.propertyPath;

            while (!SerializedProperty.EqualContents(copy, end))
            {
                builder.Append(KeySignature(copy)).Append('|');
                if (!copy.NextVisible(false)) break;
            }

            return builder.ToString();
        }

        /// <summary>
        /// 重複しているキーの位置を集める。<paramref name="results"/> には
        /// 2 回目以降に現れた位置だけが入る。
        /// </summary>
        internal static void FindDuplicateIndices(SerializedProperty keysArray, HashSet<int> results)
        {
            results.Clear();
            if (keysArray == null || !keysArray.isArray) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < keysArray.arraySize; i++)
            {
                var signature = KeySignature(keysArray.GetArrayElementAtIndex(i));
                if (!seen.Add(signature)) results.Add(i);
            }
        }

        /// <summary>行の背景を塗る。重複キーの強調に使う。</summary>
        internal static void DrawRowHighlight(Rect rect, Color color)
        {
            var padded = new Rect(rect.x - 2f, rect.y - 1f, rect.width + 4f, rect.height + 2f);
            EditorGUI.DrawRect(padded, color);
        }

        /// <summary>
        /// プロパティが 1 行で描けるか（子を展開する必要が無いか）。
        /// 辞書の値をインラインに置けるかの判断に使う。
        /// </summary>
        internal static bool IsSingleLine(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    return false;
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.Bounds:
                    return false;
                default:
                    return true;
            }
        }
    }
}
