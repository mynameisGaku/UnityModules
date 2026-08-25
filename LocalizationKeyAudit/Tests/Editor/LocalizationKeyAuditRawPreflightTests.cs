using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// typed load より前に raw YAML と read-only 条件を全件検証する契約を確認します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawPreflightTests
    {
        /// <summary>
        /// valid な LF/CRLF YAML は入力順に依存せず asset path 順の identity になります。
        /// </summary>
        [Test]
        public void TryRun_ValidYamlReturnsDeterministicReadOnlyIdentities()
        {
            var firstGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var secondGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var source = new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[]
                {
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        "Packages/com.example.localization/B Shared Data.asset",
                        LocalizationKeyAuditTestData.CreateYamlBytes(secondGuid, "\r\n")),
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        "Assets/Localization/A Shared Data.asset",
                        LocalizationKeyAuditTestData.CreateYamlBytes(firstGuid))
                }
            };

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out var failureAssetPath,
                out var failureMessage);

            Assert.That(succeeded, Is.True, failureMessage);
            Assert.That(source.ReadCallCount, Is.EqualTo(1));
            Assert.That(failureAssetPath, Is.Empty);
            Assert.That(failureMessage, Is.Empty);
            Assert.That(identities.Select(identity => identity.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Localization/A Shared Data.asset",
                "Packages/com.example.localization/B Shared Data.asset"
            }));
            Assert.That(identities.Select(identity => identity.CollectionGuid), Is.EqualTo(new[]
            {
                firstGuid,
                secondGuid
            }));
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditRawIdentity>)identities).Clear());
        }

        /// <summary>
        /// field の欠落、空、empty GUID、malformed、重複を別々に拒否します。
        /// </summary>
        [TestCase("m_Name: UI", "SharedTableData document にありません")]
        [TestCase("m_TableCollectionNameGuidString:", "が空です")]
        [TestCase("m_TableCollectionNameGuidString: 00000000000000000000000000000000", "empty GUID")]
        [TestCase("m_TableCollectionNameGuidString: not-a-guid", "GUID として解析")]
        [TestCase(
            "m_TableCollectionNameGuidString: 11111111222233334444555555555555\n" +
            "m_TableCollectionNameGuidString: aaaaaaaabbbbccccddddeeeeeeeeeeee",
            "2 件")]
        public void TryRun_InvalidGuidFieldFailsWithoutIdentity(string yaml, string expectedMessagePart)
        {
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Invalid Shared Data.asset",
                LocalizationKeyAuditTestData.CreateSharedTableDataYamlBytes(
                    yaml.Replace("\r\n", "\n").Split('\n'))));

            AssertPreflightFailure(source, "Assets/Localization/Invalid Shared Data.asset", expectedMessagePart);
        }

        /// <summary>
        /// exact field 名と大文字小文字が一致しない lookalike は identity として認識しません。
        /// </summary>
        [TestCase("m_TableCollectionNameGuidStringExtra: 11111111222233334444555555555555")]
        [TestCase("M_TableCollectionNameGuidString: 11111111222233334444555555555555")]
        [TestCase("# m_TableCollectionNameGuidString: 11111111222233334444555555555555")]
        public void TryRun_FieldLookalikesAreNotAccepted(string yaml)
        {
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Lookalike Shared Data.asset",
                LocalizationKeyAuditTestData.CreateSharedTableDataYamlBytes(new[] { yaml })));

            AssertPreflightFailure(source, "Assets/Localization/Lookalike Shared Data.asset", "SharedTableData document にありません");
        }

        /// <summary>Unity標準shapeのUTF-8 BOM付きYAMLも同じidentityとして受理します。</summary>
        [Test]
        public void TryRun_StandardYamlWithBomIsAccepted()
        {
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Bom Shared Data.asset",
                LocalizationKeyAuditTestData.CreateYamlBytes(
                    LocalizationKeyAuditTestData.CollectionGuid,
                    "\n",
                    true)));

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out _,
                out var failureMessage);

            Assert.That(succeeded, Is.True, failureMessage);
            Assert.That(identities, Has.Count.EqualTo(1));
            Assert.That(identities[0].CollectionGuid, Is.EqualTo(LocalizationKeyAuditTestData.CollectionGuid));
        }

        /// <summary>target scriptとGUID fieldが別documentなら相関を推測せず拒否します。</summary>
        [Test]
        public void TryRun_ScriptAndFieldInDifferentDocumentsFailClosed()
        {
            var yaml = CreateYaml(
                CreateMonoBehaviourDocument(
                    "11400000",
                    AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid),
                CreateMonoBehaviourDocument(
                    "11400001",
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}"));
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Separated Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(yaml)));

            AssertPreflightFailure(source, "Assets/Localization/Separated Shared Data.asset", "SharedTableData document");
        }

        /// <summary>block scalarまたはnested mapping内のlookalikeはdirect fieldとして受理しません。</summary>
        [TestCase("block-field")]
        [TestCase("block-script")]
        [TestCase("nested-field")]
        [TestCase("nested-script")]
        [TestCase("nested-duplicate")]
        [TestCase("other-root-block")]
        public void TryRun_BlockAndNestedLookalikesFailClosed(string shape)
        {
            var targetGuid = AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid;
            string document;
            switch (shape)
            {
                case "block-field":
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        targetGuid,
                        "  m_Notes: |",
                        $"    {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
                case "block-script":
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        null,
                        "  m_Notes: |",
                        $"    m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}",
                        $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
                case "nested-field":
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        targetGuid,
                        "  m_Nested:",
                        $"    {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
                case "nested-script":
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        null,
                        "  m_Nested:",
                        $"    m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}",
                        $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
                case "other-root-block":
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        targetGuid,
                        "OtherRoot: |",
                        $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
                default:
                    document = CreateMonoBehaviourDocument(
                        "11400000",
                        targetGuid,
                        $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}",
                        "  m_Nested:",
                        $"    {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}");
                    break;
            }

            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Nested Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(CreateYaml(document))));

            AssertPreflightFailure(source, "Assets/Localization/Nested Shared Data.asset", "direct field");
        }

        /// <summary>SharedTableData scriptを持つdocumentが複数ならidentityを一件へ畳みません。</summary>
        [Test]
        public void TryRun_MultipleTargetDocumentsFailClosed()
        {
            var targetGuid = AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid;
            var yaml = CreateYaml(
                CreateMonoBehaviourDocument(
                    "11400000",
                    targetGuid,
                    $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}"),
                CreateMonoBehaviourDocument(
                    "11400001",
                    targetGuid,
                    "  m_TableCollectionNameGuidString: aaaaaaaabbbbccccddddeeeeeeeeeeee"));
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Multiple Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(yaml)));

            AssertPreflightFailure(source, "Assets/Localization/Multiple Shared Data.asset", "2 件");
        }

        /// <summary>同じdocumentのtarget script fieldが複数なら相関先を推測せず拒否します。</summary>
        [Test]
        public void TryRun_DuplicateDirectTargetScriptFailsClosed()
        {
            var targetGuid = AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid;
            var yaml = CreateYaml(CreateMonoBehaviourDocument(
                "11400000",
                targetGuid,
                $"  m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}",
                $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}"));
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Duplicate Script Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(yaml)));

            AssertPreflightFailure(source, "Assets/Localization/Duplicate Script Shared Data.asset", "m_Script field");
        }

        /// <summary>別scriptのdocumentにある同名fieldはSharedTableData identityへ変換しません。</summary>
        [Test]
        public void TryRun_FieldWithoutTargetScriptCorrelationFailsClosed()
        {
            var yaml = CreateYaml(CreateMonoBehaviourDocument(
                "11400000",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}"));
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Uncorrelated Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(yaml)));

            AssertPreflightFailure(source, "Assets/Localization/Uncorrelated Shared Data.asset", "同じ document");
        }

        /// <summary>無関係なdocumentがあってもtarget documentが一件ならdirect identityを返します。</summary>
        [Test]
        public void TryRun_UnrelatedDocumentAndSingleTargetDocumentAreAccepted()
        {
            var yaml = CreateYaml(
                CreateMonoBehaviourDocument("11400000", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                CreateMonoBehaviourDocument(
                    "11400001",
                    AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid,
                    $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}"));
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Multi Document Shared Data.asset",
                LocalizationKeyAuditTestData.Utf8(yaml)));

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out _,
                out var failureMessage);

            Assert.That(succeeded, Is.True, failureMessage);
            Assert.That(identities, Has.Count.EqualTo(1));
            Assert.That(identities[0].CollectionGuid, Is.EqualTo(LocalizationKeyAuditTestData.CollectionGuid));
        }

        /// <summary>
        /// NUL を含む binary data と不正 UTF-8 を YAML parser へ渡しません。
        /// </summary>
        [Test]
        public void TryRun_BinaryAndInvalidUtf8FailClosed()
        {
            var binary = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Binary Shared Data.asset",
                new byte[] { 1, 0, 2 }));
            var invalidUtf8 = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Utf8 Shared Data.asset",
                new byte[] { 0xC3, 0x28 }));

            AssertPreflightFailure(binary, "Assets/Localization/Binary Shared Data.asset", "binary data");
            AssertPreflightFailure(invalidUtf8, "Assets/Localization/Utf8 Shared Data.asset", "strict UTF-8");
        }

        /// <summary>
        /// null または空 byte snapshot は GUID field 欠落として拒否します。
        /// </summary>
        [Test]
        public void TryRun_NullAndEmptyBytesFailClosed()
        {
            var nullBytes = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Null Shared Data.asset",
                null));
            var emptyBytes = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Empty Shared Data.asset",
                Array.Empty<byte>()));

            AssertPreflightFailure(nullBytes, "Assets/Localization/Null Shared Data.asset", "がありません");
            AssertPreflightFailure(emptyBytes, "Assets/Localization/Empty Shared Data.asset", "がありません");
        }

        /// <summary>
        /// missing file、reparse point、oversize、read error を read-only 保証不能として拒否します。
        /// </summary>
        [TestCase("missing", "存在しません")]
        [TestCase("reparse", "reparse point")]
        [TestCase("oversize", "1 file 上限")]
        [TestCase("read-error", "access denied")]
        public void TryRun_UnsafePhysicalStateFailsClosed(string state, string expectedMessagePart)
        {
            var validBytes = LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid);
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/UI Shared Data.asset",
                validBytes,
                exists: state != "missing",
                hasReparsePoint: state == "reparse",
                isOversize: state == "oversize",
                readError: state == "read-error" ? "access denied" : string.Empty);

            AssertPreflightFailure(SourceWith(asset), asset.AssetPath, expectedMessagePart);
        }

        /// <summary>
        /// source の null、null return、例外を捕捉し、部分 identity を返しません。
        /// </summary>
        [Test]
        public void TryRun_SourceFailuresAreIsolated()
        {
            var nullResult = new FakeLocalizationKeyAuditRawSource { Assets = null };
            var throwing = new FakeLocalizationKeyAuditRawSource
            {
                Exception = new InvalidOperationException("boom")
            };

            AssertPreflightFailure(null, string.Empty, "source がありません");
            AssertPreflightFailure(nullResult, string.Empty, "null を返しました");
            AssertPreflightFailure(throwing, string.Empty, "InvalidOperationException: boom");
            Assert.That(nullResult.ReadCallCount, Is.EqualTo(1));
            Assert.That(throwing.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 前半が valid でも後半が invalid なら検証済み identity を全て破棄します。
        /// </summary>
        [Test]
        public void TryRun_LaterFailureDiscardsEarlierValidatedIdentity()
        {
            var source = new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[]
                {
                    LocalizationKeyAuditTestData.CreateValidRawAsset(
                        "Assets/Localization/A Shared Data.asset",
                        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        "Assets/Localization/Z Shared Data.asset",
                        Array.Empty<byte>())
                }
            };

            AssertPreflightFailure(source, "Assets/Localization/Z Shared Data.asset", "がありません");
        }

        /// <summary>
        /// null item と asset path、physical path の重複を曖昧な raw 入力として拒否します。
        /// </summary>
        [Test]
        public void TryRun_NullAndDuplicatePathsFailClosed()
        {
            var first = LocalizationKeyAuditTestData.CreateValidRawAsset(
                "Assets/Localization/A Shared Data.asset",
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            var sameAssetPath = LocalizationKeyAuditTestData.CreateValidRawAsset(
                first.AssetPath,
                Guid.Parse("11111111-2222-3333-4444-555555555555"));
            var samePhysicalPath = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/B Shared Data.asset",
                LocalizationKeyAuditTestData.CreateYamlBytes(
                    Guid.Parse("11111111-2222-3333-4444-555555555555")),
                first.PhysicalPath);
            AssertPreflightFailure(
                new FakeLocalizationKeyAuditRawSource
                {
                    Assets = new AuditEditor.LocalizationKeyAuditRawAsset[] { null }
                },
                string.Empty,
                "null");
            AssertPreflightFailure(SourceWith(first, sameAssetPath), first.AssetPath, "asset path");
            AssertPreflightFailure(SourceWith(first, samePhysicalPath), samePhysicalPath.AssetPath, "physical path");
        }

        /// <summary>
        /// parseable な collection GUID 重複は dirty 化条件ではないため raw identity を保持します。
        /// </summary>
        [Test]
        public void TryRun_DuplicateCollectionGuidPreservesBothRawIdentities()
        {
            var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var source = SourceWith(
                LocalizationKeyAuditTestData.CreateValidRawAsset(
                    "Assets/Localization/A Shared Data.asset",
                    guid),
                LocalizationKeyAuditTestData.CreateValidRawAsset(
                    "Assets/Localization/B Shared Data.asset",
                    guid));

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out _,
                out var failureMessage);

            Assert.That(succeeded, Is.True, failureMessage);
            Assert.That(identities, Has.Count.EqualTo(2));
            Assert.That(identities.All(identity => identity.CollectionGuid == guid), Is.True);
        }

        /// <summary>
        /// Unity relative asset path と absolute physical path の境界を厳密に検証します。
        /// </summary>
        [TestCase("Localization/UI.asset", null, "Unity relative path")]
        [TestCase("Assets\\Localization\\UI.asset", null, "Unity relative path")]
        [TestCase("Assets/../UI.asset", null, "不正な segment")]
        [TestCase("Assets//UI.asset", null, "不正な segment")]
        [TestCase("Assets/Localization/UI.asset", "relative/file.asset", "absolute path")]
        public void TryRun_InvalidPathsFailClosed(
            string assetPath,
            string physicalPath,
            string expectedMessagePart)
        {
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                assetPath,
                LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid),
                physicalPath ?? LocalizationKeyAuditTestData.CreatePhysicalPath(assetPath));

            AssertPreflightFailure(SourceWith(asset), assetPath, expectedMessagePart);
        }

        /// <summary>
        /// raw asset 数が上限を一件でも超えたら item を走査せず拒否します。
        /// </summary>
        [Test]
        public void TryRun_RejectsAssetCountAboveLimit()
        {
            var assets = new AuditEditor.LocalizationKeyAuditRawAsset[
                AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets + 1];
            var source = new FakeLocalizationKeyAuditRawSource { Assets = assets };

            AssertPreflightFailure(source, string.Empty, "数が上限");
        }

        /// <summary>
        /// provider が oversize flag を付け忘れても実 byte 数が 1 file 上限を超えたら拒否します。
        /// </summary>
        [Test]
        public void TryRun_RejectsByteCountAbovePerAssetLimit()
        {
            var bytes = new byte[AuditEditor.LocalizationKeyAuditLimits.MaximumRawAssetBytes + 1];
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Large Shared Data.asset",
                bytes);

            AssertPreflightFailure(SourceWith(asset), asset.AssetPath, "1 file 上限");
        }

        /// <summary>
        /// raw asset は constructor 入力と返却 byte の変更を受けません。
        /// </summary>
        [Test]
        public void RawAsset_DefensivelyCopiesBytes()
        {
            var bytes = LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid);
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/UI Shared Data.asset",
                bytes);
            bytes[0] = 0;
            var firstCopy = asset.CopyBytes();
            firstCopy[0] = 0;

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                SourceWith(asset),
                out var identities,
                out _,
                out var failureMessage);

            Assert.That(succeeded, Is.True, failureMessage);
            Assert.That(identities, Has.Count.EqualTo(1));
            Assert.That(asset.CopyBytes()[0], Is.Not.Zero);
        }

        /// <summary>Unity YAML preambleと指定documentを連結します。</summary>
        private static string CreateYaml(params string[] documents)
        {
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
                string.Join("\n", documents ?? Array.Empty<string>()) +
                "\n";
        }

        /// <summary>指定scriptと追加行を持つ標準MonoBehaviour documentを作ります。</summary>
        private static string CreateMonoBehaviourDocument(
            string anchor,
            string scriptGuid,
            params string[] serializedLines)
        {
            var lines = new List<string>
            {
                $"--- !u!114 &{anchor}",
                "MonoBehaviour:",
                "  m_ObjectHideFlags: 0"
            };
            if (!string.IsNullOrEmpty(scriptGuid))
            {
                lines.Add($"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}");
            }

            lines.AddRange(serializedLines ?? Array.Empty<string>());
            return string.Join("\n", lines);
        }

        /// <summary>指定 asset だけを返す fake source を作ります。</summary>
        private static FakeLocalizationKeyAuditRawSource SourceWith(
            params AuditEditor.LocalizationKeyAuditRawAsset[] assets)
        {
            return new FakeLocalizationKeyAuditRawSource { Assets = assets };
        }

        /// <summary>失敗時に identity が空で、path と理由が限定されることを検証します。</summary>
        private static void AssertPreflightFailure(
            AuditEditor.ILocalizationKeyAuditRawSource source,
            string expectedAssetPath,
            string expectedMessagePart)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out var failureAssetPath,
                out var failureMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(identities, Is.Empty);
            Assert.That(failureAssetPath, Is.EqualTo(expectedAssetPath));
            StringAssert.Contains(expectedMessagePart, failureMessage);
        }
    }
}
