using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// physical SharedTableData fallbackの拡張子判定をplatform非依存に固定します。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditRawSourceTests
    {
        /// <summary>大文字小文字を問わずexact .assetだけを受理します。</summary>
        [TestCase("Assets/Tables/UI.asset", true)]
        [TestCase("Assets/Tables/UI.ASSET", true)]
        [TestCase("Assets/Tables/UI.asset.meta", false)]
        [TestCase("Assets/Tables/UI.prefab", false)]
        public void IsAssetFilePath_UsesOrdinalIgnoreCaseExtension(string path, bool expected)
        {
            Assert.That(AuditEditor.UnityLocalizationKeyAuditRawSource.IsAssetFilePath(path), Is.EqualTo(expected));
        }

        /// <summary>candidate上限到達後は同一mapping更新だけを許し、新規pathを保持前に拒否します。</summary>
        [Test]
        public void AddCandidatePath_RejectsNewPathBeforeGrowingPastLimit()
        {
            var candidates = new Dictionary<string, string>(System.StringComparer.Ordinal);
            for (var index = 0; index < AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets; index++)
            {
                candidates.Add($"Assets/Tables/{index:D4}.asset", Path.GetFullPath($"C:/Tables/{index:D4}.asset"));
            }

            var existingAssetPath = "Assets/Tables/0000.asset";
            var existingPhysicalPath = candidates[existingAssetPath];
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.AddCandidatePath(
                    candidates,
                    existingAssetPath,
                    existingPhysicalPath));
            Assert.That(candidates, Has.Count.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets));

            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.AddCandidatePath(
                    candidates,
                    "Assets/Tables/Overflow.asset",
                    Path.GetFullPath("C:/Tables/Overflow.asset")),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(candidates, Has.Count.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets));
        }

        /// <summary>raw discoveryもfileとdirectoryをstreaming上限到達直後に拒否します。</summary>
        [Test]
        public void IncrementPhysicalDiscoveryCount_RejectsBeforeNextEntry()
        {
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories - 1,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "directory"),
                Is.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "directory"),
                Throws.TypeOf<InvalidDataException>());
        }

        /// <summary>prefix外にGUIDがあるlong m_Script lineを候補なしと断定しません。</summary>
        [Test]
        public void IsTruncatedScriptLineIndeterminate_DetectsExactLongScriptKey()
        {
            var line = "  m_Script: {fileID: 11500000," + new string(' ', 1200) +
                "guid: " + AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid + ", type: 3}";
            var bytes = Encoding.ASCII.GetBytes(line);
            var prefix = new byte[1024];
            System.Array.Copy(bytes, prefix, prefix.Length);

            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.IsTruncatedScriptLineIndeterminate(
                    prefix,
                    prefix.Length,
                    true),
                Is.True);
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.IsTruncatedScriptLineIndeterminate(
                    Encoding.ASCII.GetBytes("  m_Name: " + new string('x', 1100)),
                    1024,
                    true),
                Is.False);

            var delayedColon = Encoding.ASCII.GetBytes("  m_Script" + new string(' ', 1200) +
                ": {guid: " + AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid + "}");
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.IsTruncatedScriptLineIndeterminate(
                    delayedColon,
                    1024,
                    true),
                Is.True);
        }

        /// <summary>typed candidate GUIDをsort前にexact上限で拒否します。</summary>
        [Test]
        public void EnsureTypedCandidateCountWithinLimit_RejectsAboveMaximum()
        {
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureTypedCandidateCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureTypedCandidateCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets + 1),
                Throws.TypeOf<InvalidDataException>());
        }

        /// <summary>raw actual read総量を次fileのallocation前に拒否します。</summary>
        [Test]
        public void EnsureActualReadBudget_RejectsBeforeAllocation()
        {
            var maximum = AuditEditor.LocalizationKeyAuditLimits.MaximumTotalRawBytes;
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(maximum - 1, 1),
                Is.EqualTo(maximum));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(maximum, 1),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(0, -1),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>CR-only Unity YAMLでも後続target script行をphysical fallback候補として検出します。</summary>
        [Test]
        public void ContainsSharedTableDataScriptGuid_RecognizesCarriageReturnOnlyLines()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "LocalizationKeyAuditRawSource_" + System.Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(
                    path,
                    "%YAML 1.1\r--- !u!114 &11400000\rMonoBehaviour:\r" +
                    "  m_Script: {fileID: 11500000, guid: " +
                    AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid +
                    ", type: 3}\r",
                    new UTF8Encoding(false));

                Assert.That(
                    AuditEditor.UnityLocalizationKeyAuditRawSource.ContainsSharedTableDataScriptGuid(path),
                    Is.True);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
