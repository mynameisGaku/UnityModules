// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// static reference を探した範囲、認識結果、走査完了状態を明示します。
    /// </summary>
    internal sealed class LocalizationKeyAuditCoverage
    {
        /// <summary>
        /// 宣言済み scope と、その範囲で認識できた参照を防御的に copy します。
        /// </summary>
        internal LocalizationKeyAuditCoverage(
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths,
            IReadOnlyList<LocalizationKeyAuditStaticReference> recognizedReferences,
            bool isComplete,
            string incompleteReason)
        {
            ScopeDescription = scopeDescription ?? string.Empty;
            DeclaredAssetPaths = CopyStrings(declaredAssetPaths);
            RecognizedReferences = CopyReferences(recognizedReferences);
            IsComplete = isComplete;
            IncompleteReason = incompleteReason ?? string.Empty;
        }

        /// <summary>利用者が宣言した static reference scope の説明です。</summary>
        internal string ScopeDescription { get; }

        /// <summary>走査対象として宣言した asset または folder path です。</summary>
        internal IReadOnlyList<string> DeclaredAssetPaths { get; }

        /// <summary>宣言済み scope 内で認識できた直接参照です。</summary>
        internal IReadOnlyList<LocalizationKeyAuditStaticReference> RecognizedReferences { get; }

        /// <summary>宣言済み scope の走査が打ち切りなく完了したかを示します。</summary>
        internal bool IsComplete { get; }

        /// <summary>未完了時の打ち切りまたは取得失敗理由です。</summary>
        internal string IncompleteReason { get; }

        /// <summary>結果 DTO 用に同じ内容の独立 snapshot を作ります。</summary>
        internal LocalizationKeyAuditCoverage Copy()
        {
            return new LocalizationKeyAuditCoverage(
                ScopeDescription,
                DeclaredAssetPaths,
                RecognizedReferences,
                IsComplete,
                IncompleteReason);
        }

        /// <summary>文字列一覧を読み取り専用 copy にします。</summary>
        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            var copy = new string[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            Array.Sort(copy, StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(copy);
        }

        /// <summary>参照一覧を各要素も含めて読み取り専用 copy にします。</summary>
        private static IReadOnlyList<LocalizationKeyAuditStaticReference> CopyReferences(
            IReadOnlyList<LocalizationKeyAuditStaticReference> values)
        {
            var copy = new LocalizationKeyAuditStaticReference[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                var value = values[index];
                copy[index] = value == null
                    ? null
                    : new LocalizationKeyAuditStaticReference(
                        value.SourceAssetPath,
                        value.CollectionGuid,
                        value.EntryId,
                        value.CollectionName,
                        value.EntryKey);
            }

            Array.Sort(copy, CompareReferences);
            return new ReadOnlyCollection<LocalizationKeyAuditStaticReference>(copy);
        }

        /// <summary>null、source path、GUID、entry ID の順に参照を並べます。</summary>
        private static int CompareReferences(
            LocalizationKeyAuditStaticReference left,
            LocalizationKeyAuditStaticReference right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var comparison = string.Compare(left.SourceAssetPath, right.SourceAssetPath, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.CollectionGuid.CompareTo(right.CollectionGuid);
            return comparison != 0 ? comparison : left.EntryId.CompareTo(right.EntryId);
        }
    }
}
