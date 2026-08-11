using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="SerializableType"/> を型選択のドロップダウンとして描く。
    /// <para>
    /// <see cref="TypeFilterAttribute"/> が付いていれば、その基底から派生した型だけに絞る。
    /// 保存済みの型が見つからない場合（クラス名を変えた、アセンブリを消した）は
    /// <b>黙って空にせず、失われた型名を出して警告する</b> —— 気づかないまま出荷される方が痛いので。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableType))]
    public sealed class SerializableTypeDrawer : PropertyDrawer
    {
        /// <summary>基底型ごとの候補一覧。ドメインリロードまで再利用する。</summary>
        private static readonly Dictionary<Type, Type[]> CandidateCache = new Dictionary<Type, Type[]>();

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var name = property.FindPropertyRelative("_assemblyQualifiedName");
            var missing = name != null && !string.IsNullOrEmpty(name.stringValue) && Type.GetType(name.stringValue, false) == null;
            return missing
                ? EditorGUIUtility.singleLineHeight * 3f + ContainersEditorUtility.Spacing
                : EditorGUIUtility.singleLineHeight;
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("_assemblyQualifiedName");
            if (nameProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "SerializableType の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var filter = attribute as TypeFilterAttribute;
            var baseType = filter?.BaseType ?? typeof(object);
            var candidates = GetCandidates(baseType, filter?.AllowAbstract ?? false);

            var stored = nameProperty.stringValue;
            var current = string.IsNullOrEmpty(stored) ? null : Type.GetType(stored, false);
            var isMissing = !string.IsNullOrEmpty(stored) && current == null;

            var rowRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var displayNames = new string[candidates.Length + 1];
            displayNames[0] = "(なし)";
            for (var i = 0; i < candidates.Length; i++) displayNames[i + 1] = NiceName(candidates[i]);

            var selected = 0;
            if (current != null)
            {
                var found = Array.IndexOf(candidates, current);
                selected = found >= 0 ? found + 1 : 0;
            }

            var picked = EditorGUI.Popup(rowRect, label.text, selected, displayNames);
            if (picked != selected)
            {
                nameProperty.stringValue = picked == 0 ? string.Empty : candidates[picked - 1].AssemblyQualifiedName;
            }

            if (isMissing)
            {
                var warningRect = new Rect(
                    position.x,
                    rowRect.yMax + ContainersEditorUtility.Spacing,
                    position.width,
                    EditorGUIUtility.singleLineHeight * 2f);

                EditorGUI.HelpBox(warningRect, $"保存されている型が見つからない：{ShortName(stored)}", MessageType.Error);
            }

            EditorGUI.EndProperty();
        }

        private static Type[] GetCandidates(Type baseType, bool allowAbstract)
        {
            if (CandidateCache.TryGetValue(baseType, out var cached)) return cached;

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(type => allowAbstract || (!type.IsAbstract && !type.IsInterface))
                .Where(type => !type.IsGenericTypeDefinition)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            // baseType が object のときは TypeCache が全型を返して実用にならないので、上限を掛ける。
            if (baseType == typeof(object)) types = types.Take(512).ToArray();

            CandidateCache[baseType] = types;
            return types;
        }

        private static string NiceName(Type type) =>
            string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}/{type.Name}";

        private static string ShortName(string assemblyQualifiedName)
        {
            var comma = assemblyQualifiedName.IndexOf(',');
            return comma > 0 ? assemblyQualifiedName.Substring(0, comma) : assemblyQualifiedName;
        }
    }
}
