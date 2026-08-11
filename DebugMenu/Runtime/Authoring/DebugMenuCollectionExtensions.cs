using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>パスと数値配列の行を短く追加するための拡張。</summary>
    public static class DebugMenuCollectionExtensions
    {
        /// <summary>ページ直下に、ゲーム側の文字列へ接続するパス行を足す。</summary>
        public static DebugPath Path(
            this DebugPage page,
            string label,
            DebugPathMode mode,
            Func<string> getter,
            Action<string> setter)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var element = page.Root.Add(new DebugPath(label, mode, getter, setter));
            element.StructureChanged += page.Invalidate;
            page.Invalidate();
            return element;
        }

        /// <summary>ページ直下にファイルパス行を足す。</summary>
        public static DebugPath FilePath(this DebugPage page, string label, Func<string> getter, Action<string> setter) =>
            Path(page, label, DebugPathMode.File, getter, setter);

        /// <summary>ページ直下にフォルダーパス行を足す。</summary>
        public static DebugPath FolderPath(this DebugPage page, string label, Func<string> getter, Action<string> setter) =>
            Path(page, label, DebugPathMode.Folder, getter, setter);

        /// <summary>ページ直下に整数配列行を足す。</summary>
        public static DebugIntArray IntArray(this DebugPage page, string label, IList<int> values)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var element = page.Root.Add(new DebugIntArray(label, values));
            element.StructureChanged += page.Invalidate;
            page.Invalidate();
            return element;
        }

        /// <summary>ページ直下に小数配列行を足す。</summary>
        public static DebugFloatArray FloatArray(this DebugPage page, string label, IList<float> values)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var element = page.Root.Add(new DebugFloatArray(label, values));
            element.StructureChanged += page.Invalidate;
            page.Invalidate();
            return element;
        }

        /// <summary>親行の中に、ゲーム側の文字列へ接続するパス行を足す。</summary>
        public static DebugPath Path(
            this DebugElement parent,
            string label,
            DebugPathMode mode,
            Func<string> getter,
            Action<string> setter)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return parent.Add(new DebugPath(label, mode, getter, setter));
        }

        /// <summary>親行の中にファイルパス行を足す。</summary>
        public static DebugPath FilePath(this DebugElement parent, string label, Func<string> getter, Action<string> setter) =>
            Path(parent, label, DebugPathMode.File, getter, setter);

        /// <summary>親行の中にフォルダーパス行を足す。</summary>
        public static DebugPath FolderPath(this DebugElement parent, string label, Func<string> getter, Action<string> setter) =>
            Path(parent, label, DebugPathMode.Folder, getter, setter);

        /// <summary>親行の中に整数配列行を足す。</summary>
        public static DebugIntArray IntArray(this DebugElement parent, string label, IList<int> values)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return parent.Add(new DebugIntArray(label, values));
        }

        /// <summary>親行の中に小数配列行を足す。</summary>
        public static DebugFloatArray FloatArray(this DebugElement parent, string label, IList<float> values)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return parent.Add(new DebugFloatArray(label, values));
        }
    }
}
