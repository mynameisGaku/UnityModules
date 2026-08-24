using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// asmdef の論理 path、物理 path、raw meta GUID の安全な境界を検証します。
    /// </summary>
    internal sealed class AssemblyDefinitionSourcePathUtilityTests
    {
        /// <summary>
        /// typed と物理列挙の候補を正規化し、Ordinal 順で重複なく返します。
        /// </summary>
        [Test]
        public void MergeAssetPaths_NormalizesDeduplicatesAndSortsOrdinal()
        {
            var merged = AuditEditor.AssemblyDefinitionSourcePathUtility.MergeAssetPaths(
                new[]
                {
                    "Packages/zeta/Z.asmdef",
                    "Assets\\B.asmdef",
                    "Assets/A.asmdef",
                    "Assets/A.asmdef",
                    null,
                    "ProjectSettings/Skipped.asmdef"
                },
                new[]
                {
                    "Packages/alpha/A.asmdef",
                    "Assets/B.asmdef",
                    "Assets/a.asmdef",
                    "Assets/Samples~/Skipped.asmdef",
                    "Packages/zeta/Z.asmdef"
                });

            Assert.That(merged, Is.EqualTo(new[]
            {
                "Assets/A.asmdef",
                "Assets/B.asmdef",
                "Assets/a.asmdef",
                "Packages/alpha/A.asmdef",
                "Packages/zeta/Z.asmdef"
            }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)merged).Add("Assets/C.asmdef"));
        }

        /// <summary>
        /// null または空の候補一覧は空の merge 結果になります。
        /// </summary>
        [Test]
        public void MergeAssetPaths_NullAndEmptyInputsReturnEmptyList()
        {
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.MergeAssetPaths(null, null), Is.Empty);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.MergeAssetPaths(
                Array.Empty<string>(),
                Array.Empty<string>()), Is.Empty);
        }

        /// <summary>
        /// Assets または Packages 配下の asmdef だけを対象にします。
        /// </summary>
        [TestCase("Assets/A.asmdef")]
        [TestCase("Assets/Editor/A.ASMDEF")]
        [TestCase("Packages/com.example/A.asmdef")]
        [TestCase("Packages\\com.example\\Editor\\A.asmdef")]
        public void IsIncludedAssetPath_AcceptsAssetsAndPackagesAsmdefs(string assetPath)
        {
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.IsIncludedAssetPath(assetPath), Is.True);
        }

        /// <summary>
        /// 対象 root 外、空 segment、asmdef 以外、root の大小文字違いを拒否します。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("ProjectSettings/A.asmdef")]
        [TestCase("/Assets/A.asmdef")]
        [TestCase("assets/A.asmdef")]
        [TestCase("Assets.asmdef")]
        [TestCase("Assets/A.txt")]
        [TestCase("Assets//A.asmdef")]
        public void IsIncludedAssetPath_RejectsPathsOutsideSupportedRoots(string assetPath)
        {
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.IsIncludedAssetPath(assetPath), Is.False);
        }

        /// <summary>
        /// dot 始まりと末尾 tilde の directory を階層位置に関係なく除外します。
        /// </summary>
        [TestCase("Assets/.hidden/A.asmdef")]
        [TestCase("Assets/Nested/.cache/A.asmdef")]
        [TestCase("Assets/Generated~/A.asmdef")]
        [TestCase("Assets/Samples~/A.asmdef")]
        [TestCase("Assets/Documentation~/A.asmdef")]
        [TestCase("Packages/com.example/Samples~/A.asmdef")]
        [TestCase("Packages/com.example/Documentation~/A.asmdef")]
        public void IsIncludedAssetPath_RejectsIgnoredDirectories(string assetPath)
        {
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.IsIncludedAssetPath(assetPath), Is.False);
        }

        /// <summary>
        /// Windows separator を Unity separator へ変換し、null と空は空文字へ正規化します。
        /// </summary>
        [Test]
        public void NormalizeAssetPath_NormalizesWindowsSeparatorsAndEmptyValues()
        {
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.NormalizeAssetPath("Assets\\Nested\\A.asmdef"),
                Is.EqualTo("Assets/Nested/A.asmdef"));
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(null), Is.Empty);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(string.Empty), Is.Empty);
        }

        /// <summary>
        /// Assets 配下の物理 file と asset path を双方向に同じ file へ変換します。
        /// </summary>
        [Test]
        public void PathMapping_RoundTripsAssetsPhysicalAndAssetPaths()
        {
            var root = CreatePhysicalPath("AssetsRoot");
            var file = Path.Combine(root, "Nested", "A.asmdef");

            var mappedToAsset = AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Assets",
                root,
                file,
                out var assetPath);
            var mappedToPhysical = AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Assets",
                root,
                assetPath,
                out var physicalPath);

            Assert.That(mappedToAsset, Is.True);
            Assert.That(assetPath, Is.EqualTo("Assets/Nested/A.asmdef"));
            Assert.That(mappedToPhysical, Is.True);
            Assert.That(physicalPath, Is.EqualTo(Path.GetFullPath(file)));
        }

        /// <summary>
        /// Windows 形式の package root も separator を正規化して双方向に変換します。
        /// </summary>
        [Test]
        public void PathMapping_RoundTripsPackageRootWithWindowsSeparators()
        {
            var root = CreatePhysicalPath("PackageRoot");
            var file = Path.Combine(root, "Editor", "Package.asmdef");

            var mappedToAsset = AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Packages\\com.example",
                root,
                file,
                out var assetPath);
            var mappedToPhysical = AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Packages\\com.example",
                root,
                assetPath,
                out var physicalPath);

            Assert.That(mappedToAsset, Is.True);
            Assert.That(assetPath, Is.EqualTo("Packages/com.example/Editor/Package.asmdef"));
            Assert.That(mappedToPhysical, Is.True);
            Assert.That(physicalPath, Is.EqualTo(Path.GetFullPath(file)));
        }

        /// <summary>
        /// root 外、root 自体、無視 directory、asmdef 以外の物理 path を拒否します。
        /// </summary>
        [Test]
        public void TryMapPhysicalFileToAssetPath_RejectsRootEscapeAndInvalidFiles()
        {
            var parent = CreatePhysicalPath("PhysicalBoundary");
            var root = Path.Combine(parent, "Root");
            var outside = Path.Combine(parent, "Outside", "Escape.asmdef");

            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Assets", root, outside, out var escapedPath), Is.False);
            Assert.That(escapedPath, Is.Empty);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Assets", root, root, out _), Is.False);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Assets", root, Path.Combine(root, "A.txt"), out _), Is.False);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                "Assets", root, Path.Combine(root, "Samples~", "A.asmdef"), out _), Is.False);
        }

        /// <summary>
        /// asset path からの root escape、root 不一致、空入力を拒否します。
        /// </summary>
        [Test]
        public void TryMapAssetPathToPhysicalFile_RejectsRootEscapeMismatchAndEmptyValues()
        {
            var root = CreatePhysicalPath("AssetBoundary");

            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Assets", root, "Assets/../Escape.asmdef", out var escapedPath), Is.False);
            Assert.That(escapedPath, Is.Empty);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Assets", root, "Packages/com.example/A.asmdef", out _), Is.False);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                null, root, "Assets/A.asmdef", out _), Is.False);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Assets", null, "Assets/A.asmdef", out _), Is.False);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                "Assets", root, null, out _), Is.False);
        }

        /// <summary>
        /// 32桁の16進数 GUID は値の大小文字を変えず raw meta から取得します。
        /// </summary>
        [TestCase("fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n", "0123456789abcdef0123456789abcdef")]
        [TestCase("fileFormatVersion: 2\r\nguid: ABCDEF0123456789ABCDEF0123456789\r\n", "ABCDEF0123456789ABCDEF0123456789")]
        public void TryExtractGuidFromMeta_AcceptsValidHexAndPreservesCase(string metaText, string expectedGuid)
        {
            var succeeded = AuditEditor.AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(metaText, out var guid);

            Assert.That(succeeded, Is.True);
            Assert.That(guid, Is.EqualTo(expectedGuid));
        }

        /// <summary>
        /// 空、field 名の大小文字違い、桁不足、非16進数、余分な値を拒否します。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("fileFormatVersion: 2")]
        [TestCase("Guid: 0123456789abcdef0123456789abcdef")]
        [TestCase(" guid: 0123456789abcdef0123456789abcdef")]
        [TestCase("guid: 0123456789abcdef")]
        [TestCase("guid: 0123456789abcdef0123456789abcdeg")]
        [TestCase("guid: 0123456789abcdef0123456789abcdef extra")]
        public void TryExtractGuidFromMeta_RejectsMissingOrInvalidRawGuid(string metaText)
        {
            var succeeded = AuditEditor.AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(metaText, out var guid);

            Assert.That(succeeded, Is.False);
            Assert.That(guid, Is.Empty);
        }

        /// <summary>
        /// 最初に現れた guid field が不正なら後続の値へ読み飛ばしません。
        /// </summary>
        [Test]
        public void TryExtractGuidFromMeta_FirstInvalidGuidDoesNotFallThrough()
        {
            const string metaText = "guid: invalid\nguid: 0123456789abcdef0123456789abcdef\n";

            var succeeded = AuditEditor.AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(metaText, out var guid);

            Assert.That(succeeded, Is.False);
            Assert.That(guid, Is.Empty);
        }

        /// <summary>
        /// raw meta から得た同一 GUID を analyzer が重複として二件とも報告します。
        /// </summary>
        [Test]
        public void ExtractedRawMetaGuid_DuplicateFlowsIntoAnalyzerWithoutBrokenAssets()
        {
            const string firstMeta = "guid: abcdef0123456789abcdef0123456789\n";
            const string secondMeta = "guid: ABCDEF0123456789ABCDEF0123456789\n";
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(firstMeta, out var firstGuid), Is.True);
            Assert.That(AuditEditor.AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(secondMeta, out var secondGuid), Is.True);
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", firstGuid),
                AssemblyDependencyTestData.CreateSource("Assets/B.asmdef", "B", secondGuid)
            };

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues.Count(issue => issue.Kind == AuditEditor.AssemblyDependencyIssueKind.DuplicateGuid),
                Is.EqualTo(2));
        }

        /// <summary>存在を要求しない一意な物理 path を一時directory配下に作ります。</summary>
        private static string CreatePhysicalPath(string suffix)
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "AssemblyDependencyAuditPathUtilityTests",
                suffix));
        }
    }
}
