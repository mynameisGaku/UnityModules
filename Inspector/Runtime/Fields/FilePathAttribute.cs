using System;

namespace Inspector
{
    /// <summary>
    /// <c>string</c> フィールドに「参照…」ボタンを付け、ファイル選択ダイアログから埋められるようにする。
    /// <code>
    /// [FilePath(Extension = "json", RelativeToProject = true)]
    /// [SerializeField] private string _configPath;
    /// </code>
    /// <para>
    /// 手打ちも許すのは、まだ存在しないファイルの出力先を書く場合があるため。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class FilePathAttribute : FieldDrawerAttribute
    {
        /// <summary>選ばせる拡張子（<c>"json"</c> のように点なし）。空なら何でも選べる。</summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// プロジェクトフォルダからの相対パスとして保存するか。
        /// 絶対パスのまま保存すると、別のマシンで開いたときに必ず壊れる。
        /// </summary>
        public bool RelativeToProject { get; set; } = true;

        /// <summary>ダイアログの見出し。</summary>
        public string Title { get; set; }
    }

    /// <summary>
    /// <c>string</c> フィールドに「参照…」ボタンを付け、フォルダ選択ダイアログから埋められるようにする。
    /// <see cref="FilePathAttribute"/> のフォルダ版。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class FolderPathAttribute : FieldDrawerAttribute
    {
        /// <inheritdoc cref="FilePathAttribute.RelativeToProject"/>
        public bool RelativeToProject { get; set; } = true;

        /// <summary>ダイアログの見出し。</summary>
        public string Title { get; set; }
    }
}
