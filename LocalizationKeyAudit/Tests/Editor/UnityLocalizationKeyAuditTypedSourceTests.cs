using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>typed Localization assetをDTOへcopyする前のhard limitを検証します。</summary>
    internal sealed class UnityLocalizationKeyAuditTypedSourceTests
    {
        /// <summary>aggregate上限exactを許し、残budget超過をallocation前に拒否します。</summary>
        [Test]
        public void EnsureLocalizedEntryBudget_RejectsBeforeCopyingPastAggregateLimit()
        {
            var maximum = AuditEditor.LocalizationKeyAuditLimits.MaximumLocalizedEntries;
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    maximum,
                    0,
                    "Assets/Localization/A.asset"));
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    1,
                    maximum - 1L,
                    "Assets/Localization/B.asset"));

            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    1,
                    maximum,
                    "Assets/Localization/C.asset"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    maximum + 1,
                    0,
                    "Assets/Localization/D.asset"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>同じtable groupを複数collectionへ割り当てるcopy増幅を事前に拒否します。</summary>
        [Test]
        public void EnsureCollectionViewBudget_RejectsDuplicateViewBeforeCopy()
        {
            var maximumTables = AuditEditor.LocalizationKeyAuditLimits.MaximumLocaleTables;
            var maximumEntries = AuditEditor.LocalizationKeyAuditLimits.MaximumLocalizedEntries;
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureCollectionViewBudget(
                    1,
                    1,
                    maximumTables - 1L,
                    maximumEntries - 1L,
                    "Assets/Localization/UI Shared Data.asset"));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureCollectionViewBudget(
                    1,
                    0,
                    maximumTables,
                    0,
                    "Assets/Localization/UI Shared Data.asset"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureCollectionViewBudget(
                    0,
                    1,
                    0,
                    maximumEntries,
                    "Assets/Localization/UI Shared Data.asset"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }
    }
}
