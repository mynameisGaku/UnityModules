using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public static class InspectorFieldDrawers
    {
        // 参照先を開いて描いている最中のオブジェクト。自分自身を参照する資産で無限に潜らないための番人。
        private static readonly HashSet<EntityId> Expanding = new HashSet<EntityId>();
        private static readonly Dictionary<EntityId, SerializedObject> NestedObjects =
            new Dictionary<EntityId, SerializedObject>();

        /// <summary>専用の描画があれば描いて <c>true</c> を返す。</summary>
        public static bool TryDraw(
            InspectorMember member,
            object target,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (property == null) return false;

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
                        return DrawDropdown(dropdown, member, target, property, label, errors);

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
            object target,
            SerializedProperty property,
            GUIContent label,
            List<string> errors)
        {
            if (!MemberResolver.TryGetValue(target, attribute.ValuesMember, out var source, out var error))
            {
                Report(errors, member.Name, error);
                return false;
            }

            var labels = new List<string>();
            var values = new List<object>();

            switch (source)
            {
                case IDropdownList named:
                    foreach (var entry in named)
                    {
                        labels.Add(entry.Key);
                        values.Add(entry.Value);
                    }

                    break;

                case IEnumerable sequence when !(source is string):
                    foreach (var value in sequence)
                    {
                        labels.Add(value?.ToString() ?? "null");
                        values.Add(value);
                    }

                    break;

                default:
                    Report(errors, member.Name,
                        $"'{attribute.ValuesMember}' が候補の並びを返していない。IEnumerable<T> か DropdownList<T> を返すようにする。");
                    return false;
            }

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

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.Popup(label, index, contents);

            if (EditorGUI.EndChangeCheck() && picked != index)
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
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                {
                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.LayerField(label, property.intValue);
                    if (EditorGUI.EndChangeCheck()) property.intValue = picked;

                    return true;
                }

                case SerializedPropertyType.String:
                {
                    var current = LayerMask.NameToLayer(property.stringValue);
                    if (current < 0) current = 0;

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.LayerField(label, current);
                    if (EditorGUI.EndChangeCheck()) property.stringValue = LayerMask.LayerToName(picked);

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

            var names = new List<string> { "(未設定)" };
            var indices = new List<int> { -1 };

            for (var i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;

                names.Add(Path.GetFileNameWithoutExtension(scenes[i].path));
                indices.Add(i);
            }

            if (names.Count == 1)
            {
                EditorGUILayout.HelpBox("Build Settings にシーンが 1 つも登録されていない。", MessageType.Warning);
            }

            var contents = new GUIContent[names.Count];
            for (var i = 0; i < names.Count; i++) contents[i] = new GUIContent(names[i]);

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    var selected = Math.Max(0, names.IndexOf(property.stringValue));

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, selected, contents);
                    if (EditorGUI.EndChangeCheck()) property.stringValue = picked == 0 ? string.Empty : names[picked];

                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    var selected = Math.Max(0, indices.IndexOf(property.intValue));

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, selected, contents);
                    if (EditorGUI.EndChangeCheck()) property.intValue = indices[picked];

                    return true;
                }

                default:
                    Report(errors, member.Name, "[Scene] は string（シーン名）か int（ビルド順の番号）のフィールドに付ける。");
                    return false;
            }
        }

        private static bool DrawSortingLayer(InspectorMember member, SerializedProperty property, GUIContent label, List<string> errors)
        {
            var layers = SortingLayer.layers;

            var contents = new GUIContent[layers.Length];
            for (var i = 0; i < layers.Length; i++) contents[i] = new GUIContent(layers[i].name);

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    var selected = 0;
                    for (var i = 0; i < layers.Length; i++)
                    {
                        if (!string.Equals(layers[i].name, property.stringValue, StringComparison.Ordinal)) continue;

                        selected = i;
                        break;
                    }

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, selected, contents);
                    if (EditorGUI.EndChangeCheck() && layers.Length > 0) property.stringValue = layers[picked].name;

                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    var selected = 0;
                    for (var i = 0; i < layers.Length; i++)
                    {
                        if (layers[i].id != property.intValue) continue;

                        selected = i;
                        break;
                    }

                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUILayout.Popup(label, selected, contents);
                    if (EditorGUI.EndChangeCheck() && layers.Length > 0) property.intValue = layers[picked].id;

                    return true;
                }

                default:
                    Report(errors, member.Name, "[SortingLayer] は string（名前）か int（ID）のフィールドに付ける。");
                    return false;
            }
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

        private static void Report(List<string> errors, string ownerName, string message)
        {
            if (errors == null || message == null) return;

            var text = $"{ownerName}: {message}";
            if (errors.Contains(text)) return;

            errors.Add(text);
        }
    }
}
