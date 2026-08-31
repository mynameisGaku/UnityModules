using UnityEditor;

namespace SceneWorkspace.Editor
{
    /// <summary>シーン作業セット画面を開くためのメニュー項目を追加します。</summary>
    internal static class SceneWorkspaceMenu
    {
        /// <summary>シーン作業セット画面を一つ開きます。</summary>
        [MenuItem("Tools/シーン作業セット/開く")]
        private static void Open()
        {
            SceneWorkspaceWindow.Open();
        }
    }
}
