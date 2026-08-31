using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>ビルド実行アシスタントを開くメニューを登録します。</summary>
    internal static class BuildAssistantMenu
    {
        internal const string MenuPath = "Tools/ビルド実行アシスタント/開く";

        /// <summary>ビルド実行アシスタントを開くか、既存の画面へ移動します。</summary>
        [MenuItem(MenuPath)]
        internal static void Open()
        {
            BuildAssistantWindow.Open();
        }
    }
}
