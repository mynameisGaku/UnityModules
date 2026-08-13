using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="InterfaceReference{TInterface}"/> を、そのインターフェースを実装した
    /// オブジェクトしか受け付けないフィールドとして描く。
    /// <para>
    /// 実装していないオブジェクトを D&amp;D しても<b>受け付けない</b>のが要点。
    /// 素直に <c>MonoBehaviour</c> フィールドで受けて <c>Awake</c> でキャストする書き方だと、
    /// 配線ミスが実行時まで見つからない。
    /// </para>
    /// <para>
    /// GameObject を落としたときは、その中から実装しているコンポーネントを探して差し替える ——
    /// 使う側はどのコンポーネントが実装しているか知らなくてよい。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(InterfaceReference<>))]
    public sealed class InterfaceReferenceDrawer : PropertyDrawer
    {
        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var underlying = property.FindPropertyRelative("_underlying");
            var interfaceType = ResolveInterfaceType();

            if (underlying == null || interfaceType == null)
            {
                EditorGUI.LabelField(position, label.text, "InterfaceReference の型を解決できない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // インターフェース名をラベルに添えて、何を挿すべきか一目で分かるようにする。
            var content = new GUIContent(label.text, $"{interfaceType.Name} を実装したオブジェクト");

            EditorGUI.BeginChangeCheck();
            var assigned = EditorGUI.ObjectField(position, content, underlying.objectReferenceValue, typeof(Object), true);

            if (EditorGUI.EndChangeCheck())
            {
                underlying.objectReferenceValue = Coerce(assigned, interfaceType);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 差し込まれたオブジェクトを、インターフェースを実装した参照に直す。
        /// GameObject ならコンポーネントを探す。実装が見つからなければ null。
        /// </summary>
        private static Object Coerce(Object assigned, Type interfaceType)
        {
            if (assigned == null) return null;
            if (interfaceType.IsInstanceOfType(assigned)) return assigned;

            if (assigned is GameObject gameObject)
            {
                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (interfaceType.IsInstanceOfType(component)) return component;
                }
            }

            Debug.LogWarning($"{assigned.name} は {interfaceType.Name} を実装していないので割り当てない。");
            return null;
        }

        /// <summary>フィールドの型引数からインターフェース型を取り出す。配列やリストの要素にも対応する。</summary>
        private Type ResolveInterfaceType()
        {
            var type = fieldInfo?.FieldType;
            if (type == null) return null;

            if (type.IsArray) type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (type == null || !type.IsGenericType) return null;
            if (type.GetGenericTypeDefinition() != typeof(InterfaceReference<>)) return null;

            return type.GetGenericArguments()[0];
        }
    }
}
