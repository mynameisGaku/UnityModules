using System;
using UnityEditor;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>Renders the five-step scene workspace flow with settings above Preview and Apply last.</summary>
    internal sealed class SceneWorkspaceWindow : EditorWindow
    {
        private static readonly Color[] SectionColors =
        {
            new Color(0.20f, 0.55f, 0.95f),
            new Color(0.20f, 0.75f, 0.52f),
            new Color(0.95f, 0.60f, 0.18f),
            new Color(0.66f, 0.43f, 0.92f),
            new Color(0.12f, 0.70f, 0.75f)
        };

        [SerializeField] private SceneWorkspaceProfile profile;
        [SerializeField] private Vector2 scrollPosition;
        private SceneWorkspacePresenter presenter;
        private SerializedObject serializedProfile;

        internal static void Open()
        {
            var window = GetWindow<SceneWorkspaceWindow>();
            window.titleContent = new GUIContent("Scene Workspace");
            window.minSize = new Vector2(640f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            presenter = new SceneWorkspacePresenter();
            presenter.SetProfile(profile);
            serializedProfile = profile == null ? null : new SerializedObject(profile);
        }

        private void OnGUI()
        {
            if (presenter == null)
                OnEnable();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Scene Workspace", TitleStyle());
            EditorGUILayout.LabelField("Capture and switch a saved multi-scene setup without saving or discarding scene changes automatically.", EditorStyles.wordWrappedLabel);
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

        private void DrawProfile()
        {
            EditorGUI.BeginChangeCheck();
            var selected = (SceneWorkspaceProfile)EditorGUILayout.ObjectField("Workspace Profile", profile, typeof(SceneWorkspaceProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                profile = selected;
                serializedProfile = profile == null ? null : new SerializedObject(profile);
                presenter.SetProfile(profile);
            }

            if (GUILayout.Button("Create New Profile", GUILayout.Width(180f)))
                CreateProfile();
            EditorGUILayout.LabelField("Select a profile asset before editing the scene setup below.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSetup()
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Select or create a profile in step 1.", MessageType.None);
                return;
            }

            if (serializedProfile == null || serializedProfile.targetObject != profile)
                serializedProfile = new SerializedObject(profile);
            serializedProfile.UpdateIfRequiredOrScript();
            var entries = serializedProfile.FindProperty("entries");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(entries, new GUIContent("Ordered Scene Setup"), true);
            var controlsChanged = EditorGUI.EndChangeCheck();
            var propertiesChanged = serializedProfile.ApplyModifiedProperties();
            if (controlsChanged || propertiesChanged)
                presenter.NotifyProfileChanged();

            GUILayout.Space(4f);
            if (GUILayout.Button("Capture Current Setup Into Profile", GUILayout.Height(26f)))
            {
                presenter.CaptureIntoProfile();
                serializedProfile = new SerializedObject(profile);
            }
            EditorGUILayout.LabelField("Capture requires saved, clean scenes. It marks the profile as changed but does not save it automatically.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview()
        {
            using (new EditorGUI.DisabledScope(!presenter.CanPreview))
            {
                if (GUILayout.Button("Preview Changes", GUILayout.Height(28f)))
                    presenter.Preview();
            }

            var plan = presenter.Plan;
            if (plan == null)
            {
                EditorGUILayout.LabelField("Finish the profile settings above, then create a fresh preview.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            if (!plan.IsReady)
            {
                EditorGUILayout.HelpBox(plan.Error + ": " + plan.Message, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Profile", plan.ProfileName);
            EditorGUILayout.LabelField("Current Scenes", plan.CurrentScenes.Count.ToString());
            EditorGUILayout.LabelField("Target Scenes", plan.TargetScenes.Count.ToString());
            foreach (var change in plan.Changes)
                EditorGUILayout.LabelField(FormatChange(change), EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawConfirmation()
        {
            var plan = presenter.Plan;
            if (plan == null || !plan.IsReady)
            {
                EditorGUILayout.LabelField("A ready preview is required before confirmation.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.HelpBox("Switching closes scenes that are not in the profile. The operation is blocked if any current scene is dirty.", MessageType.Warning);
            var accepted = EditorGUILayout.ToggleLeft("I reviewed the profile, order, loaded states, active scene, and scenes to close.", presenter.ConfirmationAccepted);
            if (accepted != presenter.ConfirmationAccepted)
                presenter.SetConfirmation(accepted);
        }

        private void DrawApplyAndResult()
        {
            using (new EditorGUI.DisabledScope(!presenter.CanApply))
            {
                if (GUILayout.Button("Switch Workspace", GUILayout.Height(32f)))
                    presenter.Apply();
            }

            var result = presenter.Result;
            if (result == null)
            {
                EditorGUILayout.LabelField("The apply and rollback outcomes will be shown separately here.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("Apply", result.ApplySucceeded ? "Succeeded" : "Failed");
            if (result.ApplyError != SceneWorkspaceError.None || !string.IsNullOrEmpty(result.ApplyMessage))
                EditorGUILayout.HelpBox(FormatOutcome(result.ApplyError, result.ApplyMessage), result.ApplySucceeded ? MessageType.Info : MessageType.Error);
            EditorGUILayout.LabelField("Rollback", result.RollbackAttempted ? result.RollbackSucceeded ? "Succeeded" : "Failed" : "Not required");
            if (result.RollbackAttempted)
                EditorGUILayout.HelpBox(FormatOutcome(result.RollbackError, result.RollbackMessage), result.RollbackSucceeded ? MessageType.Info : MessageType.Error);
        }

        private void CreateProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Scene Workspace Profile", "SceneWorkspaceProfile", "asset", "Choose where to save the profile under Assets.");
            if (string.IsNullOrEmpty(path))
                return;

            var created = CreateInstance<SceneWorkspaceProfile>();
            AssetDatabase.CreateAsset(created, path);
            profile = created;
            serializedProfile = new SerializedObject(profile);
            presenter.SetProfile(profile);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        private static void DrawSection(string heading, Color accent, Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeader(heading, accent);
            GUILayout.Space(5f);
            drawContent();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
        }

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

        private static GUIStyle TitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        }

        private static string FormatChange(SceneWorkspaceChange change)
        {
            var position = change.BeforeIndex < 0
                ? "target index " + change.AfterIndex
                : change.AfterIndex < 0
                    ? "current index " + change.BeforeIndex
                    : "index " + change.BeforeIndex + " to " + change.AfterIndex;
            return change.Kind + "  " + change.Path + "  (" + position + ")";
        }

        private static string FormatOutcome(SceneWorkspaceError error, string message)
        {
            return error == SceneWorkspaceError.None ? message : error + ": " + message;
        }
    }
}
