namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// assembly asset監査で検出する問題の種類を表します。
    /// </summary>
    internal enum AssemblyDependencyIssueKind
    {
        /// <summary>JSON を解析できません。</summary>
        InvalidJson,

        /// <summary>assembly 名が空です。</summary>
        MissingName,

        /// <summary>同名の assembly が複数あります。</summary>
        DuplicateName,

        /// <summary>同じ GUID を持つ asmdef が複数あります。</summary>
        DuplicateGuid,

        /// <summary>参照先を一意に決められません。</summary>
        AmbiguousReference,

        /// <summary>参照先が見つかりません。</summary>
        UnresolvedReference,

        /// <summary>自分自身を参照しています。</summary>
        SelfReference,

        /// <summary>Player 用 assembly が Editor 専用 assembly を参照しています。</summary>
        PlayerAssemblyReferencesEditorOnly,

        /// <summary>assembly 名参照と GUID 参照が混在しています。</summary>
        MixedReferenceKinds,

        /// <summary>includePlatforms と excludePlatforms が同時指定されています。</summary>
        IncludeAndExcludePlatforms,

        /// <summary>複数の assembly が循環参照しています。</summary>
        DependencyCycle,

        /// <summary>asmref JSON の構文または reference の型が不正です。</summary>
        InvalidAssemblyReferenceJson,

        /// <summary>asmref に有効な reference がありません。</summary>
        MissingAssemblyReference,

        /// <summary>asmref の target asmdef が見つかりません。</summary>
        UnresolvedAssemblyReference,

        /// <summary>asmref の target asmdef を一意に決められません。</summary>
        AmbiguousAssemblyReference
    }
}
