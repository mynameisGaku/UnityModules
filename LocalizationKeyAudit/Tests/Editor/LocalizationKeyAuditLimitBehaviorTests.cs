using System;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 実用的な allocation で到達できる request、typed、graph、issue の hard limit 分岐を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditLimitBehaviorTests
    {
        /// <summary>
        /// typed Locale と collection の件数超過を LimitExceeded terminal result にします。
        /// </summary>
        [Test]
        public void Audit_TypedTopLevelCountsAboveLimitReturnLimitExceeded()
        {
            var tooManyLocales = Enumerable.Range(
                    0,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumLocales + 1)
                .Select(index => $"locale-{index:D4}")
                .ToArray();
            var localeTyped = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                    tooManyLocales,
                    Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>())
            };
            var localeResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(new[] { "en" }),
                CreateSingleRawSource(),
                localeTyped);
            AssertTerminalKind(localeResult, AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded);

            var collection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(),
                Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>());
            var tooManyCollections = Enumerable.Repeat(
                    collection,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumCollections + 1)
                .ToArray();
            var collectionTyped = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                    new[] { "en" },
                    tooManyCollections)
            };
            var collectionResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(new[] { "en" }),
                CreateSingleRawSource(),
                collectionTyped);
            AssertTerminalKind(collectionResult, AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded);
        }

        /// <summary>
        /// direct coverage 積で graph edge 上限を超える snapshot を部分結果なしで拒否します。
        /// </summary>
        [Test]
        public void Audit_GraphEdgeCountAboveLimitReturnsLimitExceeded()
        {
            const int sharedEntryCount = 19532;
            var entries = Enumerable.Range(1, sharedEntryCount)
                .Select(index => new AuditEditor.LocalizationKeyAuditSharedEntrySnapshot(index, $"Key{index:D5}"))
                .ToArray();
            var collection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                entries,
                Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>());
            var locales = Enumerable.Range(0, AuditEditor.LocalizationKeyAuditLimits.MaximumRequiredLocales)
                .Select(index => $"locale-{index:D3}")
                .ToArray();
            var typed = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(locales, new[] { collection })
            };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(locales),
                CreateSingleRawSource(),
                typed);

            AssertTerminalKind(result, AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded);
            StringAssert.Contains("graph edge 数", result.Issues[0].Message);
        }

        /// <summary>
        /// 4096 collection と 256 required Locale から issue 上限を超え、partial issue を破棄します。
        /// </summary>
        [Test]
        public void Audit_IssueCountAboveLimitReturnsLimitExceeded()
        {
            var collectionCount = AuditEditor.LocalizationKeyAuditLimits.MaximumCollections;
            var collections = new AuditEditor.LocalizationKeyAuditCollectionSnapshot[collectionCount];
            var rawAssets = new AuditEditor.LocalizationKeyAuditRawAsset[collectionCount];
            for (var index = 0; index < collectionCount; index++)
            {
                var guid = GuidForIndex(index);
                var path = $"Assets/Localization/C{index:D4} Shared Data.asset";
                collections[index] = CreateCollection(
                    $"C{index:D4}",
                    guid,
                    Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(),
                    Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>(),
                    path);
                rawAssets[index] = LocalizationKeyAuditTestData.CreateValidRawAsset(path, guid);
            }

            var locales = Enumerable.Range(0, AuditEditor.LocalizationKeyAuditLimits.MaximumRequiredLocales)
                .Select(index => $"locale-{index:D3}")
                .ToArray();
            var raw = new FakeLocalizationKeyAuditRawSource { Assets = rawAssets };
            var typed = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(locales, collections)
            };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(locales),
                raw,
                typed);

            AssertTerminalKind(result, AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded);
            StringAssert.Contains("issue 数", result.Issues[0].Message);
        }

        /// <summary>
        /// request text と typed text/value の文字数上限超過を各所有境界で拒否します。
        /// </summary>
        [Test]
        public void Audit_TextLengthsAboveLimitFailAtOwningBoundary()
        {
            var longText = new string('x', AuditEditor.LocalizationKeyAuditLimits.MaximumTextCharacters + 1);
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                longText,
                new[] { "Assets/Scenes" },
                Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                true,
                string.Empty);
            var rawNotCalled = new FakeLocalizationKeyAuditRawSource();
            var typedNotCalled = new FakeLocalizationKeyAuditTypedSource();
            var requestResult = AuditEditor.LocalizationKeyAuditService.Audit(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, coverage),
                rawNotCalled,
                typedNotCalled);
            AssertTerminalKind(requestResult, AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration);
            Assert.That(rawNotCalled.ReadCallCount, Is.Zero);
            Assert.That(typedNotCalled.ReadCallCount, Is.Zero);

            var invalidCollection = CreateCollection(
                longText,
                LocalizationKeyAuditTestData.CollectionGuid,
                Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(),
                Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>());
            var collectionResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(new[] { "en" }),
                CreateSingleRawSource(),
                new FakeLocalizationKeyAuditTypedSource
                {
                    Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                        new[] { "en" },
                        new[] { invalidCollection })
                });
            AssertTerminalKind(collectionResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);

            var longValue = new string('v', AuditEditor.LocalizationKeyAuditLimits.MaximumLocalizedValueCharacters + 1);
            var table = new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                "en",
                "Assets/Localization/UI_en.asset",
                new[] { new AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot(10, longValue) });
            var valueCollection = CreateCollection(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                new[] { new AuditEditor.LocalizationKeyAuditSharedEntrySnapshot(10, "Start") },
                new[] { table });
            var valueResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(new[] { "en" }),
                CreateSingleRawSource(),
                new FakeLocalizationKeyAuditTypedSource
                {
                    Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                        new[] { "en" },
                        new[] { valueCollection })
                });
            AssertTerminalKind(valueResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
        }

        /// <summary>指定 required Locale を持つ valid request を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditRequest CreateRequest(string[] locales)
        {
            return new AuditEditor.LocalizationKeyAuditRequest(
                locales,
                new AuditEditor.LocalizationKeyAuditCoverage(
                    "Scenes",
                    new[] { "Assets/Scenes" },
                    Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                    true,
                    string.Empty));
        }

        /// <summary>既知の SharedTableData 一件を返す raw source を作ります。</summary>
        private static FakeLocalizationKeyAuditRawSource CreateSingleRawSource()
        {
            return new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[] { LocalizationKeyAuditTestData.CreateValidRawAsset() }
            };
        }

        /// <summary>指定 children を持つ collection を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCollectionSnapshot CreateCollection(
            string name,
            Guid guid,
            AuditEditor.LocalizationKeyAuditSharedEntrySnapshot[] entries,
            AuditEditor.LocalizationKeyAuditLocaleTableSnapshot[] tables,
            string assetPath = "Assets/Localization/UI Shared Data.asset")
        {
            return new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                name,
                guid,
                assetPath,
                entries,
                tables);
        }

        /// <summary>連番から non-empty unique GUID を作ります。</summary>
        private static Guid GuidForIndex(int index)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(index + 1).CopyTo(bytes, 0);
            return new Guid(bytes);
        }

        /// <summary>terminal result が指定 kind 一件だけを持つことを検証します。</summary>
        private static void AssertTerminalKind(
            AuditEditor.LocalizationKeyAuditResult result,
            AuditEditor.LocalizationKeyAuditIssueKind kind)
        {
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.LocaleIdentifiers, Is.Empty);
            Assert.That(result.Collections, Is.Empty);
            Assert.That(result.OrphanLocaleTables, Is.Empty);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(kind));
            Assert.That(result.GraphEdgeCount, Is.Zero);
        }
    }
}
