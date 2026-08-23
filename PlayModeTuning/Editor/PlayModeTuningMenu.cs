using UnityEditor;

namespace PlayModeTuning.Editor
{
    /// <summary>Exposes the editor window from a stable Tools menu path.</summary>
    internal static class PlayModeTuningMenu
    {
        [MenuItem("Tools/Play Mode Tuning/Open")]
        private static void Open()
        {
            PlayModeTuningWindow.Open();
        }
    }
}
