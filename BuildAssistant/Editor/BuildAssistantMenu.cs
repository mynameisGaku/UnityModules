using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>Registers the stable Tools menu entry for Build Assistant.</summary>
    internal static class BuildAssistantMenu
    {
        internal const string MenuPath = "Tools/Build Assistant/Open";

        /// <summary>Opens or focuses the Build Assistant editor window.</summary>
        [MenuItem(MenuPath)]
        internal static void Open()
        {
            BuildAssistantWindow.Open();
        }
    }
}
