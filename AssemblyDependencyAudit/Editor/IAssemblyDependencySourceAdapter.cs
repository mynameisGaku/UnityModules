using System.Collections.Generic;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// Unity project から asmdef source と参照解決結果を取得します。
    /// </summary>
    internal interface IAssemblyDependencySourceAdapter
    {
        /// <summary>
        /// 全 asmdef を読み取ります。1 件でも読めない場合は false を返します。
        /// </summary>
        bool TryReadAll(
            out IReadOnlyList<AssemblyDefinitionSource> sources,
            out AssemblyDependencyAuditError error,
            out string errorMessage);

        /// <summary>
        /// Unity compiler と同じ規則で参照表記から asmdef path を取得します。
        /// </summary>
        bool TryResolveReferencePath(string reference, out string assetPath);
    }
}
