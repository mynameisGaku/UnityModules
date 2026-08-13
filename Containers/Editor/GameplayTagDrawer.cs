using System.Collections.Generic;
using Containers.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Containers.Editor
{
    /// <summary>
    /// <see cref="GameplayTag"/> を、自由入力のテキストと登録済みタグのドロップダウンの両方で編集できるようにする。
    /// <para>
    /// 両方を残しているのは、タグの登録が実行時に起きるため。まだ一度も
    /// <see cref="GameplayTagRegistry"/> に載っていないタグも先に書けないと、
    /// 新しいタグを作る最初の一回が詰む。
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public sealed class GameplayTagDrawer : PropertyDrawer
    {
        private const float DropdownWidth = 20f;

        private static readonly List<string> KnownTags = new List<string>();

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("_name");
            if (nameProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "GameplayTag の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, label);
            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var textRect = new Rect(contentRect.x, contentRect.y, contentRect.width - DropdownWidth, contentRect.height);
            var buttonRect = new Rect(textRect.xMax, contentRect.y, DropdownWidth, contentRect.height);

            nameProperty.stringValue = EditorGUI.TextField(textRect, nameProperty.stringValue);

            if (GUI.Button(buttonRect, GUIContent.none, EditorStyles.popup))
            {
                ShowTagMenu(nameProperty);
            }

            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        private static void ShowTagMenu(SerializedProperty nameProperty)
        {
            KnownTags.Clear();
            GameplayTagRegistry.AllTagNames(KnownTags);
            KnownTags.Sort(System.StringComparer.Ordinal);

            var menu = new GenericMenu();
            var serializedObject = nameProperty.serializedObject;
            var path = nameProperty.propertyPath;

            menu.AddItem(new GUIContent("(なし)"), string.IsNullOrEmpty(nameProperty.stringValue), () => Assign(serializedObject, path, string.Empty));

            if (KnownTags.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("登録済みのタグがない（一度実行すると集まる）"));
            }
            else
            {
                menu.AddSeparator(string.Empty);

                foreach (var tagName in KnownTags)
                {
                    var captured = tagName;
                    // '.' 区切りをメニューの階層に写す。
                    menu.AddItem(new GUIContent(tagName.Replace('.', '/')), tagName == nameProperty.stringValue,
                        () => Assign(serializedObject, path, captured));
                }
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject serializedObject, string propertyPath, string value)
        {
            serializedObject.Update();

            var property = serializedObject.FindProperty(propertyPath);
            if (property == null) return;

            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// <see cref="GameplayTagContainer"/> を、タグ名のリストとして描く。
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public sealed class GameplayTagContainerDrawer : PropertyDrawer
    {
        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var names = property.FindPropertyRelative("_tagNames");
            return names == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(names, label, true);
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var names = property.FindPropertyRelative("_tagNames");
            if (names == null)
            {
                EditorGUI.LabelField(position, label.text, "GameplayTagContainer の中身を読めない");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, names, label, true);
            EditorGUI.EndProperty();
        }
    }
}
