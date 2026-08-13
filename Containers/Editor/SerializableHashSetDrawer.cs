using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="SerializableHashSet{T}"/> を、重複要素を強調表示する並べ替え可能なリストとして描く。
    /// <para>
    /// 素の配列として描いてしまうと、同じ値を 2 回入れても編集画面では気づけず、
    /// 実行時に黙って 1 件に縮む。ここでは編集中に赤く出る。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableHashSet<>), true)]
    public sealed class SerializableHashSetDrawer : PropertyDrawer
    {
        private sealed class State
        {
            public ReorderableList List;
            public SerializedProperty Items;
            public readonly HashSet<int> Duplicates = new HashSet<int>();
        }

        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            var state = GetState(property);
            if (state == null) return EditorGUIUtility.singleLineHeight;

            var height = EditorGUIUtility.singleLineHeight + ContainersEditorUtility.Spacing + state.List.GetHeight();
            if (state.Duplicates.Count > 0) height += EditorGUIUtility.singleLineHeight * 2f + ContainersEditorUtility.Spacing;
            return height;
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var state = GetState(property);
            if (state == null)
            {
                EditorGUI.LabelField(position, label.text, "SerializableHashSet の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, $"{label.text}  ({state.Items.arraySize})", true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            ContainersEditorUtility.FindDuplicateIndices(state.Items, state.Duplicates);

            var y = headerRect.yMax + ContainersEditorUtility.Spacing;

            if (state.Duplicates.Count > 0)
            {
                var warningRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(warningRect, $"要素が {state.Duplicates.Count} 件重複している。集合なので実行時には 1 件に縮む。", MessageType.Warning);
                y = warningRect.yMax + ContainersEditorUtility.Spacing;
            }

            state.List.DoList(new Rect(position.x, y, position.width, state.List.GetHeight()));

            EditorGUI.EndProperty();
        }

        private State GetState(SerializedProperty property)
        {
            var items = property.FindPropertyRelative("_items");
            if (items == null) return null;

            var id = $"{property.serializedObject.targetObject.GetEntityId()}:{property.propertyPath}";

            if (_states.TryGetValue(id, out var cached))
            {
                cached.Items = items;
                cached.List.serializedProperty = items;
                return cached;
            }

            var state = new State { Items = items };
            state.List = new ReorderableList(items.serializedObject, items, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Items", EditorStyles.miniBoldLabel)
            };

            state.List.elementHeightCallback = index => index < state.Items.arraySize
                ? EditorGUI.GetPropertyHeight(state.Items.GetArrayElementAtIndex(index), GUIContent.none, true) + ContainersEditorUtility.Spacing
                : EditorGUIUtility.singleLineHeight;

            state.List.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index >= state.Items.arraySize) return;

                rect.y += 1f;
                rect.height -= ContainersEditorUtility.Spacing;

                if (state.Duplicates.Contains(index)) ContainersEditorUtility.DrawRowHighlight(rect, ContainersEditorUtility.WarningTint);

                EditorGUI.PropertyField(rect, state.Items.GetArrayElementAtIndex(index), GUIContent.none, true);
            };

            _states[id] = state;
            return state;
        }
    }
}
