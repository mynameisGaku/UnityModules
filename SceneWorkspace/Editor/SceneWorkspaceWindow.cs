using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>設定、差分確認、内容確認、切り替え、結果確認の五段階を上から順に表示します。</summary>
    internal sealed class SceneWorkspaceWindow : EditorWindow
    {
        /// <summary>各段階を見分けるための見出し色です。</summary>
        private static readonly Color[] SectionColors =
        {
            new Color(0.20f, 0.55f, 0.95f),
            new Color(0.20f, 0.75f, 0.52f),
            new Color(0.95f, 0.60f, 0.18f),
            new Color(0.66f, 0.43f, 0.92f),
            new Color(0.12f, 0.70f, 0.75f)
        };

        /// <summary>現在選択している作業セット設定です。</summary>
        [SerializeField] private SceneWorkspaceProfile profile;

        /// <summary>縦方向の表示位置です。</summary>
        [SerializeField] private Vector2 scrollPosition;

        /// <summary>画面に表示する処理状態を所有します。</summary>
        private SceneWorkspacePresenter presenter;

        /// <summary>選択中の設定アセットを編集するための直列化情報です。</summary>
        private SerializedObject serializedProfile;

        /// <summary>シーン構成を日本語の項目名で並べ替える一覧です。</summary>
        private ReorderableList entriesList;

        /// <summary>シーン作業セット画面を開き、操作可能な最小寸法を設定します。</summary>
        internal static void Open()
        {
            var window = GetWindow<SceneWorkspaceWindow>();
            window.titleContent = new GUIContent("シーン作業セット");
            window.minSize = new Vector2(640f, 560f);
            window.Show();
        }

        /// <summary>領域再読込後に表示状態と選択中の設定を結び直します。</summary>
        private void OnEnable()
        {
            presenter = new SceneWorkspacePresenter();
            presenter.SetProfile(profile);
            SetSerializedProfile(profile);
        }

        /// <summary>五段階の操作と現在の結果を縦方向へ描画します。</summary>
        private void OnGUI()
        {
            if (presenter == null)
                OnEnable();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("シーン作業セット", TitleStyle());
            EditorGUILayout.LabelField("保存済みの複数シーン構成を確認し、未保存の変更を自動で保存・破棄せずに切り替えます。", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10f);

            DrawSection(SceneWorkspaceUiText.Step1, SectionColors[0], DrawProfile);
            DrawSection(SceneWorkspaceUiText.Step2, SectionColors[1], DrawSetup);
            DrawSection(SceneWorkspaceUiText.Step3, SectionColors[2], DrawPreview);
            DrawSection(SceneWorkspaceUiText.Step4, SectionColors[3], DrawConfirmation);
            DrawSection(SceneWorkspaceUiText.Step5, SectionColors[4], DrawApplyAndResult);

            if (!string.IsNullOrEmpty(presenter.Message))
                EditorGUILayout.HelpBox(presenter.Message, MessageType.Info);
            GUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>作業セット設定の選択と新規作成を描画します。</summary>
        private void DrawProfile()
        {
            EditorGUI.BeginChangeCheck();
            var selected = (SceneWorkspaceProfile)EditorGUILayout.ObjectField("作業セット設定", profile, typeof(SceneWorkspaceProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                profile = selected;
                SetSerializedProfile(profile);
                presenter.SetProfile(profile);
            }

            if (GUILayout.Button("新しい設定を作成", GUILayout.Width(180f)))
                CreateProfile();
            EditorGUILayout.LabelField("下のシーン構成を編集する前に、設定アセットを選択してください。", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>順序付きシーン構成と現在構成の取り込み操作を描画します。</summary>
        private void DrawSetup()
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox("①で設定アセットを選ぶか作成してください。", MessageType.None);
                return;
            }

            if (serializedProfile == null || serializedProfile.targetObject != profile)
                SetSerializedProfile(profile);
            serializedProfile.UpdateIfRequiredOrScript();
            EnsureEntriesList();
            EditorGUI.BeginChangeCheck();
            entriesList.DoLayoutList();
            var controlsChanged = EditorGUI.EndChangeCheck();
            var propertiesChanged = serializedProfile.ApplyModifiedProperties();
            if (controlsChanged || propertiesChanged)
                presenter.NotifyProfileChanged();

            GUILayout.Space(4f);
            if (GUILayout.Button("現在の構成を設定へ取り込む", GUILayout.Height(26f)))
            {
                presenter.CaptureIntoProfile();
                SetSerializedProfile(profile);
            }
            EditorGUILayout.LabelField("保存済みで未変更のシーンだけを取り込めます。設定アセットは変更済みになりますが、自動保存しません。", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>現在構成と設定内容の差分確認を描画します。</summary>
        private void DrawPreview()
        {
            using (new EditorGUI.DisabledScope(!presenter.CanPreview))
            {
                if (GUILayout.Button("差分を確認", GUILayout.Height(28f)))
                    presenter.Preview();
            }

            var plan = presenter.Plan;
            if (plan == null)
            {
                EditorGUILayout.LabelField("上の設定を完了してから、最新の差分を確認してください。", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            if (!plan.IsReady)
            {
                EditorGUILayout.HelpBox(SceneWorkspaceDisplayText.FormatOutcome(plan.Error, plan.Message), MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("設定名", plan.ProfileName);
            EditorGUILayout.LabelField("現在のシーン数", plan.CurrentScenes.Count.ToString());
            EditorGUILayout.LabelField("切り替え後のシーン数", plan.TargetScenes.Count.ToString());
            foreach (var change in plan.Changes)
                EditorGUILayout.LabelField(SceneWorkspaceDisplayText.FormatChange(change), EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>差分内容の確認と明示的な同意欄を描画します。</summary>
        private void DrawConfirmation()
        {
            var plan = presenter.Plan;
            if (plan == null || !plan.IsReady)
            {
                EditorGUILayout.LabelField("差分確認が完了すると、内容を確認できます。", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.HelpBox("設定に含まれないシーンを閉じます。未保存の変更があるシーンを検出した場合は、切り替え前に停止します。", MessageType.Warning);
            var accepted = EditorGUILayout.ToggleLeft("設定、順番、読込状態、使用中のシーン、閉じるシーンを確認しました。", presenter.ConfirmationAccepted);
            if (accepted != presenter.ConfirmationAccepted)
                presenter.SetConfirmation(accepted);
        }

        /// <summary>切り替え操作と、切り替え・復元の独立した結果を描画します。</summary>
        private void DrawApplyAndResult()
        {
            using (new EditorGUI.DisabledScope(!presenter.CanApply))
            {
                if (GUILayout.Button("作業セットを切り替える", GUILayout.Height(32f)))
                    presenter.Apply();
            }

            var result = presenter.Result;
            if (result == null)
            {
                EditorGUILayout.LabelField("切り替え結果と復元結果を分けて表示します。", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("切り替え", result.ApplySucceeded ? "成功" : "失敗");
            if (result.ApplyError != SceneWorkspaceError.None || !string.IsNullOrEmpty(result.ApplyMessage))
                EditorGUILayout.HelpBox(SceneWorkspaceDisplayText.FormatOutcome(result.ApplyError, result.ApplyMessage), result.ApplySucceeded ? MessageType.Info : MessageType.Error);
            EditorGUILayout.LabelField("復元", result.RollbackAttempted ? result.RollbackSucceeded ? "成功" : "失敗" : "不要");
            if (result.RollbackAttempted)
                EditorGUILayout.HelpBox(SceneWorkspaceDisplayText.FormatOutcome(result.RollbackError, result.RollbackMessage), result.RollbackSucceeded ? MessageType.Info : MessageType.Error);
        }

        /// <summary>保存先を確認して新しい設定アセットを作成します。取り消した場合は何も変更しません。</summary>
        private void CreateProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject("シーン作業セット設定を作成", "シーン作業セット設定", "asset", "Assetsフォルダー以下の保存先を選んでください。");
            if (string.IsNullOrEmpty(path))
                return;

            var created = CreateInstance<SceneWorkspaceProfile>();
            AssetDatabase.CreateAsset(created, path);
            profile = created;
            SetSerializedProfile(profile);
            presenter.SetProfile(profile);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        /// <summary>選択中の設定に対応する直列化情報を作り直します。</summary>
        private void SetSerializedProfile(SceneWorkspaceProfile selected)
        {
            serializedProfile = selected == null ? null : new SerializedObject(selected);
            entriesList = null;
        }

        /// <summary>直列化名を維持したまま、日本語表示の並べ替え一覧を構築します。</summary>
        private void EnsureEntriesList()
        {
            if (entriesList != null)
                return;

            var entries = serializedProfile.FindProperty("entries");
            entriesList = new ReorderableList(serializedProfile, entries, true, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "順序付きシーン構成"),
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "シーンが登録されていません。")
            };
            entriesList.drawElementCallback = (rect, index, active, focused) => DrawEntry(rect, entries, index);
            entriesList.onAddCallback = AddEntry;
        }

        /// <summary>一覧へ空のシーン項目を追加し、安全な初期状態を設定します。</summary>
        private static void AddEntry(ReorderableList list)
        {
            var entries = list.serializedProperty;
            var index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("scene").objectReferenceValue = null;
            entry.FindPropertyRelative("loaded").boolValue = true;
            entry.FindPropertyRelative("active").boolValue = false;
            list.index = index;
        }

        /// <summary>一つのシーン項目を日本語の項目名で一段表示します。</summary>
        private static void DrawEntry(Rect rect, SerializedProperty entries, int index)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            var scene = entry.FindPropertyRelative("scene");
            var loaded = entry.FindPropertyRelative("loaded");
            var active = entry.FindPropertyRelative("active");
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var labelWidth = 62f;
            var loadedWidth = 92f;
            var activeWidth = 122f;
            var sceneWidth = Mathf.Max(80f, rect.width - labelWidth - loadedWidth - activeWidth - (spacing * 3f));
            var rowY = rect.y + 2f;
            EditorGUI.LabelField(new Rect(rect.x, rowY, labelWidth, lineHeight), "シーン " + (index + 1));
            EditorGUI.PropertyField(new Rect(rect.x + labelWidth + spacing, rowY, sceneWidth, lineHeight), scene, GUIContent.none);
            loaded.boolValue = EditorGUI.ToggleLeft(new Rect(rect.x + labelWidth + sceneWidth + (spacing * 2f), rowY, loadedWidth, lineHeight), "読み込む", loaded.boolValue);
            active.boolValue = EditorGUI.ToggleLeft(new Rect(rect.x + labelWidth + sceneWidth + loadedWidth + (spacing * 3f), rowY, activeWidth, lineHeight), "使用中にする", active.boolValue);
        }

        /// <summary>色付き見出しと内容を一つの枠として描画します。</summary>
        private static void DrawSection(string heading, Color accent, Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeader(heading, accent);
            GUILayout.Space(5f);
            drawContent();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        /// <summary>段階を見分けるための色付き見出しを描画します。</summary>
        private static void DrawHeader(string heading, Color accent)
        {
            var rect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            var baseColor = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.94f, 0.94f, 0.94f);
            EditorGUI.DrawRect(rect, Color.Lerp(baseColor, accent, EditorGUIUtility.isProSkin ? 0.42f : 0.24f));
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 6, 0, 0),
                fontSize = 13
            };
            GUI.Label(rect, heading, style);
        }

        /// <summary>画面名に使う大きな見出し様式を返します。</summary>
        private static GUIStyle TitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        }
    }
}
