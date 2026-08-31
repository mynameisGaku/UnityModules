using System;
using UnityEditor;

namespace PlayModeTuning.Editor
{
    /// <summary>調整作業の段階だけを進め、値の記録や反映は自動実行しません。</summary>
    [InitializeOnLoad]
    internal static class PlayModeTuningLifecycle
    {
        // 一時的な保存領域障害に対して行う、状態遷移だけの最大試行回数です。
        private const int MaximumTransitionAttempts = 3;

        // 自動試行後に、利用者が画面から再試行できる状態かを保持します。
        private static bool retryAvailable;

        // 保存領域障害中に観測した再生状態の変化を、手動再試行まで保持します。
        private static EPlayModeTuningObservedTransition pendingTransition;

        static PlayModeTuningLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += () => RunWithRetry(() => PlayModeTuningService.InternalOperations.ResumeLifecycle(), EPlayModeTuningObservedTransition.None, MaximumTransitionAttempts);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => RunWithRetry(PlayModeTuningService.InternalOperations.OnEnteredPlayMode, EPlayModeTuningObservedTransition.EnteredPlayMode, MaximumTransitionAttempts);
            else if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += () => RunWithRetry(PlayModeTuningService.InternalOperations.OnEnteredEditMode, EPlayModeTuningObservedTransition.EnteredEditMode, MaximumTransitionAttempts);
        }

        /// <summary>保存領域が復旧した後、現在の再生状態に対応する段階更新だけを再試行します。</summary>
        internal static void Retry()
        {
            retryAvailable = false;
            RunWithRetry(() => PlayModeTuningService.InternalOperations.ResumeLifecycle(pendingTransition), pendingTransition, MaximumTransitionAttempts);
        }

        /// <summary>自動再試行後も保存領域の問題が残り、画面から再試行できるかを返します。</summary>
        internal static bool CanRetry => retryAvailable;

        private static void RunWithRetry(Func<bool> transition, EPlayModeTuningObservedTransition observedTransition, int remainingAttempts)
        {
            if (transition())
            {
                retryAvailable = false;
                pendingTransition = EPlayModeTuningObservedTransition.None;
                return;
            }
            pendingTransition = observedTransition;
            if (remainingAttempts <= 1)
            {
                retryAvailable = true;
                return;
            }
            EditorApplication.delayCall += () => RunWithRetry(() => PlayModeTuningService.InternalOperations.ResumeLifecycle(observedTransition), observedTransition, remainingAttempts - 1);
        }
    }

    /// <summary>保存処理中に実際に通知された再生状態の変化を表します。</summary>
    internal enum EPlayModeTuningObservedTransition
    {
        // 再生状態の変化を伴わない、読み込み直後の再調停です。
        None,

        // Unityから再生開始完了の通知を受けています。
        EnteredPlayMode,

        // Unityから編集状態への復帰完了通知を受けています。
        EnteredEditMode
    }
}
