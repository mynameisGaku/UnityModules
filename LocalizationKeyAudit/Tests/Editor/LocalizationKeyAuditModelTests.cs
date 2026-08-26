using System;
using System.Collections.Generic;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 入力と finding DTO が呼び出し元の変更から独立した値になることを検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditModelTests
    {
        /// <summary>
        /// request と coverage は元一覧と各 snapshot を防御的に copy します。
        /// </summary>
        [Test]
        public void RequestAndCoverage_DefensivelyCopyCollectionsAndReferences()
        {
            var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var locales = new List<string> { "en" };
            var paths = new List<string> { "Assets/Scenes/Main.unity" };
            var references = new List<AuditEditor.LocalizationKeyAuditStaticReference>
            {
                new AuditEditor.LocalizationKeyAuditStaticReference(
                    "Assets/Scenes/Main.unity",
                    guid,
                    10,
                    "UI",
                    "Start")
            };
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                paths,
                references,
                true,
                null);
            var request = new AuditEditor.LocalizationKeyAuditRequest(locales, coverage);

            locales.Add("ja");
            paths.Clear();
            references.Clear();

            Assert.That(request.RequiredLocaleIdentifiers, Is.EqualTo(new[] { "en" }));
            Assert.That(request.Coverage, Is.Not.SameAs(coverage));
            Assert.That(request.Coverage.DeclaredAssetPaths, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
            Assert.That(request.Coverage.RecognizedReferences, Has.Count.EqualTo(1));
            Assert.That(request.Coverage.RecognizedReferences[0], Is.Not.SameAs(coverage.RecognizedReferences[0]));
            Assert.That(request.Coverage.IncompleteReason, Is.Empty);
            Assert.Throws<NotSupportedException>(
                () => ((IList<string>)request.RequiredLocaleIdentifiers).Add("fr"));
            Assert.Throws<NotSupportedException>(
                () => ((IList<string>)request.Coverage.DeclaredAssetPaths).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditStaticReference>)request.Coverage.RecognizedReferences).Clear());
        }

        /// <summary>
        /// null の一覧、要素、文字列を空の読み取り専用値へ正規化します。
        /// </summary>
        [Test]
        public void Constructors_NormalizeNullCollectionsAndStrings()
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(null, null, null, false, null);
            var request = new AuditEditor.LocalizationKeyAuditRequest(null, coverage);
            var reference = new AuditEditor.LocalizationKeyAuditStaticReference(null, Guid.Empty, 0, null, null);
            var issue = new AuditEditor.LocalizationKeyAuditIssue(
                AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed,
                null,
                null,
                null,
                Guid.Empty,
                null,
                null,
                0,
                null);

            Assert.That(request.RequiredLocaleIdentifiers, Is.Empty);
            Assert.That(coverage.ScopeDescription, Is.Empty);
            Assert.That(coverage.DeclaredAssetPaths, Is.Empty);
            Assert.That(coverage.RecognizedReferences, Is.Empty);
            Assert.That(coverage.IncompleteReason, Is.Empty);
            Assert.That(reference.SourceAssetPath, Is.Empty);
            Assert.That(reference.CollectionName, Is.Empty);
            Assert.That(reference.EntryKey, Is.Empty);
            Assert.That(issue.AssetPath, Is.Empty);
            Assert.That(issue.RelatedAssetPath, Is.Empty);
            Assert.That(issue.CollectionName, Is.Empty);
            Assert.That(issue.LocaleIdentifier, Is.Empty);
            Assert.That(issue.EntryKey, Is.Empty);
            Assert.That(issue.Message, Is.Empty);
        }

        /// <summary>
        /// typed snapshot は全階層を copy し、null direct value は空文字へ変換しません。
        /// </summary>
        [Test]
        public void TypedSnapshots_DefensivelyCopyNestedCollectionsAndPreserveNullValue()
        {
            var localizedEntries = new List<AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot>
            {
                new AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot(10, null)
            };
            var table = new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                "ja",
                "Assets/Localization/UI_ja.asset",
                localizedEntries);
            var sharedEntries = new List<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>
            {
                new AuditEditor.LocalizationKeyAuditSharedEntrySnapshot(10, "Start")
            };
            var tables = new List<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot> { table };
            var collection = new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                "UI",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                "Assets/Localization/UI Shared Data.asset",
                sharedEntries,
                tables);
            var locales = new List<string> { "ja" };
            var collections = new List<AuditEditor.LocalizationKeyAuditCollectionSnapshot> { collection };
            var nonStringIdentity = new AuditEditor.LocalizationKeyAuditNonStringSharedDataIdentity(
                "Assets/Localization/Asset Shared Data.asset",
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            var nonStringIdentities = new List<AuditEditor.LocalizationKeyAuditNonStringSharedDataIdentity>
            {
                nonStringIdentity
            };
            var snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                locales,
                collections,
                nonStringSharedDataIdentities: nonStringIdentities);

            localizedEntries.Clear();
            sharedEntries.Clear();
            tables.Clear();
            locales.Clear();
            collections.Clear();
            nonStringIdentities.Clear();

            Assert.That(snapshot.LocaleIdentifiers, Is.EqualTo(new[] { "ja" }));
            Assert.That(snapshot.Collections, Has.Count.EqualTo(1));
            Assert.That(snapshot.Collections[0], Is.Not.SameAs(collection));
            Assert.That(snapshot.Collections[0].SharedEntries, Has.Count.EqualTo(1));
            Assert.That(snapshot.Collections[0].LocaleTables, Has.Count.EqualTo(1));
            Assert.That(snapshot.Collections[0].LocaleTables[0], Is.Not.SameAs(table));
            Assert.That(snapshot.Collections[0].LocaleTables[0].Entries, Has.Count.EqualTo(1));
            Assert.That(snapshot.Collections[0].LocaleTables[0].Entries[0].Value, Is.Null);
            Assert.That(snapshot.NonStringSharedDataIdentities, Has.Count.EqualTo(1));
            Assert.That(snapshot.NonStringSharedDataIdentities[0], Is.Not.SameAs(nonStringIdentity));
            Assert.That(
                snapshot.NonStringSharedDataIdentities[0].AssetPath,
                Is.EqualTo("Assets/Localization/Asset Shared Data.asset"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.LocaleIdentifiers).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditCollectionSnapshot>)snapshot.Collections).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot>)
                    snapshot.Collections[0].LocaleTables[0].Entries).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditNonStringSharedDataIdentity>)
                    snapshot.NonStringSharedDataIdentities).Clear());
        }

        /// <summary>
        /// collection に属さない table snapshot も table/entry を含めて防御的に copy します。
        /// </summary>
        [Test]
        public void TypedSnapshot_DefensivelyCopiesOrphanLocaleTables()
        {
            var table = new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                "en",
                "Assets/Localization/Orphan_en.asset",
                new[] { new AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot(10, "Value") });
            var orphan = new AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot(
                "Assets/Localization/Orphan Shared Data.asset",
                LocalizationKeyAuditTestData.CollectionGuid,
                table);
            var orphans = new List<AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot> { orphan };
            var snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                new[] { "en" },
                Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                orphans);

            orphans.Clear();

            Assert.That(snapshot.OrphanLocaleTables, Has.Count.EqualTo(1));
            Assert.That(snapshot.OrphanLocaleTables[0], Is.Not.SameAs(orphan));
            Assert.That(snapshot.OrphanLocaleTables[0].LocaleTable, Is.Not.SameAs(table));
            Assert.That(snapshot.OrphanLocaleTables[0].LocaleTable.Entries, Has.Count.EqualTo(1));
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot>)
                    snapshot.OrphanLocaleTables).Clear());
        }

        /// <summary>
        /// result は coverage、typed data、issue の全てを独立した読み取り専用 snapshot にします。
        /// </summary>
        [Test]
        public void Result_DefensivelyCopiesAllNestedValues()
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                new[] { "Assets/Scenes" },
                Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                true,
                string.Empty);
            var locales = new List<string> { "en" };
            var collections = new List<AuditEditor.LocalizationKeyAuditCollectionSnapshot>
            {
                new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                    "UI",
                    LocalizationKeyAuditTestData.CollectionGuid,
                    "Assets/Localization/UI Shared Data.asset",
                    Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(),
                    Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>())
            };
            var issues = new List<AuditEditor.LocalizationKeyAuditIssue>
            {
                new AuditEditor.LocalizationKeyAuditIssue(
                    AuditEditor.LocalizationKeyAuditIssueKind.NoStaticReferenceFoundWithinDeclaredScope,
                    "Assets/Localization/UI Shared Data.asset",
                    string.Empty,
                    "UI",
                    LocalizationKeyAuditTestData.CollectionGuid,
                    string.Empty,
                    "Start",
                    10,
                    "scope 内で参照を検出できませんでした。")
            };
            var orphanTables = new List<AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot>
            {
                new AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot(
                    "Assets/Localization/Orphan Shared Data.asset",
                    Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                        "en",
                        "Assets/Localization/Orphan_en.asset",
                        Array.Empty<AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot>()))
            };
            var result = new AuditEditor.LocalizationKeyAuditResult(
                true,
                coverage,
                locales,
                collections,
                issues,
                3,
                orphanTables);

            locales.Clear();
            collections.Clear();
            issues.Clear();
            orphanTables.Clear();

            Assert.That(result.Coverage, Is.Not.SameAs(coverage));
            Assert.That(result.LocaleIdentifiers, Is.EqualTo(new[] { "en" }));
            Assert.That(result.Collections, Has.Count.EqualTo(1));
            Assert.That(result.OrphanLocaleTables, Has.Count.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.GraphEdgeCount, Is.EqualTo(3));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)result.LocaleIdentifiers).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditCollectionSnapshot>)result.Collections).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot>)
                    result.OrphanLocaleTables).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<AuditEditor.LocalizationKeyAuditIssue>)result.Issues).Clear());
        }

        /// <summary>
        /// terminal result が coverage を受け取れない場合は unavailable snapshot を補います。
        /// </summary>
        [Test]
        public void Result_NullCoverageCreatesExplicitIncompleteCoverage()
        {
            var result = new AuditEditor.LocalizationKeyAuditResult(
                false,
                null,
                null,
                null,
                null,
                0);

            Assert.That(result.Coverage, Is.Not.Null);
            Assert.That(result.Coverage.IsComplete, Is.False);
            Assert.That(result.Coverage.IncompleteReason, Is.Not.Empty);
            Assert.That(result.LocaleIdentifiers, Is.Empty);
            Assert.That(result.Collections, Is.Empty);
            Assert.That(result.OrphanLocaleTables, Is.Empty);
            Assert.That(result.Issues, Is.Empty);
        }

        /// <summary>
        /// coverage 内の null 参照は constructor では保持し、service 境界の validation に渡します。
        /// </summary>
        [Test]
        public void Coverage_PreservesNullReferenceForValidation()
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                new string[] { null },
                new AuditEditor.LocalizationKeyAuditStaticReference[] { null },
                true,
                string.Empty);

            Assert.That(coverage.DeclaredAssetPaths, Is.EqualTo(new[] { string.Empty }));
            Assert.That(coverage.RecognizedReferences, Has.Count.EqualTo(1));
            Assert.That(coverage.RecognizedReferences[0], Is.Null);
        }
    }
}
