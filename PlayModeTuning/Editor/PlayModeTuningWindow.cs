using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>対象選択、再生中の記録、差分確認、承認、反映の順で操作画面を表示します。</summary>
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
        private MessageType messageType = MessageType.Info;

        /// <summary>実行中調整の編集画面を開きます。</summary>
        internal static void Open()
        {
            var window = GetWindow<PlayModeTuningWindow>();
            window.titleContent = new GUIContent("実行中調整");
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
            EditorGUILayout.LabelField("実行中調整", TitleStyle());
            EditorGUILayout.LabelField("再生中に選んだ値を記録し、再生終了後に差分を確認してから、一度だけシーンへ反映します。", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10f);

            DrawSection(PlayModeTuningUiText.Step1, SectionColors[0], DrawTargets);
            DrawSection(PlayModeTuningUiText.Step2, SectionColors[1], DrawCapture);
            DrawSection(PlayModeTuningUiText.Step3, SectionColors[2], DrawPreview);
            DrawSection(PlayModeTuningUiText.Step4, SectionColors[3], DrawReview);
            DrawSection(PlayModeTuningUiText.Step5, SectionColors[4], DrawApplyAndResult);

            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, messageType);
            GUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargets()
        {
            for (var index = 0; index < rows.Count; index++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("対象項目 " + (index + 1), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("削除", GUILayout.Width(70f)))
                {
                    rows.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                rows[index].target = (Component)EditorGUILayout.ObjectField("対象コンポーネント", rows[index].target, typeof(Component), true);
                rows[index].propertyPath = EditorGUILayout.TextField("項目の識別名", rows[index].propertyPath ?? string.Empty);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("対象項目を追加", GUILayout.Width(160f)))
                rows.Add(new SelectionRow());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("調整を開始", GUILayout.Width(150f), GUILayout.Height(26f)))
                RunUserAction(StartSession);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("各行で、シーン上の動作部品（MonoBehaviour）を一つ選び、最上位のシリアル化項目の正確な識別名を入力してください。", EditorStyles.wordWrappedMiniLabel);
            DrawSessionSummary();
        }

        private void DrawCapture()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase != PlayModeTuningPhase.Capturable))
            {
                if (GUILayout.Button("選んだ値を記録", GUILayout.Height(30f)))
                    RunUserAction(() => Capture(session));
            }
            EditorGUILayout.LabelField("再生中に値を調整した後、このボタンを明示的に押して記録します。", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase != PlayModeTuningPhase.ReadyToPreview))
            {
                if (GUILayout.Button("記録した差分を表示", GUILayout.Height(30f)))
                    RunUserAction(() => Preview(session));
            }
            EditorGUILayout.LabelField("差分は、値を記録して再生を終了した後に表示できます。この時点ではシーンを変更しません。", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawReview()
        {
            if (plan == null || !plan.IsReady)
            {
                EditorGUILayout.LabelField("確認する前に、手順3で最新の差分を表示してください。", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            EditorGUILayout.LabelField("変更数", plan.Changes.Count.ToString());
            foreach (var change in plan.Changes)
                EditorGUILayout.LabelField(change.TargetName + " / " + change.PropertyPath + " / " + PlayModeTuningDisplayText.ValueKind(change.ValueKind) + "    " + change.BeforeValue + "  →  " + change.AfterValue, EditorStyles.wordWrappedMiniLabel);
            confirmationAccepted = EditorGUILayout.ToggleLeft("すべての対象、項目、変更前の値、記録した値を確認しました。", confirmationAccepted);
        }

        private void DrawApplyAndResult()
        {
            using (new EditorGUI.DisabledScope(plan == null || !plan.IsReady || !confirmationAccepted || PlayModeTuningService.GetCurrentSession().Phase != PlayModeTuningPhase.Previewed))
            {
                if (GUILayout.Button("変更を反映", GUILayout.Height(34f)))
                    RunUserAction(Apply);
            }
            if (result != null)
            {
                EditorGUILayout.LabelField("反映", result.ApplySucceeded ? "成功" : result.ApplyAttempted ? "失敗" : "未実行");
                EditorGUILayout.LabelField("復元", result.RollbackAttempted ? result.RollbackSucceeded ? "成功" : "失敗" : "不要");
                if (result.RollbackAttempted && !string.IsNullOrEmpty(result.RollbackMessage))
                    EditorGUILayout.HelpBox(result.RollbackMessage, result.RollbackSucceeded ? MessageType.Info : MessageType.Error);
            }
            var session = PlayModeTuningService.GetCurrentSession();
            using (new EditorGUI.DisabledScope(session.Phase == PlayModeTuningPhase.Idle || session.IsTerminal))
            {
                if (GUILayout.Button("調整を破棄", GUILayout.Width(140f)))
                {
                    RunUserAction(() => Discard(session));
                }
            }
        }

        private void Capture(PlayModeTuningSession session)
        {
            var capture = PlayModeTuningService.CaptureDuringPlay(session.SessionId);
            SetMessage(capture.Succeeded ? capture.CapturedPropertyCount + "件の対象項目を記録しました。" : PlayModeTuningDisplayText.Failure(capture.Error, capture.Message), capture.Succeeded ? MessageType.Info : MessageType.Error);
        }

        private void Preview(PlayModeTuningSession session)
        {
            plan = PlayModeTuningService.PreviewAfterPlay(session.SessionId);
            result = null;
            confirmationAccepted = false;
            SetMessage(plan.IsReady ? "差分を作成しました。下の変更内容をすべて確認してください。" : PlayModeTuningDisplayText.Failure(plan.Error, plan.Message), plan.IsReady ? MessageType.Info : MessageType.Error);
        }

        private void Apply()
        {
            result = PlayModeTuningService.Apply(plan);
            SetMessage(result.ApplySucceeded ? result.ApplyMessage : PlayModeTuningDisplayText.Failure(result.ApplyError, result.ApplyMessage), result.ApplySucceeded ? MessageType.Info : MessageType.Error);
            confirmationAccepted = false;
        }

        private void Discard(PlayModeTuningSession session)
        {
            var discarded = PlayModeTuningService.Discard(session.SessionId);
            if (discarded.Error != PlayModeTuningError.None)
            {
                SetMessage(PlayModeTuningDisplayText.Failure(discarded.Error, discarded.Message), MessageType.Error);
                return;
            }
            plan = null;
            result = null;
            confirmationAccepted = false;
            SetMessage("調整を破棄しました。", MessageType.Info);
        }

        private void StartSession()
        {
            var selections = rows.Select(row => new PlayModeTuningPropertySelection(row.target, row.propertyPath)).ToArray();
            var start = PlayModeTuningService.Start(selections);
            plan = null;
            result = null;
            confirmationAccepted = false;
            SetMessage(start.Succeeded ? "調整を開始しました。準備ができたら再生を開始してください。" : PlayModeTuningDisplayText.Failure(start.Error, start.Message), start.Succeeded ? MessageType.Info : MessageType.Error);
        }

        private static void DrawSessionSummary()
        {
            var session = PlayModeTuningService.GetCurrentSession();
            EditorGUILayout.LabelField("現在の段階", PlayModeTuningDisplayText.Phase(session.Phase));
            EditorGUILayout.LabelField("対象数", session.ComponentCount + "コンポーネント / " + session.PropertyCount + "項目");
            if (session.Error != PlayModeTuningError.None)
                EditorGUILayout.HelpBox(PlayModeTuningDisplayText.Failure(session.Error, session.Message), MessageType.Error);
            if (session.Error == PlayModeTuningError.SessionStorageFailed && PlayModeTuningLifecycle.CanRetry && GUILayout.Button("状態更新を再試行", GUILayout.Width(160f)))
                PlayModeTuningLifecycle.Retry();
        }

        private void RunUserAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetMessage("処理中に予期しない問題が発生しました。詳しくはコンソールを確認してください。", MessageType.Error);
            }
        }

        private void SetMessage(string value, MessageType type)
        {
            message = value ?? string.Empty;
            messageType = type;
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
            /// <summary>値を読み書きするシーン上のコンポーネントです。</summary>
            public Component target;

            /// <summary>最上位のシリアル化項目を示す正確な識別名です。</summary>
            public string propertyPath = string.Empty;
        }
    }
}
