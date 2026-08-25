using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 監査の全 hard limit を v1.0.0 の固定契約として検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLimitsTests
    {
        /// <summary>
        /// 入力、raw data、typed snapshot、graph、結果の上限値を意図せず緩めません。
        /// </summary>
        [Test]
        public void Constants_MatchVersionOneSafetyBudget()
        {
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumRequiredLocales, Is.EqualTo(256));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths, Is.EqualTo(100000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumStaticReferences, Is.EqualTo(250000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageAssetBytes, Is.EqualTo(16 * 1024 * 1024));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageYamlLines, Is.EqualTo(1000000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageTotalBytes, Is.EqualTo(512L * 1024L * 1024L));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets, Is.EqualTo(4096));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumRawAssetBytes, Is.EqualTo(16 * 1024 * 1024));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumTotalRawBytes, Is.EqualTo(256L * 1024L * 1024L));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles, Is.EqualTo(1000000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories, Is.EqualTo(100000));
            Assert.That(
                AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDiscoveryBytes,
                Is.EqualTo(8L * 1024L * 1024L * 1024L));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumLocales, Is.EqualTo(4096));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumCollections, Is.EqualTo(4096));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumLocaleTables, Is.EqualTo(65536));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumSharedEntries, Is.EqualTo(1000000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumLocalizedEntries, Is.EqualTo(4000000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumGraphEdges, Is.EqualTo(5000000L));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumIssues, Is.EqualTo(100000));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters, Is.EqualTo(32768));
            Assert.That(AuditEditor.LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters, Is.EqualTo(1048576));
        }
    }
}
