// SPDX-License-Identifier: MIT

using UnityEditor;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// Localization Key Audit の手動 Window を Tools menu から開きます。
    /// </summary>
    internal static class LocalizationKeyAuditMenu
    {
        /// <summary>Window を開く Tools menu path です。</summary>
        internal const string MenuPath = "Tools/Localization Key Audit/Open";

        /// <summary>読み取り専用 advisory 監査 Window を開きます。</summary>
        [MenuItem(MenuPath)]
        private static void Open()
        {
            LocalizationKeyAuditWindow.Open();
        }
    }
}
