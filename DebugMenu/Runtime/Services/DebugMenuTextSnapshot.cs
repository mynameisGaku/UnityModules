using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>メニュー全体を人が読める1行1項目のテキストへ変換する。</summary>
    public static class DebugMenuTextSnapshot
    {
        /// <summary>登録ページと接続された子ページを巡回してテキスト化する。</summary>
        public static string Capture(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var builder = new StringBuilder();
            var visitedPages = new HashSet<DebugPage>();
            var visitedElements = new HashSet<DebugElement>();
            var pages = menu.Pages;

            for (var i = 0; i < pages.Count; i++)
            {
                AppendPage(builder, pages[i], pages[i].Name, visitedPages, visitedElements);
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>メニュー全体のテキストをOSのクリップボードへ入れる。</summary>
        public static string CopyToClipboard(DebugMenuRoot menu)
        {
            var text = Capture(menu);
            GUIUtility.systemCopyBuffer = text;
            return text;
        }

        private static void AppendPage(
            StringBuilder builder,
            DebugPage page,
            string path,
            HashSet<DebugPage> visitedPages,
            HashSet<DebugElement> visitedElements)
        {
            if (page == null || !visitedPages.Add(page)) return;

            if (builder.Length > 0) builder.AppendLine();
            builder.Append("# ").AppendLine(path);

            var children = page.Root.Children;
            for (var i = 0; i < children.Count; i++)
            {
                AppendElement(builder, children[i], path, visitedPages, visitedElements);
            }
        }

        private static void AppendElement(
            StringBuilder builder,
            DebugElement element,
            string parentPath,
            HashSet<DebugPage> visitedPages,
            HashSet<DebugElement> visitedElements)
        {
            if (element == null) return;

            element.TryGetDisplayLabel(out var displayLabel);
            var label = Sanitize(displayLabel);
            var path = string.IsNullOrEmpty(parentPath) ? label : parentPath + " / " + label;
            if (visitedElements.Add(element))
            {
                builder.Append(path);
                element.TryGetDisplayValueText(out var displayValue);
                var value = Sanitize(displayValue);
                if (!string.IsNullOrEmpty(value)) builder.Append(" = ").Append(value);
                builder.AppendLine();
            }

            if (element is DebugPageLink link)
            {
                AppendPage(builder, link.Target, path, visitedPages, visitedElements);
                return;
            }

            var children = element.Children;
            for (var i = 0; i < children.Count; i++)
            {
                AppendElement(builder, children[i], path, visitedPages, visitedElements);
            }
        }

        private static string Sanitize(string value) => (value ?? string.Empty)
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n");
    }
}
