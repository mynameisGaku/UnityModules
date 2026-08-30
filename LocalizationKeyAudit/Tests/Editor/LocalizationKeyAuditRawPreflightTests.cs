using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 型として読み取る前に未加工のYAMLと読み取り専用条件を全件検証する契約を確認します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawPreflightTests
    {
        /// <summary>
        /// 正常なLF/CRLFのYAMLは入力順に依存せず、アセットパス順の識別情報になります。
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
        /// 項目の欠落、空、空のGUID、不正形式、重複を別々に拒否します。
        /// </summary>
        [TestCase(
            "m_Name: UI",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が共有テーブルデータ文書にありません。")]
        [TestCase(
            "m_TableCollectionNameGuidString:",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が空です。型として読み取るとアセットが未保存変更ありの状態になる可能性があります。")]
        [TestCase(
            "m_TableCollectionNameGuidString: 00000000000000000000000000000000",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が空のGUIDです。型として読み取るとアセットが未保存変更ありの状態になる可能性があります。")]
        [TestCase(
            "m_TableCollectionNameGuidString: not-a-guid",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString をGUIDとして解析できません。")]
        [TestCase(
            "m_TableCollectionNameGuidString: 11111111222233334444555555555555\n" +
            "m_TableCollectionNameGuidString: aaaaaaaabbbbccccddddeeeeeeeeeeee",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が共有テーブルデータ文書に 2 件あります。")]
        public void TryRun_InvalidGuidFieldFailsWithoutIdentity(string yaml, string expectedMessage)
        {
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Invalid Shared Data.asset",
                LocalizationKeyAuditTestData.CreateSharedTableDataYamlBytes(
                    yaml.Replace("\r\n", "\n").Split('\n'))));

            AssertPreflightFailure(source, "Assets/Localization/Invalid Shared Data.asset", expectedMessage);
        }

        /// <summary>
        /// 完全一致する項目名ではない類似名は、大文字小文字だけが違う場合も識別情報として認識しません。
        /// </summary>
        [TestCase("m_TableCollectionNameGuidStringExtra: 11111111222233334444555555555555")]
        [TestCase("M_TableCollectionNameGuidString: 11111111222233334444555555555555")]
        [TestCase("# m_TableCollectionNameGuidString: 11111111222233334444555555555555")]
        public void TryRun_FieldLookalikesAreNotAccepted(string yaml)
        {
            var source = SourceWith(LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Lookalike Shared Data.asset",
                LocalizationKeyAuditTestData.CreateSharedTableDataYamlBytes(new[] { yaml })));

            AssertPreflightFailure(
                source,
                "Assets/Localization/Lookalike Shared Data.asset",
                "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が共有テーブルデータ文書にありません。");
        }

        /// <summary>Unity標準形式のUTF-8 BOM付きYAMLも同じ識別情報として受理します。</summary>
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

        /// <summary>対象スクリプトとGUID項目が別文書なら対応を推測せず拒否します。</summary>
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

            AssertPreflightFailure(
                source,
                "Assets/Localization/Separated Shared Data.asset",
                "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が共有テーブルデータ文書にありません。");
        }

        /// <summary>複数行文字列または入れ子のマッピング内にある類似項目を直下項目として受理しません。</summary>
        [TestCase(
            "block-field",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString がMonoBehaviourの直下項目ではありません。")]
        [TestCase(
            "block-script",
            "共有テーブルデータのm_ScriptがMonoBehaviourの直下項目ではありません。")]
        [TestCase(
            "nested-field",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString がMonoBehaviourの直下項目ではありません。")]
        [TestCase(
            "nested-script",
            "共有テーブルデータのm_ScriptがMonoBehaviourの直下項目ではありません。")]
        [TestCase(
            "nested-duplicate",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString がMonoBehaviourの直下項目ではありません。")]
        [TestCase(
            "other-root-block",
            "Unity形式のYAMLの項目 m_TableCollectionNameGuidString がMonoBehaviourの直下項目ではありません。")]
        public void TryRun_BlockAndNestedLookalikesFailClosed(string shape, string expectedMessage)
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

            AssertPreflightFailure(source, "Assets/Localization/Nested Shared Data.asset", expectedMessage);
        }

        /// <summary>共有テーブルデータのスクリプトを持つ文書が複数なら識別情報を1件へまとめません。</summary>
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

            AssertPreflightFailure(
                source,
                "Assets/Localization/Multiple Shared Data.asset",
                "共有テーブルデータのスクリプトGUIDを持つUnity形式のYAML文書が 2 件あります。");
        }

        /// <summary>同じ文書の対象スクリプト項目が複数なら対応先を推測せず拒否します。</summary>
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

            AssertPreflightFailure(
                source,
                "Assets/Localization/Duplicate Script Shared Data.asset",
                "共有テーブルデータの直下にあるm_Script項目を一意に確定できません。");
        }

        /// <summary>別スクリプトの文書にある同名項目は共有テーブルデータの識別情報へ変換しません。</summary>
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

            AssertPreflightFailure(
                source,
                "Assets/Localization/Uncorrelated Shared Data.asset",
                "Unity形式のYAMLの項目 m_TableCollectionNameGuidString が共有テーブルデータのスクリプトと同じ文書にありません。");
        }

        /// <summary>無関係な文書があっても対象文書が1件なら直下項目から識別情報を返します。</summary>
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
        /// NULを含むバイナリデータと不正なUTF-8をYAML解析処理へ渡しません。
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

            AssertPreflightFailure(
                binary,
                "Assets/Localization/Binary Shared Data.asset",
                "共有テーブルデータにバイナリデータが含まれています。");
            AssertPreflightFailure(
                invalidUtf8,
                "Assets/Localization/Utf8 Shared Data.asset",
                "共有テーブルデータを厳密なUTF-8のUnity形式のYAMLとして読めません。");
        }

        /// <summary>
        /// 参照なしまたは空のバイト列は、共有テーブルデータ文書の欠落として拒否します。
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

            const string expectedMessage = "共有テーブルデータのスクリプトGUIDを持つUnity形式のYAML文書がありません。";
            AssertPreflightFailure(nullBytes, "Assets/Localization/Null Shared Data.asset", expectedMessage);
            AssertPreflightFailure(emptyBytes, "Assets/Localization/Empty Shared Data.asset", expectedMessage);
        }

        /// <summary>
        /// ファイル欠落、再解析点、容量超過、読み取りエラーを読み取り専用保証不能として拒否します。
        /// </summary>
        [TestCase("missing")]
        [TestCase("reparse")]
        [TestCase("oversize")]
        [TestCase("read-error")]
        public void TryRun_UnsafePhysicalStateFailsClosed(string state)
        {
            var validBytes = LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid);
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/UI Shared Data.asset",
                validBytes,
                exists: state != "missing",
                hasReparsePoint: state == "reparse",
                isOversize: state == "oversize",
                readError: state == "read-error" ? "IOException" : string.Empty);

            string expectedMessage;
            switch (state)
            {
                case "missing":
                    expectedMessage = "共有テーブルデータの物理ファイルが存在しません。";
                    break;
                case "reparse":
                    expectedMessage = "共有テーブルデータのパスに再解析点が含まれています。";
                    break;
                case "oversize":
                    expectedMessage =
                        $"共有テーブルデータがファイル1件あたりの上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumRawAssetBytes} バイトを超えています。";
                    break;
                default:
                    expectedMessage = "共有テーブルデータの物理ファイルを読み取れません：IOException";
                    break;
            }

            AssertPreflightFailure(SourceWith(asset), asset.AssetPath, expectedMessage);
        }

        /// <summary>記号を含む不透明な未加工入力のReadErrorを「あり」へ丸め、本文を公開しません。</summary>
        [Test]
        public void TryRun_OpaqueReadErrorDoesNotExposePhysicalCanary()
        {
            const string physicalCanary = "C:\\private\\raw-read-error-canary";
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/UI Shared Data.asset",
                LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid),
                readError: physicalCanary);

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                SourceWith(asset),
                out var identities,
                out var failureAssetPath,
                out var failureMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(identities, Is.Empty);
            Assert.That(failureAssetPath, Is.EqualTo(asset.AssetPath));
            Assert.That(failureMessage, Is.EqualTo("共有テーブルデータの物理ファイルを読み取れません：あり"));
            StringAssert.DoesNotContain(physicalCanary, failureMessage);
        }

        /// <summary>
        /// 取得元の参照なし、参照なしの戻り値、例外を捕捉し、部分的な識別情報を返しません。
        /// </summary>
        [Test]
        public void TryRun_SourceFailuresAreIsolated()
        {
            var nullResult = new FakeLocalizationKeyAuditRawSource { Assets = null };
            const string physicalCanary = "C:\\private\\raw-throw-canary";
            var throwing = new FakeLocalizationKeyAuditRawSource
            {
                Exception = new InvalidOperationException(physicalCanary)
            };

            AssertPreflightFailure(null, string.Empty, "未加工の共有テーブルデータの取得元がありません。");
            AssertPreflightFailure(nullResult, string.Empty, "未加工の共有テーブルデータの取得元から戻り値がありません。");
            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                throwing,
                out var identities,
                out var failureAssetPath,
                out var failureMessage);
            Assert.That(succeeded, Is.False);
            Assert.That(identities, Is.Empty);
            Assert.That(failureAssetPath, Is.Empty);
            Assert.That(
                failureMessage,
                Is.EqualTo("未加工の共有テーブルデータの全件収集に失敗しました：InvalidOperationException"));
            StringAssert.DoesNotContain(physicalCanary, failureMessage);
            Assert.That(nullResult.ReadCallCount, Is.EqualTo(1));
            Assert.That(throwing.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 前半が正常でも後半が不正なら検証済みの識別情報を全て破棄します。
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

            AssertPreflightFailure(
                source,
                "Assets/Localization/Z Shared Data.asset",
                "共有テーブルデータのスクリプトGUIDを持つUnity形式のYAML文書がありません。");
        }

        /// <summary>
        /// 参照なしの要素と、アセットパスまたは物理パスの重複を曖昧な未加工入力として拒否します。
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
                "未加工の共有テーブルデータ一覧に未設定の要素が含まれています。");
            AssertPreflightFailure(
                SourceWith(first, sameAssetPath),
                first.AssetPath,
                "同じ共有テーブルデータのアセットパスが複数回列挙されました。");
            AssertPreflightFailure(
                SourceWith(first, samePhysicalPath),
                samePhysicalPath.AssetPath,
                "同じ共有テーブルデータの物理パスが複数のアセットに対応しています。");
        }

        /// <summary>
        /// 解析可能なコレクション識別子（GUID）の重複は未保存変更の原因ではないため、未加工の識別情報を保持します。
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
        /// Unity相対アセットパスと絶対物理パスの境界を厳密に検証します。
        /// </summary>
        [TestCase("Localization/UI.asset", null, "共有テーブルデータのアセットパスがUnityの相対パスではありません。")]
        [TestCase("Assets\\Localization\\UI.asset", null, "共有テーブルデータのアセットパスがUnityの相対パスではありません。")]
        [TestCase("Assets/../UI.asset", null, "共有テーブルデータのアセットパスに不正な区切り要素があります。")]
        [TestCase("Assets//UI.asset", null, "共有テーブルデータのアセットパスに不正な区切り要素があります。")]
        [TestCase("Assets/Localization/UI.asset", "relative/file.asset", "共有テーブルデータの物理パスが絶対パスではありません。")]
        public void TryRun_InvalidPathsFailClosed(
            string assetPath,
            string physicalPath,
            string expectedMessage)
        {
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                assetPath,
                LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid),
                physicalPath ?? LocalizationKeyAuditTestData.CreatePhysicalPath(assetPath));

            AssertPreflightFailure(
                SourceWith(asset),
                physicalPath == null ? string.Empty : assetPath,
                expectedMessage);
        }

        /// <summary>絶対物理パスを装う不正なアセットパスを、失敗パスや理由へ公開しません。</summary>
        [Test]
        public void TryRun_InvalidAssetPathDoesNotExposePhysicalCanary()
        {
            const string physicalCanary = "C:\\private\\invalid-raw-asset-canary.asset";
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                physicalCanary,
                LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid),
                Path.GetFullPath("C:/Project/Localization/UI Shared Data.asset"));

            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                SourceWith(asset),
                out var identities,
                out var failureAssetPath,
                out var failureMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(identities, Is.Empty);
            Assert.That(failureAssetPath, Is.Empty);
            StringAssert.DoesNotContain(physicalCanary, failureMessage);
            Assert.That(failureMessage, Is.EqualTo("共有テーブルデータのアセットパスがUnityの相対パスではありません。"));
        }

        /// <summary>
        /// 未加工アセット数が上限を1件でも超えたら、要素を走査せず拒否します。
        /// </summary>
        [Test]
        public void TryRun_RejectsAssetCountAboveLimit()
        {
            var assets = new AuditEditor.LocalizationKeyAuditRawAsset[
                AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets + 1];
            var source = new FakeLocalizationKeyAuditRawSource { Assets = assets };

            AssertPreflightFailure(
                source,
                string.Empty,
                $"共有テーブルデータ数が上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
        }

        /// <summary>
        /// 取得元が容量超過フラグを付け忘れても、実バイト数がファイル1件の上限を超えたら拒否します。
        /// </summary>
        [Test]
        public void TryRun_RejectsByteCountAbovePerAssetLimit()
        {
            var bytes = new byte[AuditEditor.LocalizationKeyAuditLimits.MaximumRawAssetBytes + 1];
            var asset = LocalizationKeyAuditTestData.CreateRawAsset(
                "Assets/Localization/Large Shared Data.asset",
                bytes);

            AssertPreflightFailure(
                SourceWith(asset),
                asset.AssetPath,
                $"共有テーブルデータがファイル1件あたりの上限 {AuditEditor.LocalizationKeyAuditLimits.MaximumRawAssetBytes} バイトを超えています。");
        }

        /// <summary>
        /// 未加工アセットはコンストラクター入力と返却バイト列の変更を受けません。
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

        /// <summary>Unity形式のYAMLの冒頭宣言と指定文書を連結します。</summary>
        private static string CreateYaml(params string[] documents)
        {
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
                string.Join("\n", documents ?? Array.Empty<string>()) +
                "\n";
        }

        /// <summary>指定スクリプトと追加行を持つ標準MonoBehaviour文書を作ります。</summary>
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

        /// <summary>指定アセットだけを返す模擬取得元を作ります。</summary>
        private static FakeLocalizationKeyAuditRawSource SourceWith(
            params AuditEditor.LocalizationKeyAuditRawAsset[] assets)
        {
            return new FakeLocalizationKeyAuditRawSource { Assets = assets };
        }

        /// <summary>失敗時に識別情報が空で、パスと理由が限定されることを検証します。</summary>
        private static void AssertPreflightFailure(
            AuditEditor.ILocalizationKeyAuditRawSource source,
            string expectedAssetPath,
            string expectedMessage)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditRawPreflight.TryRun(
                source,
                out var identities,
                out var failureAssetPath,
                out var failureMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(identities, Is.Empty);
            Assert.That(failureAssetPath, Is.EqualTo(expectedAssetPath));
            Assert.That(failureMessage, Is.EqualTo(expectedMessage));
        }
    }
}
