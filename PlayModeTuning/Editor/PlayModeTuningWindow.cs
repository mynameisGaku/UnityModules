using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>Renders targets first, capture during Play, preview after Play, review, and apply last.</summary>
    internal sealed class PlayModeTuningWindow : EditorWindow
    {
        private static readonly Color[] SectionColors =
        {
            new Color(0.20f, 0.55f, 0.95f),
            new Color(0.20f, 0.75f, 0.52f),
            new Color(0.95f, 0.60f, 0.18f),
            new Color(0.66f, 0.43f, 0.92f),
            new Color(0.90f, 0.28f, 0.34f)
        };

        [SerializeField] private List<SelectionRow> rows = new List<SelectionRow>();
        [SerializeField] private Vector2 scrollPosition;
        private PlayModeTuningPlan plan;
        private PlayModeTuningApplyResult result;
        private bool confirmationAccepted;
        private string message = string.Empty;

        internal static void Open()
        {
            var window = GetWindow<PlayModeTuningWindow>();
            window.titleContent = new GUIContent("Play Mode Tuning");
            window.minSize = new Vector2(640f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            if (rows == null)
                rows = new List<SelectionRow>();
            if (rows.Count == 0)
                rows.Add(new SelectionRow());
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Play Mode Tuning", TitleStyle());
            EditorGUILayout.LabelField("Capture selected values manually during Play Mode, preview after Play Mode, then apply one confirmed plan.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10f);

            DrawSection(PlayModeTuningUiText.Step1, SectionColors[0], DrawTargets);
            DrawSection(PlayModeTuningUiText.Step2, SectionColors[1], DrawCapture);
            DrawSection(PlayModeTuningUiText.Step3, SectionColors[2], DrawPreview);
            DrawSection(PlayModeTuningUiText.Step4, SectionColors[3], DrawReview);
            DrawSection(PlayModeTuningUiText.Step5, SectionColors[4], DrawApplyAndResult);

            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, MessageType.Info);
            GUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargets()
        {
            for (var index = 0; index < rows.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                rows[index].target = (Component)EditorGUILayout.ObjectField(rows[index].target, typeof(Component), true, GUILayout.MinWidth(210f));
                rows[index].propertyPath = EditorGUILayout.TextField(rows[index].propertyPath ?? string.Empty, GUILayout.MinWidth(180f));
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    rows.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Target Property", GUILayout.Width(160f)))
                rows.Add(new SelectionRow());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start Session", GUILayout.Width(150f), GUILayout.Height(26f)))
                StartSession();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Use one scene MonoBehaviour and one top-level serialized property path per row.", EditorStyles.wordWrappedMiniLabel);
            DrawSessionSummary();
        }

        private void DrawCapture()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase != PlayModeTuningPhase.Capturable))
            {
                if (GUILayout.Button("Capture Selected Values", GUILayout.Height(30f)))
                {
                    var capture = PlayModeTuningService.CaptureDuringPlay(session.SessionId);
                    message = capture.Succeeded ? "Captured " + capture.CapturedPropertyCount + " selected properties." : capture.Error + ": " + capture.Message;
                }
            }
            EditorGUILayout.LabelField("Tune the game first, then press this button explicitly while Play Mode is active.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase != PlayModeTuningPhase.ReadyToPreview))
            {
                if (GUILayout.Button("Preview Captured Differences", GUILayout.Height(30f)))
                {
                    plan = PlayModeTuningService.PreviewAfterPlay(session.SessionId);
                    result = null;
                    confirmationAccepted = false;
                    message = plan.IsReady ? "Preview created. Review every change below." : plan.Error + ": " + plan.Message;
                }
            }
            EditorGUILayout.LabelField("Preview is available only after capture and Play Mode exit. It does not change scene values.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawReview()
        {
            if (plan == null || !plan.IsReady)
            {
                EditorGUILayout.LabelField("Create a fresh preview in step 3 before confirmation.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            EditorGUILayout.LabelField("Changes", plan.Changes.Count.ToString());
            foreach (var change in plan.Changes)
                EditorGUILayout.LabelField(change.TargetName + " / " + change.PropertyPath + "    " + change.BeforeValue + "  ->  " + change.AfterValue, EditorStyles.wordWrappedMiniLabel);
            confirmationAccepted = EditorGUILayout.ToggleLeft("I reviewed every target, property, before value, and captured value.", confirmationAccepted);
        }

        private void DrawApplyAndResult()
        {
            using (new EditorGUI.DisabledScope(plan == null || !plan.IsReady || !confirmationAccepted || PlayModeTuningService.GetCurrentSession().Phase != PlayModeTuningPhase.Previewed))
            {
                if (GUILayout.Button("Apply Tuning", GUILayout.Height(34f)))
                {
                    result = PlayModeTuningService.Apply(plan);
                    message = result.ApplySucceeded ? result.ApplyMessage : result.ApplyError + ": " + result.ApplyMessage;
                    confirmationAccepted = false;
                }
            }
            if (result != null)
            {
                EditorGUILayout.LabelField("Apply", result.ApplySucceeded ? "Succeeded" : result.ApplyAttempted ? "Failed" : "Not attempted");
                EditorGUILayout.LabelField("Rollback", result.RollbackAttempted ? result.RollbackSucceeded ? "Succeeded" : "Failed" : "Not required");
                if (result.RollbackAttempted && !string.IsNullOrEmpty(result.RollbackMessage))
                    EditorGUILayout.HelpBox(result.RollbackMessage, result.RollbackSucceeded ? MessageType.Info : MessageType.Error);
            }
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase == PlayModeTuningPhase.Idle || session.IsTerminal))
            {
                if (GUILayout.Button("Discard Session", GUILayout.Width(140f)))
                {
                    PlayModeTuningService.Discard(session.SessionId);
                    plan = null;
                    result = null;
                    confirmationAccepted = false;
                    message = "The session was discarded.";
                }
            }
        }

        private void StartSession()
        {
            var selections = rows.Select(row => new PlayModeTuningPropertySelection(row.target, row.propertyPath)).ToArray();
            var start = PlayModeTuningService.Start(selections);
            plan = null;
            result = null;
            confirmationAccepted = false;
            message = start.Succeeded ? "Session armed. Enter Play Mode when ready." : start.Error + ": " + start.Message;
        }

        private static void DrawSessionSummary()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            EditorGUILayout.LabelField("Phase", session.Phase.ToString());
            EditorGUILayout.LabelField("Targets", session.ComponentCount + " components / " + session.PropertyCount + " properties");
            if (session.Error != PlayModeTuningError.None)
                EditorGUILayout.HelpBox(session.Error + ": " + session.Message, MessageType.Error);
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
            var rect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Color.Lerp(new Color(0.08f, 0.08f, 0.09f), accent, 0.38f));
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 6, 0, 0),
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, heading, style);
        }

        private static GUIStyle TitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        }

        [Serializable]
        private sealed class SelectionRow
        {
            public Component target;
            public string propertyPath = string.Empty;
        }
    }
}
