using System;
using System.Collections.Generic;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// asmdef・asmref source 読み取りと compiler path 解決を memory 上で再現します。
    /// </summary>
    internal sealed class FakeAssemblyDependencySourceAdapter :
        Editor.IAssemblyDependencySourceAdapter,
        Editor.IAssemblyReferenceSourceAdapter
    {
        /// <summary>読み取り成功時に返す source 一覧です。</summary>
        internal IReadOnlyList<Editor.AssemblyDefinitionSource> Sources { get; set; } = Array.Empty<Editor.AssemblyDefinitionSource>();

        /// <summary>source 読み取りを成功させるかを示します。</summary>
        internal bool ReadSucceeds { get; set; } = true;

        /// <summary>source 読み取り失敗時に返す説明です。</summary>
        internal string ReadError { get; set; } = string.Empty;

        /// <summary>asmdef source 読み取り失敗時に返す typed error です。</summary>
        internal Editor.AssemblyDependencyAuditError ReadAuditError { get; set; } =
            Editor.AssemblyDependencyAuditError.None;

        /// <summary>asmref 読み取り成功時に返す source 一覧です。</summary>
        internal IReadOnlyList<Editor.AssemblyReferenceSource> AssemblyReferenceSources { get; set; } =
            Array.Empty<Editor.AssemblyReferenceSource>();

        /// <summary>asmref source 読み取りを成功させるかを示します。</summary>
        internal bool AssemblyReferenceReadSucceeds { get; set; } = true;

        /// <summary>asmref source 読み取り失敗時に返す typed error です。</summary>
        internal Editor.AssemblyDependencyAuditError AssemblyReferenceReadError { get; set; } =
            Editor.AssemblyDependencyAuditError.None;

        /// <summary>asmref source 読み取り失敗時に返す説明です。</summary>
        internal string AssemblyReferenceReadErrorMessage { get; set; } = string.Empty;

        /// <summary>参照表記から compiler 解決 path への対応です。</summary>
        internal IDictionary<string, string> ReferencePaths { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>source 読み取り呼び出し回数です。</summary>
        internal int ReadCallCount { get; private set; }

        /// <summary>compiler path 解決呼び出し回数です。</summary>
        internal int ResolveCallCount { get; private set; }

        /// <summary>asmref source 読み取り呼び出し回数です。</summary>
        internal int AssemblyReferenceReadCallCount { get; private set; }

        /// <summary>
        /// 設定済みの source または失敗理由を返します。
        /// </summary>
        public bool TryReadAll(
            out IReadOnlyList<Editor.AssemblyDefinitionSource> sources,
            out Editor.AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            ReadCallCount++;
            sources = Sources;
            error = ReadAuditError;
            errorMessage = ReadError;
            return ReadSucceeds;
        }

        /// <summary>
        /// 登録済みの参照表記だけを path へ解決します。
        /// </summary>
        public bool TryResolveReferencePath(string reference, out string assetPath)
        {
            ResolveCallCount++;
            return ReferencePaths.TryGetValue(reference, out assetPath);
        }

        /// <summary>
        /// 設定済みの asmref source または typed failure を返します。
        /// </summary>
        public bool TryReadAllAssemblyReferences(
            out IReadOnlyList<Editor.AssemblyReferenceSource> sources,
            out Editor.AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            AssemblyReferenceReadCallCount++;
            sources = AssemblyReferenceSources;
            error = AssemblyReferenceReadError;
            errorMessage = AssemblyReferenceReadErrorMessage;
            return AssemblyReferenceReadSucceeds;
        }
    }
}
