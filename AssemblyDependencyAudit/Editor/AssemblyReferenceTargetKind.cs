namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmref の reference が使う対象指定方法を表します。
    /// </summary>
    internal enum AssemblyReferenceTargetKind
    {
        /// <summary>有効な reference を取得できませんでした。</summary>
        Unknown,

        /// <summary>assembly 名による指定です。</summary>
        Name,

        /// <summary>GUID による指定です。</summary>
        Guid
    }
}
