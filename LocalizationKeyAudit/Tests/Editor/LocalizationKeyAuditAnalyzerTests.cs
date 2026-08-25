using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// typed snapshot の integrity、direct coverage、static reference graph を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditAnalyzerTests
    {
        /// <summary>
        /// locale table 欠落、direct entry 欠落、null/empty value を分け、空白 value は保持します。
        /// </summary>
        [Test]
        public void Analyze_DistinguishesDirectCoverageFindingsWithoutFallbackClaims()
        {
            var collection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                new[]
                {
                    Entry(10, "Empty"),
                    Entry(20, "Missing"),
                    Entry(30, "Whitespace")
                },
                new[]
                {
                    Table("en", Localized(10, null), Localized(30, " ")),
                    Table("ja", Localized(10, string.Empty), Localized(20, "値"), Localized(30, "　"))
                });
            var references = new[]
            {
                Reference(collection.CollectionGuid, 10),
                Reference(collection.CollectionGuid, 20),
                Reference(collection.CollectionGuid, 30)
            };

            var result = Analyze(
                new[] { collection },
                new[] { "en", "ja", "fr" },
                new[] { "en", "ja", "fr" },
                references);

            Assert.That(result.IsComplete, Is.True);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.MissingLocaleTable, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.EmptyDirectValue, 2);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);
            Assert.That(
                result.Issues.Single(issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.MissingLocaleTable)
                    .LocaleIdentifier,
                Is.EqualTo("fr"));
            Assert.That(
                result.Issues.Single(issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry)
                    .EntryId,
                Is.EqualTo(20));
            Assert.That(
                result.Issues.Where(issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.EmptyDirectValue)
                    .Select(issue => issue.EntryId),
                Is.EqualTo(new long[] { 10, 10 }));
            Assert.That(result.Issues.Any(issue => issue.EntryId == 30), Is.False);
            Assert.That(result.Issues.All(issue => !issue.Message.Contains("runtime で利用できません")), Is.True);
            Assert.That(result.Issues.All(issue => !issue.Message.Contains("未翻訳です")), Is.True);
            Assert.That(result.GraphEdgeCount, Is.EqualTo(19));
        }

        /// <summary>
        /// required Locale の settings 不在は direct table の有無と独立して報告します。
        /// </summary>
        [Test]
        public void Analyze_ReportsRequiredLocaleNotConfiguredIndependently()
        {
            var collection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                new[] { Entry(10, "Start") },
                new[] { Table("fr", Localized(10, "Démarrer")) });

            var result = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "fr" },
                new[] { Reference(collection.CollectionGuid, 10) });

            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.RequiredLocaleNotConfigured, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.MissingLocaleTable, 0);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry, 0);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.EmptyDirectValue, 0);
        }

        /// <summary>
        /// Locale、shared identity/key、table、localized identity、orphan を別々の integrity issue にします。
        /// </summary>
        [Test]
        public void Analyze_ReportsAllNestedDuplicateAndOrphanKinds()
        {
            var collection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                new[]
                {
                    Entry(10, "First"),
                    Entry(10, "Second"),
                    Entry(20, "SameKey"),
                    Entry(30, "SameKey")
                },
                new[]
                {
                    TableAt("en", "Assets/Localization/UI_en_A.asset", Localized(20, "A"), Localized(20, "B")),
                    TableAt("EN", "Assets/Localization/UI_en_B.asset", Localized(99, "X"), Localized(99, "Y"))
                });
            var result = Analyze(
                new[] { collection },
                new[] { "en", "EN" },
                new[] { "en" },
                new[]
                {
                    Reference(collection.CollectionGuid, 20),
                    Reference(collection.CollectionGuid, 30)
                });

            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleIdentifier, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryId, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryKey, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocaleTable, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateLocalizedEntryId, 2);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocalizedEntry, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.MissingDirectEntry, 0);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);
        }

        /// <summary>
        /// collection に属さない typed table と typed object のない valid raw SharedTableData を結果へ残します。
        /// </summary>
        [Test]
        public void Analyze_PreservesTypedAndRawOrphansAsExplicitFindings()
        {
            var orphanGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var rawOnlyGuid = Guid.Parse("99999999-8888-7777-6666-555555555555");
            var orphan = new AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot(
                "Assets/Localization/Orphan Shared Data.asset",
                orphanGuid,
                TableAt(
                    "en",
                    "Assets/Localization/Orphan_en.asset",
                    Localized(10, "Value")));
            var rawOnly = new AuditEditor.LocalizationKeyAuditRawIdentity(
                "Assets/Localization/RawOnly Shared Data.asset",
                rawOnlyGuid);

            var result = Analyze(
                Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                new[] { "en" },
                new[] { "en" },
                orphanLocaleTables: new[] { orphan },
                additionalRawIdentities: new[] { rawOnly });

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.OrphanLocaleTables, Has.Count.EqualTo(1));
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocaleTable, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.OrphanedSharedTableData, 1);
            var typedOrphanIssue = result.Issues.Single(
                issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.OrphanedLocaleTable);
            Assert.That(typedOrphanIssue.AssetPath, Is.EqualTo("Assets/Localization/Orphan_en.asset"));
            Assert.That(typedOrphanIssue.RelatedAssetPath, Is.EqualTo("Assets/Localization/Orphan Shared Data.asset"));
            Assert.That(typedOrphanIssue.CollectionGuid, Is.EqualTo(orphanGuid));
            var rawOrphanIssue = result.Issues.Single(
                issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.OrphanedSharedTableData);
            Assert.That(rawOrphanIssue.AssetPath, Is.EqualTo("Assets/Localization/RawOnly Shared Data.asset"));
            Assert.That(rawOrphanIssue.CollectionGuid, Is.EqualTo(rawOnlyGuid));
            Assert.That(result.GraphEdgeCount, Is.EqualTo(2));
        }

        /// <summary>
        /// collection 名と GUID の重複は該当する各 collection に決定論的な issue を付けます。
        /// </summary>
        [Test]
        public void Analyze_ReportsDuplicateCollectionNameAndGuidPerCollection()
        {
            var firstGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var secondGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var collections = new[]
            {
                CreateCollection("DuplicateName", firstGuid, Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(), Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>(), "Assets/Localization/A Shared Data.asset"),
                CreateCollection("DuplicateName", secondGuid, Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(), Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>(), "Assets/Localization/B Shared Data.asset"),
                CreateCollection("OtherA", firstGuid, Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(), Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>(), "Assets/Localization/C Shared Data.asset")
            };

            var result = Analyze(collections, new[] { "en" }, new[] { "en" });

            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionName, 2);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, 2);
        }

        /// <summary>
        /// static reference は表示 hint でなく collection GUID と entry ID だけで解決します。
        /// </summary>
        [Test]
        public void Analyze_StaticReferencesUseGuidAndEntryIdAndRejectAmbiguity()
        {
            var firstGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var duplicateGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var missingGuid = Guid.Parse("99999999-8888-7777-6666-555555555555");
            var collections = new[]
            {
                CreateCollection(
                    "Unique",
                    firstGuid,
                    new[] { Entry(10, "A"), Entry(10, "B"), Entry(30, "Valid") },
                    new[] { Table("en", Localized(30, "ok")) },
                    "Assets/Localization/Unique Shared Data.asset"),
                CreateCollection("DuplicateA", duplicateGuid, new[] { Entry(20, "A") }, new[] { Table("en", Localized(20, "a")) }, "Assets/Localization/DuplicateA Shared Data.asset"),
                CreateCollection("DuplicateB", duplicateGuid, new[] { Entry(20, "B") }, new[] { Table("en", Localized(20, "b")) }, "Assets/Localization/DuplicateB Shared Data.asset")
            };
            var references = new[]
            {
                Reference(missingGuid, 1, "Missing", "Missing"),
                Reference(firstGuid, 10, "WrongName", "WrongKey"),
                Reference(duplicateGuid, 20, "Hint", "Hint"),
                Reference(firstGuid, 30, "CompletelyWrong", "StillWrong")
            };

            var result = Analyze(collections, new[] { "en" }, new[] { "en" }, references);

            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference, 3);
            Assert.That(
                result.Issues.Any(issue =>
                    issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference &&
                    issue.CollectionGuid == firstGuid &&
                    issue.EntryId == 30),
                Is.False);
        }

        /// <summary>
        /// typed object のない raw SharedTableData でも GUID 重複なら static identity を曖昧にします。
        /// </summary>
        [Test]
        public void Analyze_RawOrphanDuplicateGuidMakesStaticIdentityAmbiguous()
        {
            var collection = CompleteCollectionWithEntries(10);
            var rawOrphan = new AuditEditor.LocalizationKeyAuditRawIdentity(
                "Assets/Localization/RawDuplicate Shared Data.asset",
                collection.CollectionGuid);

            var result = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                new[] { Reference(collection.CollectionGuid, 10) },
                additionalRawIdentities: new[] { rawOrphan });

            Assert.That(result.IsComplete, Is.True);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.OrphanedSharedTableData, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, 2);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.DanglingStaticReference, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);
        }

        /// <summary>
        /// 3 path 以上の raw GUID 重複でも related path と issue 順を raw 入力順から独立させます。
        /// </summary>
        [Test]
        public void Analyze_RawGuidDuplicateIssuesAreDeterministicForManyPaths()
        {
            var collection = CompleteCollectionWithEntries(10);
            var second = new AuditEditor.LocalizationKeyAuditRawIdentity(
                "Assets/Localization/B Shared Data.asset",
                collection.CollectionGuid);
            var third = new AuditEditor.LocalizationKeyAuditRawIdentity(
                "Assets/Localization/C Shared Data.asset",
                collection.CollectionGuid);

            var forward = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                additionalRawIdentities: new[] { second, third });
            var reversed = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                additionalRawIdentities: new[] { third, second });

            AssertIssueCount(forward, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, 3);
            Assert.That(ProjectIssues(reversed), Is.EqualTo(ProjectIssues(forward)));
        }

        /// <summary>
        /// complete coverage で参照がゼロなら一意な全 shared key を NoStatic として列挙します。
        /// </summary>
        [Test]
        public void Analyze_CompleteCoverageWithNoReferencesReportsEveryUniqueSharedKey()
        {
            var collection = CompleteCollectionWithEntries(10, 20, 30);

            var result = Analyze(new[] { collection }, new[] { "en" }, new[] { "en" });

            Assert.That(NoStaticEntryIds(result), Is.EqualTo(new long[] { 10, 20, 30 }));
        }

        /// <summary>
        /// 一部 key に参照があっても同じ collection の未参照 key を NoStatic として残します。
        /// </summary>
        [Test]
        public void Analyze_PartialReferencesReportOnlyRemainingSharedKeys()
        {
            var collection = CompleteCollectionWithEntries(10, 20, 30);

            var result = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                new[] { Reference(collection.CollectionGuid, 20) });

            Assert.That(NoStaticEntryIds(result), Is.EqualTo(new long[] { 10, 30 }));
        }

        /// <summary>
        /// duplicate collection GUID と duplicate shared ID は一意でないため NoStatic を断定しません。
        /// </summary>
        [Test]
        public void Analyze_AmbiguousIdentitiesSuppressNoStaticFinding()
        {
            var duplicateGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var duplicateCollections = new[]
            {
                CreateCollection("A", duplicateGuid, new[] { Entry(10, "A") }, new[] { Table("en", Localized(10, "a")) }, "Assets/Localization/A Shared Data.asset"),
                CreateCollection("B", duplicateGuid, new[] { Entry(20, "B") }, new[] { Table("en", Localized(20, "b")) }, "Assets/Localization/B Shared Data.asset")
            };
            var duplicateGuidResult = Analyze(
                duplicateCollections,
                new[] { "en" },
                new[] { "en" });
            AssertIssueCount(duplicateGuidResult, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateCollectionGuid, 2);
            AssertIssueCount(duplicateGuidResult, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);

            var uniqueGuid = LocalizationKeyAuditTestData.CollectionGuid;
            var duplicateShared = CreateCollection(
                "UI",
                uniqueGuid,
                new[] { Entry(10, "A"), Entry(10, "B"), Entry(20, "Unique") },
                new[] { Table("en", Localized(20, "ok")) });
            var duplicateSharedResult = Analyze(
                new[] { duplicateShared },
                new[] { "en" },
                new[] { "en" },
                new[] { Reference(uniqueGuid, 20) });
            AssertIssueCount(duplicateSharedResult, AuditEditor.LocalizationKeyAuditIssueKind.DuplicateSharedEntryId, 1);
            AssertIssueCount(duplicateSharedResult, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);
        }

        /// <summary>
        /// incomplete coverage は理由を一件だけ示し、未参照 key を NoStatic と断定しません。
        /// </summary>
        [Test]
        public void Analyze_IncompleteCoverageReportsIncompleteWithoutNoStatic()
        {
            var collection = CompleteCollectionWithEntries(10, 20);

            var result = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                coverageComplete: false,
                incompleteReason: "scan cancelled");

            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.StaticReferenceCoverageIncomplete, 1);
            AssertIssueCount(result, AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope, 0);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// 大量 sibling を事前構築 index で解決し、入力順に依存しない未参照 key 一覧を返します。
        /// </summary>
        [Test]
        public void Analyze_ManySiblingEntriesAndReferencesRemainDeterministic()
        {
            const int entryCount = 1024;
            var entries = Enumerable.Range(1, entryCount)
                .Select(index => Entry(index, $"Key{index:D4}"))
                .ToArray();
            var localized = Enumerable.Range(1, entryCount)
                .Select(index => Localized(index, $"Value{index:D4}"))
                .ToArray();
            var references = Enumerable.Range(1, entryCount)
                .Where(index => index % 2 == 0)
                .Select(index => Reference(LocalizationKeyAuditTestData.CollectionGuid, index, "Wrong", "Wrong"))
                .ToArray();
            var firstCollection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                entries,
                new[] { Table("en", localized) });
            var secondCollection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                entries.Reverse().ToArray(),
                new[] { Table("en", localized.Reverse().ToArray()) });

            var first = Analyze(
                new[] { firstCollection },
                new[] { "en" },
                new[] { "en" },
                references);
            var second = Analyze(
                new[] { secondCollection },
                new[] { "en" },
                new[] { "en" },
                references.Reverse().ToArray());

            var expectedMissing = Enumerable.Range(1, entryCount)
                .Where(index => index % 2 != 0)
                .Select(index => (long)index)
                .ToArray();
            Assert.That(NoStaticEntryIds(first), Is.EqualTo(expectedMissing));
            Assert.That(NoStaticEntryIds(second), Is.EqualTo(expectedMissing));
            Assert.That(ProjectIssues(second), Is.EqualTo(ProjectIssues(first)));
            Assert.That(first.Collections[0].SharedEntries.Select(entry => entry.Id), Is.EqualTo(Enumerable.Range(1, entryCount).Select(index => (long)index)));
            Assert.That(first.GraphEdgeCount, Is.EqualTo(2561));
        }

        /// <summary>
        /// Locale、collection、table、entry、issue の順序を入力配列の順序から独立させます。
        /// </summary>
        [Test]
        public void Analyze_NormalizesAllResultOrdering()
        {
            var a = CompleteCollectionWithEntries(20, 10);
            var bGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var b = CreateCollection(
                "ZZZ",
                bGuid,
                new[] { Entry(2, "B"), Entry(1, "A") },
                new[] { Table("ja", Localized(2, "b"), Localized(1, "a")) },
                "Assets/Localization/ZZZ Shared Data.asset");

            var first = Analyze(new[] { b, a }, new[] { "ja", "en" }, new[] { "en" });
            var second = Analyze(new[] { a, b }, new[] { "en", "ja" }, new[] { "en" });

            Assert.That(first.LocaleIdentifiers, Is.EqualTo(new[] { "en", "ja" }));
            Assert.That(first.Collections.Select(collection => collection.CollectionName), Is.EqualTo(new[] { "UI", "ZZZ" }));
            Assert.That(ProjectIssues(second), Is.EqualTo(ProjectIssues(first)));
        }

        /// <summary>
        /// declared scope の同じ集合は入力順に関係なく同じ coverage と static issue を返します。
        /// </summary>
        [Test]
        public void Analyze_NormalizesDeclaredScopePathOrdering()
        {
            var collection = CompleteCollectionWithEntries(10);
            var forward = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                declaredAssetPaths: new[] { "Assets/Scenes", "Assets/Prefabs" });
            var reversed = Analyze(
                new[] { collection },
                new[] { "en" },
                new[] { "en" },
                declaredAssetPaths: new[] { "Assets/Prefabs", "Assets/Scenes" });

            Assert.That(
                forward.Coverage.DeclaredAssetPaths,
                Is.EqualTo(new[] { "Assets/Prefabs", "Assets/Scenes" }));
            Assert.That(reversed.Coverage.DeclaredAssetPaths, Is.EqualTo(forward.Coverage.DeclaredAssetPaths));
            Assert.That(ProjectIssues(reversed), Is.EqualTo(ProjectIssues(forward)));
        }

        /// <summary>direct analyzer を実行する valid request、snapshot、raw identity を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditResult Analyze(
            AuditEditor.LocalizationKeyAuditCollectionSnapshot[] collections,
            string[] configuredLocales,
            string[] requiredLocales,
            AuditEditor.LocalizationKeyAuditStaticReference[] references = null,
            bool coverageComplete = true,
            string incompleteReason = "",
            AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot[] orphanLocaleTables = null,
            AuditEditor.LocalizationKeyAuditRawIdentity[] additionalRawIdentities = null,
            string[] declaredAssetPaths = null)
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                declaredAssetPaths ?? new[] { "Assets/Scenes" },
                references ?? Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                coverageComplete,
                incompleteReason);
            var request = new AuditEditor.LocalizationKeyAuditRequest(requiredLocales, coverage);
            var orphans = orphanLocaleTables ?? Array.Empty<AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot>();
            var snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(configuredLocales, collections, orphans);
            var collectionRawIdentities = collections
                .Select(collection => new AuditEditor.LocalizationKeyAuditRawIdentity(
                    collection.SharedDataAssetPath,
                    collection.CollectionGuid))
                .ToList();
            collectionRawIdentities.AddRange(orphans
                .GroupBy(orphan => orphan.SharedDataAssetPath, StringComparer.Ordinal)
                .Select(group => new AuditEditor.LocalizationKeyAuditRawIdentity(
                    group.Key,
                    group.First().CollectionGuid)));
            collectionRawIdentities.AddRange(
                additionalRawIdentities ?? Array.Empty<AuditEditor.LocalizationKeyAuditRawIdentity>());
            return AuditEditor.LocalizationKeyAuditAnalyzer.Analyze(request, snapshot, collectionRawIdentities);
        }

        /// <summary>valid direct table を持つ UI collection を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCollectionSnapshot CompleteCollectionWithEntries(
            params long[] entryIds)
        {
            var entries = entryIds.Select(id => Entry(id, $"Key{id:D4}")).ToArray();
            var localized = entryIds.Select(id => Localized(id, $"Value{id:D4}")).ToArray();
            return CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                entries,
                new[] { Table("en", localized) });
        }

        /// <summary>指定した identity と children を持つ collection を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCollectionSnapshot CreateCollection(
            string name,
            Guid guid,
            IReadOnlyList<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot> entries,
            IReadOnlyList<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot> tables,
            string assetPath = null)
        {
            return new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                name,
                guid,
                assetPath ?? $"Assets/Localization/{name} {guid:N} Shared Data.asset",
                entries,
                tables);
        }

        /// <summary>shared entry を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditSharedEntrySnapshot Entry(long id, string key)
        {
            return new AuditEditor.LocalizationKeyAuditSharedEntrySnapshot(id, key);
        }

        /// <summary>localized entry を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot Localized(long id, string value)
        {
            return new AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot(id, value);
        }

        /// <summary>既定 path の Locale table を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditLocaleTableSnapshot Table(
            string locale,
            params AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot[] entries)
        {
            return TableAt(locale, $"Assets/Localization/UI_{locale}.asset", entries);
        }

        /// <summary>指定 path の Locale table を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditLocaleTableSnapshot TableAt(
            string locale,
            string assetPath,
            params AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot[] entries)
        {
            return new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(locale, assetPath, entries);
        }

        /// <summary>宣言 scope 内の static reference を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditStaticReference Reference(
            Guid collectionGuid,
            long entryId,
            string collectionName = "UI",
            string entryKey = "Key")
        {
            return new AuditEditor.LocalizationKeyAuditStaticReference(
                "Assets/Scenes/Main.unity",
                collectionGuid,
                entryId,
                collectionName,
                entryKey);
        }

        /// <summary>指定 issue kind の件数を検証します。</summary>
        private static void AssertIssueCount(
            AuditEditor.LocalizationKeyAuditResult result,
            AuditEditor.LocalizationKeyAuditIssueKind kind,
            int expectedCount)
        {
            Assert.That(result.Issues.Count(issue => issue.Kind == kind), Is.EqualTo(expectedCount));
        }

        /// <summary>NoStatic finding の entry ID を結果順で返します。</summary>
        private static long[] NoStaticEntryIds(AuditEditor.LocalizationKeyAuditResult result)
        {
            return result.Issues
                .Where(issue => issue.Kind == AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope)
                .Select(issue => issue.EntryId)
                .ToArray();
        }

        /// <summary>決定論比較用に全 issue field を文字列化します。</summary>
        private static string[] ProjectIssues(AuditEditor.LocalizationKeyAuditResult result)
        {
            return result.Issues.Select(issue => string.Join("|", new[]
            {
                issue.Kind.ToString(),
                issue.AssetPath,
                issue.RelatedAssetPath,
                issue.CollectionName,
                issue.CollectionGuid.ToString("N"),
                issue.LocaleIdentifier,
                issue.EntryKey,
                issue.EntryId.ToString(),
                issue.Message
            })).ToArray();
        }
    }
}
