using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>型として読み取ったローカライズ用アセットを転送値へ複製する前の上限を検証します。</summary>
    internal sealed class UnityLocalizationKeyAuditTypedSourceTests
    {
        /// <summary>総数が上限と同じ場合を許し、残り上限の超過を割り当て前に拒否します。</summary>
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

            var exhausted = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    1,
                    maximum,
                    "Assets/Localization/C.asset"));
            var oversized = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureLocalizedEntryBudget(
                    maximum + 1,
                    0,
                    "Assets/Localization/D.asset"));

            Assert.That(
                exhausted.Message,
                Is.EqualTo($"ローカライズ済み項目総数が上限 {maximum} 件を超えています: Assets/Localization/C.asset"));
            Assert.That(
                oversized.Message,
                Is.EqualTo($"ローカライズ済み項目総数が上限 {maximum} 件を超えています: Assets/Localization/D.asset"));
        }

        /// <summary>同じテーブル群を複数コレクションへ割り当てる複製増幅を事前に拒否します。</summary>
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
            var tableLimit = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureCollectionViewBudget(
                    1,
                    0,
                    maximumTables,
                    0,
                    "Assets/Localization/UI Shared Data.asset"));
            var entryLimit = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditTypedSource.EnsureCollectionViewBudget(
                    0,
                    1,
                    0,
                    maximumEntries,
                    "Assets/Localization/UI Shared Data.asset"));

            Assert.That(
                tableLimit.Message,
                Is.EqualTo($"コレクション表示のロケールテーブル総数が上限 {maximumTables} 件を超えています: Assets/Localization/UI Shared Data.asset"));
            Assert.That(
                entryLimit.Message,
                Is.EqualTo($"コレクション表示のローカライズ済み項目総数が上限 {maximumEntries} 件を超えています: Assets/Localization/UI Shared Data.asset"));
        }
    }
}
