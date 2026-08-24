namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmdef の参照表記を表します。
    /// </summary>
    internal enum AssemblyDependencyReferenceKind
    {
        /// <summary>assembly 名による参照です。</summary>
        Name,

        /// <summary>GUID による参照です。</summary>
        Guid
    }
}
