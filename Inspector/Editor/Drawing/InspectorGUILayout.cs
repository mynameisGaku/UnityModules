using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>
    /// 属性を解釈して Inspector 全体を描く。
    /// <para>
    /// <see cref="InspectorEditor"/> から呼ばれるが、独自の <c>CustomEditor</c> を持つ型からも
    /// <see cref="Draw(SerializedObject, UnityEngine.Object[])"/> を呼べば同じ見た目にできる。
    /// </para>
    /// </summary>
    public static class InspectorGUILayout
    {
        private sealed class DrawContext
        {
            public SerializedObject SerializedObject;
            public UnityEngine.Object[] Targets;
            public object Target;
            public Type Type;
            public bool IsPlaying;
            public readonly List<string> Errors = new List<string>();
            public readonly List<Action> Deferred = new List<Action>();
        }

        /// <summary>
        /// 属性を反映した Inspector を描く。
        /// <para>
        /// <c>Update</c> と <c>ApplyModifiedProperties</c> はこの中で行う。
        /// </para>
        /// </summary>
        /// <param name="serializedObject">描く対象。</param>
        /// <param name="targets">選択中のオブジェクト。ボタンや変更通知を全部に流すのに使う。</param>
        public static void Draw(SerializedObject serializedObject, UnityEngine.Object[] targets)
        {
            if (serializedObject == null) return;

            var main = serializedObject.targetObject;
            if (main == null)
            {
                EditorGUILayout.HelpBox("対象が失われている。", MessageType.Warning);
                return;
            }

            var context = new DrawContext
            {
                SerializedObject = serializedObject,
                Targets = targets != null && targets.Length > 0 ? targets : new[] { main },
                Target = main,
                Type = main.GetType(),
                IsPlaying = EditorApplication.isPlaying,
            };

            var layout = InspectorLayoutCache.Get(context.Type, serializedObject);

            serializedObject.Update();

            DrawScriptField(serializedObject);
            DrawItems(context, layout.Root);

            serializedObject.ApplyModifiedProperties();

            // 値を書き戻したあとに呼ぶ。ここより前だと、コールバックが読むフィールドが
            // まだ編集前の値のままになる。
            for (var i = 0; i < context.Deferred.Count; i++) context.Deferred[i].Invoke();

            DrawMessages(layout.Errors);
            DrawMessages(context.Errors);
        }

        private static void DrawScriptField(SerializedObject serializedObject)
        {
            var script = serializedObject.FindProperty("m_Script");
            if (script == null) return;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(script);
            }
        }

        private static void DrawMessages(IReadOnlyList<string> messages)
        {
            if (messages == null) return;

            for (var i = 0; i < messages.Count; i++)
            {
                EditorGUILayout.HelpBox(messages[i], MessageType.Error);
            }
        }

        private static void DrawItems(DrawContext context, InspectorGroup group)
        {
            var items = group.Items;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsGroup) DrawGroup(context, items[i].Group);
                else DrawMember(context, items[i].Member);
            }
        }

        private static void DrawGroup(DrawContext context, InspectorGroup group)
        {
            switch (group.Kind)
            {
                case GroupKind.Box:
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(group.Name, InspectorStyles.BoxHeader);
                        DrawItems(context, group);
                    }

                    break;

                case GroupKind.Horizontal:
                    DrawHorizontal(context, group);
                    break;

                case GroupKind.Tabs:
                    DrawTabs(context, group);
                    break;

                default:
                    DrawFoldout(context, group);
                    break;
            }
        }

        private static void DrawFoldout(DrawContext context, InspectorGroup group)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var open = InspectorState.GetFoldout(context.Type, group.Path, true);
                var next = EditorGUILayout.Foldout(open, group.Name, true);

                if (next != open) InspectorState.SetFoldout(context.Type, group.Path, next);
                if (!next) return;

                EditorGUI.indentLevel++;
                DrawItems(context, group);
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawHorizontal(DrawContext context, InspectorGroup group)
        {
            var showLabel = (group.Source as HorizontalGroupAttribute)?.ShowLabel ?? false;
            if (showLabel) EditorGUILayout.LabelField(group.Name, EditorStyles.miniBoldLabel);

            var savedLabelWidth = EditorGUIUtility.labelWidth;
            var savedIndent = EditorGUI.indentLevel;

            // 横に並べると 1 件あたりの幅が狭くなる。ラベル幅をそのままにすると
            // 値の欄が潰れて読めなくなるので、件数に応じて詰める。
            var count = Mathf.Max(1, group.Items.Count);
            EditorGUIUtility.labelWidth = Mathf.Max(38f, savedLabelWidth / count);
            EditorGUI.indentLevel = 0;

            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawItems(context, group);
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = savedLabelWidth;
                EditorGUI.indentLevel = savedIndent;
            }
        }

        private static void DrawTabs(DrawContext context, InspectorGroup group)
        {
            var pages = new List<InspectorGroup>();

            for (var i = 0; i < group.Items.Count; i++)
            {
                var item = group.Items[i];

                if (item.IsGroup && item.Group.Kind == GroupKind.TabPage) pages.Add(item.Group);
                else if (item.IsGroup) DrawGroup(context, item.Group);
                else DrawMember(context, item.Member);
            }

            if (pages.Count == 0) return;

            var names = new string[pages.Count];
            for (var i = 0; i < pages.Count; i++) names[i] = pages[i].Name;

            var selected = Mathf.Clamp(InspectorState.GetTab(context.Type, group.Path), 0, pages.Count - 1);
            var picked = GUILayout.Toolbar(selected, names);

            if (picked != selected) InspectorState.SetTab(context.Type, group.Path, picked);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawItems(context, pages[picked]);
            }
        }

        private static void DrawMember(DrawContext context, InspectorMember member)
        {
            var state = ConditionEvaluator.Resolve(context.Target, member, context.IsPlaying, context.Errors);
            if (!state.Visible) return;

            SerializedProperty property = null;

            if (member.Kind == InspectorMemberKind.SerializedField)
            {
                property = context.SerializedObject.FindProperty(member.Name);

                if (property == null)
                {
                    context.Errors.Add($"{member.Name}: 保存されたデータが見つからない。");
                    return;
                }
            }

            InspectorDecorators.Draw(member, context.Target, DecoratorPosition.Before, property, context.Errors);

            var indent = member.GetAttribute<IndentAttribute>();
            var color = member.GetAttribute<GUIColorAttribute>();
            var labelWidth = member.GetAttribute<LabelWidthAttribute>();

            var savedColor = GUI.color;
            var savedLabelWidth = EditorGUIUtility.labelWidth;

            if (color != null) GUI.color = color.Resolve(savedColor);
            if (labelWidth != null) EditorGUIUtility.labelWidth = labelWidth.Width;
            if (indent != null) EditorGUI.indentLevel += indent.Levels;

            try
            {
                using (new EditorGUI.DisabledScope(!state.Enabled))
                {
                    switch (member.Kind)
                    {
                        case InspectorMemberKind.SerializedField:
                            DrawSerializedField(context, member, property);
                            break;

                        case InspectorMemberKind.Method:
                            DrawButton(context, member);
                            break;

                        default:
                            DrawReadOnlyValue(context, member);
                            break;
                    }
                }
            }
            finally
            {
                if (indent != null) EditorGUI.indentLevel -= indent.Levels;
                EditorGUIUtility.labelWidth = savedLabelWidth;
                GUI.color = savedColor;
            }

            if (property != null) InspectorValidators.Draw(member, context.Target, property, context.Errors);

            InspectorDecorators.Draw(member, context.Target, DecoratorPosition.After, property, context.Errors);
        }

        private static void DrawSerializedField(DrawContext context, InspectorMember member, SerializedProperty property)
        {
            var label = BuildLabel(member, property);
            var suffix = member.GetAttribute<SuffixAttribute>();
            var buttons = member.GetAttributes<InlineButtonAttribute>();
            var inline = (suffix != null || buttons.Length > 0) && IsSingleLine(member, property);

            EditorGUI.BeginChangeCheck();

            if (inline)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(context, member, property, label);

                    if (suffix != null)
                    {
                        GUILayout.Label(suffix.Text, InspectorStyles.Suffix, GUILayout.Width(suffix.Width));
                    }

                    DrawInlineButtons(context, member, buttons);
                }
            }
            else
            {
                DrawValue(context, member, property, label);

                if (buttons.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth);
                        DrawInlineButtons(context, member, buttons);
                    }
                }
            }

            if (!EditorGUI.EndChangeCheck()) return;

            var callbacks = member.GetAttributes<OnValueChangedAttribute>();
            for (var i = 0; i < callbacks.Length; i++)
            {
                var methodName = callbacks[i].Method;
                context.Deferred.Add(() => InvokeOnTargets(context, methodName, member.Name, record: false));
            }
        }

        private static void DrawInlineButtons(DrawContext context, InspectorMember member, InlineButtonAttribute[] buttons)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var text = string.IsNullOrEmpty(button.Label)
                    ? ObjectNames.NicifyVariableName(button.Method)
                    : button.Label;

                if (!GUILayout.Button(text, InspectorStyles.InlineButton, GUILayout.Width(button.Width))) continue;

                var methodName = button.Method;
                context.Deferred.Add(() => InvokeOnTargets(context, methodName, member.Name, record: true));
            }
        }

        private static void DrawValue(DrawContext context, InspectorMember member, SerializedProperty property, GUIContent label)
        {
            if (InspectorFieldDrawers.TryDraw(member, context.Target, property, label, context.Errors)) return;

            EditorGUILayout.PropertyField(property, label, true);
        }

        private static void DrawButton(DrawContext context, InspectorMember member)
        {
            if (!(member.Member is MethodInfo method)) return;

            var attribute = member.GetAttribute<ButtonAttribute>();
            if (attribute == null) return;

            if (method.GetParameters().Length != 0)
            {
                context.Errors.Add($"{member.Name}: [Button] を付けられるのは引数なしのメソッドだけ。");
                return;
            }

            var label = string.IsNullOrEmpty(attribute.Label)
                ? ObjectNames.NicifyVariableName(method.Name)
                : attribute.Label;

            var pressable = attribute.EnableMode == ButtonEnableMode.Always
                || (attribute.EnableMode == ButtonEnableMode.PlayMode) == context.IsPlaying;

            using (new EditorGUI.DisabledScope(!pressable))
            {
                if (!GUILayout.Button(label, GUILayout.Height(attribute.Height))) return;
            }

            var name = method.Name;
            context.Deferred.Add(() => InvokeOnTargets(context, name, member.Name, record: true));
        }

        /// <summary>
        /// 選択中の全オブジェクトに対してメソッドを呼ぶ。
        /// <para>
        /// <paramref name="record"/> が真なら呼ぶ前に <c>Undo</c> に控えを取る。
        /// ボタンは中で何を書き換えるか分からないので、押した結果を取り消せるようにしておく。
        /// </para>
        /// </summary>
        private static void InvokeOnTargets(DrawContext context, string methodName, string ownerName, bool record)
        {
            if (record) Undo.RecordObjects(context.Targets, ownerName);

            for (var i = 0; i < context.Targets.Length; i++)
            {
                var target = context.Targets[i];
                if (target == null) continue;

                if (!MemberResolver.TryInvoke(target, methodName, out var error))
                {
                    Debug.LogError($"[Inspector] {context.Type.Name}.{ownerName}: {error}", target);
                    continue;
                }

                EditorUtility.SetDirty(target);
            }
        }

        private static void DrawReadOnlyValue(DrawContext context, InspectorMember member)
        {
            if (!MemberResolver.TryGetValue(context.Target, member.Name, out var value, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            var label = BuildLabel(member, null);

            using (new EditorGUI.DisabledScope(true))
            {
                DrawStaticValue(label, value);
            }
        }

        /// <summary>
        /// 保存対象ではない値を、型に合った欄で表示だけする。
        /// 見慣れた形で出したほうが桁や成分を読み違えないので、素の文字列にはしない。
        /// </summary>
        private static void DrawStaticValue(GUIContent label, object value)
        {
            switch (value)
            {
                case null:
                    EditorGUILayout.LabelField(label, new GUIContent("null"));
                    break;

                case bool flag:
                    EditorGUILayout.Toggle(label, flag);
                    break;

                case int number:
                    EditorGUILayout.IntField(label, number);
                    break;

                case long number:
                    EditorGUILayout.LongField(label, number);
                    break;

                case float number:
                    EditorGUILayout.FloatField(label, number);
                    break;

                case double number:
                    EditorGUILayout.DoubleField(label, number);
                    break;

                case string text:
                    EditorGUILayout.TextField(label, text);
                    break;

                case Vector2 vector:
                    EditorGUILayout.Vector2Field(label, vector);
                    break;

                case Vector3 vector:
                    EditorGUILayout.Vector3Field(label, vector);
                    break;

                case Vector4 vector:
                    EditorGUILayout.Vector4Field(label, vector);
                    break;

                case Vector2Int vector:
                    EditorGUILayout.Vector2IntField(label, vector);
                    break;

                case Vector3Int vector:
                    EditorGUILayout.Vector3IntField(label, vector);
                    break;

                case Color color:
                    EditorGUILayout.ColorField(label, color);
                    break;

                case Quaternion rotation:
                    EditorGUILayout.Vector3Field(label, rotation.eulerAngles);
                    break;

                case Rect rect:
                    EditorGUILayout.RectField(label, rect);
                    break;

                case Bounds bounds:
                    EditorGUILayout.BoundsField(label, bounds);
                    break;

                case AnimationCurve curve:
                    EditorGUILayout.CurveField(label, curve);
                    break;

                case Enum enumValue:
                    EditorGUILayout.EnumPopup(label, enumValue);
                    break;

                case UnityEngine.Object reference:
                    EditorGUILayout.ObjectField(label, reference, reference.GetType(), true);
                    break;

                default:
                    EditorGUILayout.LabelField(label, new GUIContent(value.ToString()));
                    break;
            }
        }

        private static GUIContent BuildLabel(InspectorMember member, SerializedProperty property)
        {
            if (member.HasAttribute<HideLabelAttribute>()) return GUIContent.none;

            var custom = member.GetAttribute<LabelTextAttribute>();
            if (custom != null) return new GUIContent(custom.Text, custom.Tooltip);

            if (property != null) return new GUIContent(property.displayName, property.tooltip);

            return new GUIContent(ObjectNames.NicifyVariableName(member.Name));
        }

        /// <summary>
        /// 単位やボタンを横に並べてよい高さか。
        /// 折りたためる欄や複数行の欄を 1 行に混ぜると、並べたものが上端に取り残されて読めなくなる。
        /// </summary>
        private static bool IsSingleLine(InspectorMember member, SerializedProperty property)
        {
            switch (member.GetAttribute<FieldDrawerAttribute>())
            {
                case null:
                    break;

                case ResizableTextAreaAttribute _:
                case ExpandableAttribute _:
                case ProgressBarAttribute _:
                case FilePathAttribute _:
                case FolderPathAttribute _:
                    return false;

                default:
                    return true;
            }

            if (property.hasVisibleChildren && property.isExpanded) return false;

            return EditorGUI.GetPropertyHeight(property, GUIContent.none, true) <= EditorGUIUtility.singleLineHeight + 0.5f;
        }
    }
}
