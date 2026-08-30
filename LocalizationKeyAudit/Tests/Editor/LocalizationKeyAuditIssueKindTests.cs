using System;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 版1.0.0で区別する監査停止失敗と助言検出内容の種別を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditIssueKindTests
    {
        /// <summary>
        /// 代替処理、網羅情報、重複、孤立を異なる問題種別として保持します。
        /// </summary>
        [Test]
        public void Values_ContainCompleteVersionOneInventory()
        {
            Assert.That(Enum.GetNames(typeof(AuditEditor.LocalizationKeyAuditIssueKind)), Is.EqualTo(new[]
            {
                "ReadOnlyGuaranteeUnavailable",
                "InvalidConfiguration",
                "LimitExceeded",
                "AuditFailed",
                "RequiredLocaleNotConfigured",
                "MissingLocaleTable",
                "MissingDirectEntry",
                "EmptyDirectValue",
                "DanglingStaticReference",
                "NoStaticReferenceFoundWithinDeclaredScope",
                "StaticReferenceCoverageIncomplete",
                "DuplicateCollectionName",
                "DuplicateCollectionGuid",
                "DuplicateSharedEntryId",
                "DuplicateSharedEntryKey",
                "DuplicateLocaleTable",
                "DuplicateLocalizedEntryId",
                "OrphanedLocalizedEntry",
                "OrphanedLocaleTable",
                "OrphanedSharedTableData",
                "DuplicateLocaleIdentifier"
            }));
        }
    }
}
