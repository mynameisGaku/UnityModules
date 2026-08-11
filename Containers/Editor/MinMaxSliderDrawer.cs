using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="FloatRange"/> と <see cref="IntRange"/> を、
    /// <see cref="MinMaxSliderAttribute"/> が付いていればスライダーとして描く。
    /// <para>
    /// 属性が無い場合は 2 つの数値フィールドとして描き、いずれの場合も
    /// <b>最小が最大を超えたら自動的に押し戻す</b> —— 逆転した範囲は下流で必ず事故になるため。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(FloatRange))]
    [CustomPropertyDrawer(typeof(IntRange))]
    public sealed class MinMaxRangeDrawer : PropertyDrawer
    {
        private const float FieldWidth = 52f;
        private const float Gap = 4f;

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProperty = property.FindPropertyRelative("_min");
            var maxProperty = property.FindPropertyRelative("_max");

            if (minProperty == null || maxProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Range の中身を読めない");
                return;
            }

            var isInteger = minProperty.propertyType == SerializedPropertyType.Integer;
            var slider = attribute as MinMaxSliderAttribute;

            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, label);
            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            if (slider == null)
            {
                DrawFields(contentRect, minProperty, maxProperty, isInteger);
            }
            else
            {
                DrawSlider(contentRect, minProperty, maxProperty, isInteger, slider);
            }

            ClampOrder(minProperty, maxProperty, isInteger);

            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        private static void DrawFields(Rect rect, SerializedProperty min, SerializedProperty max, bool isInteger)
        {
            var half = (rect.width - Gap) * 0.5f;
            var minRect = new Rect(rect.x, rect.y, half, rect.height);
            var maxRect = new Rect(minRect.xMax + Gap, rect.y, half, rect.height);

            if (isInteger)
            {
                min.intValue = EditorGUI.IntField(minRect, min.intValue);
                max.intValue = EditorGUI.IntField(maxRect, max.intValue);
                return;
            }

            min.floatValue = EditorGUI.FloatField(minRect, min.floatValue);
            max.floatValue = EditorGUI.FloatField(maxRect, max.floatValue);
        }

        private static void DrawSlider(Rect rect, SerializedProperty min, SerializedProperty max, bool isInteger, MinMaxSliderAttribute slider)
        {
            var showFields = slider.ShowFields;
            var fieldsWidth = showFields ? (FieldWidth + Gap) * 2f : 0f;
            var sliderRect = new Rect(rect.x + (showFields ? FieldWidth + Gap : 0f), rect.y, rect.width - fieldsWidth, rect.height);

            var minValue = isInteger ? min.intValue : min.floatValue;
            var maxValue = isInteger ? max.intValue : max.floatValue;

            if (showFields)
            {
                var leftRect = new Rect(rect.x, rect.y, FieldWidth, rect.height);
                minValue = isInteger ? EditorGUI.IntField(leftRect, (int)minValue) : EditorGUI.FloatField(leftRect, minValue);
            }

            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, slider.LowerLimit, slider.UpperLimit);

            if (showFields)
            {
                var rightRect = new Rect(sliderRect.xMax + Gap, rect.y, FieldWidth, rect.height);
                maxValue = isInteger ? EditorGUI.IntField(rightRect, (int)maxValue) : EditorGUI.FloatField(rightRect, maxValue);
            }

            if (isInteger)
            {
                min.intValue = Mathf.RoundToInt(minValue);
                max.intValue = Mathf.RoundToInt(maxValue);
                return;
            }

            min.floatValue = minValue;
            max.floatValue = maxValue;
        }

        /// <summary>最小が最大を追い越したら、いま編集していない側を押し戻す。</summary>
        private static void ClampOrder(SerializedProperty min, SerializedProperty max, bool isInteger)
        {
            if (isInteger)
            {
                if (min.intValue > max.intValue) max.intValue = min.intValue;
                return;
            }

            if (min.floatValue > max.floatValue) max.floatValue = min.floatValue;
        }
    }
}
