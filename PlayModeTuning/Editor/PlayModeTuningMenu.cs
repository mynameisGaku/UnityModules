using UnityEditor;

namespace PlayModeTuning.Editor
{
    /// <summary>編集画面を固定されたツールメニューから開けるようにします。</summary>
    internal static class PlayModeTuningMenu
    {
        [MenuItem("Tools/実行中調整/開く")]
        private static void Open()
        {
            PlayModeTuningWindow.Open();
        }
    }
}
