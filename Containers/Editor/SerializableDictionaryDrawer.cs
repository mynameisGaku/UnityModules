using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="SerializableDictionary{TKey,TValue}"/> を、キーと値が横並びになった
    /// 並べ替え可能なリストとして描く。
    /// <para>
    /// このドロワーが本体と言ってよい。2 本のリストを別々に見せてしまうと、
    /// 挿入や削除でキーと値の対応がずれても気づけない。ここでは常に組で扱い、
    /// <b>重複キーは行を赤くして警告を出す</b> —— 実行時には後から来た方が黙って捨てられるので、
    /// 編集時に見えることに意味がある。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const float KeyRatio = 0.42f;
        private const float Gap = 6f;

        private sealed class State
        {
            public ReorderableList List;
            public SerializedProperty Keys;
            public SerializedProperty Values;
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
                EditorGUI.LabelField(position, label.text, "SerializableDictionary の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var count = Mathf.Min(state.Keys.arraySize, state.Values.arraySize);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, $"{label.text}  ({count})", true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            ContainersEditorUtility.FindDuplicateIndices(state.Keys, state.Duplicates);

            var y = headerRect.yMax + ContainersEditorUtility.Spacing;

            if (state.Duplicates.Count > 0)
            {
                var warningRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(warningRect, $"キーが {state.Duplicates.Count} 件重複している。実行時には最初の 1 件だけが残る。", MessageType.Warning);
                y = warningRect.yMax + ContainersEditorUtility.Spacing;
            }

            var listRect = new Rect(position.x, y, position.width, state.List.GetHeight());
            state.List.DoList(listRect);

            EditorGUI.EndProperty();
        }

        private State GetState(SerializedProperty property)
        {
            var keys = property.FindPropertyRelative("_keys");
            var values = property.FindPropertyRelative("_values");
            if (keys == null || values == null) return null;

            var id = $"{property.serializedObject.targetObject.GetEntityId()}:{property.propertyPath}";

            if (_states.TryGetValue(id, out var cached))
            {
                // SerializedProperty は毎フレーム作り直されるので、参照だけ差し替える。
                cached.Keys = keys;
                cached.Values = values;
                cached.List.serializedProperty = keys;
                return cached;
            }

            var state = new State { Keys = keys, Values = values };
            state.List = BuildList(state);
            _states[id] = state;
            return state;
        }

        private static ReorderableList BuildList(State state)
        {
            var list = new ReorderableList(state.Keys.serializedObject, state.Keys, true, true, true, true);

            list.drawHeaderCallback = rect =>
            {
                var keyWidth = (rect.width - Gap) * KeyRatio;
                var keyRect = new Rect(rect.x + 14f, rect.y, keyWidth, rect.height);
                var valueRect = new Rect(keyRect.xMax + Gap, rect.y, rect.width - keyWidth - Gap - 14f, rect.height);

                EditorGUI.LabelField(keyRect, "Key", EditorStyles.miniBoldLabel);
                EditorGUI.LabelField(valueRect, "Value", EditorStyles.miniBoldLabel);
            };

            list.elementHeightCallback = index =>
            {
                if (index >= state.Keys.arraySize) return EditorGUIUtility.singleLineHeight;

                var keyHeight = EditorGUI.GetPropertyHeight(state.Keys.GetArrayElementAtIndex(index), GUIContent.none, true);
                var valueHeight = index < state.Values.arraySize
                    ? EditorGUI.GetPropertyHeight(state.Values.GetArrayElementAtIndex(index), GUIContent.none, true)
                    : EditorGUIUtility.singleLineHeight;

                return Mathf.Max(keyHeight, valueHeight) + ContainersEditorUtility.Spacing;
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index >= state.Keys.arraySize) return;

                rect.y += 1f;
                rect.height -= ContainersEditorUtility.Spacing;

                if (state.Duplicates.Contains(index)) ContainersEditorUtility.DrawRowHighlight(rect, ContainersEditorUtility.WarningTint);

                var keyWidth = (rect.width - Gap) * KeyRatio;
                var keyRect = new Rect(rect.x, rect.y, keyWidth, rect.height);
                var valueRect = new Rect(keyRect.xMax + Gap, rect.y, rect.width - keyWidth - Gap, rect.height);

                EditorGUI.PropertyField(keyRect, state.Keys.GetArrayElementAtIndex(index), GUIContent.none, true);

                if (index < state.Values.arraySize)
                {
                    EditorGUI.PropertyField(valueRect, state.Values.GetArrayElementAtIndex(index), GUIContent.none, true);
                }
            };

            // 追加・削除・並べ替えは必ず両方のリストに同じ操作をする。片方だけ動かすと対応が崩れる。
            list.onAddCallback = _ =>
            {
                state.Keys.arraySize++;
                state.Values.arraySize = state.Keys.arraySize;
            };

            list.onRemoveCallback = target =>
            {
                var index = target.index;
                if (index < 0 || index >= state.Keys.arraySize) return;

                state.Keys.DeleteArrayElementAtIndex(index);
                if (index < state.Values.arraySize) state.Values.DeleteArrayElementAtIndex(index);
            };

            list.onReorderCallbackWithDetails = (_, oldIndex, newIndex) =>
            {
                if (oldIndex < state.Values.arraySize && newIndex < state.Values.arraySize)
                {
                    state.Values.MoveArrayElement(oldIndex, newIndex);
                }
            };

            return list;
        }
    }
}
