using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <c>[SerializeReference]</c> フィールドを「派生型を選ぶドロップダウン＋その中身」として描く。
    /// <para>
    /// これがあるかどうかで <c>[SerializeReference]</c> の実用性が変わる。標準のままでは
    /// Inspector から型を差し込む手段が無く、コードで代入するしかない。
    /// ここで選べるようにすると、条件・効果・スポーン規則といった多態なデータを
    /// 1 つのアセットの中で designer が組み立てられるようになる。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> CandidateCache = new Dictionary<Type, Type[]>();

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUIUtility.singleLineHeight * 2f;
            }

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "[SubclassSelector] は [SerializeReference] と併用する必要がある。", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // 型名のボタンは行の右端に重ねて置く。ラベルと値の表示は標準のまま残したいため。
            var buttonRect = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                position.y,
                position.width - EditorGUIUtility.labelWidth - 2f,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(position, property, label, true);

            var currentName = TypeNameOf(property.managedReferenceFullTypename);

            if (GUI.Button(buttonRect, currentName, EditorStyles.popup))
            {
                ShowMenu(property);
            }

            EditorGUI.EndProperty();
        }

        private void ShowMenu(SerializedProperty property)
        {
            var options = attribute as SubclassSelectorAttribute;
            var baseType = ResolveType(property.managedReferenceFieldTypename);

            if (baseType == null)
            {
                Debug.LogWarning($"[SubclassSelector] 基底型を解決できない：{property.managedReferenceFieldTypename}");
                return;
            }

            var menu = new GenericMenu();
            var target = property.serializedObject;
            var path = property.propertyPath;

            if (options == null || options.AllowNull)
            {
                menu.AddItem(new GUIContent("(なし)"), string.IsNullOrEmpty(property.managedReferenceFullTypename), () =>
                {
                    Assign(target, path, null);
                });

                menu.AddSeparator(string.Empty);
            }

            foreach (var candidate in GetCandidates(baseType))
            {
                var label = options != null && options.GroupByNamespace && !string.IsNullOrEmpty(candidate.Namespace)
                    ? $"{candidate.Namespace.Replace('.', '/')}/{ObjectNames.NicifyVariableName(candidate.Name)}"
                    : ObjectNames.NicifyVariableName(candidate.Name);

                var captured = candidate;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    Assign(target, path, Activator.CreateInstance(captured));
                });
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject serializedObject, string propertyPath, object value)
        {
            serializedObject.Update();

            var property = serializedObject.FindProperty(propertyPath);
            if (property == null) return;

            property.managedReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static Type[] GetCandidates(Type baseType)
        {
            if (CandidateCache.TryGetValue(baseType, out var cached)) return cached;

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(type => !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition)
                // Unity は UnityEngine.Object 派生を managed reference として保存できない。
                .Where(type => !typeof(UnityEngine.Object).IsAssignableFrom(type))
                // 引数なしで作れないと選んだ瞬間に落ちる。
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();

            CandidateCache[baseType] = types;
            return types;
        }

        /// <summary>
        /// Unity が返す "&lt;アセンブリ名&gt; &lt;型名&gt;" 形式を <see cref="Type"/> に戻す。
        /// </summary>
        private static Type ResolveType(string managedReferenceTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceTypename)) return null;

            var parts = managedReferenceTypename.Split(' ');
            if (parts.Length != 2) return null;

            return Type.GetType($"{parts[1]}, {parts[0]}", false);
        }

        private static string TypeNameOf(string managedReferenceFullTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFullTypename)) return "(なし)";

            var parts = managedReferenceFullTypename.Split(' ');
            var fullName = parts.Length == 2 ? parts[1] : managedReferenceFullTypename;
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }
    }
}
