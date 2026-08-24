using UnityEditor;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// Assembly Dependency Audit を Tools menu から開きます。
    /// </summary>
    internal static class AssemblyDependencyAuditMenu
    {
        /// <summary>Window を開く Tools menu の path です。</summary>
        private const string MenuPath = "Tools/Assembly Dependency Audit/Open";

        /// <summary>
        /// 読み取り専用の監査 Window を開きます。
        /// </summary>
        [MenuItem(MenuPath)]
        private static void Open()
        {
            AssemblyDependencyAuditWindow.Open();
        }
    }
}
