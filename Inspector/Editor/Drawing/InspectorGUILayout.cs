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
            public Type Type;
            public bool IsPlaying;
            public string StatePath;
            public readonly List<string> Errors = new List<string>();
            public readonly List<Action> Deferred = new List<Action>();
            public readonly List<PendingValueChange> PendingValueChanges = new List<PendingValueChange>();
            public readonly Dictionary<string, object[]> OwnersByPath = new Dictionary<string, object[]>();
        }

        /// <summary>対象ごとに保存した 1 プロパティの値。</summary>
        internal readonly struct PropertyValueSnapshot
        {
            /// <summary>比較対象とプロパティの現在値を保存する。</summary>
            public PropertyValueSnapshot(UnityEngine.Object target, string propertyPath, bool exists, uint contentHash)
            {
                Target = target;
                PropertyPath = propertyPath;
                Exists = exists;
                ContentHash = contentHash;
            }

            /// <summary>値を持つオブジェクト。</summary>
            public UnityEngine.Object Target { get; }

            /// <summary>比較する保存プロパティのパス。</summary>
            public string PropertyPath { get; }

            /// <summary>保存時にプロパティが存在したか。</summary>
            public bool Exists { get; }

            /// <summary>子要素を含む保存値の識別値。</summary>
            public uint ContentHash { get; }
        }

        /// <summary>値の書き戻し後に変更通知を判定するための情報。</summary>
        private sealed class PendingValueChange
        {
            /// <summary>変更前の対象別の値。</summary>
            public PropertyValueSnapshot[] Before;

            /// <summary>変更されたときに呼ぶメソッド。</summary>
            public OnValueChangedAttribute[] Callbacks;

            /// <summary>Undo 履歴に表示するフィールド名。</summary>
            public string OwnerName;

            /// <summary>変更通知メソッドを持つ入れ子所有者までのパス。</summary>
            public string OwnerPath;

            /// <summary>変更通知メソッドを宣言している所有者の型。</summary>
            public Type OwnerType;
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
                Type = main.GetType(),
                IsPlaying = EditorApplication.isPlaying,
            };

            var layout = InspectorLayoutCache.Get(context.Type, serializedObject);

            serializedObject.Update();

            DrawScriptField(serializedObject);
            DrawItems(context, layout.Root);

            serializedObject.ApplyModifiedProperties();

            // 値を全部書き戻してから差分を確定する。補助ボタンや開閉操作で GUI.changed が立っても、
            // 保存値が同じなら変更通知には含めない。
            QueueValueChangedCallbacks(context);

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
                var statePath = CombinePath(context.StatePath, group.Path);
                var open = InspectorState.GetFoldout(context.Type, statePath, true);
                var next = EditorGUILayout.Foldout(open, group.Name, true);

                if (next != open) InspectorState.SetFoldout(context.Type, statePath, next);
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

            var statePath = CombinePath(context.StatePath, group.Path);
            var selected = Mathf.Clamp(InspectorState.GetTab(context.Type, statePath), 0, pages.Count - 1);
            var picked = GUILayout.Toolbar(selected, names);

            if (picked != selected) InspectorState.SetTab(context.Type, statePath, picked);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawItems(context, pages[picked]);
            }
        }

        private static void DrawMember(DrawContext context, InspectorMember member)
        {
            var owners = ResolveOwners(context, member);
            var primaryOwner = FirstOwner(owners);
            var state = ConditionEvaluator.ResolveAll(owners, member, context.IsPlaying, context.Errors);
            if (!state.Visible) return;

            SerializedProperty property = null;

            if (member.Kind == InspectorMemberKind.SerializedField)
            {
                property = context.SerializedObject.FindProperty(member.PropertyPath);

                if (property == null)
                {
                    Report(context.Errors, member.Name, $"保存されたデータ '{member.PropertyPath}' が見つからない。");
                    return;
                }
            }

            InspectorDecorators.DrawAll(member, owners, DecoratorPosition.Before, property, context.Errors);

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
                            DrawSerializedField(context, member, property, owners);
                            break;

                        case InspectorMemberKind.Method:
                            DrawButton(context, member);
                            break;

                        default:
                            DrawReadOnlyValue(context, member, owners, primaryOwner);
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

            if (state.Mixed)
            {
                EditorGUILayout.HelpBox(
                    "選択対象の表示・編集条件が揃っていないか、入れ子の所有者を取得できないため、この欄は表示だけにしている。選択を分けると確認しやすい。",
                    MessageType.Info);
            }

            InspectorValidators.DrawAll(
                member,
                owners,
                context.Targets,
                property,
                context.Errors,
                allowMutation: !state.Mixed);

            InspectorDecorators.DrawAll(member, owners, DecoratorPosition.After, property, context.Errors);
        }

        private static void DrawSerializedField(
            DrawContext context,
            InspectorMember member,
            SerializedProperty property,
            IReadOnlyList<object> owners)
        {
            var label = BuildLabel(member, property);
            var suffix = member.GetAttribute<SuffixAttribute>();
            var buttons = member.GetAttributes<InlineButtonAttribute>();
            var inline = (suffix != null || buttons.Length > 0) && IsSingleLine(member, property);
            var callbacks = member.GetAttributes<OnValueChangedAttribute>();
            var before = callbacks.Length > 0
                ? CapturePropertyValues(context.Targets, property.propertyPath)
                : null;

            if (member.HasChildren)
            {
                DrawNestedValue(context, member, property, label, suffix, buttons);
            }
            else if (inline)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(member, owners, property, label, context.Errors);

                    if (suffix != null)
                    {
                        GUILayout.Label(suffix.Text, InspectorStyles.Suffix, GUILayout.Width(suffix.Width));
                    }

                    DrawInlineButtons(context, member, buttons);
                }
            }
            else
            {
                DrawValue(member, owners, property, label, context.Errors);

                if (buttons.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth);
                        DrawInlineButtons(context, member, buttons);
                    }
                }
            }

            if (callbacks.Length > 0)
            {
                context.PendingValueChanges.Add(new PendingValueChange
                {
                    Before = before,
                    Callbacks = callbacks,
                    OwnerName = member.Name,
                    OwnerPath = member.OwnerPath,
                    OwnerType = member.Member?.DeclaringType,
                });
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
                var ownerType = member.Member?.DeclaringType ?? context.Type;
                context.Deferred.Add(() => InvokeOnOwners(
                    context.Targets,
                    ownerType,
                    member.OwnerPath,
                    methodName,
                    member.Name,
                    record: true));
            }
        }

        private static void DrawValue(
            InspectorMember member,
            IReadOnlyList<object> owners,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (InspectorFieldDrawers.TryDrawAll(member, owners, property, label, errors)) return;

            EditorGUILayout.PropertyField(property, label, true);
        }

        /// <summary>属性を持つ Serializable の親欄と、その内側の独自レイアウトを描く。</summary>
        private static void DrawNestedValue(
            DrawContext context,
            InspectorMember member,
            SerializedProperty property,
            GUIContent label,
            SuffixAttribute suffix,
            InlineButtonAttribute[] buttons)
        {
            if (suffix != null || buttons.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(property, label, false);

                    if (suffix != null)
                    {
                        GUILayout.Label(suffix.Text, InspectorStyles.Suffix, GUILayout.Width(suffix.Width));
                    }

                    DrawInlineButtons(context, member, buttons);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(property, label, false);
            }

            if (!property.isExpanded) return;

            var layout = member.ChildLayout;
            if (layout == null) return;

            var savedStatePath = context.StatePath;
            context.StatePath = CombinePath(savedStatePath, member.PropertyPath);
            EditorGUI.indentLevel++;
            try
            {
                DrawItems(context, layout.Root);
            }
            finally
            {
                EditorGUI.indentLevel--;
                context.StatePath = savedStatePath;
            }

            for (var i = 0; i < layout.Errors.Count; i++)
            {
                Report(context.Errors, member.Name, layout.Errors[i]);
            }
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
            var ownerType = method.DeclaringType ?? context.Type;
            context.Deferred.Add(() => InvokeOnOwners(
                context.Targets,
                ownerType,
                member.OwnerPath,
                name,
                member.Name,
                record: true));
        }

        /// <summary>
        /// 保存値が実際に変わった対象へ変更通知を予約する。
        /// 補助ボタンや開閉操作だけでは値の識別値が変わらないため、通知しない。
        /// </summary>
        private static void QueueValueChangedCallbacks(DrawContext context)
        {
            for (var i = 0; i < context.PendingValueChanges.Count; i++)
            {
                var pending = context.PendingValueChanges[i];
                var changedTargets = FindChangedTargets(pending.Before);
                if (changedTargets.Length == 0) continue;

                for (var callbackIndex = 0; callbackIndex < pending.Callbacks.Length; callbackIndex++)
                {
                    var methodName = pending.Callbacks[callbackIndex].Method;
                    var ownerName = pending.OwnerName;
                    var ownerType = pending.OwnerType ?? context.Type;
                    var ownerPath = pending.OwnerPath;
                    context.Deferred.Add(() => InvokeOnOwners(
                        changedTargets,
                        ownerType,
                        ownerPath,
                        methodName,
                        ownerName,
                        record: true));
                }
            }
        }

        /// <summary>選択対象ごとに指定プロパティの現在値を保存する。</summary>
        internal static PropertyValueSnapshot[] CapturePropertyValues(UnityEngine.Object[] targets, string propertyPath)
        {
            if (targets == null || targets.Length == 0) return Array.Empty<PropertyValueSnapshot>();

            var snapshots = new PropertyValueSnapshot[targets.Length];

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    snapshots[i] = new PropertyValueSnapshot(null, propertyPath, false, 0u);
                    continue;
                }

                using (var serialized = new SerializedObject(target))
                {
                    serialized.Update();
                    var property = serialized.FindProperty(propertyPath);
                    snapshots[i] = property == null
                        ? new PropertyValueSnapshot(target, propertyPath, false, 0u)
                        : new PropertyValueSnapshot(target, propertyPath, true, property.contentHash);
                }
            }

            return snapshots;
        }

        /// <summary>保存時点から指定プロパティの値が変わった対象だけを返す。</summary>
        internal static UnityEngine.Object[] FindChangedTargets(PropertyValueSnapshot[] before)
        {
            if (before == null || before.Length == 0) return Array.Empty<UnityEngine.Object>();

            var changed = new List<UnityEngine.Object>(before.Length);

            for (var i = 0; i < before.Length; i++)
            {
                var snapshot = before[i];
                if (snapshot.Target == null) continue;

                using (var serialized = new SerializedObject(snapshot.Target))
                {
                    serialized.Update();
                    var property = serialized.FindProperty(snapshot.PropertyPath);
                    var exists = property != null;
                    var contentHash = exists ? property.contentHash : 0u;

                    if (exists != snapshot.Exists || contentHash != snapshot.ContentHash)
                    {
                        changed.Add(snapshot.Target);
                    }
                }
            }

            return changed.ToArray();
        }

        /// <summary>
        /// 選択中の対象にメソッドを呼ぶ。instance メソッドは対象ごと、static メソッドは選択数に関係なく 1 回だけ呼ぶ。
        /// <para>
        /// <paramref name="record"/> が真なら呼ぶ前に <c>Undo</c> に控えを取る。
        /// ボタンは中で何を書き換えるか分からないので、押した結果を取り消せるようにしておく。
        /// </para>
        /// </summary>
        internal static void InvokeOnTargets(UnityEngine.Object[] targets, Type targetType, string methodName, string ownerName, bool record)
        {
            InvokeOnOwners(targets, targetType, null, methodName, ownerName, record);
        }

        /// <summary>
        /// 選択対象の根を Undo に記録し、指定された入れ子所有者へメソッドを流す。
        /// static メソッドは選択数に関係なく 1 回だけ呼ぶ。
        /// </summary>
        internal static void InvokeOnOwners(
            UnityEngine.Object[] targets,
            Type ownerType,
            string ownerPath,
            string methodName,
            string ownerName,
            bool record)
        {
            if (targets == null || targets.Length == 0) return;

            var validTargets = new List<UnityEngine.Object>(targets.Length);
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && !validTargets.Contains(targets[i])) validTargets.Add(targets[i]);
            }

            if (validTargets.Count == 0) return;

            var resolvedType = ownerType ?? validTargets[0].GetType();
            var invocationCount = InspectorOwnerResolver.IsStaticMethod(resolvedType, methodName) ? 1 : validTargets.Count;

            if (record) Undo.RecordObjects(validTargets.ToArray(), ownerName);

            for (var i = 0; i < invocationCount; i++)
            {
                var target = validTargets[i];

                if (!InspectorOwnerResolver.TryInvoke(target, ownerPath, resolvedType, methodName, out var error))
                {
                    Debug.LogError($"[Inspector] {resolvedType.Name}.{ownerName}: {error}", target);
                    continue;
                }

                EditorUtility.SetDirty(target);
            }
        }

        private static void DrawReadOnlyValue(
            DrawContext context,
            InspectorMember member,
            IReadOnlyList<object> owners,
            object owner)
        {
            if (!MemberResolver.TryGetValue(owner, member.Name, out var value, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            var label = BuildLabel(member, null);
            var suffix = member.GetAttribute<SuffixAttribute>();
            var buttons = member.GetAttributes<InlineButtonAttribute>();
            var mixed = ReadOnlyValuesDiffer(member, owners, value);

            if (suffix != null || buttons.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawStaticValue(label, value, mixed);
                    }

                    if (suffix != null)
                    {
                        GUILayout.Label(suffix.Text, InspectorStyles.Suffix, GUILayout.Width(suffix.Width));
                    }

                    DrawInlineButtons(context, member, buttons);
                }

                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                DrawStaticValue(label, value, mixed);
            }
        }

        /// <summary>複数選択中の読み取り専用値が同じかを、全所有者から判定する。</summary>
        internal static bool ReadOnlyValuesDiffer(InspectorMember member, IReadOnlyList<object> owners, object firstValue)
        {
            if (owners == null || owners.Count <= 1) return false;

            for (var i = 1; i < owners.Count; i++)
            {
                if (!MemberResolver.TryGetValue(owners[i], member.Name, out var value, out _)) return true;
                if (!Equals(firstValue, value)) return true;
            }

            return false;
        }

        /// <summary>
        /// 保存対象ではない値を、型に合った欄で表示だけする。
        /// 見慣れた形で出したほうが桁や成分を読み違えないので、素の文字列にはしない。
        /// </summary>
        private static void DrawStaticValue(GUIContent label, object value, bool mixed = false)
        {
            if (mixed)
            {
                EditorGUILayout.LabelField(label, new GUIContent("—"));
                return;
            }

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

        /// <summary>各選択対象から、属性を解決する所有者を同じ順で取り出す。</summary>
        private static object[] ResolveOwners(DrawContext context, InspectorMember member)
        {
            var key = member.OwnerPath ?? string.Empty;
            if (context.OwnersByPath.TryGetValue(key, out var cached)) return cached;

            var targets = context.Targets;
            var owners = new object[targets.Length];

            for (var i = 0; i < targets.Length; i++)
            {
                if (InspectorOwnerResolver.TryGet(targets[i], member.OwnerPath, out owners[i], out var error)) continue;

                Report(context.Errors, member.Name, error);
            }

            context.OwnersByPath[key] = owners;
            return owners;
        }

        /// <summary>描画補助へ渡す、最初に解決できた所有者を返す。</summary>
        private static object FirstOwner(IReadOnlyList<object> owners)
        {
            if (owners == null) return null;

            for (var i = 0; i < owners.Count; i++)
            {
                if (owners[i] != null) return owners[i];
            }

            return null;
        }

        /// <summary>同じ設定ミスを 1 回だけ記録する。</summary>
        private static void Report(List<string> errors, string ownerName, string message)
        {
            if (errors == null || string.IsNullOrEmpty(message)) return;

            var text = string.IsNullOrEmpty(ownerName) ? message : ownerName + ": " + message;
            if (!errors.Contains(text)) errors.Add(text);
        }

        /// <summary>入れ子ごとに重ならない Inspector 状態キーを作る。</summary>
        private static string CombinePath(string parent, string child)
        {
            if (string.IsNullOrEmpty(parent)) return child ?? string.Empty;
            if (string.IsNullOrEmpty(child)) return parent;

            return parent + "/" + child;
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
