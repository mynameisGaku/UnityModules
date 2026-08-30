// SPDX-License-Identifier: MIT

using UnityEditor;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// ローカライズキー監査の手動ウィンドウをToolsメニューから開きます。
    /// </summary>
    internal static class LocalizationKeyAuditMenu
    {
        /// <summary>ウィンドウを開くToolsメニューのパスです。</summary>
        internal const string MenuPath = "Tools/ローカライズキー監査/開く";

        /// <summary>読み取り専用の判断用監査ウィンドウを開きます。</summary>
        [MenuItem(MenuPath)]
        private static void Open()
        {
            LocalizationKeyAuditWindow.Open();
        }
    }
}
