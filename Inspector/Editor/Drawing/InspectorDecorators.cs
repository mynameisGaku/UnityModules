using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>フィールドの前後に足す表示（見出し・注意書き・区切り線・プレビュー）を描く。</summary>
    internal static class InspectorDecorators
    {
        /// <summary>指定した位置の装飾を宣言順に描く。</summary>
        public static void Draw(
            InspectorMember member,
            object target,
            DecoratorPosition position,
            SerializedProperty property,
            List<string> errors)
        {
            DrawAll(member, new[] { target }, position, property, errors);
        }

        /// <summary>複数選択した全所有者を条件表示へ反映して装飾を描く。</summary>
        internal static void DrawAll(
            InspectorMember member,
            IReadOnlyList<object> targets,
            DecoratorPosition position,
            SerializedProperty property,
            List<string> errors)
        {
            var attributes = member.Attributes;

            for (var i = 0; i < attributes.Length; i++)
            {
                if (!(attributes[i] is DecoratorAttribute decorator)) continue;
                if (decorator.Position != position) continue;

                switch (decorator)
                {
                    case TitleAttribute title:
                        DrawTitle(title);
                        break;

                    case InfoBoxAttribute info:
                        DrawInfoBox(info, targets, member.Name, errors);
                        break;

                    case HorizontalLineAttribute line:
                        InspectorStyles.HorizontalLine(
                            line.Height,
                            line.Color.ToColor(Color.gray),
                            line.SpaceBefore,
                            line.SpaceAfter);
                        break;

                    case ShowAssetPreviewAttribute preview:
                        DrawAssetPreview(preview, property);
                        break;
                }
            }
        }

        private static void DrawTitle(TitleAttribute title)
        {
            GUILayout.Space(4f);

            var style = title.Bold ? InspectorStyles.Title : EditorStyles.label;
            EditorGUILayout.LabelField(title.Title, style);

            if (!string.IsNullOrEmpty(title.Subtitle))
            {
                EditorGUILayout.LabelField(title.Subtitle, InspectorStyles.Subtitle);
            }

            if (title.Line)
            {
                InspectorStyles.HorizontalLine(1f, new Color(0.4f, 0.4f, 0.4f, 0.6f), 0f, 3f);
            }
        }

        private static void DrawInfoBox(
            InfoBoxAttribute info,
            IReadOnlyList<object> targets,
            string ownerName,
            List<string> errors)
        {
            if (!string.IsNullOrEmpty(info.VisibleIf))
            {
                var visible = false;

                for (var i = 0; targets != null && i < targets.Count; i++)
                {
                    visible |= ConditionEvaluator.EvaluateFlag(targets[i], info.VisibleIf, ownerName, errors);
                }

                if (!visible) return;
            }

            EditorGUILayout.HelpBox(info.Text, InspectorStyles.ToMessageType(info.Kind));
        }

        private static void DrawAssetPreview(ShowAssetPreviewAttribute attribute, SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;

            var asset = property.objectReferenceValue;
            if (asset == null) return;

            // まだ生成されていないことがある。Unity が裏で作るので、次の描画で出る。
            var preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null) return;

            var rect = EditorGUILayout.GetControlRect(false, attribute.Height);
            rect.width = attribute.Width;
            rect.x += EditorGUIUtility.labelWidth;

            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }
    }
}
