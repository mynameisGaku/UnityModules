using System.Collections.Generic;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// Unity project から asmref source を読み取り専用で取得します。
    /// </summary>
    internal interface IAssemblyReferenceSourceAdapter
    {
        /// <summary>
        /// 全 asmref を読み取ります。1 件でも読めない場合は false を返します。
        /// </summary>
        bool TryReadAllAssemblyReferences(
            out IReadOnlyList<AssemblyReferenceSource> sources,
            out AssemblyDependencyAuditError error,
            out string errorMessage);
    }
}
