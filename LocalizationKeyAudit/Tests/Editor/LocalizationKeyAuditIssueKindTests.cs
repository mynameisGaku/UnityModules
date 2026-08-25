using System;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// v1.0.0 が区別する terminal failure と advisory finding の種別を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditIssueKindTests
    {
        /// <summary>
        /// fallback、coverage、重複、orphan を異なる問題種別として保持します。
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
