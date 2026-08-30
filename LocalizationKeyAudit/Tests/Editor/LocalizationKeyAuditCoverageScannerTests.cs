using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Assetsと登録済みパッケージのUnity形式のYAMLから、GUIDと項目識別子による直接参照を安全側に倒して抽出する契約を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditCoverageScannerTests
    {
        /// <summary>
        /// LF、CRLF、BOM、引用符付きGUIDを受理し、重複を除いた参照をパス、GUID、項目識別子の順へ固定します。
        /// </summary>
        [Test]
        public void Scan_ValidAssetsReturnDeterministicCompleteCoverage()
        {
            var firstGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var secondGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var duplicatePair = ReferenceYaml(firstGuid, 10, "\r\n", true);
            var firstBytes = AddUtf8Bom(ConcatYamlDocuments(duplicatePair, duplicatePair));
            var source = new FakeLocalizationKeyAuditCoverageSource
            {
                Assets = new[]
                {
                    Asset("Assets/Scenes/Z.unity", ReferenceYaml(secondGuid, 20)),
                    Asset("Assets/Prefabs/A.prefab", firstBytes)
                }
            };
            var declaredPaths = new[] { "Assets/Scenes", "Assets/Prefabs" };

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "ゲーム用アセット",
                declaredPaths,
                source);

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.IncompleteReason, Is.Empty);
            Assert.That(coverage.ScopeDescription, Is.EqualTo("ゲーム用アセット"));
            Assert.That(
                coverage.DeclaredAssetPaths,
                Is.EqualTo(new[] { "Assets/Prefabs", "Assets/Scenes" }));
            Assert.That(source.ReadCallCount, Is.EqualTo(1));
            Assert.That(
                source.LastDeclaredAssetPaths,
                Is.EqualTo(new[] { "Assets/Prefabs", "Assets/Scenes" }));
            Assert.That(coverage.RecognizedReferences, Has.Count.EqualTo(2));
            Assert.That(
                coverage.RecognizedReferences.Select(reference => reference.SourceAssetPath),
                Is.EqualTo(new[] { "Assets/Prefabs/A.prefab", "Assets/Scenes/Z.unity" }));
            Assert.That(
                coverage.RecognizedReferences.Select(reference => reference.CollectionGuid),
                Is.EqualTo(new[] { firstGuid, secondGuid }));
            Assert.That(
                coverage.RecognizedReferences.Select(reference => reference.EntryId),
                Is.EqualTo(new long[] { 10, 20 }));
        }

        /// <summary>Assetsとパッケージ、または異なるパッケージの混在を取得元の呼出前に拒否します。</summary>
        [TestCase("Assets", "Packages/com.example")]
        [TestCase("Packages/com.alpha", "Packages/com.beta")]
        public void Scan_MixedLogicalRootsRejectWithoutReadingSourceOrPartialData(
            string firstDeclaredPath,
            string secondDeclaredPath)
        {
            var source = SourceWith(Asset(
                "Packages/com.example/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "異なる論理ルート",
                new[] { firstDeclaredPath, secondDeclaredPath },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.DeclaredAssetPaths, Is.Empty);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(source.ReadCallCount, Is.Zero);
            StringAssert.Contains("論理上のルート", coverage.IncompleteReason);
        }

        /// <summary>
        /// 同じパッケージのルート、入れ子のフォルダー、直接指定したファイルを受理し、参照を取得元パスの序数順へ固定します。
        /// </summary>
        [Test]
        public void Scan_SamePackageScopesReturnOrdinalCompleteCoverage()
        {
            var source = SourceWith(
                Asset(
                    "Packages/com.example/Runtime/Z.asset",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 30)),
                Asset(
                    "Packages/com.example/A.prefab",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)),
                Asset(
                    "Packages/com.example/Runtime/A.asset",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 20)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[]
                {
                    "Packages/com.example/Runtime/Z.asset",
                    "Packages/com.example",
                    "Packages/com.example/Runtime"
                },
                source);

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.DeclaredAssetPaths, Is.EqualTo(new[]
            {
                "Packages/com.example",
                "Packages/com.example/Runtime",
                "Packages/com.example/Runtime/Z.asset"
            }));
            Assert.That(source.LastDeclaredAssetPaths, Is.EqualTo(coverage.DeclaredAssetPaths));
            Assert.That(
                coverage.RecognizedReferences.Select(reference => reference.SourceAssetPath),
                Is.EqualTo(new[]
                {
                    "Packages/com.example/A.prefab",
                    "Packages/com.example/Runtime/A.asset",
                    "Packages/com.example/Runtime/Z.asset"
                }));
            Assert.That(
                coverage.RecognizedReferences.Select(reference => reference.EntryId),
                Is.EqualTo(new long[] { 10, 20, 30 }));
        }

        /// <summary>Packagesだけの指定、PackageCache、安全でない区切り要素、逆斜線を取得元の呼出前に拒否します。</summary>
        [TestCase("Packages")]
        [TestCase("Library/PackageCache/com.example/A.asset")]
        [TestCase("Packages/com.example/../A.asset")]
        [TestCase("Packages/com.example//A.asset")]
        [TestCase("Packages/com.example/Generated~/A.asset")]
        [TestCase("Packages/com.example/Bad:Name/A.asset")]
        [TestCase("Packages/com.example/Trailing./A.asset")]
        [TestCase("Packages/com.example/Trailing /A.asset")]
        [TestCase("Packages\\com.example\\A.asset")]
        public void Scan_InvalidDeclaredPackagePathRejectsWithoutPartialData(string declaredPath)
        {
            var source = SourceWith(Asset(
                "Packages/com.example/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { declaredPath },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(source.ReadCallCount, Is.Zero);
            StringAssert.Contains("対象アセットパス", coverage.IncompleteReason);
        }

        /// <summary>同じパッケージ内の指定パス重複を取得元の呼出前に拒否します。</summary>
        [Test]
        public void Scan_DuplicatePackageDeclaredPathRejectsWithoutPartialData()
        {
            var source = SourceWith(Asset(
                "Packages/com.example/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { "Packages/com.example", "Packages/com.example" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(source.ReadCallCount, Is.Zero);
            StringAssert.Contains("重複", coverage.IncompleteReason);
        }

        /// <summary>パッケージ取得元の失敗を網羅未完了へ隔離し、物理パスの詳細を公開しません。</summary>
        [Test]
        public void Scan_PackageSourceFailureReturnsNoPartialDataOrOpaqueDetails()
        {
            const string physicalCanary = "C:\\private\\registered-package-canary";
            var source = new FakeLocalizationKeyAuditCoverageSource
            {
                Exception = new System.IO.InvalidDataException(
                    "登録済みパッケージのルートを解決できません: " + physicalCanary)
            };

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { "Packages/com.missing" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(source.ReadCallCount, Is.EqualTo(1));
            StringAssert.Contains("InvalidDataException", coverage.IncompleteReason);
            StringAssert.DoesNotContain(physicalCanary, coverage.IncompleteReason);
        }

        /// <summary>CRだけのUnity形式のYAMLも、LFやCRLFと同じ直接参照として解析します。</summary>
        [Test]
        public void Scan_CarriageReturnOnlyYamlReturnsCompleteCoverage()
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset(
                    "Assets/CarriageReturn.prefab",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10, "\r"))));

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.RecognizedReferences, Has.Count.EqualTo(1));
        }

        /// <summary>Unity形式のYAMLの行数を、配列へ保持する前の正確な境界で拒否します。</summary>
        [Test]
        public void CoverageYamlLineCount_UsesExactHardLimit()
        {
            Assert.That(
                AuditEditor.LocalizationKeyAuditCoverageScanner.IsCoverageYamlLineCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageYamlLines),
                Is.True);
            Assert.That(
                AuditEditor.LocalizationKeyAuditCoverageScanner.IsCoverageYamlLineCountWithinLimit(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageYamlLines + 1),
                Is.False);
        }

        /// <summary>
        /// バイナリー、無効なUTF-8、ヘッダー欠落、タブによる字下げを網羅未完了にします。
        /// </summary>
        [Test]
        public void Scan_UnsupportedEncodingAndYamlFormsFailClosed()
        {
            AssertIncompleteReason(
                SourceWith(Asset("Assets/Binary.asset", new byte[] { 1, 0, 2 })),
                "Assets/Binary.asset: バイナリーデータは、第1版の静的参照走査では未対応です。");
            AssertIncompleteReason(
                SourceWith(Asset("Assets/Utf8.asset", new byte[] { 0xC3, 0x28 })),
                "Assets/Utf8.asset: Unity形式のYAMLを厳密なUTF-8として読み取れません。");
            AssertIncompleteReason(
                SourceWith(Asset("Assets/Text.asset", Encoding.UTF8.GetBytes("plain text"))),
                "Assets/Text.asset: Unity形式のYAMLヘッダーがないテキストまたはバイナリー形式は未対応です。");
            AssertIncompleteReason(
                SourceWith(Asset(
                    "Assets/Tab.asset",
                    Encoding.UTF8.GetBytes("%YAML 1.1\n\tm_TableReference:\n"))),
                "Assets/Tab.asset: タブ文字は、Unity形式のYAMLを安全側に倒して解析する処理では未対応です。");
            AssertIncompleteReason(
                SourceWith(Asset(
                    "Assets/InlineTab.asset",
                    Encoding.UTF8.GetBytes("%YAML 1.1\nMonoBehaviour:\n  m_Text:\t|\n"))),
                "Assets/InlineTab.asset: タブ文字は、Unity形式のYAMLを安全側に倒して解析する処理では未対応です。");
        }

        /// <summary>
        /// 名前指定のテーブル、空の項目識別子、項目重複を対応済みの識別情報と誤認しません。
        /// </summary>
        [Test]
        public void Scan_UnsupportedReferenceIdentitiesFailClosed()
        {
            var nameBased = ReferenceYamlText(LocalizationKeyAuditTestData.CollectionGuid, 10)
                .Replace($"GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}", "UI");
            var emptyId = ReferenceYamlText(LocalizationKeyAuditTestData.CollectionGuid, 10)
                .Replace("m_KeyId: 10", "m_KeyId: 0");
            var duplicateGuidField = ReferenceYamlText(LocalizationKeyAuditTestData.CollectionGuid, 10)
                .Replace(
                    $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                    $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}\n" +
                    $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}");

            AssertIncomplete(
                SourceWith(Asset("Assets/NameBased.asset", Encoding.UTF8.GetBytes(nameBased))),
                "名前指定");
            AssertIncomplete(
                SourceWith(Asset("Assets/EmptyId.asset", Encoding.UTF8.GetBytes(emptyId))),
                "テーブル項目参照");
            AssertIncomplete(
                SourceWith(Asset("Assets/Duplicate.asset", Encoding.UTF8.GetBytes(duplicateGuidField))),
                "空でない単一の値");
        }

        /// <summary>
        /// テーブルブロックが項目ブロックより前に途切れたアセットを、参照ゼロの網羅完了としません。
        /// </summary>
        [Test]
        public void Scan_TruncatedTableReferenceBlockIsIncomplete()
        {
            var yaml = string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                string.Empty
            });

            AssertIncomplete(
                SourceWith(Asset("Assets/Truncated.asset", Encoding.UTF8.GetBytes(yaml))),
                "項目参照");
        }

        /// <summary>
        /// テーブルと項目の間に同じ字下げ幅の別項目がある未対応配置を網羅未完了にし、先行参照も破棄します。
        /// </summary>
        [Test]
        public void NonAdjacentEntryBlock_ReturnsIncompleteAndDiscardsReferences()
        {
            var unsupportedSiblingShapes = new[]
            {
                new[] { "    m_Other: 1", "    m_TableEntryReference:", "      m_KeyId: 20" },
                new[] { "  m_TableEntryReference:", "    m_KeyId: 20" },
                new[] { "    m_TableEntryReference: { m_KeyId: 20 }" }
            };

            for (var index = 0; index < unsupportedSiblingShapes.Length; index++)
            {
                var lines = new List<string>
                {
                    "%YAML 1.1",
                    "--- !u!114 &11400001",
                    "MonoBehaviour:",
                    "  m_Localized:",
                    "    m_TableReference:",
                    $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}"
                };
                lines.AddRange(unsupportedSiblingShapes[index]);
                lines.Add(string.Empty);
                var malformed = Encoding.UTF8.GetBytes(string.Join("\n", lines));
                var source = SourceWith(Asset(
                    $"Assets/UnsupportedSibling{index}.prefab",
                    ConcatYamlDocuments(
                        ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                        malformed)));

                var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                    "Assets",
                    new[] { "Assets" },
                    source);

                Assert.That(coverage.IsComplete, Is.False, $"shape {index}");
                Assert.That(coverage.RecognizedReferences, Is.Empty, $"shape {index}");
                Assert.That(coverage.IncompleteReason, Is.Not.Empty, $"shape {index}");
            }
        }

        /// <summary>配列要素にある未対応の参照構造を見逃さず、先行参照も破棄します。</summary>
        [Test]
        public void SequenceTableReference_ReturnsIncompleteAndDiscardsReferences()
        {
            var sequenceYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_LocalizedValues:",
                "  - m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                string.Empty
            }));
            var source = SourceWith(Asset(
                "Assets/Sequence.prefab",
                ConcatYamlDocuments(
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                    sequenceYaml)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("YAMLの配列", coverage.IncompleteReason);
        }

        /// <summary>ブロック形式の複数行文字列内にある見かけ上の参照を、構造として採用しません。</summary>
        [TestCase("|")]
        [TestCase("&note |")]
        [TestCase("!!str >-")]
        public void BlockScalarLookalike_IsIgnoredWithoutCreatingReference(string scalarHeader)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Text: " + scalarHeader,
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/Text.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("ブロック形式の複数行文字列", coverage.IncompleteReason);
        }

        /// <summary>配列内のブロック形式文字列にある見かけ上の参照も、構造として採用しません。</summary>
        [Test]
        public void SequenceBlockScalarLookalike_IsIgnoredWithoutCreatingReference()
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Texts:",
                "  - |",
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/SequenceText.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("ブロック形式の複数行文字列", coverage.IncompleteReason);
        }

        /// <summary>引用符付きマッピングキー内のコロンを、値の区切りと誤認しません。</summary>
        [TestCase("  \"label: note\": |")]
        [TestCase("  'label: note': >-")]
        public void QuotedMappingKeyBlockScalar_ReturnsIncomplete(string header)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                header,
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/QuotedKey.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("ブロック形式の複数行文字列", coverage.IncompleteReason);
        }

        /// <summary>非固有タグ付きのブロック形式文字列も、本文を構造として採用しません。</summary>
        [TestCase("  m_Text: ! |")]
        [TestCase("  - ! >-")]
        public void NonSpecificTagBlockScalar_ReturnsIncomplete(string header)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                header,
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/NonSpecificTag.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("ブロック形式の複数行文字列", coverage.IncompleteReason);
        }

        /// <summary>入れ子の配列内にあるブロック形式文字列の本文も、構造として採用しません。</summary>
        [TestCase("  - - |")]
        [TestCase("  - - m_Text: >-")]
        public void NestedSequenceBlockScalar_ReturnsIncomplete(string header)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                header,
                "      m_TableReference:",
                $"        m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "      m_TableEntryReference:",
                "        m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/NestedSequence.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("ブロック形式の複数行文字列", coverage.IncompleteReason);
        }

        /// <summary>明示的なマッピングキー内の見かけ上の参照を、構造として採用しません。</summary>
        [TestCase("  ? |")]
        [TestCase("  - ? >-")]
        public void ExplicitMappingKeyScalar_ReturnsIncomplete(string header)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                header,
                "      m_TableReference:",
                $"        m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "      m_TableEntryReference:",
                "        m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/ExplicitMappingKey.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("明示的なマッピングキー", coverage.IncompleteReason);
        }

        /// <summary>ブロック形式文字列に見えるコメントは無視し、その後の実参照を隠しません。</summary>
        [Test]
        public void CommentBlockScalarLookalike_DoesNotHideActualReference()
        {
            var yaml = Encoding.UTF8.GetBytes(
                ReferenceYamlText(LocalizationKeyAuditTestData.CollectionGuid, 20)
                    .Replace(
                        "  m_Localized:\n    m_TableReference:",
                        "  m_Localized:\n  # note: |\n    m_TableReference:"));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/CommentThenReference.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.RecognizedReferences, Has.Count.EqualTo(1));
        }

        /// <summary>複数行の引用符付き文字列内にある見かけ上の参照を、構造参照として採用しません。</summary>
        [Test]
        public void MultilineQuotedScalar_ReturnsIncompleteAndDiscardsReferences()
        {
            var quotedYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Text: \"hello",
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                "  world\"",
                string.Empty
            }));
            var source = SourceWith(Asset(
                "Assets/Quoted.prefab",
                ConcatYamlDocuments(
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                    quotedYaml)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("引用符付き文字列", coverage.IncompleteReason);
        }

        /// <summary>複数行のフロー形式コレクション内にある引用符付きの見かけ上の参照も、構造参照として採用しません。</summary>
        [Test]
        public void MultilineFlowCollection_ReturnsIncompleteAndDiscardsReferences()
        {
            var flowYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Map: { \"foo\": \"hello",
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                "  world\" }",
                string.Empty
            }));
            var source = SourceWith(Asset(
                "Assets/MultilineFlow.prefab",
                ConcatYamlDocuments(
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                    flowYaml)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("フロー形式コレクション", coverage.IncompleteReason);
        }

        /// <summary>配列のフローマッピング内にあるコロンを、外側のマッピング区切りと誤認しません。</summary>
        [Test]
        public void SequenceMultilineFlowCollection_ReturnsIncompleteAndDiscardsReferences()
        {
            var flowYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  - { \"closed\": \"yes\", \"open\": \"hello",
                "    m_TableReference:",
                $"      m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_KeyId: 20",
                "  world\" }",
                string.Empty
            }));
            var source = SourceWith(Asset(
                "Assets/SequenceMultilineFlow.prefab",
                ConcatYamlDocuments(
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                    flowYaml)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("フロー形式コレクション", coverage.IncompleteReason);
        }

        /// <summary>引用符付きまたは余分な空白を持つ非正規形のテーブル参照を、黙って見逃しません。</summary>
        [TestCase("m_TableReference   :")]
        [TestCase("'m_TableReference':")]
        [TestCase("\"m_TableReference\":")]
        public void NonCanonicalTableReferenceKey_ReturnsIncomplete(string key)
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Localized:",
                "    " + key,
                string.Empty
            }));

            AssertIncomplete(
                SourceWith(Asset("Assets/NonCanonical.prefab", yaml)),
                "正規形でない記述");
        }

        /// <summary>コメント内の参照文字列をフローマッピング候補として扱いません。</summary>
        [Test]
        public void CommentLookalike_IsIgnoredWithoutCreatingReference()
        {
            var yaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  # m_TableReference: {m_TableCollectionName: GUID:11111111222233334444555555555555}",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/Comment.prefab", yaml)));

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
        }

        /// <summary>フローマッピング内の未対応参照文字列を、参照なしの網羅完了にしません。</summary>
        [Test]
        public void FlowSequenceTableReference_ReturnsIncompleteAndDiscardsReferences()
        {
            var flowYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                string.Concat(
                    "  m_LocalizedValues: [{m_TableReference: {m_TableCollectionName: GUID:",
                    LocalizationKeyAuditTestData.CollectionGuid.ToString("N"),
                    "}, m_TableEntryReference: {m_KeyId: 20}}]"),
                string.Empty
            }));
            var source = SourceWith(Asset(
                "Assets/Flow.prefab",
                ConcatYamlDocuments(
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                    flowYaml)));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("フローマッピング形式", coverage.IncompleteReason);
        }

        /// <summary>入れ子の項目を、直下の子要素にあるGUIDまたは項目識別子と誤認しません。</summary>
        [Test]
        public void NestedReferenceFields_ReturnIncompleteAndDiscardReferences()
        {
            var nestedYaml = Encoding.UTF8.GetBytes(string.Join("\n", new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400001",
                "MonoBehaviour:",
                "  m_Localized:",
                "    m_TableReference:",
                "      m_Nested:",
                $"        m_TableCollectionName: GUID:{LocalizationKeyAuditTestData.CollectionGuid:N}",
                "    m_TableEntryReference:",
                "      m_Nested:",
                "        m_KeyId: 20",
                string.Empty
            }));

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(Asset("Assets/Nested.prefab", nestedYaml)));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("直下の正確な子要素", coverage.IncompleteReason);
        }

        /// <summary>
        /// 同じパッケージ内の後半アセットが失敗した場合は、前半から抽出済みの参照を全て破棄します。
        /// </summary>
        [Test]
        public void Scan_LaterPackageFailureDiscardsEarlierPackageReferences()
        {
            var source = new FakeLocalizationKeyAuditCoverageSource
            {
                Assets = new[]
                {
                    Asset(
                        "Packages/com.example/A.prefab",
                        ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)),
                    Asset("Packages/com.example/Z.asset", Encoding.UTF8.GetBytes("not yaml"))
                }
            };

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { "Packages/com.example" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("Packages/com.example/Z.asset", coverage.IncompleteReason);
        }

        /// <summary>同じパッケージの後半に安全でない区切り要素が現れても、先行参照を返しません。</summary>
        [Test]
        public void Scan_LaterAmbiguousPackagePathDiscardsEarlierPackageReferences()
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { "Packages/com.example" },
                SourceWith(
                    Asset(
                        "Packages/com.example/A.prefab",
                        ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)),
                    Asset(
                        "Packages/com.example/Z~.asset",
                        ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 20))));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("走査対象のアセットパス", coverage.IncompleteReason);
        }

        /// <summary>
        /// 取得元の欠落、空の返却、例外を網羅未完了に隔離します。
        /// </summary>
        [Test]
        public void Scan_SourceFailuresAreIsolated()
        {
            AssertIncomplete(null, "取得元がありません");
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource { Assets = null },
                "空の結果を返しました");
            const string physicalCanary = "C:\\private\\source-exception-canary";
            var exceptionCoverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                new FakeLocalizationKeyAuditCoverageSource
                {
                    Exception = new InvalidOperationException(physicalCanary)
                });

            Assert.That(exceptionCoverage.IsComplete, Is.False);
            Assert.That(exceptionCoverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("InvalidOperationException", exceptionCoverage.IncompleteReason);
            StringAssert.DoesNotContain(physicalCanary, exceptionCoverage.IncompleteReason);
        }

        /// <summary>
        /// 空要素、対応ルート外、重複パスを含むアセット一覧を、部分結果を残さず拒否します。
        /// </summary>
        [Test]
        public void Scan_InvalidAndDuplicateAssetPathsFailClosed()
        {
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource
                {
                    Assets = new AuditEditor.LocalizationKeyAuditCoverageAsset[] { null }
                },
                "アセットパス");
            AssertIncomplete(
                SourceWith(Asset(
                    "Library/PackageCache/com.example/A.asset",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10))),
                "アセットパス");
            var asset = Asset(
                "Assets/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10));
            AssertIncomplete(SourceWith(asset, asset), "重複");

            var outsideScope = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Scenes",
                new[] { "Assets/Scenes" },
                SourceWith(Asset(
                    "Assets/Other/A.asset",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10))));
            Assert.That(outsideScope.IsComplete, Is.False);
            Assert.That(outsideScope.RecognizedReferences, Is.Empty);
            StringAssert.Contains("走査対象のアセットパス", outsideScope.IncompleteReason);
        }

        /// <summary>
        /// パッケージルート自体、別パッケージ、接頭部だけが似たパッケージの取得元アセットを、部分結果なしで拒否します。
        /// </summary>
        [TestCase("Packages/com.example")]
        [TestCase("Packages/com.other/A.asset")]
        [TestCase("Packages/com.examples/A.asset")]
        public void Scan_InvalidOrOutOfScopePackageSourcePathReturnsNoPartialData(string sourcePath)
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "パッケージ範囲",
                new[] { "Packages/com.example" },
                SourceWith(Asset(
                    sourcePath,
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10))));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("走査対象のアセットパス", coverage.IncompleteReason);
        }

        /// <summary>
        /// 不在、再解析点、容量超過、読取失敗がある場合は、網羅完了にしません。
        /// </summary>
        [TestCase("missing", "Assets/A.asset を安全に読み取れません: 存在=いいえ, 再解析点=いいえ, 容量超過=いいえ, 読取失敗=なし")]
        [TestCase("reparse", "Assets/A.asset を安全に読み取れません: 存在=はい, 再解析点=はい, 容量超過=いいえ, 読取失敗=なし")]
        [TestCase("oversize", "Assets/A.asset を安全に読み取れません: 存在=はい, 再解析点=いいえ, 容量超過=はい, 読取失敗=なし")]
        [TestCase("read-error", "Assets/A.asset を安全に読み取れません: 存在=はい, 再解析点=いいえ, 容量超過=いいえ, 読取失敗=IOException")]
        public void Scan_UnsafeAssetStateIsIncomplete(string state, string expectedReasonPart)
        {
            var asset = new AuditEditor.LocalizationKeyAuditCoverageAsset(
                "Assets/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                exists: state != "missing",
                hasReparsePoint: state == "reparse",
                isOversize: state == "oversize",
                readError: state == "read-error" ? "IOException" : string.Empty);

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(asset));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(coverage.IncompleteReason, Is.EqualTo(expectedReasonPart));
        }

        /// <summary>記号を含む読取失敗本文を「あり」へ置き換え、物理パスの検査用文字列を公開しません。</summary>
        [Test]
        public void Scan_OpaqueReadErrorDoesNotExposePhysicalCanary()
        {
            const string physicalCanary = "C:\\private\\read-error-canary";
            var asset = new AuditEditor.LocalizationKeyAuditCoverageAsset(
                "Assets/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                readError: physicalCanary);

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(asset));

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("読取失敗=あり", coverage.IncompleteReason);
            StringAssert.DoesNotContain(physicalCanary, coverage.IncompleteReason);
        }

        /// <summary>
        /// 走査対象ファイル数と1ファイルのバイト数の上限超過を解析前に拒否します。
        /// </summary>
        [Test]
        public void Scan_RejectsFileCountAndPerAssetBytesAboveLimits()
        {
            var tooMany = new AuditEditor.LocalizationKeyAuditCoverageAsset[
                AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles + 1];
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource { Assets = tooMany },
                "走査対象ファイル数");

            var tooLarge = new byte[AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageAssetBytes + 1];
            AssertIncomplete(
                SourceWith(Asset("Assets/Large.asset", tooLarge)),
                "安全に読み取れません");
        }

        /// <summary>
        /// 走査対象アセットは、構築時の入力と返却されたバイト列の変更を受けません。
        /// </summary>
        [Test]
        public void CoverageAsset_DefensivelyCopiesBytes()
        {
            var bytes = ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10);
            var asset = Asset("Assets/A.asset", bytes);
            bytes[0] = 0;
            var firstCopy = asset.CopyBytes();
            firstCopy[0] = 0;

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                SourceWith(asset));

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(asset.CopyBytes()[0], Is.Not.Zero);
        }

        /// <summary>
        /// サービスの要求生成処理は指定範囲を一度だけ走査し、網羅完了または未完了の状態を保持します。
        /// </summary>
        [Test]
        public void CreateRequest_UsesInjectedCoverageSourceOnce()
        {
            var source = SourceWith(Asset(
                "Assets/Scenes/Main.unity",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)));

            var request = AuditEditor.LocalizationKeyAuditService.CreateRequest(
                new[] { "en" },
                "Scenes",
                new[] { "Assets/Scenes" },
                source);

            Assert.That(source.ReadCallCount, Is.EqualTo(1));
            Assert.That(request.Coverage.IsComplete, Is.True);
            Assert.That(request.Coverage.RecognizedReferences, Has.Count.EqualTo(1));
            Assert.That(
                AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure),
                Is.True,
                failure?.Message);
        }

        /// <summary>
        /// フォルダー範囲は第1版で対応する、Unity形式のYAMLで保存されたアセットの種類だけを列挙し、ソースコードとバイナリーアセットを除外します。
        /// </summary>
        [TestCase(".asset", true)]
        [TestCase(".ASSET", true)]
        [TestCase(".prefab", true)]
        [TestCase(".unity", true)]
        [TestCase(".cs", false)]
        [TestCase(".png", false)]
        [TestCase(".wav", false)]
        [TestCase(".meta", false)]
        public void CoverageSource_FolderAllowlistContainsOnlySupportedUnityYaml(
            string extension,
            bool expected)
        {
            var method = typeof(AuditEditor.UnityLocalizationKeyAuditCoverageSource).GetMethod(
                "IsSupportedYamlAssetExtension",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(
                (bool)method.Invoke(null, new object[] { "C:/Project/Assets/Test" + extension }),
                Is.EqualTo(expected));

            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.ShouldIncludeYamlAssetFile(
                    "C:/Project/Assets/Test" + extension,
                    false),
                Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(
                    AuditEditor.UnityLocalizationKeyAuditCoverageSource.ShouldIncludeYamlAssetFile(
                        "C:/Project/Assets/Test" + extension,
                        true),
                    Is.True);
            }
            else
            {
                Assert.That(
                    () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.ShouldIncludeYamlAssetFile(
                        "C:/Project/Assets/Test" + extension,
                        true),
                    Throws.TypeOf<System.IO.InvalidDataException>());
            }
        }

        /// <summary>未対応のファイルやディレクトリーも、逐次列挙時に全体上限を消費します。</summary>
        [Test]
        public void CoverageSource_PhysicalDiscoveryBudgetRejectsBeforeNextEntry()
        {
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles - 1,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    "file"),
                Is.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles));
            var fileException = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(() =>
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    "file"));
            StringAssert.Contains("物理ファイル数", fileException.Message);
            var directoryException = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(() =>
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "directory"));
            StringAssert.Contains("物理ディレクトリー数", directoryException.Message);
        }

        /// <summary>実際に読み取る総量を、次のファイル用領域を確保する前に拒否します。</summary>
        [Test]
        public void CoverageSource_ActualReadBudgetRejectsBeforeAllocation()
        {
            var maximum = AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageTotalBytes;
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(maximum - 1, 1),
                Is.EqualTo(maximum));
            var upperLimitException = Assert.Throws<AuditEditor.LocalizationKeyAuditLimitException>(() =>
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(maximum, 1));
            StringAssert.Contains("実読取バイト数", upperLimitException.Message);
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(-1, 0),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>静的参照の上限を、次の固有な組を追加する前に拒否し、先行結果も破棄します。</summary>
        [Test]
        public void StaticReferenceLimit_RejectsBeforeNextPairAndDiscardsEarlierReferences()
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Scenes",
                new[] { "Assets" },
                SourceWith(
                    Asset("Assets/A.prefab", ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)),
                    Asset("Assets/B.prefab", ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 20))),
                1);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("上限 1", coverage.IncompleteReason);

            var exact = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Scenes",
                new[] { "Assets" },
                SourceWith(Asset(
                    "Assets/A.prefab",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10))),
                1);
            Assert.That(exact.IsComplete, Is.True, exact.IncompleteReason);
            Assert.That(exact.RecognizedReferences, Has.Count.EqualTo(1));
        }

        /// <summary>祖先ディレクトリーは入れ子のディレクトリーと対応ファイルを包含し、明示指定された未対応ファイルは保持します。</summary>
        [Test]
        public void CoverageSource_SelectNonOverlappingTargetsScansContainedSupportedAssetsOnce()
        {
            var root = System.IO.Path.GetFullPath("C:/Project/Assets");
            var nested = System.IO.Path.Combine(root, "Sub");
            var supported = System.IO.Path.Combine(nested, "A.prefab");
            var unsupported = System.IO.Path.Combine(nested, "Notes.txt");

            var selected = AuditEditor.UnityLocalizationKeyAuditCoverageSource.SelectNonOverlappingTargets(
                new[] { root, nested, supported, unsupported },
                new[] { true, true, false, false },
                new[] { false, false, true, false });

            Assert.That(selected, Is.EqualTo(new[] { 0, 3 }));
        }

        /// <summary>ドット区切り要素の正規化後に同じ物理対象となる指定パスを拒否します。</summary>
        [Test]
        public void CoverageSource_NormalizedDuplicateDeclaredTargetsAreRejected()
        {
            var root = Path.GetFullPath("C:/Project/Packages/com.example");
            var direct = Path.Combine(root, "A.asset");
            var normalizedDuplicate = Path.Combine(root, "Nested", "..", "A.asset");
            var method = typeof(AuditEditor.UnityLocalizationKeyAuditCoverageSource).GetMethod(
                "EnsureDistinctDeclaredTargets",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(
                null,
                new object[]
                {
                    new[] { direct, normalizedDuplicate },
                    new[] { "Packages/com.example/A.asset", "Packages/com.example/Nested/../A.asset" }
                }));

            Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
            StringAssert.Contains("同じ物理対象", exception.InnerException.Message);
        }

        /// <summary>末端が存在しなくても、既存の祖先にある接合点をルートまで検査して拒否します。</summary>
        [Test]
        public void CoverageSource_MissingTargetUnderWindowsJunctionIsRejected()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Windowsの再解析点専用の検証です。");
            }

            var temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "LocalizationKeyAuditCoverageSourceTests_" + Guid.NewGuid().ToString("N"));
            var registeredRoot = Path.Combine(temporaryRoot, "Registered");
            var target = Path.Combine(temporaryRoot, "Target");
            var junction = Path.Combine(registeredRoot, "Linked");
            Directory.CreateDirectory(registeredRoot);
            Directory.CreateDirectory(target);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /c mklink /J \"{junction}\" \"{target}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Assert.Ignore("接合点の作成処理を開始できませんでした。");
                    }

                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Assert.Ignore("この環境では接合点を作成できませんでした。");
                    }
                }

                var method = typeof(AuditEditor.UnityLocalizationKeyAuditCoverageSource).GetMethod(
                    "EnsureNoReparsePoint",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var missingTarget = Path.Combine(junction, "Missing", "Never.asset");

                var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(
                    null,
                    new object[] { registeredRoot, missingTarget }));

                Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
                StringAssert.Contains("再解析点", exception.InnerException.Message);
            }
            finally
            {
                if (Directory.Exists(junction))
                {
                    Directory.Delete(junction);
                }

                if (Directory.Exists(registeredRoot))
                {
                    Directory.Delete(registeredRoot);
                }

                if (Directory.Exists(target))
                {
                    Directory.Delete(target);
                }

                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot);
                }
            }
        }

        /// <summary>指定アセットだけを返す網羅走査の取得元を作ります。</summary>
        private static FakeLocalizationKeyAuditCoverageSource SourceWith(
            params AuditEditor.LocalizationKeyAuditCoverageAsset[] assets)
        {
            return new FakeLocalizationKeyAuditCoverageSource { Assets = assets };
        }

        /// <summary>走査対象アセットを作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCoverageAsset Asset(string path, byte[] bytes)
        {
            return new AuditEditor.LocalizationKeyAuditCoverageAsset(path, bytes);
        }

        /// <summary>有効なGUIDと項目識別子の直接参照の組を持つUnity形式のYAMLバイト列を作ります。</summary>
        private static byte[] ReferenceYaml(
            Guid collectionGuid,
            long entryId,
            string lineEnding = "\n",
            bool quoteGuid = false)
        {
            var text = ReferenceYamlText(collectionGuid, entryId, lineEnding, quoteGuid);
            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>有効なGUIDと項目識別子の直接参照の組を持つUnity形式のYAML文字列を作ります。</summary>
        private static string ReferenceYamlText(
            Guid collectionGuid,
            long entryId,
            string lineEnding = "\n",
            bool quoteGuid = false)
        {
            var table = $"GUID:{collectionGuid:N}";
            if (quoteGuid)
            {
                table = $"\"{table}\"";
            }

            return string.Join(lineEnding, new[]
            {
                "%YAML 1.1",
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_Localized:",
                "    m_TableReference:",
                $"      m_TableCollectionName: {table}",
                "    m_TableEntryReference:",
                $"      m_KeyId: {entryId}",
                string.Empty
            });
        }

        /// <summary>複数のYAML文書を1つのファイル文字列へ連結します。</summary>
        private static byte[] ConcatYamlDocuments(params byte[][] documents)
        {
            return Encoding.UTF8.GetBytes(string.Join(
                "\n",
                documents.Select(document => Encoding.UTF8.GetString(document))));
        }

        /// <summary>UTF-8のBOMをバイト列の先頭へ追加します。</summary>
        private static byte[] AddUtf8Bom(byte[] bytes)
        {
            return new UTF8Encoding(true).GetPreamble().Concat(bytes).ToArray();
        }

        /// <summary>網羅未完了結果が途中までの参照を返さないことを検証します。</summary>
        private static void AssertIncomplete(
            AuditEditor.ILocalizationKeyAuditCoverageSource source,
            string expectedReasonPart)
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains(expectedReasonPart, coverage.IncompleteReason);
        }

        /// <summary>網羅未完了結果が途中までの参照を返さず、失敗理由も完全一致することを検証します。</summary>
        private static void AssertIncompleteReason(
            AuditEditor.ILocalizationKeyAuditCoverageSource source,
            string expectedReason)
        {
            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(coverage.IncompleteReason, Is.EqualTo(expectedReason));
        }
    }
}
