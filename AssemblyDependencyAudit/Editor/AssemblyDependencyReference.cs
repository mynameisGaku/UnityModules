namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// 1 件の asmdef 参照と解決結果を保持します。
    /// </summary>
    internal sealed class AssemblyDependencyReference
    {
        /// <summary>
        /// 元の表記、表記種別、解決先 index を保持します。
        /// </summary>
        internal AssemblyDependencyReference(string value, AssemblyDependencyReferenceKind kind, int resolvedAssemblyIndex)
        {
            Value = value ?? string.Empty;
            Kind = kind;
            ResolvedAssemblyIndex = resolvedAssemblyIndex;
        }

        /// <summary>asmdef に記録されていた参照表記です。</summary>
        internal string Value { get; }

        /// <summary>assembly 名参照か GUID 参照かを示します。</summary>
        internal AssemblyDependencyReferenceKind Kind { get; }

        /// <summary>解決先 index です。未解決または曖昧な場合は -1 です。</summary>
        internal int ResolvedAssemblyIndex { get; }

        /// <summary>参照先を一意に解決できたかを示します。</summary>
        internal bool IsResolved => ResolvedAssemblyIndex >= 0;
    }
}
