using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>
    /// <see cref="FieldDrawerAttribute"/> が付いたフィールドを、専用の見た目で描く。
    /// <para>
    /// どれも当てはまらなければ何もせず <c>false</c> を返し、通常の描画に任せる。
    /// </para>
    /// </summary>
    internal static class InspectorFieldDrawers
    {
        /// <summary>1 対象から得た Dropdown の表示名と保存値。</summary>
        private sealed class DropdownOptions
        {
            internal readonly List<string> Labels = new List<string>();
            internal readonly List<object> Values = new List<object>();
        }

        // 参照先を開いて描いている最中のオブジェクト。自分自身を参照する資産で無限に潜らないための番人。
        private static readonly HashSet<EntityId> Expanding = new HashSet<EntityId>();
        private static readonly Dictionary<EntityId, SerializedObject> NestedObjects =
            new Dictionary<EntityId, SerializedObject>();
        private static readonly ConditionalWeakTable<UnityEngine.Object, HashSet<string>> InitializedExpandableProperties =
            new ConditionalWeakTable<UnityEngine.Object, HashSet<string>>();

        /// <summary>専用の描画があれば描いて <c>true</c> を返す。</summary>
        internal static bool TryDraw(
            InspectorMember member,
            object target,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            return TryDrawAll(member, new[] { target }, property, label, errors);
        }

        /// <summary>複数選択した全所有者を考慮して専用描画する。</summary>
        internal static bool TryDrawAll(
            InspectorMember member,
            IReadOnlyList<object> targets,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (property == null) return false;

            var target = FirstTarget(targets);

            var attribute = member.GetAttribute<FieldDrawerAttribute>();
            if (attribute == null) return false;

            var all = member.GetAttributes<FieldDrawerAttribute>();
            if (all.Length > 1)
            {
                Report(errors, member.Name,
                    $"値の描き方を決める属性が {all.Length} 個付いている。1 つだけにする（今は {all[0].GetType().Name} を使った）。");
            }

            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            try
            {
                switch (attribute)
                {
                    case DropdownAttribute dropdown:
                        return DrawDropdown(dropdown, member, targets, property, label, errors);

                    case TagAttribute _:
                        return DrawTag(member, property, label, errors);

                    case LayerAttribute _:
                        return DrawLayer(member, property, label, errors);

                    case SceneAttribute _:
                        return DrawScene(member, property, label, errors);

                    case SortingLayerAttribute _:
                        return DrawSortingLayer(member, property, label, errors);

                    case ProgressBarAttribute bar:
                        return DrawProgressBar(bar, member, target, property, label, errors);

                    case ExpandableAttribute expandable:
                        return DrawExpandable(expandable, member, property, label, errors);

                    case ResizableTextAreaAttribute textArea:
                        return DrawResizableTextArea(textArea, member, property, label, errors);

                    case FilePathAttribute filePath:
                        return DrawPath(member, property, label, errors, filePath.Title, filePath.Extension, filePath.RelativeToProject, folder: false);

                    case FolderPathAttribute folderPath:
                        return DrawPath(member, property, label, errors, folderPath.Title, null, folderPath.RelativeToProject, folder: true);

                    default:
                        return false;
                }
            }
            finally
            {
                EditorGUI.showMixedValue = mixed;
            }
        }

        private static bool DrawDropdown(
            DropdownAttribute attribute,
            InspectorMember member,
            IReadOnlyList<object> targets,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (!TryCollectAllDropdownOptions(
                    targets,
                    attribute.ValuesMember,
                    out var options,
                    out var candidatesMatch,
                    out var error))
            {
                Report(errors, member.Name, error);
                return false;
            }

            if (!string.IsNullOrEmpty(error)) Report(errors, member.Name, error);

            var labels = options.Labels;
            var values = options.Values;

            object current = null;
            try
            {
                current = property.boxedValue;
            }
            catch (Exception)
            {
                // 箱に入れられない形の値。この後の一致判定で「範囲外」として扱う。
            }

            var index = -1;
            for (var i = 0; i < values.Count; i++)
            {
                if (!ConditionEvaluator.AreEqual(current, values[i])) continue;

                index = i;
                break;
            }

            if (index < 0)
            {
                // 勝手に候補の先頭へ寄せると、保存済みの値が開いただけで書き換わる。
                // 今の値をそのまま見せて、選び直すかどうかは人に任せる。
                labels.Insert(0, $"(候補に無い) {current ?? "null"}");
                values.Insert(0, current);
                index = 0;
            }

            var contents = new GUIContent[labels.Count];
            for (var i = 0; i < labels.Count; i++) contents[i] = new GUIContent(labels[i]);

            using (new EditorGUI.DisabledScope(!candidatesMatch))
            {
                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, index), contents);

                if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count)
                {
                    try
                    {
                        property.boxedValue = values[picked];
                    }
                    catch (Exception exception)
                    {
                        Report(errors, member.Name, $"選んだ値を書き込めない: {exception.Message}");
                    }
                }
            }

            if (!candidatesMatch)
            {
                EditorGUILayout.HelpBox(
                    "複数選択した対象で Dropdown の候補が異なるため、この欄は表示だけにしている。選択を分けると編集できる。",
                    MessageType.Info);
            }

            return true;
        }

        /// <summary>複数対象から得る Dropdown 候補の表示名・値・順番が全て同じか。</summary>
        internal static bool DropdownOptionsMatch(
            IReadOnlyList<object> targets,
            string valuesMember,
            out string error)
        {
            return TryCollectAllDropdownOptions(targets, valuesMember, out _, out var match, out error) && match;
        }

        private static bool TryCollectAllDropdownOptions(
            IReadOnlyList<object> targets,
            string valuesMember,
            out DropdownOptions first,
            out bool match,
            out string error)
        {
            first = null;
            match = true;
            error = null;

            if (targets == null || targets.Count == 0)
            {
                error = "Dropdown の候補を取得する対象が無い。";
                return false;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (!TryCollectDropdownOptions(targets[i], valuesMember, out var current, out var currentError))
                {
                    match = false;
                    if (error == null) error = $"選択対象 {i + 1}: {currentError}";
                    continue;
                }

                if (first == null)
                {
                    first = current;
                    continue;
                }

                if (!SameDropdownOptions(first, current)) match = false;
            }

            return first != null;
        }

        private static bool TryCollectDropdownOptions(
            object target,
            string valuesMember,
            out DropdownOptions options,
            out string error)
        {
            options = null;
            error = null;

            if (!MemberResolver.TryGetValue(target, valuesMember, out var source, out error)) return false;

            var collected = new DropdownOptions();
            switch (source)
            {
                case IDropdownList named:
                    foreach (var entry in named)
                    {
                        collected.Labels.Add(entry.Key);
                        collected.Values.Add(entry.Value);
                    }

                    break;

                case IEnumerable sequence when !(source is string):
                    foreach (var value in sequence)
                    {
                        collected.Labels.Add(value?.ToString() ?? "null");
                        collected.Values.Add(value);
                    }

                    break;

                default:
                    error = $"'{valuesMember}' が候補の並びを返していない。IEnumerable<T> か DropdownList<T> を返すようにする。";
                    return false;
            }

            options = collected;
            return true;
        }

        private static bool SameDropdownOptions(DropdownOptions left, DropdownOptions right)
        {
            if (left.Labels.Count != right.Labels.Count || left.Values.Count != right.Values.Count) return false;

            for (var i = 0; i < left.Labels.Count; i++)
            {
                if (!string.Equals(left.Labels[i], right.Labels[i], StringComparison.Ordinal)) return false;
                if (!ConditionEvaluator.AreEqual(left.Values[i], right.Values[i])) return false;
            }

            return true;
        }

        private static bool DrawTag(InspectorMember member, SerializedProperty property, GUIContent label, List<string> errors)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                Report(errors, member.Name, "[Tag] は string のフィールドに付ける。");
                return false;
            }

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.TagField(label, property.stringValue);
            if (EditorGUI.EndChangeCheck()) property.stringValue = picked;

            return true;
        }

        private static bool DrawLayer(InspectorMember member, SerializedProperty property, GUIContent label, List<string> errors)
        {
            var names = new List<string>();
            var indices = new List<int>();

            for (var layerIndex = 0; layerIndex < 32; layerIndex++)
            {
                var layerName = LayerMask.LayerToName(layerIndex);
                if (string.IsNullOrEmpty(layerName)) continue;

                names.Add(layerName);
                indices.Add(layerIndex);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                {
                    var current = property.intValue;
                    var labels = new List<string>(names);
                    var values = new List<int>(indices);
                    var selected = ResolveIntSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(labels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(labels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.intValue = values[picked];

                    return true;
                }

                case SerializedPropertyType.String:
                {
                    var current = property.stringValue;
                    var labels = new List<string>(names);
                    var values = new List<string>(names);
                    var selected = ResolveStringSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(labels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(labels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.stringValue = values[picked];

                    return true;
                }

                default:
                    Report(errors, member.Name, "[Layer] は int か string のフィールドに付ける。");
                    return false;
            }
        }

        private static bool DrawScene(InspectorMember member, SerializedProperty property, GUIContent label, List<string> errors)
        {
            var scenes = EditorBuildSettings.scenes;

            var labels = new List<string> { "(未設定)" };
            var names = new List<string> { string.Empty };
            var indices = new List<int> { -1 };

            for (var i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;

                var sceneName = Path.GetFileNameWithoutExtension(scenes[i].path);
                labels.Add(sceneName);
                names.Add(sceneName);
                indices.Add(ResolveEnabledSceneBuildIndex(scenes, i));
            }

            if (labels.Count == 1)
            {
                EditorGUILayout.HelpBox("Build Settings にシーンが 1 つも登録されていない。", MessageType.Warning);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    var current = property.stringValue;
                    var displayedLabels = new List<string>(labels);
                    var values = new List<string>(names);
                    var selected = ResolveStringSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(displayedLabels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(displayedLabels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.stringValue = values[picked];

                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    var current = property.intValue;
                    var displayedLabels = new List<string>(labels);
                    var values = new List<int>(indices);
                    var selected = ResolveIntSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(displayedLabels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(displayedLabels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.intValue = values[picked];

                    return true;
                }

                default:
                    Report(errors, member.Name, "[Scene] は string（シーン名）か int（ビルド順の番号）のフィールドに付ける。");
                    return false;
            }
        }

        /// <summary>
        /// Build Settings 上の位置から、無効なシーンを除外した実際の buildIndex を求める。
        /// </summary>
        private static int ResolveEnabledSceneBuildIndex(EditorBuildSettingsScene[] scenes, int settingsIndex)
        {
            var buildIndex = 0;
            for (var i = 0; i < settingsIndex; i++)
            {
                if (scenes[i].enabled) buildIndex++;
            }

            return buildIndex;
        }

        private static bool DrawSortingLayer(InspectorMember member, SerializedProperty property, GUIContent label, List<string> errors)
        {
            var layers = SortingLayer.layers;
            var names = new List<string>(layers.Length);
            var ids = new List<int>(layers.Length);

            for (var i = 0; i < layers.Length; i++)
            {
                names.Add(layers[i].name);
                ids.Add(layers[i].id);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    var current = property.stringValue;
                    var labels = new List<string>(names);
                    var values = new List<string>(names);
                    var selected = ResolveStringSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(labels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(labels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.stringValue = values[picked];

                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    var current = property.intValue;
                    var labels = new List<string>(names);
                    var values = new List<int>(ids);
                    var selected = ResolveIntSelection(values, current);
                    if (selected < 0) selected = InsertMissingOption(labels, values, current);

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, ResolveDisplayedPopupIndex(property.hasMultipleDifferentValues, selected), ToContents(labels));
                    if (EditorGUI.EndChangeCheck() && picked >= 0 && picked < values.Count) property.intValue = values[picked];

                    return true;
                }

                default:
                    Report(errors, member.Name, "[SortingLayer] は string（名前）か int（ID）のフィールドに付ける。");
                    return false;
            }
        }

        /// <summary>複数の保存値が異なるときは、先頭対象の値ではなく未選択表示を返す。</summary>
        private static int ResolveDisplayedPopupIndex(bool hasMultipleDifferentValues, int selected)
        {
            return hasMultipleDifferentValues ? -1 : selected;
        }

        /// <summary>文字列の保存値が候補にある位置を、大文字と小文字を区別して返す。</summary>
        private static int ResolveStringSelection(IReadOnlyList<string> values, string current)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], current, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        /// <summary>整数の保存値が候補にある位置を返す。</summary>
        private static int ResolveIntSelection(IReadOnlyList<int> values, int current)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == current) return i;
            }

            return -1;
        }

        /// <summary>候補外の保存値を先頭へ足し、誤って通常候補に見えない表示位置を返す。</summary>
        private static int InsertMissingOption<T>(List<string> labels, List<T> values, T current)
        {
            labels.Insert(0, $"(候補に無い) {FormatMissingValue(current)}");
            values.Insert(0, current);
            return 0;
        }

        /// <summary>候補外の保存値を、人が識別できる文字列へ変換する。</summary>
        private static string FormatMissingValue<T>(T value)
        {
            return ReferenceEquals(value, null) ? "null" : value.ToString();
        }

        /// <summary>候補名を Popup へ渡す表示要素へ変換する。</summary>
        private static GUIContent[] ToContents(IReadOnlyList<string> labels)
        {
            var contents = new GUIContent[labels.Count];
            for (var i = 0; i < labels.Count; i++) contents[i] = new GUIContent(labels[i]);

            return contents;
        }

        private static bool DrawProgressBar(
            ProgressBarAttribute attribute,
            InspectorMember member,
            object target,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            float value;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    value = property.intValue;
                    break;

                case SerializedPropertyType.Float:
                    value = property.floatValue;
                    break;

                default:
                    Report(errors, member.Name, "[ProgressBar] は int か float のフィールドに付ける。");
                    return false;
            }

            var max = attribute.Max;

            if (!string.IsNullOrEmpty(attribute.MaxMember))
            {
                if (MemberResolver.TryGetValue(target, attribute.MaxMember, out var raw, out var error))
                {
                    try
                    {
                        max = Convert.ToSingle(raw);
                    }
                    catch (Exception)
                    {
                        Report(errors, member.Name, $"'{attribute.MaxMember}' が数値ではない。");
                    }
                }
                else
                {
                    Report(errors, member.Name, error);
                }
            }

            var text = string.IsNullOrEmpty(attribute.Label) ? label.text : attribute.Label;
            var rect = EditorGUILayout.GetControlRect(true, attribute.Height);
            var barRect = EditorGUI.PrefixLabel(rect, new GUIContent(text, label.tooltip));

            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.16f, 0.16f, 1f));

            var ratio = Mathf.Approximately(max, 0f) ? 0f : Mathf.Clamp01(value / max);
            if (ratio > 0f)
            {
                var fill = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
                EditorGUI.DrawRect(fill, attribute.Color.ToColor(Color.cyan));
            }

            if (attribute.ShowValue)
            {
                var caption = property.hasMultipleDifferentValues ? "—" : $"{value:0.##} / {max:0.##}";
                GUI.Label(barRect, caption, InspectorStyles.CenteredLabel);
            }

            return true;
        }

        private static bool DrawExpandable(
            ExpandableAttribute attribute,
            InspectorMember member,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                Report(errors, member.Name, "[Expandable] は ScriptableObject などの参照フィールドに付ける。");
                return false;
            }

            EditorGUILayout.PropertyField(property, label);

            if (!CanExpandReference(property.hasMultipleDifferentValues))
            {
                EditorGUILayout.HelpBox(
                    "複数選択した対象で参照先が異なるため、中身は展開しない。選択を分けると編集できる。",
                    MessageType.Info);
                return true;
            }

            ApplyExpandableInitialState(attribute, property);

            var referenced = property.objectReferenceValue;
            if (referenced == null) return true;

            var id = referenced.GetEntityId();
            if (Expanding.Contains(id))
            {
                EditorGUILayout.HelpBox("参照が自分自身に戻ってきている。ここでは開かない。", MessageType.Warning);
                return true;
            }

            EditorGUI.indentLevel++;
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, "中身", true);

            if (property.isExpanded)
            {
                Expanding.Add(id);

                try
                {
                    DrawNested(referenced);
                }
                finally
                {
                    Expanding.Remove(id);
                }
            }

            EditorGUI.indentLevel--;
            return true;
        }

        /// <summary>参照先が 1 件に定まるときだけ、その中身を展開する。</summary>
        internal static bool CanExpandReference(bool hasMultipleDifferentValues)
        {
            return !hasMultipleDifferentValues;
        }

        /// <summary>対象とプロパティごとに一度だけ、属性で指定された初期開閉状態を反映する。</summary>
        internal static void ApplyExpandableInitialState(ExpandableAttribute attribute, SerializedProperty property)
        {
            var path = property.propertyPath;
            var targets = property.serializedObject.targetObjects;
            var shouldInitialize = true;

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null) continue;

                var initialized = InitializedExpandableProperties.GetOrCreateValue(target);
                if (initialized.Contains(path)) shouldInitialize = false;
            }

            if (shouldInitialize) property.isExpanded = attribute.Expanded;

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null) continue;
                InitializedExpandableProperties.GetOrCreateValue(target).Add(path);
            }
        }

        private static void DrawNested(UnityEngine.Object referenced)
        {
            var nested = GetNestedSerializedObject(referenced);
            nested.Update();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var iterator = nested.GetIterator();

                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal)) continue;

                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    while (iterator.NextVisible(false));
                }
            }

            nested.ApplyModifiedProperties();
        }

        /// <summary>
        /// 開いて描く相手ごとに <see cref="SerializedObject"/> を作り置きする。
        /// 毎フレーム作り直すと、その参照ぶんだけ確保が積み上がるため。
        /// </summary>
        private static SerializedObject GetNestedSerializedObject(UnityEngine.Object referenced)
        {
            var id = referenced.GetEntityId();

            if (NestedObjects.TryGetValue(id, out var cached) && cached != null && cached.targetObject != null)
            {
                return cached;
            }

            var created = new SerializedObject(referenced);
            NestedObjects[id] = created;
            return created;
        }

        private static bool DrawResizableTextArea(
            ResizableTextAreaAttribute attribute,
            InspectorMember member,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                Report(errors, member.Name, "[ResizableTextArea] は string のフィールドに付ける。");
                return false;
            }

            EditorGUILayout.LabelField(label);

            var text = property.stringValue ?? string.Empty;
            var lines = 1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lines++;
            }

            lines = Mathf.Clamp(lines + 1, Mathf.Max(1, attribute.MinLines), Mathf.Max(1, attribute.MaxLines));
            var height = EditorGUIUtility.singleLineHeight * lines;

            EditorGUI.indentLevel++;
            var rect = EditorGUILayout.GetControlRect(false, height);
            rect = EditorGUI.IndentedRect(rect);
            EditorGUI.indentLevel--;

            EditorGUI.BeginChangeCheck();
            var edited = EditorGUI.TextArea(rect, text, EditorStyles.textArea);
            if (EditorGUI.EndChangeCheck()) property.stringValue = edited;

            return true;
        }

        private static bool DrawPath(
            InspectorMember member,
            SerializedProperty property,
            GUIContent label,
            List<string> errors,
            string title,
            string extension,
            bool relativeToProject,
            bool folder)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                Report(errors, member.Name, "[FilePath] / [FolderPath] は string のフィールドに付ける。");
                return false;
            }

            var browse = false;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var edited = EditorGUILayout.TextField(label, property.stringValue);
                if (EditorGUI.EndChangeCheck()) property.stringValue = edited;

                browse = GUILayout.Button("参照…", EditorStyles.miniButton, GUILayout.Width(52f));
            }

            if (!browse) return true;

            var start = ResolveStartDirectory(property.stringValue);
            var caption = string.IsNullOrEmpty(title) ? (folder ? "フォルダを選ぶ" : "ファイルを選ぶ") : title;

            var picked = folder
                ? EditorUtility.OpenFolderPanel(caption, start, string.Empty)
                : EditorUtility.OpenFilePanel(caption, start, extension ?? string.Empty);

            if (!string.IsNullOrEmpty(picked)) property.stringValue = Relativize(picked, relativeToProject);

            return true;
        }

        private static string ResolveStartDirectory(string current)
        {
            if (string.IsNullOrEmpty(current)) return Application.dataPath;

            try
            {
                var full = Path.IsPathRooted(current)
                    ? current
                    : Path.Combine(ProjectRoot(), current);

                if (Directory.Exists(full)) return full;

                var parent = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) return parent;
            }
            catch (Exception)
            {
                // 手打ちの途中で成り立たないパスになっていることがある。既定の場所から開けばよい。
            }

            return Application.dataPath;
        }

        /// <summary>
        /// 絶対パスをプロジェクトからの相対パスに直す。
        /// プロジェクトの外を指している場合は絶対パスのまま返すしかない。
        /// </summary>
        public static string Relativize(string path, bool relativeToProject)
        {
            var normalized = path.Replace('\\', '/');
            if (!relativeToProject) return normalized;

            var root = ProjectRoot();
            if (!normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) return normalized;

            return normalized.Substring(root.Length + 1);
        }

        private static string ProjectRoot()
        {
            var assets = Application.dataPath.Replace('\\', '/');
            var lastSlash = assets.LastIndexOf('/');
            return lastSlash < 0 ? assets : assets.Substring(0, lastSlash);
        }

        /// <summary>所有者配列から最初に解決できた対象を返す。</summary>
        private static object FirstTarget(IReadOnlyList<object> targets)
        {
            if (targets == null) return null;

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) return targets[i];
            }

            return null;
        }

        private static void Report(List<string> errors, string ownerName, string message)
        {
            if (errors == null || message == null) return;

            var text = $"{ownerName}: {message}";
            if (errors.Contains(text)) return;

            errors.Add(text);
        }
    }
}
