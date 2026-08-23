using UnityEditor;

namespace PlayModeTuning.Editor
{
    /// <summary>Moves only lifecycle phases; it never captures or applies values automatically.</summary>
    [InitializeOnLoad]
    internal static class PlayModeTuningLifecycle
    {
        static PlayModeTuningLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += Resume;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += PlayModeTuningService.InternalOperations.OnEnteredPlayMode;
            else if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += PlayModeTuningService.InternalOperations.OnEnteredEditMode;
        }

        private static void Resume()
        {
            PlayModeTuningService.InternalOperations.ResumeLifecycle();
        }
    }
}
