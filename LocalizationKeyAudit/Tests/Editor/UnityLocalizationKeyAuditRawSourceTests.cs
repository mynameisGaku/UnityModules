using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 共有テーブルデータの物理走査による代替経路で、拡張子判定をプラットフォーム非依存に固定します。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditRawSourceTests
    {
        /// <summary>大文字小文字を問わず、拡張子が完全一致する.assetだけを受理します。</summary>
        [TestCase("Assets/Tables/UI.asset", true)]
        [TestCase("Assets/Tables/UI.ASSET", true)]
        [TestCase("Assets/Tables/UI.asset.meta", false)]
        [TestCase("Assets/Tables/UI.prefab", false)]
        public void IsAssetFilePath_UsesOrdinalIgnoreCaseExtension(string path, bool expected)
        {
            Assert.That(AuditEditor.UnityLocalizationKeyAuditRawSource.IsAssetFilePath(path), Is.EqualTo(expected));
        }

        /// <summary>候補上限への到達後は同じ対応の再登録だけを許し、新規パスを保持前に拒否します。</summary>
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

            var exception = Assert.Throws<InvalidDataException>(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.AddCandidatePath(
                    candidates,
                    "Assets/Tables/Overflow.asset",
                    Path.GetFullPath("C:/Tables/Overflow.asset")));
            Assert.That(
                exception.Message,
                Is.EqualTo(
                    $"共有テーブルデータ候補数が上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。"));
            Assert.That(candidates, Has.Count.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets));
        }

        /// <summary>未加工データの物理探索でも、ファイルとディレクトリを逐次上限へ達した直後に拒否します。</summary>
        [Test]
        public void IncrementPhysicalDiscoveryCount_RejectsBeforeNextEntry()
        {
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories - 1,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "ディレクトリ"),
                Is.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories));
            var exception = Assert.Throws<InvalidDataException>(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "ディレクトリ"));
            Assert.That(
                exception.Message,
                Is.EqualTo(
                    $"物理探索のディレクトリ数が上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories} 件を超えています。"));
        }

        /// <summary>保持範囲外にGUIDがある長いm_Script行を、候補なしと断定しません。</summary>
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

        /// <summary>型として読み取った共有テーブルデータの候補GUIDを並べ替える前に、厳密な件数上限で拒否します。</summary>
        [Test]
        public void EnsureTypedCandidateCountWithinLimit_RejectsAboveMaximum()
        {
            Assert.DoesNotThrow(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureTypedCandidateCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets));
            var exception = Assert.Throws<InvalidDataException>(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureTypedCandidateCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets + 1));
            Assert.That(
                exception.Message,
                Is.EqualTo(
                    $"型として読み取った共有テーブルデータ候補数が上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。"));
        }

        /// <summary>未加工データの実読取総量を、次のファイル分のメモリ確保前に拒否します。</summary>
        [Test]
        public void EnsureActualReadBudget_RejectsBeforeAllocation()
        {
            var maximum = AuditEditor.LocalizationKeyAuditLimits.MaximumTotalRawBytes;
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(maximum - 1, 1),
                Is.EqualTo(maximum));
            var overflow = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(maximum, 1));
            Assert.That(
                overflow.Message,
                Is.EqualTo($"未加工データの実読取バイト数が上限 {maximum} を超えています。"));
            var negative = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(
                () => AuditEditor.UnityLocalizationKeyAuditRawSource.EnsureActualReadBudget(0, -1));
            Assert.That(
                negative.Message,
                Is.EqualTo($"未加工データの実読取バイト数が上限 {maximum} を超えています。"));
        }

        /// <summary>物理パス由来の例外本文を未加工アセットへ入れず、安全な型名だけを保持します。</summary>
        [Test]
        public void ReadCandidate_PhysicalFailureStoresOnlyExceptionType()
        {
            var physicalCanary = Path.Combine(
                Path.GetTempPath(),
                "LocalizationKeyAuditRawSourcePhysicalCanary_" + System.Guid.NewGuid().ToString("N") + ".asset");
            File.WriteAllBytes(physicalCanary, new byte[] { 1 });
            try
            {
                var method = typeof(AuditEditor.UnityLocalizationKeyAuditRawSource).GetMethod(
                    "ReadCandidate",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var arguments = new object[]
                {
                    "Assets/Localization/UI Shared Data.asset",
                    physicalCanary,
                    0L
                };

                var asset = (AuditEditor.LocalizationKeyAuditRawAsset)method.Invoke(null, arguments);

                Assert.That(asset, Is.Not.Null);
                Assert.That(asset.ReadError, Is.EqualTo("InvalidDataException"));
                StringAssert.DoesNotContain(physicalCanary, asset.ReadError);
            }
            finally
            {
                if (File.Exists(physicalCanary))
                {
                    File.Delete(physicalCanary);
                }
            }
        }

        /// <summary>CRのみのUnity形式のYAMLでも、後続する対象スクリプト行を物理走査の代替候補として検出します。</summary>
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
