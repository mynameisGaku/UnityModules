using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Assets-only Unity YAML から direct GUID/key-ID reference を保守的に抽出する契約を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditCoverageScannerTests
    {
        /// <summary>
        /// LF/CRLF、BOM、quoted GUID を受理し、重複を除いた参照を path/GUID/ID 順へ固定します。
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
                "Gameplay assets",
                declaredPaths,
                source);

            Assert.That(coverage.IsComplete, Is.True, coverage.IncompleteReason);
            Assert.That(coverage.IncompleteReason, Is.Empty);
            Assert.That(coverage.ScopeDescription, Is.EqualTo("Gameplay assets"));
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

        /// <summary>CR-only Unity YAMLもLF/CRLFと同じdirect referenceとして解析します。</summary>
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

        /// <summary>Unity YAML line数を配列保持前のexact境界で拒否します。</summary>
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
        /// binary、invalid UTF-8、header 欠落、tab indentation を incomplete にします。
        /// </summary>
        [Test]
        public void Scan_UnsupportedEncodingAndYamlFormsFailClosed()
        {
            AssertIncomplete(
                SourceWith(Asset("Assets/Binary.asset", new byte[] { 1, 0, 2 })),
                "binary data");
            AssertIncomplete(
                SourceWith(Asset("Assets/Utf8.asset", new byte[] { 0xC3, 0x28 })),
                "strict UTF-8");
            AssertIncomplete(
                SourceWith(Asset("Assets/Text.asset", Encoding.UTF8.GetBytes("plain text"))),
                "Unity YAML header");
            AssertIncomplete(
                SourceWith(Asset(
                    "Assets/Tab.asset",
                    Encoding.UTF8.GetBytes("%YAML 1.1\n\tm_TableReference:\n"))),
                "tab character");
            AssertIncomplete(
                SourceWith(Asset(
                    "Assets/InlineTab.asset",
                    Encoding.UTF8.GetBytes("%YAML 1.1\nMonoBehaviour:\n  m_Text:\t|\n"))),
                "tab character");
        }

        /// <summary>
        /// name-based table、empty key ID、field 重複を対応済み identity と誤認しません。
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
                "name-based");
            AssertIncomplete(
                SourceWith(Asset("Assets/EmptyId.asset", Encoding.UTF8.GetBytes(emptyId))),
                "table entry reference");
            AssertIncomplete(
                SourceWith(Asset("Assets/Duplicate.asset", Encoding.UTF8.GetBytes(duplicateGuidField))),
                "1 件の non-empty scalar");
        }

        /// <summary>
        /// table block が entry block より前に途切れた asset を参照ゼロの complete としません。
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
                "entry reference");
        }

        /// <summary>
        /// table と entry の間に同一 indent の別 field がある unsupported 配置を incomplete にし、先行参照も破棄します。
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

        /// <summary>sequence要素の未対応reference shapeを見逃さず、先行参照も破棄します。</summary>
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
            StringAssert.Contains("YAML sequence", coverage.IncompleteReason);
        }

        /// <summary>block scalar本文の見かけ上のreferenceを構造として採用しません。</summary>
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
            StringAssert.Contains("block scalar", coverage.IncompleteReason);
        }

        /// <summary>sequence block scalar本文のlookalikeも構造として採用しません。</summary>
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
            StringAssert.Contains("block scalar", coverage.IncompleteReason);
        }

        /// <summary>quoted mapping key内のcolonをvalue separatorと誤認しません。</summary>
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
            StringAssert.Contains("block scalar", coverage.IncompleteReason);
        }

        /// <summary>non-specific tag付きblock scalarも本文を構造として採用しません。</summary>
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
            StringAssert.Contains("block scalar", coverage.IncompleteReason);
        }

        /// <summary>nested sequence内のblock scalar本文も構造として採用しません。</summary>
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
            StringAssert.Contains("block scalar", coverage.IncompleteReason);
        }

        /// <summary>explicit mapping key scalar内のlookalikeを構造として採用しません。</summary>
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
            StringAssert.Contains("explicit mapping key", coverage.IncompleteReason);
        }

        /// <summary>block scalarに見えるcommentは無視し、その後の実referenceをmaskしません。</summary>
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

        /// <summary>複数行quoted scalar内のlookalikeを構造referenceとして採用しません。</summary>
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
            StringAssert.Contains("quoted scalar", coverage.IncompleteReason);
        }

        /// <summary>複数行flow collection内のquoted lookalikeも構造referenceとして採用しません。</summary>
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
            StringAssert.Contains("flow collection", coverage.IncompleteReason);
        }

        /// <summary>sequence flow mapping内のcolonをouter mapping separatorと誤認しません。</summary>
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
            StringAssert.Contains("flow collection", coverage.IncompleteReason);
        }

        /// <summary>quoted/whitespace keyのnon-canonical table referenceをsilent missしません。</summary>
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
                "non-canonical");
        }

        /// <summary>comment内のreference tokenをflow mapping候補として扱いません。</summary>
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

        /// <summary>flow mapping内の未対応reference tokenをno-reference completeにしません。</summary>
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
            StringAssert.Contains("flow mapping", coverage.IncompleteReason);
        }

        /// <summary>nested fieldをdirect childのGUID/key IDと誤認しません。</summary>
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
            StringAssert.Contains("direct child", coverage.IncompleteReason);
        }

        /// <summary>
        /// 後半 asset の失敗時は前半から抽出済みの参照を全て破棄します。
        /// </summary>
        [Test]
        public void Scan_LaterFailureDiscardsEarlierReferences()
        {
            var source = new FakeLocalizationKeyAuditCoverageSource
            {
                Assets = new[]
                {
                    Asset("Assets/A.prefab", ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10)),
                    Asset("Assets/Z.asset", Encoding.UTF8.GetBytes("not yaml"))
                }
            };

            var coverage = AuditEditor.LocalizationKeyAuditCoverageScanner.Scan(
                "Assets",
                new[] { "Assets" },
                source);

            Assert.That(coverage.IsComplete, Is.False);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            StringAssert.Contains("Assets/Z.asset", coverage.IncompleteReason);
        }

        /// <summary>
        /// source の欠落、null return、例外を incomplete coverage に隔離します。
        /// </summary>
        [Test]
        public void Scan_SourceFailuresAreIsolated()
        {
            AssertIncomplete(null, "source がありません");
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource { Assets = null },
                "null を返しました");
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource
                {
                    Exception = new InvalidOperationException("coverage boom")
                },
                "InvalidOperationException: coverage boom");
        }

        /// <summary>
        /// null/Packages/重複 path を含む asset 一覧を partial result なしで拒否します。
        /// </summary>
        [Test]
        public void Scan_InvalidAndDuplicateAssetPathsFailClosed()
        {
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource
                {
                    Assets = new AuditEditor.LocalizationKeyAuditCoverageAsset[] { null }
                },
                "path");
            AssertIncomplete(
                SourceWith(Asset(
                    "Packages/com.example/A.asset",
                    ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10))),
                "path");
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
            StringAssert.Contains("coverage asset path", outsideScope.IncompleteReason);
        }

        /// <summary>
        /// missing、reparse、oversize、read error を complete coverage にしません。
        /// </summary>
        [TestCase("missing", "exists=False")]
        [TestCase("reparse", "reparse=True")]
        [TestCase("oversize", "oversize=True")]
        [TestCase("read-error", "access denied")]
        public void Scan_UnsafeAssetStateIsIncomplete(string state, string expectedReasonPart)
        {
            var asset = new AuditEditor.LocalizationKeyAuditCoverageAsset(
                "Assets/A.asset",
                ReferenceYaml(LocalizationKeyAuditTestData.CollectionGuid, 10),
                exists: state != "missing",
                hasReparsePoint: state == "reparse",
                isOversize: state == "oversize",
                readError: state == "read-error" ? "access denied" : string.Empty);

            AssertIncomplete(SourceWith(asset), expectedReasonPart);
        }

        /// <summary>
        /// coverage file 数と 1 file byte 数の hard limit 超過を parse 前に拒否します。
        /// </summary>
        [Test]
        public void Scan_RejectsFileCountAndPerAssetBytesAboveLimits()
        {
            var tooMany = new AuditEditor.LocalizationKeyAuditCoverageAsset[
                AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles + 1];
            AssertIncomplete(
                new FakeLocalizationKeyAuditCoverageSource { Assets = tooMany },
                "coverage file 数");

            var tooLarge = new byte[AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageAssetBytes + 1];
            AssertIncomplete(
                SourceWith(Asset("Assets/Large.asset", tooLarge)),
                "安全に読み取れません");
        }

        /// <summary>
        /// coverage asset は constructor 入力と返却 byte の変更を受けません。
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
        /// service の request factory は宣言 scope を一度だけ走査し、complete/incomplete 状態を保持します。
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
        /// folder scope は v1 対応 Unity YAML 種別だけを列挙し、source code と binary asset を除外します。
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

        /// <summary>unsupported fileやdirectoryもstreaming列挙時にglobal budgetを消費します。</summary>
        [Test]
        public void CoverageSource_PhysicalDiscoveryBudgetRejectsBeforeNextEntry()
        {
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles - 1,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    "file"),
                Is.EqualTo(AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                    "file"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.IncrementPhysicalDiscoveryCount(
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                    "directory"),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>coverage actual read総量を次fileのallocation前に拒否します。</summary>
        [Test]
        public void CoverageSource_ActualReadBudgetRejectsBeforeAllocation()
        {
            var maximum = AuditEditor.LocalizationKeyAuditLimits.MaximumCoverageTotalBytes;
            Assert.That(
                AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(maximum - 1, 1),
                Is.EqualTo(maximum));
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(maximum, 1),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
            Assert.That(
                () => AuditEditor.UnityLocalizationKeyAuditCoverageSource.EnsureActualReadBudget(-1, 0),
                Throws.TypeOf<AuditEditor.LocalizationKeyAuditLimitException>());
        }

        /// <summary>static reference上限を次のunique pair追加前に拒否し、先行結果も破棄します。</summary>
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

        /// <summary>ancestor directoryはnested directory/supported fileを包含し、explicit unsupported fileは保持します。</summary>
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

        /// <summary>指定 asset だけを返す coverage source を作ります。</summary>
        private static FakeLocalizationKeyAuditCoverageSource SourceWith(
            params AuditEditor.LocalizationKeyAuditCoverageAsset[] assets)
        {
            return new FakeLocalizationKeyAuditCoverageSource { Assets = assets };
        }

        /// <summary>coverage asset を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCoverageAsset Asset(string path, byte[] bytes)
        {
            return new AuditEditor.LocalizationKeyAuditCoverageAsset(path, bytes);
        }

        /// <summary>valid direct GUID/key-ID pair を持つ Unity YAML byte を作ります。</summary>
        private static byte[] ReferenceYaml(
            Guid collectionGuid,
            long entryId,
            string lineEnding = "\n",
            bool quoteGuid = false)
        {
            var text = ReferenceYamlText(collectionGuid, entryId, lineEnding, quoteGuid);
            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>valid direct GUID/key-ID pair を持つ Unity YAML text を作ります。</summary>
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

        /// <summary>複数 YAML document を一つの file text に連結します。</summary>
        private static byte[] ConcatYamlDocuments(params byte[][] documents)
        {
            return Encoding.UTF8.GetBytes(string.Join(
                "\n",
                documents.Select(document => Encoding.UTF8.GetString(document))));
        }

        /// <summary>UTF-8 BOM を byte 先頭へ追加します。</summary>
        private static byte[] AddUtf8Bom(byte[] bytes)
        {
            return new UTF8Encoding(true).GetPreamble().Concat(bytes).ToArray();
        }

        /// <summary>incomplete coverage が partial reference を返さないことを検証します。</summary>
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
    }
}
