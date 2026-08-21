using UnityEditor;

namespace ReferenceFinder
{
    internal static class ReferenceFinderMenu
    {
        private const string MenuPath = "Assets/Find Direct References";

        [MenuItem(MenuPath, false, 2000)]
        private static void FindSelected()
        {
            ReferenceFinderWindow.Open(Selection.activeObject);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateFindSelected()
        {
            return ReferenceFinderWindow.CanSearch(Selection.activeObject);
        }
    }
}
