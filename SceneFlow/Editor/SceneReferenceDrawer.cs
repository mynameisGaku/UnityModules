using UnityEditor;
using UnityEngine;

namespace SceneFlow.Editor
{
    /// <summary>SceneReferenceをScene Asset選択欄として描画し、移動後の参照とBuild Profile登録を検証する。</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    internal sealed class SceneReferenceDrawer : PropertyDrawer
    {
        private const string GuidPropertyName = "_guid";
        private const string PathPropertyName = "_path";
        private const float HelpBoxLineCount = 2f;

        /// <summary>Scene選択欄と必要な警告欄を描画する。</summary>
        /// <param name="position">描画に使用できる領域。</param>
        /// <param name="property">SceneReferenceの直列化データ。</param>
        /// <param name="label">Inspectorに表示するラベル。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var guidProperty = property.FindPropertyRelative(GuidPropertyName);
            var pathProperty = property.FindPropertyRelative(PathPropertyName);
            if (guidProperty == null || pathProperty == null)
            {
                EditorGUI.HelpBox(position, "SceneReference の直列化データを読み取れません。", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var hasMixedValues = guidProperty.hasMultipleDifferentValues || pathProperty.hasMultipleDifferentValues;
            var currentAsset = ResolveAndRepair(guidProperty, pathProperty, hasMixedValues);

            var previousShowMixedValue = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = hasMixedValues;
                EditorGUI.BeginChangeCheck();
                var selectedAsset = (SceneAsset)EditorGUI.ObjectField(fieldRect, label, currentAsset, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck()) StoreSelection(guidProperty, pathProperty, selectedAsset);
            }
            finally
            {
                EditorGUI.showMixedValue = previousShowMixedValue;
            }

            if (!hasMixedValues)
            {
                DrawValidation(position, guidProperty.stringValue, pathProperty.stringValue);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Scene選択欄と警告欄を合わせた高さを返す。</summary>
        /// <param name="property">SceneReferenceの直列化データ。</param>
        /// <param name="label">Inspectorに表示するラベル。</param>
        /// <returns>現在の検証結果を表示できる高さ。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var guidProperty = property.FindPropertyRelative(GuidPropertyName);
            var pathProperty = property.FindPropertyRelative(PathPropertyName);
            if (guidProperty == null || pathProperty == null) return HelpBoxHeight;
            if (guidProperty.hasMultipleDifferentValues || pathProperty.hasMultipleDifferentValues) return EditorGUIUtility.singleLineHeight;

            var status = GetValidationStatus(guidProperty.stringValue, pathProperty.stringValue);
            return status == SceneReferenceEditorUtility.ValidationStatus.Valid
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + HelpBoxHeight;
        }

        private static float HelpBoxHeight => EditorGUIUtility.singleLineHeight * HelpBoxLineCount;

        private static SceneAsset ResolveAndRepair(SerializedProperty guidProperty, SerializedProperty pathProperty, bool hasMixedValues)
        {
            if (hasMixedValues) return null;
            if (!SceneReferenceEditorUtility.TryResolve(guidProperty.stringValue, pathProperty.stringValue, out var sceneAsset, out var guid, out var path)) return null;

            if (guidProperty.stringValue != guid) guidProperty.stringValue = guid;
            if (pathProperty.stringValue != path) pathProperty.stringValue = path;
            return sceneAsset;
        }

        private static void StoreSelection(SerializedProperty guidProperty, SerializedProperty pathProperty, SceneAsset sceneAsset)
        {
            if (!SceneReferenceEditorUtility.TryGetIdentity(sceneAsset, out var guid, out var path))
            {
                guidProperty.stringValue = string.Empty;
                pathProperty.stringValue = string.Empty;
                return;
            }

            guidProperty.stringValue = guid;
            pathProperty.stringValue = path;
        }

        private static void DrawValidation(Rect position, string guid, string path)
        {
            var status = GetValidationStatus(guid, path);
            if (status == SceneReferenceEditorUtility.ValidationStatus.Valid) return;

            var messageRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                HelpBoxHeight);
            GetMessage(status, out var message, out var messageType);
            EditorGUI.HelpBox(messageRect, message, messageType);
        }

        private static SceneReferenceEditorUtility.ValidationStatus GetValidationStatus(string guid, string path)
        {
            if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(path)) return SceneReferenceEditorUtility.ValidationStatus.Empty;
            if (!SceneReferenceEditorUtility.TryResolve(guid, path, out _, out _, out var resolvedPath)) return SceneReferenceEditorUtility.ValidationStatus.Missing;
            return SceneReferenceEditorUtility.Validate(resolvedPath);
        }

        private static void GetMessage(SceneReferenceEditorUtility.ValidationStatus status, out string message, out MessageType messageType)
        {
            switch (status)
            {
                case SceneReferenceEditorUtility.ValidationStatus.Empty:
                    message = "Scene Assetを指定してください。";
                    messageType = MessageType.Info;
                    return;
                case SceneReferenceEditorUtility.ValidationStatus.Missing:
                    message = "Scene Assetを解決できません。削除またはGUID変更を確認してください。";
                    messageType = MessageType.Error;
                    return;
                case SceneReferenceEditorUtility.ValidationStatus.Disabled:
                    message = "現在のBuild Profileでは、このSceneが無効です。";
                    messageType = MessageType.Warning;
                    return;
                default:
                    message = "現在のBuild ProfileのScene一覧に、このSceneがありません。";
                    messageType = MessageType.Warning;
                    return;
            }
        }
    }
}
